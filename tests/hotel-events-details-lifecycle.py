"""Run after hotel-events-details.py with the same owned preview fixture."""
import json
import sys
from pathlib import Path
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parent.parent
fixture = json.loads((root / '.probe/event-preview.json').read_text(encoding='utf-8-sig'))
base = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5083'
event_id = fixture['PastEventId']

def login(browser, actor):
    page = browser.new_page(viewport={'width': 1440, 'height': 1000}, reduced_motion='reduce')
    page.set_default_timeout(60000)
    page.goto(base + '/Account/Login')
    page.locator('[name=Username]').fill(fixture[actor]['Username'])
    page.locator('[name=Password]').fill(fixture['Password'])
    with page.expect_navigation(wait_until='domcontentloaded'):
        page.locator('button[type=submit]').click()
    return page

with sync_playwright() as p:
    browser = p.chromium.launch(channel='msedge', headless=True)
    page = login(browser, 'Admin')
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    day = fixture['PastEventDate']
    calendar = f'{base}/SuKien?year={day[:4]}&month={day[5:7]}&date={day}'
    page.goto(calendar)
    page.locator('[data-selected-events] a').filter(has_text=fixture['Prefix']).click()
    dialog = page.locator('[data-event-details-dialog]')
    try:
        dialog.locator('[data-event-details]').wait_for()
    except Exception:
        print('Details load diagnostic:', ascii(dialog.inner_text()), 'JS errors:', errors)
        raise
    dialog.get_by_role('button', name='Nhân sự', exact=False).click()
    roles = {
        'TIEN_SANH':['Tiếp nhận đăng ký','Đón tiếp đại biểu'],
        'AM_THUC':['Phục vụ tiệc trà','Phục vụ đồ uống'],
        'BEP':['Chế biến suất ăn','Chuẩn bị tiệc trà'],
        'KY_THUAT':['Phụ trách âm thanh và micro','Trực thiết bị hội trường'],
        'IT':['Phụ trách mạng và Wi-Fi','Hỗ trợ máy tính và trình chiếu'],
        'AN_NINH':['Kiểm soát thẻ đại biểu','Bảo đảm an ninh hội trường']}
    for department, role_names in roles.items():
      for index, role_name in enumerate(role_names):
        worker = fixture['RoleWorkers'][department][index]
        card = dialog.locator(f'[data-department-card="{department}"]')
        if card.locator(f'[data-employee-id="{worker}"]').count():
            continue
        card.locator('[data-add-member]').click()
        assignment = page.locator('[data-event-assignment-dialog]')
        assignment.locator(f'[data-choose-member="{worker}"]').click()
        assignment.locator('[data-continue-assignment]').click()
        form = assignment.locator('[data-assignment-popup-form]')
        form.wait_for()
        form.locator('[name="Assignments[0].Role"]').select_option(role_name)
        form.locator('details').evaluate('(element)=>element.open=true')
        form.locator('[name="Assignment.Allowance"]').fill('50')
        form.get_by_role('button', name='Lưu phân công').click()
        assignment.wait_for(state='hidden')
        dialog.wait_for(state='visible')
        card.locator(f'[data-employee-id="{worker}"]').wait_for()
    assert dialog.locator('[data-assigned-member]').count() == 12
    assert dialog.locator('[data-role-missing]:not([data-role-missing="0"])').count() == 0
    page.screenshot(path=str(root / '.probe/event-details-staff-desktop.png'), full_page=True)
    # An old version must return an inline error and preserve the entered role.
    tech = dialog.locator('[data-department-card="KY_THUAT"]')
    tech.locator('[data-add-member]').click()
    spare_worker = fixture['RoleWorkers']['KY_THUAT'][2]
    page.locator('[data-event-assignment-dialog]').locator(f'[data-choose-member="{spare_worker}"]').click()
    page.locator('[data-continue-assignment]:visible').click()
    form = page.locator('[data-assignment-popup-form]')
    form.wait_for()
    form.locator('[name="Assignments[0].Role"]').select_option('__OTHER__')
    form.locator('[name="Assignments[0].OtherRole"]').fill('Vai trò cần giữ lại')
    version = form.locator('[name="Assignment.Version"]')
    version.evaluate('(element)=>element.value=Number(element.value)-1')
    form.get_by_role('button', name='Lưu phân công').click()
    page.locator('[data-event-assignment-dialog] .validation-summary-errors').wait_for()
    assert page.locator('[name="Assignments[0].Role"]').input_value() == '__OTHER__'
    assert page.locator('[name="Assignments[0].OtherRole"]').input_value() == 'Vai trò cần giữ lại'
    page.locator('[data-assignment-popup-form]').get_by_role('button', name='Hủy', exact=True).click()
    dialog.wait_for(state='visible')
    assert dialog.locator('[data-assigned-member]').count() == 12
    dialog.get_by_role('button', name='Thông tin sự kiện', exact=False).click()
    dialog.get_by_role('button', name='Bắt đầu sự kiện', exact=False).click()
    dialog.get_by_role('button', name='Chuyển chờ nghiệm thu', exact=False).wait_for()
    dialog.get_by_role('button', name='Chuyển chờ nghiệm thu', exact=False).click()
    dialog.get_by_role('button', name='Nhân sự', exact=False).click()
    page.wait_for_function('document.querySelector("[data-event-details]").dataset.eventStatus === "CHO_NGHIEM_THU"')
    assert dialog.locator('[data-add-member]').count() == 0
    dialog.get_by_role('button', name='Đánh giá & nghiệm thu', exact=False).click()
    assert dialog.locator('[name="Review.Rating"] option[value="XUAT_SAC"]').count() == 0
    for _ in range(12):
        review = dialog.locator('.event-review-form').filter(has=page.locator('select option[value="HOAN_THANH"]:not([selected])')).first
        # Each successful save rerenders with a canonical selected rating.
        if review.count() == 0:
            break
        review.locator('[name="Review.Rating"]').select_option('HOAN_THANH')
        review.locator('[name="Review.ActualAllowance"]').fill('50')
        with page.expect_response(lambda response: '/SuKien/Review/' in response.url and response.request.method == 'POST'):
            review.get_by_role('button', name='Lưu đánh giá').click()
        page.wait_for_function('!document.querySelector("[data-event-details-body]").hasAttribute("aria-busy") || document.querySelector("[data-event-details-body]").getAttribute("aria-busy") === "false"')
    assert dialog.locator('[name="Review.Rating"] option[value="HOAN_THANH"][selected]').count() == 12
    # Assigned Manager can assess but cannot accept; another Manager cannot read staff.
    manager = login(browser, 'Manager')
    manager.goto(f'{base}/SuKien/Details/{event_id}?tab=acceptance')
    assert manager.locator('[data-event-details]').count() == 1
    assert manager.get_by_role('button', name='Nghiệm thu & khóa').count() == 0
    other = login(browser, 'OtherManager')
    assert other.request.get(f'{base}/SuKien/Details/{event_id}?popup=true').status == 404
    assert other.request.get(f'{base}/SuKien/Assignment/{event_id}?employeeId={fixture["WorkerB"]}&department=KY_THUAT').status == 404
    assert page.request.post(f'{base}/SuKien/Accept/{event_id}', form={'IsPopup':'true'}).status == 400
    dialog.get_by_role('button', name='Nghiệm thu & khóa').click()
    page.wait_for_function('document.querySelector("[data-event-details]").dataset.eventStatus === "HOAN_THANH"')
    assert dialog.locator('.event-review-form').count() == 0
    assert dialog.locator('[data-add-member]').count() == 0
    denied = page.request.get(f'{base}/SuKien/Assignment/{event_id}?employeeId={fixture["WorkerB"]}&department=KY_THUAT')
    assert denied.status == 403 or urlparse(denied.url).path == '/Account/AccessDenied'
    page.set_viewport_size({'width':390,'height':844})
    dialog.get_by_role('button', name='Nhân sự', exact=False).click()
    assert page.evaluate('document.documentElement.scrollWidth <= window.innerWidth + 1')
    page.screenshot(path=str(root / '.probe/event-details-staff-mobile.png'), full_page=True)
    assert urlparse(page.url).path == '/SuKien'
    assert not errors, errors
    print('PASS full staffing, stale version, lifecycle, review grouping/options, acceptance freeze, CSRF, Manager scope and mobile staffing')
    browser.close()
