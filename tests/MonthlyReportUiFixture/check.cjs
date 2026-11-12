const { chromium } = require('/home/bryan/.agents/skills/playwright-skill/node_modules/playwright');
const base=process.env.REPORT_FIXTURE_URL || 'http://127.0.0.1:18765';
(async () => {
  const browser = await chromium.launch({headless:true, executablePath:'/home/bryan/.cache/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell', args:['--no-sandbox']});
  const page = await browser.newPage({viewport:{width:1280,height:900}});
  for (const [tab, expected] of [['co-cau','Có nghỉ phép trong tháng'], ['hoc-van','Chưa có dữ liệu']]) {
    const response = await page.goto(`${base}/fixture/${tab}`);
    if (response.status() !== 200) throw new Error(`HTTP ${response.status()} for ${tab}`);
    const panel = page.locator(`[data-baocao-panel="${tab}"]`);
    if (!await page.getByText('So sánh với năm trước', {exact:false}).count()) throw new Error('Comparison heading must clearly mention last year');
    if (!await page.getByText('Tháng 10/2025', {exact:false}).count()) throw new Error('Comparison must identify the actual previous period');
    if (!await panel.isVisible() || !await panel.getByText(expected, {exact:false}).count()) throw new Error(`Missing ${expected}`);
    if (tab === 'co-cau') {
      const labels = await panel.locator('.baocao-kpi-grid .catalog-kpi-label').allTextContents();
      const expectedLabels = ['Nhân sự cuối tháng', 'Đang làm việc', 'Có nghỉ phép trong tháng', 'Nghỉ việc trong tháng'];
      if (JSON.stringify(labels.map(x => x.trim())) !== JSON.stringify(expectedLabels)) throw new Error(`Wrong personnel summary: ${labels}`);
    }
    if (await panel.locator('[data-baocao-drill], [data-personnel-section], [data-personnel-select]').count()) throw new Error('Personnel reports must show aggregate figures without employee lists');
  }
  await page.goto(base+'/fixture/hoc-van?manager=true');
  if (await page.locator('[data-baocao-panel="hoc-van"]').getByText('Lương cơ bản TB').count()) throw new Error('Manager must not see salary statistics');
  await page.goto(base+'/fixture/quy-luong');
  const fund=page.locator('[data-baocao-panel="quy-luong"]');
  if(!await fund.getByText('Định mức (hồ sơ hợp lệ)',{exact:true}).count() || !await fund.locator('.alert-warning').getByText('định mức chưa đầy đủ',{exact:false}).count())throw new Error('Fund must disclose incomplete references');
  await page.goto(base+'/fixture/quy-luong?print=true');
  if(!await page.getByText('Định mức (hồ sơ hợp lệ)',{exact:true}).count() || !await page.getByText('định mức chưa đầy đủ',{exact:false}).count())throw new Error('Printed report must preserve incomplete-reference warning');
  await browser.close();
  console.log('Razor fixture browser checks passed.');
})().catch(error => { console.error(error); process.exit(1); });
