"""Exercise the real Razor acceptance dialog and AJAX replacement; no database writes."""
from pathlib import Path
from playwright.sync_api import sync_playwright, expect

root = Path.cwd()
web = root / 'src/OvertimeHotel.HRM.WebAdmin/wwwroot'
export = root / '.probe/codex-phase1-html'
calendar = (export / 'calendar.html').read_text(encoding='utf-8')
acceptance = (export / 'hourly-acceptance.html').read_text(encoding='utf-8')
requests = []

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))

    def respond(route):
        if route.request.method == 'POST':
            requests.append(route.request.post_data)
            route.fulfill(content_type='text/html; charset=utf-8', body=acceptance)
        else:
            route.fulfill(content_type='text/html; charset=utf-8', body=acceptance if '/Details' in route.request.url else calendar)

    page.route('http://acceptance.test/**', respond)
    page.goto('http://acceptance.test/')
    page.add_style_tag(path=str(web / 'lib/bootstrap/dist/css/bootstrap.min.css'))
    page.add_style_tag(path=str(web / 'css/events.css'))
    for script in ['lib/bootstrap/dist/js/bootstrap.bundle.min.js', 'js/events.js', 'js/event-details.js']:
        page.add_script_tag(path=str(web / script))
    page.evaluate("document.dispatchEvent(new Event('DOMContentLoaded'))")
    page.locator('[data-event-link]').first.click()
    body = page.locator('[data-event-details-body]')
    form = body.locator('[data-event-acceptance-form]')
    form.locator('[name="paymentPeriodId"]').select_option('41')
    form.locator('button[type="submit"]').click()
    dialog = form.locator('[data-acceptance-dialog]')
    expect(dialog).to_be_visible()
    expect(dialog.locator('[data-acceptance-period]')).to_contain_text('05/2024')
    expect(dialog).to_contain_text('153.846')
    page.screenshot(path=str(root / '.probe/event-phase3/acceptance-dialog.png'))
    assert requests == [], 'Acceptance posted before the final confirmation'
    dialog.locator('[data-acceptance-cancel]').click()
    expect(dialog).not_to_be_visible()
    assert requests == [], 'Cancel must not write a settlement'
    form.locator('button[type="submit"]').click()
    dialog.locator('[data-acceptance-confirm]').click()
    expect(body.locator('[data-acceptance-dialog]')).not_to_be_visible()
    assert len(requests) == 1, 'Confirmation must post exactly once'
    assert 'name="paymentPeriodId"\r\n\r\n41' in requests[0]
    assert 'name="previewToken"\r\n\r\nsaved-preview' in requests[0]
    # After replacing the form with a response, a second attempt needs a fresh confirmation.
    form = body.locator('[data-event-acceptance-form]')
    form.locator('[name="paymentPeriodId"]').select_option('41')
    form.locator('button[type="submit"]').click()
    expect(form.locator('[data-acceptance-dialog]')).to_be_visible()
    assert len(requests) == 1, 'A replacement form silently bypassed confirmation'
    page.keyboard.press('Escape')
    expect(form.locator('[data-acceptance-dialog]')).not_to_be_visible()
    expect(body).to_be_visible()
    assert page.locator('[data-event-details-dialog]').evaluate("element => element.classList.contains('show')"), 'Escape also dismissed the event details'
    assert not errors, errors
    browser.close()

print('PASS acceptance confirmation, cancellation, exact payload and fresh confirmation after DOM replacement')
