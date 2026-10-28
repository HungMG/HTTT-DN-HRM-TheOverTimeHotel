"""Check department filtering on real Razor output, in the popup and standalone page; no database writes."""
from pathlib import Path
from playwright.sync_api import sync_playwright, expect

root = Path.cwd()
web = root / 'src/OvertimeHotel.HRM.WebAdmin/wwwroot'
export = root / '.probe/codex-phase1-html'
staff = (export / 'staff-filter.html').read_text(encoding='utf-8')
calendar = (export / 'calendar.html').read_text(encoding='utf-8')
dialogs = (export / 'dialogs.html').read_text(encoding='utf-8')
posts = []

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    for standalone in [False, True]:
        page = browser.new_page(viewport={'width': 1440, 'height': 1100}, reduced_motion='reduce')
        page.set_default_timeout(8000)
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))

        def respond(route):
            request = route.request
            if request.method == 'POST':
                posts.append(request.post_data)
                body = staff
            elif '/SuKien/Details' in request.url:
                body = staff
            elif '/standalone' in request.url:
                body = '<div data-event-standalone>' + staff + '</div>' + dialogs
            else:
                body = calendar
            route.fulfill(content_type='text/html; charset=utf-8', body=body)

        page.route('http://staff-filter.test/**', respond)
        page.goto('http://staff-filter.test/standalone' if standalone else 'http://staff-filter.test/')
        for stylesheet in ['lib/bootstrap/dist/css/bootstrap.min.css', 'css/app-shell.css', 'css/events.css']:
            page.add_style_tag(path=str(web / stylesheet))
        for script in ['lib/bootstrap/dist/js/bootstrap.bundle.min.js', 'js/events.js', 'js/event-details.js']:
            page.add_script_tag(path=str(web / script))
        page.evaluate("document.dispatchEvent(new Event('DOMContentLoaded'))")
        if not standalone:
            page.locator('[data-event-link]').first.click()
        body = page.locator('[data-event-standalone]' if standalone else '[data-event-details-body]')
        directory = body.locator('[data-event-staff]')
        expect(directory.locator('[data-department-card]:visible')).to_have_count(3)
        expect(directory.locator('[data-staff-department-filter="all"]')).to_have_attribute('aria-pressed', 'true')
        # Click the icon itself, rather than the button text.
        directory.locator('[data-staff-department-filter="KY_THUAT"] i').click()
        expect(directory.locator('[data-department-card]:visible')).to_have_count(1)
        expect(directory.locator('[data-department-card="KY_THUAT"]')).to_be_visible()
        expect(directory.locator('[data-staff-department-filter="KY_THUAT"]')).to_have_attribute('aria-pressed', 'true')
        assert '#' not in page.url, 'Department filtering must not navigate to an anchor'
        body.locator('[data-details-section="overview"]').click()
        body.locator('[data-details-section="staff"]').click()
        expect(directory.locator('[data-department-card]:visible')).to_have_count(1)
        # Native buttons also filter using the keyboard.
        directory.locator('[data-staff-department-filter="TIEN_SANH"]').focus()
        page.keyboard.press('Enter')
        expect(directory.locator('[data-department-card="TIEN_SANH"]')).to_be_visible()
        expect(directory.locator('[data-department-card="KY_THUAT"]')).to_be_hidden()
        directory.locator('[data-staff-department-filter="AN_NINH"]').click()
        expect(directory.locator('[data-department-card]:visible')).to_have_count(1)
        expect(directory.locator('[data-assigned-member]:visible')).to_have_count(0)
        directory.locator('[data-staff-department-filter="all"]').click()
        expect(directory.locator('[data-department-card]:visible')).to_have_count(3)
        expect(directory.locator('[data-assigned-member]')).to_have_count(2)
        expect(directory.locator('[data-employee-id="12"]')).to_have_count(0)
        assert 'Lịch sử phân công' not in directory.inner_text()
        assert posts == [], 'Filtering sent a write request'
        member = directory.locator('[data-assigned-member]').first
        assert member.evaluate('(el)=>el.tagName') == 'ARTICLE', 'Employee details still use a compact table row'
        card_box = member.bounding_box()
        grid_box = member.locator('..').bounding_box()
        assert card_box['width'] >= grid_box['width'] * .95, 'Employee cards must fill one horizontal row'
        assert card_box['height'] < 220, 'Employee cards remain too tall on desktop'
        for label in ['Vai trò / Nhiệm vụ', 'Ca nguồn', 'Thời gian tham gia', 'Trạng thái']:
            expect(member).to_contain_text(label)
        assert member.locator('table').count() == 0
        identity = member.locator('.event-staff-person-card')
        identity_box = identity.bounding_box()
        details_box = member.locator('.event-staff-member-details').bounding_box()
        assert details_box['x'] >= identity_box['x'] + identity_box['width'], 'Details must sit beside the employee name'
        assert float(member.locator('dd').first.evaluate('(el)=>getComputedStyle(el).fontSize').replace('px', '')) >= 16
        assert float(identity.locator('strong').evaluate('(el)=>getComputedStyle(el).fontSize').replace('px', '')) >= 17
        assert identity.locator('.avatar-initials').bounding_box()['width'] >= 48
        if not standalone:
            page.screenshot(path=str(root / '.probe/event-staff-filter/desktop.png'), full_page=True)
            page.set_viewport_size({'width': 390, 'height': 844})
            directory.locator('[data-staff-department-filter="KY_THUAT"]').click()
            expect(directory.locator('[data-department-card]:visible')).to_have_count(1)
            assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth'), 'Staff view overflows the mobile page'
            mobile_card = directory.locator('[data-department-card="KY_THUAT"] [data-assigned-member]')
            assert mobile_card.evaluate('(el)=>el.scrollWidth <= el.clientWidth + 1'), 'Employee details require horizontal scrolling'
            mobile_card.scroll_into_view_if_needed()
            page.screenshot(path=str(root / '.probe/event-staff-filter/mobile.png'), full_page=True)
            page.set_viewport_size({'width': 1440, 'height': 1100})
            # Replacing the popup after saving another tab must retain working delegated filters.
            body.locator('[data-details-section="overview"]').click()
            body.locator('[data-edit-need]').first.click()
            body.locator('[name="Need.Reason"]').fill('Verify staff filters after refresh')
            body.locator('[data-need-form] button[type="submit"]').click()
            page.wait_for_function("document.querySelector('[data-event-details-body]').getAttribute('aria-busy') === 'false'")
            body.locator('[data-details-section="staff"]').click()
            directory.locator('[data-staff-department-filter="TIEN_SANH"]').click()
            expect(directory.locator('[data-department-card]:visible')).to_have_count(1)
            expect(directory.locator('[data-department-card="TIEN_SANH"]')).to_be_visible()
            assert len(posts) == 1
            posts.clear()
        assert not errors, errors
        page.close()
    browser.close()
print('PASS staff filters in popup and standalone, keyboard, empty departments, mobile cards, and AJAX replacement')
