"""Browser regression; seed with HotelEventsIntegration --preview-fixture, then clean up."""
import json
import os
import sys
from pathlib import Path
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
fixture = json.loads((root / '.probe/event-preview.json').read_text())
base = os.environ.get('EVENT_BROWSER_URL', sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5077')

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page(viewport={'width': 1450, 'height': 1000}, reduced_motion='reduce')
    page.set_default_timeout(60000)
    errors = []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.goto(base + '/Account/Login?returnUrl=/SuKien')
    page.locator('[name=Username]').fill(fixture['Admin']['Username'])
    page.locator('[name=Password]').fill(fixture['Password'])
    with page.expect_navigation(wait_until='domcontentloaded'):
        page.locator('button[type=submit]').click()
    page.goto(base + '/SuKien?year=2028&month=2&date=2028-02-29')
    page.locator('[data-date="2028-03-01"] .event-day-number').click()
    assert page.locator('[data-calendar-day][aria-selected=true]').count() == 2
    page.locator('[data-date="2028-03-01"] .event-day-number').dblclick()
    assert urlparse(page.url).path == '/SuKien', 'Double click must keep the calendar page open'
    popup = page.locator('[data-event-create-dialog]')
    popup.wait_for(state='visible')
    popup.locator('[name="Input.Start"]').wait_for()
    assert popup.locator('[name="Input.Start"]').input_value() == '2028-02-29T09:00'
    assert popup.locator('[name="Input.End"]').input_value() == '2028-03-01T17:00'
    popup.get_by_role('button', name='Hủy', exact=True).click()
    popup.wait_for(state='hidden')
    assert page.locator('[data-date="2028-02-29"]').get_attribute('aria-selected') == 'true'
    assert page.locator('[data-calendar-day][aria-selected=true]').count() == 2
    trigger = page.locator('[data-create-event]').first
    trigger.focus()
    page.keyboard.press('Enter')
    popup.locator('[name="Input.Start"]').wait_for()
    page.keyboard.press('Escape')
    popup.wait_for(state='hidden')
    assert trigger.evaluate('(e)=>document.activeElement === e'), 'Closing popup must return keyboard focus to its trigger'
    page.set_viewport_size({'width': 390, 'height': 844})
    trigger.click()
    popup.locator('[name="Input.Start"]').wait_for()
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth + 1'), 'Popup must fit a mobile viewport'
    page.screenshot(path=str(root / '.probe/events-create-popup-mobile.png'), full_page=True)
    popup.get_by_role('button', name='Hủy', exact=True).click()
    popup.wait_for(state='hidden')
    page.set_viewport_size({'width': 1450, 'height': 1000})
    page.reload()
    assert page.locator('[data-calendar-day][aria-selected=true]').count() == 2
    page.locator('[data-date="2028-02-29"] .event-day-number').click()
    assert page.locator('[data-calendar-day][aria-selected=true]').count() == 1
    page.locator('[data-date="2028-02-29"] .event-day-number').click()
    page.locator('[data-create-event]').first.click()
    popup.locator('[name="Input.Name"]').wait_for()
    for name, value in {'Name': fixture['Prefix'] + '-popup', 'Venue': fixture['Prefix'] + '-popup-venue', 'Budget': '12345.50', 'ExpectedGuests': '50'}.items():
        popup.locator('[name="Input.' + name + '"]').fill(value)
    popup.locator('[name="Input.ManagerId"]').select_option(str(fixture['Manager']['EmployeeId']))
    popup.locator('[name="Input.End"]').fill('2028-02-28T17:00')
    assert not popup.locator('[name="Input.End"]').evaluate('(e)=>e.checkValidity()')
    popup.locator('[name="Input.End"]').fill('2028-03-01T17:00')
    page.screenshot(path=str(root / '.probe/events-create-popup.png'), full_page=True)
    with page.expect_navigation(wait_until='domcontentloaded'):
        popup.get_by_role('button', name='Lưu sự kiện', exact=False).click()
    assert urlparse(page.url).path == '/SuKien'
    assert page.locator('[data-calendar-day][aria-selected=true]').count() == 2
    assert fixture['Prefix'] + '-popup' in page.locator('[data-selected-events]').inner_text()
    page.locator('[data-create-event]').first.click()
    popup.locator('[name="Input.Name"]').wait_for()
    popup.locator('[name="Input.Name"]').fill(fixture['Prefix'] + '-popup-conflict')
    popup.locator('[name="Input.Venue"]').fill(fixture['Prefix'] + '-popup-venue')
    popup.locator('[name="Input.ManagerId"]').select_option(str(fixture['Manager']['EmployeeId']))
    popup.get_by_role('button', name='Lưu sự kiện', exact=False).click()
    popup.locator('[data-valmsg-for="Input.Venue"]').filter(has_text='Địa điểm đã có sự kiện').wait_for()
    assert popup.locator('[name="Input.Name"]').input_value() == fixture['Prefix'] + '-popup-conflict'
    assert urlparse(page.url).path == '/SuKien'
    popup.get_by_role('button', name='Hủy', exact=True).click()
    popup.wait_for(state='hidden')
    assert not errors, errors
    print('PASS multiple days toggle/persist, double click popup range, cancel, save, inline conflict and validation')
    browser.close()
