"""Exercise actual Razor work correction forms and popup JavaScript; HTTP is mocked, no database writes."""
from pathlib import Path
from playwright.sync_api import sync_playwright, expect

root = Path.cwd()
web = root / 'src/OvertimeHotel.HRM.WebAdmin/wwwroot'
export = root / '.probe/codex-phase1-html'
calendar = (export / 'calendar.html').read_text(encoding='utf-8')
work = (export / 'hourly-work.html').read_text(encoding='utf-8')
invalid = (export / 'hourly-work-invalid.html').read_text(encoding='utf-8')
requests = []

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))

    def respond(route):
        request = route.request
        if request.method == 'POST':
            requests.append(request.post_data)
            route.fulfill(content_type='text/html; charset=utf-8', body=invalid if len(requests) == 1 else work)
        elif '/SuKien/Details' in request.url:
            route.fulfill(content_type='text/html; charset=utf-8', body=work)
        else:
            route.fulfill(content_type='text/html; charset=utf-8', body=calendar)

    page.route('http://phase2.test/**', respond)
    page.goto('http://phase2.test/')
    for script in ['lib/bootstrap/dist/js/bootstrap.bundle.min.js', 'js/events.js', 'js/event-details.js']:
        page.add_script_tag(path=str(web / script))
    page.evaluate("document.dispatchEvent(new Event('DOMContentLoaded'))")
    page.locator('[data-event-link][data-event-id="17"]').first.click()
    body = page.locator('[data-event-details-body]')
    edit = body.locator('input[name="Work.WorkId"][value="81"]').locator('xpath=ancestor::details[1]')
    edit.locator('summary').click()
    edit.locator('[name="Work.Start"]').fill('2028-05-10T09:30')
    edit.locator('[name="Work.Reason"]').fill('Correct entry with evidence')
    edit.locator('button[type="submit"]').click()
    expect(body.locator('.validation-summary-errors')).to_contain_text('Review attendance evidence')
    edit = body.locator('input[name="Work.WorkId"][value="81"]').locator('xpath=ancestor::details[1]')
    expect(edit.locator('[name="Work.Start"]')).to_have_value('2028-05-10T09:30')
    expect(edit.locator('[name="Work.Reason"]')).to_have_value('Correct entry with evidence')
    edit.locator('[name="Work.End"]').fill('2028-05-10T09:45')
    edit.locator('button[type="submit"]').click()
    expect(body.locator('.validation-summary-errors')).to_have_count(0)
    assert len(requests) == 2, 'Correction posted an unexpected number of times'
    for payload in requests:
        assert 'name="Work.WorkId"\r\n\r\n81' in payload, 'Correction lost the existing work ID'
        assert 'name="Work.Reason"\r\n\r\nCorrect entry with evidence' in payload, 'Correction lost its reason'
    assert '2028-05-10T09:45' in requests[1], 'Corrected end time was not submitted after DOM replacement'
    assert not errors, errors
    browser.close()
print('PASS hourly correction submission, error preservation and resubmission after popup replacement')
