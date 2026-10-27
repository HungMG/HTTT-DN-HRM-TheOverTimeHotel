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
    browser.close()
print('PASS need editing, submission, DOM replacement and switching back to a new need')
