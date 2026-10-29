"""Run against the freshly provisioned local acceptance DB and actual WebAdmin.
Usage: /path/to/venv/bin/python tests/PayrollBudgetAcceptance/browser.py
Requires playwright with chromium installed. Uses only disposable test accounts.
"""
import json
import os
import zipfile
from pathlib import Path
from urllib.parse import urlparse, parse_qs
from playwright.sync_api import sync_playwright, expect

BASE = "http://127.0.0.1:5065"
OUT = Path(os.environ.get("HRM_ACCEPTANCE_ARTIFACTS", "/tmp/hrm-budget-acceptance"))
OUT.mkdir(parents=True, exist_ok=True)
checks = []

def checked(name):
    checks.append(name)
    print("PASS", name, flush=True)

def ready(page):
    page.wait_for_load_state("networkidle")

def login(context, user):
    page = context.new_page()
    page.goto(BASE + "/Account/Login?returnUrl=%2FPayrollBudget%3Fnam%3D2026")
    ready(page)
    page.locator('[name="Username"]').fill(user)
    page.locator('[name="Password"]').fill("Acceptance-local-only-2026!")
    page.locator("#loginSubmitBtn").click()
    ready(page)
    assert "/Account/Login" not in page.url, page.locator("body").inner_text()
    page.goto(BASE + "/PayrollBudget?nam=2026")
    ready(page)
    expect(page.get_by_role("main").get_by_role("heading", name="Ngân sách lương được duyệt", exact=True)).to_be_visible()
    return page

def row(page):
    return page.locator("section").filter(has=page.get_by_role("heading", name="Tháng 7/2026 · Acceptance HR", exact=True))

def filter_value(page, selector, value):
    with page.expect_response(lambda r: "/BaoCao?" in r.url and r.request.resource_type == "fetch" and r.status == 200):
        page.locator(selector).select_option(value)
    page.wait_for_function("() => !document.querySelector('[data-baocao-loading]') || document.querySelector('[data-baocao-loading]').hidden")
    ready(page)

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    maker_context = browser.new_context(viewport={"width": 1440, "height": 1000}, accept_downloads=True)
    checker_context = browser.new_context(viewport={"width": 1440, "height": 1000})
    errors = []
    maker_context.on("page", lambda page: page.on("pageerror", lambda error: errors.append(str(error))))
    checker_context.on("page", lambda page: page.on("pageerror", lambda error: errors.append(str(error))))
    maker = login(maker_context, "budget7101")
    checker = login(checker_context, "budget7102")
    checked("Two separate browser sessions log in with account IDs different from employee IDs")

    maker.locator("#new-month").fill("7")
    maker.locator("#new-dept").select_option("701")
    maker.locator("#new-amount").fill("13000000")
    maker.get_by_role("button", name="Lưu nháp", exact=True).click()
    ready(maker)
    expect(row(maker)).to_contain_text("Người lập #7101")
    checked("Browser creates draft with correct employee identity")

    stale = maker_context.new_page()
    stale.goto(BASE + "/PayrollBudget?nam=2026")
    ready(stale)
    row(maker).locator('[name="SoTien"]').fill("14000000")
    row(maker).get_by_role("button", name="Lưu sửa").click()
    ready(maker)
    row(stale).locator('[name="SoTien"]').fill("15000000")
    row(stale).get_by_role("button", name="Lưu sửa").click()
    ready(stale)
    expect(stale.locator("body")).to_contain_text("Bản ghi đã đổi trạng thái")
    expect(row(stale)).to_contain_text("14,000,000")
    checked("Stale browser edit is rejected and preserves the first saved amount")

    row(maker).get_by_role("button", name="Gửi duyệt", exact=True).click()
    ready(maker)
    expect(row(maker)).to_contain_text("CHO_DUYET")
    expect(row(maker).get_by_role("button", name="Duyệt", exact=True)).to_have_count(0)
    checker.reload()
    ready(checker)
    row(checker).get_by_role("button", name="Duyệt", exact=True).click()
    ready(checker)
    expect(row(checker)).to_contain_text("DA_DUYET")
    expect(row(checker)).to_contain_text("Người duyệt #7102")
    row(checker).get_by_role("button", name="Khóa ngân sách", exact=True).click()
    ready(checker)
    expect(row(checker)).to_contain_text("KHOA")
    expect(row(checker).locator('[name="SoTien"]')).to_have_count(0)
    checker.screenshot(path=str(OUT / "budget-workflow.png"), full_page=True)
    checked("Browser submits, blocks maker approval, independently approves and locks")

    maker.goto(BASE + "/BaoCao?tab=quy-luong&ky=THANG&nam=2026&thang=10&phongBan=701")
    ready(maker)
    expect(maker.locator("#baocaoPhongBan")).to_have_value("701")
    expect(maker.locator("[data-baocao-board-host]")).to_contain_text("12,000,000")
    expect(maker.locator("[data-baocao-board-host]")).to_contain_text("-3,000,000")
    checked("Rendered report reconciles budget 12m, approved gross 9m, variance -3m")
    filter_value(maker, "#baocaoThang", "9")
    assert parse_qs(urlparse(maker.url).query)["thang"] == ["9"]
    expect(maker.locator("[data-baocao-board-host]")).to_contain_text("Tháng 09/2026")
    filter_value(maker, "#baocaoKy", "QUY")
    filter_value(maker, "#baocaoQuy", "4")
    assert parse_qs(urlparse(maker.url).query)["quy"] == ["4"]
    filter_value(maker, "#baocaoKy", "NAM")
    assert parse_qs(urlparse(maker.url).query)["ky"] == ["NAM"]
    filter_value(maker, "#baocaoPhongBan", "")
    assert "phongBan" not in parse_qs(urlparse(maker.url).query)
    checked("Live AJAX month, quarter, year and department filters update server data and URL")
    maker.goto(BASE + "/BaoCao?tab=quy-luong&ky=THANG&nam=2026&thang=10&phongBan=701")
    ready(maker)
    maker.screenshot(path=str(OUT / "report-reconciliation.png"), full_page=True)
    with maker.expect_download() as pending:
        maker.locator("[data-baocao-export-excel]").click()
    download = pending.value
    download.save_as(OUT / "report.xlsx")
    with zipfile.ZipFile(OUT / "report.xlsx") as workbook:
        sheet = workbook.read("xl/worksheets/sheet1.xml").decode()
        assert "9000000" in sheet and "12000000" in sheet
    with maker.expect_download() as pending:
        maker.locator("[data-baocao-export]").click()
    pending.value.save_as(OUT / "report.csv")
    assert "9000000" in (OUT / "report.csv").read_text(encoding="utf-8-sig")
    checked("Browser downloads genuine XLSX and CSV with reconciled values")

    with maker.expect_popup() as pending:
        maker.locator("[data-baocao-print]").click()
    printed = pending.value
    ready(printed)
    expect(printed.locator("body")).to_contain_text("9,000,000")
    printed.pdf(path=str(OUT / "report.pdf"), format="A4", print_background=True)
    checked("Print route renders authenticated report and Chromium exports PDF")
    assert not errors, errors
    checked("No uncaught JavaScript errors in tested browser flows")
    (OUT / "browser-results.json").write_text(json.dumps({"passed": len(checks), "checks": checks}, ensure_ascii=False, indent=2))
    browser.close()
print(f"BrowserAcceptance: {len(checks)} passed, 0 failed.")
