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
    picker = department.locator('[data-member-picker]')
    picker.wait_for(state='visible')
    picker.locator('[data-member-search]').fill('worker-a')
    worker = picker.locator(f'[data-choose-member="{fixture["WorkerA"]}"]')
    worker.click()
    assignment = page.locator('[data-event-assignment-dialog]')
    assignment.wait_for(state='visible')
    form = assignment.locator('[data-assignment-popup-form]')
    form.wait_for()
    assert not dialog.is_visible(), 'Only one Bootstrap modal can be visible'
    role = form.locator('[name="Assignment.Role"]')
    assert role.evaluate('(element)=>element.tagName') == 'SELECT'
    assert role.get_attribute('required') is not None
    assert form.locator('[name="Assignment.Task"]').count() == 0
    assert role.locator('option[value="Tiếp nhận đăng ký"]').count() == 0, 'Department must scope the role catalog'
    role.select_option('__OTHER__')
    custom = form.locator('[name="Assignment.OtherRole"]')
    assert custom.is_visible() and not custom.evaluate('(element)=>element.checkValidity()')
    custom.fill('Hỗ trợ tại hội trường')
    role.select_option('Phụ trách âm thanh và micro')
    assert not custom.is_visible()
    assignment.locator('form').get_by_role('button', name='Hủy', exact=True).click()
    dialog.wait_for(state='visible')
    assert picker.is_visible(), 'Cancel must return to the same employee picker'
    assert picker.locator('[data-member-search]').input_value() == 'worker-a'
    worker.click()
    assignment.wait_for(state='visible')
    form = assignment.locator('[data-assignment-popup-form]')
    form.locator('[name="Assignment.Role"]').select_option('Phụ trách âm thanh và micro')
    form.locator('details').evaluate('(element) => element.open = true')
    form.locator('[name="Assignment.Allowance"]').fill('9999999999999.99')
    assignment.get_by_role('button', name='Lưu phân công', exact=True).click()
    assignment.locator('.validation-summary-errors').wait_for()
    assert assignment.locator('[name="Assignment.Role"]').input_value() == 'Phụ trách âm thanh và micro'
    assignment.locator('details').evaluate('(element) => element.open = true')
    assignment.locator('[name="Assignment.Allowance"]').fill('123.45')
    assignment.get_by_role('button', name='Lưu phân công', exact=True).dblclick()
    dialog.wait_for(state='visible')
    member = dialog.locator('[data-department-card="KY_THUAT"] [data-assigned-member]')
    assert member.count() == 1, 'Saving twice must create one assignment'
    assert 'Phụ trách âm thanh và micro' in member.inner_text()
    assert dialog.locator('[data-department-card="KY_THUAT"] [data-event-role="Trực thiết bị hội trường"]').get_attribute('data-role-missing') == '1'
    assert '123,45' in member.inner_text()
    dialog.get_by_role('button', name='Đánh giá & nghiệm thu', exact=False).click()
    assert dialog.locator('[data-details-pane="acceptance"]').is_visible()
    assert dialog.locator('select option[value="XUAT_SAC"]').count() == 0
    dialog.get_by_role('button', name='Thông tin sự kiện', exact=False).click()
    assert dialog.locator('[data-details-pane="overview"]').is_visible()
    dialog.get_by_role('button', name='Đóng', exact=True).click()
    dialog.wait_for(state='hidden')
    assert page.locator('[data-selected-events]').inner_text().find('1 nhân sự') >= 0
    assert page.evaluate('document.activeElement.matches("[data-event-link]")'), 'Close must restore focus to the event'
    # The calendar chip uses the same popup; mobile content must fit the viewport.
    page.set_viewport_size({'width': 390, 'height': 844})
    page.locator('[data-calendar-day] [data-event-link]').filter(has_text=fixture['Prefix']).first.click()
    dialog.wait_for(state='visible')
    dialog.locator('[data-event-details]').wait_for()
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth + 1')
    page.screenshot(path=str(root / '.probe/event-details-mobile.png'), full_page=True)
    assert not errors, errors
    print('PASS details popup, department picker, role form, cancel/context, budget validation, single save, review navigation and mobile')
    browser.close()
