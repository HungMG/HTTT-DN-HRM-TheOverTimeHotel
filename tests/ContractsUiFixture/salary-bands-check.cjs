const assert = require('node:assert/strict');
const {chromium} = require('/home/bryan/.agents/skills/playwright-skill/node_modules/playwright');
const base = process.env.CONTRACT_FIXTURE_URL || 'http://127.0.0.1:18774';
(async () => {
 const browser=await chromium.launch({headless:true,executablePath:'/home/bryan/.cache/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell',args:['--no-sandbox']});
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base+'/CauHinhLuong');
  const trigger=page.getByRole('button',{name:'Khung lương cấp bậc',exact:true});
  const modal=page.locator('#salaryBandsModal');
  async function open(){await trigger.click();await modal.waitFor({state:'visible'});await page.waitForFunction(()=>!bootstrap.Modal.getInstance(document.getElementById('salaryBandsModal'))._isTransitioning);}
  async function close(){await page.keyboard.press('Escape');await modal.waitFor({state:'hidden'});await page.waitForFunction(()=>document.activeElement?.classList.contains('salary-band-trigger'));}
  const url=page.url();await open();
  assert.equal(page.url(),url,'Opening must not navigate');
  assert.equal(await modal.locator('[data-salary-band-id]').count(),13);
  for(const [name,count] of [['Nhân viên',9],['Quản lý',3],['Tổng Giám Đốc',1]])assert.equal(await modal.locator(`tbody[aria-label="${name}"] [data-salary-band-id]`).count(),count);
  assert.match(await modal.locator('tbody[aria-label="Nhân viên"] tr[data-salary-band-id]').first().innerText(),/Trung cấp.*Dưới 1 năm.*6\.000\.000.*8\.000\.000/s);
  assert.match(await modal.locator('tbody[aria-label="Tổng Giám Đốc"]').innerText(),/25\.000\.000.*40\.000\.000/s);
  assert.equal(await modal.locator('input,select,textarea,form').count(),0,'Read-only lookup');
  const closeBounds=await modal.getByRole('button',{name:'Đóng khung lương',exact:true}).boundingBox();
  const contentBounds=await modal.locator('.modal-content').boundingBox();
  assert.ok(contentBounds.x+contentBounds.width-closeBounds.x-closeBounds.width<32,'Header close must align right');
  await page.screenshot({path:'/tmp/hrm-salary-bands-desktop.png'});
  await close();
  await page.locator('.payroll-category-tab').filter({hasText:'Phụ cấp'}).click();
  await page.waitForFunction(()=>document.querySelector('.payroll-category-tab.is-active')?.textContent.trim()==='Phụ cấp');
  await open();assert.equal(await modal.locator('[data-salary-band-id]').count(),13);await close();
  await page.setViewportSize({width:390,height:844});
  await open();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'Page must not overflow on mobile');
  assert.equal(await modal.locator('.table-responsive').evaluate(el=>el.scrollWidth>el.clientWidth),false,'Salary table must fit mobile');
  assert.equal(await modal.locator('.modal-footer button').evaluate(el=>el.scrollWidth>el.clientWidth),false,'Footer close label must fit');
  await page.screenshot({path:'/tmp/hrm-salary-bands-mobile.png'});
  await close();
  for(const mode of ['empty','error']){
   await page.goto(base+'/CauHinhLuong?bandsMode='+mode);await open();
   assert.equal(await modal.locator('[data-salary-band-id]').count(),0);
   assert.match(await modal.innerText(),mode==='empty'?/Chưa có khung lương/:/Không thể tải khung lương/);
   await close();
  }
  assert.deepEqual(errors,[]);
  console.log('PASS: 13 real-controller fixture bands; 9/3/1 groups; no navigation or write controls; category switching; keyboard close/focus; mobile fit; empty/error states; no JavaScript errors.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
