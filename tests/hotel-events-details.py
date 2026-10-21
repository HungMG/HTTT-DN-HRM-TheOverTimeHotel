"""Real browser regression for calendar details and department assignment dialogs.

Seed HotelEventsIntegration --preview-fixture first, then clean that owned fixture.
"""
import json
import sys
from datetime import date, timedelta
from pathlib import Path
from urllib.parse import urlparse

from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
fixture = json.loads((root / '.probe/event-preview.json').read_text(encoding='utf-8-sig'))
base = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5083'

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page(viewport={'width': 1440, 'height': 1000}, reduced_motion='reduce')
    page.set_default_timeout(60000)
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(base + '/Account/Login?returnUrl=/SuKien')
    page.locator('[name=Username]').fill(fixture['Admin']['Username'])
    page.locator('[name=Password]').fill(fixture['Password'])
    with page.expect_navigation(wait_until='domcontentloaded'):
        page.locator('button[type=submit]').click()
    day = date.fromisoformat(fixture['PastEventDate'])
    page.goto(f'{base}/SuKien?year={day.year}&month={day.month}&date={day}')
    trigger = page.locator('[data-selected-events] a').filter(has_text=fixture['Prefix']).first
    trigger.click()
    dialog = page.locator('[data-event-details-dialog]')
    dialog.wait_for(state='visible')
    assert urlparse(page.url).path == '/SuKien', 'Opening event details must keep the calendar open'
    dialog.locator('[data-event-details]').wait_for()
    if '--smoke' in sys.argv:
        dialog.get_by_role('button', name='Nhân sự', exact=False).click()
        assert dialog.locator('[data-department-card]').count() >= 5
        dialog.get_by_role('button', name='Đánh giá & nghiệm thu', exact=False).click()
        assert dialog.locator('[data-details-pane="acceptance"]').is_visible()
        dialog.get_by_role('button', name='Đóng', exact=True).click()
        dialog.wait_for(state='hidden')
        assert page.evaluate('document.activeElement.matches("[data-event-link]")')
        assert not errors, errors
        print('PASS deployed event popup, staffing/acceptance navigation, focus and no JavaScript errors')
        browser.close()
        sys.exit(0)
    dialog.get_by_role('button', name='Nhân sự', exact=False).click()
    department = dialog.locator('[data-department-card="KY_THUAT"]')
    department.get_by_role('button', name='Thêm người', exact=False).click()
    assignment = page.locator('[data-event-assignment-dialog]')
    picker = assignment.locator('[data-member-picker]')
    picker.wait_for(state='visible')
    assert dialog.is_visible(), 'Opening the picker must keep staff visible below it'
    assert picker.locator('[data-continue-assignment]').is_disabled()
    assert picker.locator('[data-member-search]').evaluate('(e) => document.activeElement === e')
    picker.locator('[data-member-search]').fill('worker-a')
    worker = picker.locator(f'[data-choose-member="{fixture["WorkerA"]}"]')
    worker.click()
    assignment.locator('[data-continue-assignment]').click()
    assignment.wait_for(state='visible')
    form = assignment.locator('[data-assignment-popup-form]')
    form.wait_for()
    assert dialog.is_visible(), 'The staff section must remain visible beneath the picker'
    role = form.locator('[name="Assignments[0].Role"]')
    assert role.evaluate('(element)=>element.tagName') == 'SELECT'
    assert role.get_attribute('required') is not None
    assert form.locator('[name="Assignment.Task"]').count() == 0
    assert role.locator('option[value="Tiếp nhận đăng ký"]').count() == 0, 'Department must scope the role catalog'
    role.select_option('__OTHER__')
    custom = form.locator('[name="Assignments[0].OtherRole"]')
    assert custom.is_visible() and not custom.evaluate('(element)=>element.checkValidity()')
    custom.fill('Hỗ trợ tại hội trường')
    role.select_option('Phụ trách âm thanh và micro')
    assert not custom.is_visible()
    assignment.locator('form').get_by_role('button', name='Hủy', exact=True).click()
    dialog.wait_for(state='visible')
    assert department.locator('[data-add-member]').is_visible(), 'Cancel must return to the staffing section'
    department.get_by_role('button', name='Thêm người', exact=False).click()
    picker.wait_for(state='visible')
    picker.locator('[data-member-search]').fill('worker-a')
    worker.click()
    picker.locator('[data-member-search]').fill('worker-b')
    picker.locator(f'[data-choose-member="{fixture["WorkerB"]}"]').check()
    assert picker.locator('[data-selected-count]').inner_text() == '(2)'
    assignment.locator('[data-continue-assignment]').click()
    assignment.wait_for(state='visible')
    form = assignment.locator('[data-assignment-popup-form]')
    form.locator('[name="Assignments[0].Role"]').select_option('Phụ trách âm thanh và micro')
    form.locator('[name="Assignments[1].Role"]').select_option('Trực thiết bị hội trường')
    form.locator('details').evaluate('(element) => element.open = true')
    form.locator('[name="Assignment.Allowance"]').fill('9999999999999.99')
    assignment.get_by_role('button', name='Lưu phân công', exact=True).click()
    assignment.locator('.validation-summary-errors').wait_for()
    assert assignment.locator('[name="Assignments[0].Role"]').input_value() == 'Phụ trách âm thanh và micro'
    assert assignment.locator('[name="Assignments[1].Role"]').input_value() == 'Trực thiết bị hội trường'
    assert department.locator('[data-assigned-member]').count() == 0, 'Invalid group must not partially save'
    assignment.locator('details').evaluate('(element) => element.open = true')
    assignment.locator('[name="Assignment.Allowance"]').fill('123.45')
    assignment.get_by_role('button', name='Lưu phân công', exact=True).dblclick()
    assignment.wait_for(state='hidden')
    dialog.wait_for(state='visible')
    member = dialog.locator('[data-department-card="KY_THUAT"] [data-assigned-member]')
    assert member.count() == 2, 'Saving twice must create each selected employee once'
    assert 'Phụ trách âm thanh và micro' in member.first.inner_text()
    assert dialog.locator('[data-department-card="KY_THUAT"] [data-event-role="Trực thiết bị hội trường"]').get_attribute('data-role-missing') == '0'
    assert '123,45' in member.first.inner_text()
    dialog.get_by_role('button', name='Đánh giá & nghiệm thu', exact=False).click()
    assert dialog.locator('[data-details-pane="acceptance"]').is_visible()
    assert dialog.locator('select option[value="XUAT_SAC"]').count() == 0
    dialog.get_by_role('button', name='Thông tin sự kiện', exact=False).click()
    assert dialog.locator('[data-details-pane="overview"]').is_visible()
    dialog.get_by_role('button', name='Đóng', exact=True).click()
    dialog.wait_for(state='hidden')
    assert page.locator('[data-selected-events]').inner_text().find('2 nhân sự') >= 0
    assert page.evaluate('document.activeElement.matches("[data-event-link]")'), 'Close must restore focus to the event'
    # The calendar chip uses the same popup; mobile content must fit the viewport.
    page.set_viewport_size({'width': 390, 'height': 844})
    page.locator('[data-calendar-day] [data-event-link]').filter(has_text=fixture['Prefix']).first.click()
    dialog.wait_for(state='visible')
    dialog.locator('[data-event-details]').wait_for()
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth + 1')
    page.screenshot(path=str(root / '.probe/event-details-mobile.png'), full_page=True)
    assert not errors, errors
    print('PASS details overlay, multiple selection, individual roles, cancel/focus, atomic budget validation, batch save and mobile')
    browser.close()
