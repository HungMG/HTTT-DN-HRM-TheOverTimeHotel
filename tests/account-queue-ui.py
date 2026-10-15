"""Browser regression for queue actions; HTTP fixtures avoid provisioning real accounts."""
from pathlib import Path
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
view = (root / 'src/OvertimeHotel.HRM.WebAdmin/Views/YeuCauTaiKhoan/Index.cshtml').read_text(encoding='utf-8')
script = view.split('@section Scripts {', 1)[1].split('<script>', 1)[1].split('</script>', 1)[0]


def table(status):
    action = 'Claim' if status == 'CHO_IT' else 'Complete'
    form = '' if status == 'DA_CAP' else f'''
        <form action="/YeuCauTaiKhoan/{action}/42" method="post" data-request-action-form>
            <input name="__RequestVerificationToken" value="test-csrf" type="hidden">
            <button type="submit">{action}</button>
        </form>'''
    return f'<b data-request-result-count>1</b><table><tbody><tr data-request-row="42"><td>{status}{form}</td></tr></tbody></table>'


with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = browser.new_page()
    errors, requests = [], []
    state = {'status': 'CHO_IT', 'fail_complete': True}
    page.on('pageerror', lambda error: errors.append(str(error)))

    def respond(route):
        request = route.request
        path = urlparse(request.url).path
        requests.append((request.method, request.resource_type, request.url))
        if request.method == 'POST':
            assert request.headers.get('x-requested-with') == 'XMLHttpRequest'
            assert 'test-csrf' in request.post_data
            if '/Complete/' in path and state['fail_complete']:
                state['fail_complete'] = False
                route.fulfill(status=400, json={'success': False, 'message': 'Provisioning failed'})
                return
            state['status'] = 'DANG_XU_LY' if '/Claim/' in path else 'DA_CAP'
            route.fulfill(json={'success': True, 'message': 'Updated'})
        else:
            route.fulfill(body=table(state['status']), content_type='text/html', headers={
                'X-Total-Requests': '1', 'X-Result-Count': '1',
                'X-Processing-Requests': '1' if state['status'] == 'DANG_XU_LY' else '0',
                'X-Granted-Requests': '1' if state['status'] == 'DA_CAP' else '0',
            })

    page.route('http://queue.test/**', respond)
    page.goto('http://queue.test/YeuCauTaiKhoan?Keyword=test&page=2')
    page.set_content('''
        <div data-request-workspace>
            <form action="/YeuCauTaiKhoan" data-request-filter-form>
                <input name="Keyword" value="test"><input name="page" value="2" data-request-page-input>
            </form>
            <div class="account-popup-stack"></div>
            <b data-request-processing>0</b><b data-request-granted>0</b>
            <div id="requestLoadingOverlay" class="d-none"></div>
            <div id="requestTableContainer"></div>
        </div>''')
    page.locator('#requestTableContainer').evaluate('(e, html) => e.innerHTML = html', table(state['status']))
    page.evaluate('''() => {
        window.queuePageMarker = {};
        window.refreshEffects = [];
        window.HotelMotion = {
            applyEntrance: () => refreshEffects.push('entrance'),
            applyBlurText: () => refreshEffects.push('blur')
        };
        new MutationObserver(records => {
            for (const record of records) {
                if (record.oldValue.includes('d-none') && !record.target.classList.contains('d-none'))
                    refreshEffects.push('overlay');
            }
        }).observe(document.getElementById('requestLoadingOverlay'), {
            attributes: true, attributeFilter: ['class'], attributeOldValue: true
        });
    }''')
    page.add_script_tag(content=script)
    requests.clear()
    page.locator('form[action*="Claim"] button').click()
    page.locator('form[action*="Complete"] button').wait_for()
    assert page.evaluate('refreshEffects.length === 0'), 'Actions must not replay table loading/entrance effects'
    assert page.locator('[name=Keyword]').input_value() == 'test'
    assert page.locator('[data-request-page-input]').input_value() == '2'

    # Newly rendered forms must work; an error leaves the action available to retry.
    page.locator('form[action*="Complete"] button').click()
    page.get_by_text('Provisioning failed', exact=True).wait_for()
    assert page.locator('form[action*="Complete"] button').is_enabled()
    page.locator('form[action*="Complete"] button').click()
    page.locator('[data-request-row]').filter(has_text='DA_CAP').wait_for()
    page.wait_for_function('document.querySelector("[data-request-granted]").textContent === "1"')
    assert page.evaluate('refreshEffects.length === 0')
    assert page.evaluate('Boolean(window.queuePageMarker)')
    assert not any(kind == 'document' for _, kind, _ in requests), 'Actions must never navigate/reload'
    assert len([method for method, _, _ in requests if method == 'POST']) == 3
    assert all('Keyword=test' in url and 'page=2' in url for method, _, url in requests if method == 'GET')
    assert not errors, errors
    browser.close()
    print('PASS claim, complete, retry, CSRF, filters and pagination without page reload or table loading effects')
