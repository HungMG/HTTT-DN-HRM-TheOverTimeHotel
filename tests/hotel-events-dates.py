"""Run against the web app with a HotelEventsIntegration --preview-fixture, then clean up."""
import json
import os
from datetime import date, timedelta
from pathlib import Path
from urllib.parse import parse_qs, urlparse
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
fixture = json.loads((root / '.probe/event-preview.json').read_text())
base = os.environ.get('EVENT_BROWSER_URL', 'http://localhost:5065')

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page(viewport={'width': 1450, 'height': 1000}, reduced_motion='reduce')
    page.set_default_timeout(60000)
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(base + '/Account/Login')
    page.locator('[name=Username]').fill(fixture['Admin']['Username'])
    page.locator('[name=Password]').fill(fixture['Password'])
    with page.expect_navigation(wait_until='domcontentloaded'):
        page.locator('button[type=submit]').click()
    page.goto(base + '/SuKien')
    today = date.fromisoformat(page.locator('[data-event-calendar]').get_attribute('data-today'))
    past = today - timedelta(days=1)
    future = today + timedelta(days=1)
    page.goto(f'{base}/SuKien?year={past.year}&month={past.month}&date={past}')
    popup = page.locator('[data-event-create-dialog]')
    page.locator(f'[data-date="{past}"] .event-day-number').dblclick()
    page.wait_for_timeout(250)
    assert not popup.is_visible(), 'Double-clicking a past day must not open creation'
    assert all(value == 'true' for value in page.locator('[data-create-event]').evaluate_all('(links)=>links.map(a=>a.getAttribute("aria-disabled"))'))
    assert page.locator('[data-create-date-note]').is_visible()
    page.locator('[data-create-event]').first.click()
    assert not popup.is_visible(), 'Disabled create action must not open creation'
    print('PASS past calendar days are view-only and create actions are disabled')

    page.goto(f'{base}/SuKien?year={past.year}&month={past.month}&date={today}&dates={past},{today}')
    assert page.locator('[data-create-event]').first.get_attribute('aria-disabled') == 'true'
    page.locator(f'[data-date="{past}"] .event-day-number').click()
    assert page.locator('[data-create-event]').first.get_attribute('aria-disabled') == 'false'
    page.locator(f'[data-date="{today}"] .event-day-number').dblclick()
    popup.locator('[name="Input.Start"]').wait_for()
    assert popup.locator('[name="Input.Start"]').input_value() == f'{today}T09:00'
    assert popup.locator('[name="Input.Start"]').get_attribute('min') == f'{today}T00:00'
    popup.locator('[name="Input.Start"]').fill(f'{past}T09:00')
    assert not popup.locator('[name="Input.Start"]').evaluate('(input)=>input.checkValidity()')
    popup.get_by_role('button', name='Hủy', exact=True).click()
    popup.wait_for(state='hidden')
    print('PASS mixed selections are blocked; today creates normally and form disallows old dates')

    for suffix in ['', '&popup=true']:
        response = page.request.get(f'{base}/SuKien/Create?date={past}{suffix}')
        assert response.status == 422, 'Direct past-date creation route must be rejected'
    page.goto(f'{base}/SuKien/Create?date={future}')
    assert page.locator('[name="Input.Start"]').input_value() == f'{future}T09:00'
    fields = dict(page.locator('form[data-event-time-form]').evaluate('(form)=>Array.from(new FormData(form).entries())'))
    fields.update({'Input.Name': fixture['Prefix'] + '-past-forged', 'Input.Venue': fixture['Prefix'] + '-past-forged',
                   'Input.Start': f'{past}T09:00', 'Input.End': f'{future}T10:00',
                   'Input.ManagerId': str(fixture['Manager']['EmployeeId']), 'Input.Id': '0'})
    response = page.request.post(base + '/SuKien/Create', form=fields)
    assert response.status == 200, 'Invalid creation returns the form with validation errors'
    html = response.text()
    assert 'field-validation-error' in html and 'Input.Start' in html
    assert fixture['Prefix'] + '-past-forged' in html, 'Validation must preserve entered data'
    fields['IsPopup'] = 'true'
    response = page.request.post(base + '/SuKien/Create', form=fields)
    assert response.status == 200 and 'field-validation-error' in response.text()
    print('PASS direct GET and forged normal/popup POST cannot create a past event')

    historical = date.fromisoformat(fixture['PastEventDate'])
    page.goto(f'{base}/SuKien?year={historical.year}&month={historical.month}&date={historical}')
    link = page.locator(f'[data-date="{historical}"] [data-event-link]').filter(has_text=fixture['Prefix'])
    assert link.count() == 1
    with page.expect_navigation(wait_until='domcontentloaded'):
        link.click()
    details = urlparse(page.url)
    assert details.path.startswith('/SuKien/Details'), page.url
    detail_id = parse_qs(details.query).get('id', [details.path.rsplit('/', 1)[-1]])[0]
    assert detail_id == str(fixture['PastEventId']), 'Historical event link must open that event'
    assert page.locator('body').inner_text().find(fixture['Prefix']) >= 0
    assert not errors, errors
    print('PASS historical events remain visible and their details are accessible; no JavaScript errors')
    browser.close()
