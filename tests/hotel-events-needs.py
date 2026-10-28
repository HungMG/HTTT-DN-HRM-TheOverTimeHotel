"""Exercise real Razor output and popup JavaScript without writing to a database.

First run HotelEventsWeb with --export, then run this file from the repo root.
"""
from pathlib import Path
from playwright.sync_api import sync_playwright, expect

root = Path.cwd()
web = root / 'src/OvertimeHotel.HRM.WebAdmin/wwwroot'
export = root / '.probe/codex-phase1-html'
details = (export / 'details.html').read_text(encoding='utf-8')
calendar = (export / 'calendar.html').read_text(encoding='utf-8')
requests = []

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))

    def respond(route):
        request = route.request
        if request.method == 'POST':
            # FormData uses multipart encoding. Keep the actual payload for assertions.
            requests.append(request.post_data)
            route.fulfill(content_type='text/html', body=details)
        elif '/SuKien/Details' in request.url:
            route.fulfill(content_type='text/html', body=details)
        else:
            route.fulfill(content_type='text/html; charset=utf-8', body=calendar)

    page.route('http://phase1.test/**', respond)
    page.goto('http://phase1.test/')
    page.add_script_tag(path=str(web / 'lib/bootstrap/dist/js/bootstrap.bundle.min.js'))
    page.add_script_tag(path=str(web / 'js/events.js'))
    page.add_script_tag(path=str(web / 'js/event-details.js'))
    page.evaluate("document.dispatchEvent(new Event('DOMContentLoaded'))")
    page.locator('[data-event-link][data-event-id="17"]').first.click()
    body = page.locator('[data-event-details-body]')
    body.get_by_role('button', name='Sửa', exact=True).click()
    expect(body.locator('[name="Need.NeedId"]')).to_have_value('21')
    expect(body.locator('[name="Need.Role"]')).to_have_value('Phụ trách âm thanh và micro')
    expect(body.locator('[name="Need.Count"]')).to_have_value('2')
    body.locator('[name="Need.Count"]').fill('7')
    body.locator('[name="Need.Reason"]').fill('Bổ sung kỹ thuật cho khách')
    body.get_by_role('button', name='Lưu yêu cầu', exact=True).click()
    page.wait_for_function("!document.querySelector('[data-event-details-body]').hasAttribute('aria-busy') || document.querySelector('[data-event-details-body]').getAttribute('aria-busy') === 'false'")
    assert len(requests) == 1, 'Editing did not submit the need form exactly once'
    assert 'name="Need.NeedId"\r\n\r\n21' in requests[0], 'Edit submitted a new need instead of the selected row'
    assert 'name="Need.Count"\r\n\r\n7' in requests[0], 'Edited headcount was not submitted'
    # The response replaces the popup DOM. Editing must still work without inline scripts.
    body.get_by_role('button', name='Sửa', exact=True).click()
    expect(body.locator('[name="Need.NeedId"]')).to_have_value('21')
    body.get_by_role('button', name='Điều chỉnh / Thêm yêu cầu', exact=True).click()
    expect(body.locator('[name="Need.NeedId"]')).to_have_value('0')
    expect(body.locator('[name="Need.Role"]')).to_have_value('')
    expect(body.locator('[name="Need.Count"]')).to_have_value('1')
    expect(body.locator('[name="Need.Reason"]')).to_have_value('')
    # A failed staff refresh has an unknown count, and must preserve the loaded calendar count.
    page.evaluate("document.dispatchEvent(new CustomEvent('hotel-event-updated', { detail: { id: '17', count: '', status: 'LEN_KE_HOACH', statusLabel: 'Lên kế hoạch' } }))")
    expect(page.locator('.event-calendar-chip .event-chip-meta').first).to_contain_text('1 người')
    assert not errors, errors

    press = (export / 'press-needs.html').read_text(encoding='utf-8')
    press_invalid = (export / 'press-needs-invalid.html').read_text(encoding='utf-8')
    press_requests = []

    def respond_press(route):
        if route.request.method == 'POST':
            press_requests.append(route.request.post_data)
            content = press_invalid
        elif '/SuKien/Details' in route.request.url:
            content = press
        else:
            content = calendar
        route.fulfill(content_type='text/html; charset=utf-8', body=content)

    page.route('http://press-needs.test/**', respond_press)
    page.goto('http://press-needs.test/')
    for script in ['lib/bootstrap/dist/js/bootstrap.bundle.min.js', 'js/events.js', 'js/event-details.js']:
        page.add_script_tag(path=str(web / script))
    page.evaluate("document.dispatchEvent(new Event('DOMContentLoaded'))")
    page.locator('[data-event-link][data-event-id="17"]').first.click()
    body = page.locator('[data-event-details-body]')
    body.locator('[data-new-need]').click()
    department = body.locator('[name="Need.Department"]')
    role = body.locator('[name="Need.Role"]')
    department.select_option('AM_THUC')
    suggestions = lambda: role.evaluate('(input) => Array.from(input.list?.options || [], option => option.value)')
    assert suggestions() == ['Phục vụ tiệc trà họp báo', 'Phục vụ đồ uống họp báo', 'Điều phối phục vụ'], 'Fresh press catering has no usable role suggestions'
    role.fill('Phục vụ tiệc trà họp báo')
    body.locator('[name="Need.Count"]').fill('4')
    body.locator('[name="Need.Reason"]').fill('Bổ sung tiệc trà')
    body.get_by_role('button', name='Lưu yêu cầu', exact=True).click()
    page.wait_for_function("document.querySelector('[data-event-details-body]').getAttribute('aria-busy') === 'false'")
    assert len(press_requests) == 1 and 'Phục vụ tiệc trà họp báo' in press_requests[0], 'Suggested role was not submitted'
    expect(department).to_have_value('AM_THUC')
    expect(role).to_have_value('Vai trò đang sửa')
    assert suggestions() == ['Phục vụ tiệc trà họp báo', 'Phục vụ đồ uống họp báo', 'Điều phối phục vụ'], 'Replacement lost the selected department suggestions'
    body.locator('[data-edit-need="21"]').click()
    expect(role).to_have_value('Phụ trách micro và trình chiếu')
    assert suggestions() == ['Phụ trách micro và trình chiếu', 'Phụ trách ánh sáng họp báo', 'Trực thiết bị họp báo'], 'Editing a need retained the previous department suggestions'
    body.locator('[data-new-need]').click()
    expect(role).to_have_value('')
    assert suggestions()[0] == 'Tiếp nhận phóng viên và khách mời', 'Reset retained the edited department suggestions'
    department.select_option('AM_THUC')
    assert suggestions()[0] == 'Phục vụ tiệc trà họp báo', 'Delegated change handler stopped after replacement'
    assert not errors, errors
    browser.close()
print('PASS need editing, submission, DOM replacement, optional department role suggestions and resetting a new need')
