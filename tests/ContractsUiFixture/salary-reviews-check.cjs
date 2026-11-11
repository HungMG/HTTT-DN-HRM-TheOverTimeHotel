const assert = require('node:assert/strict');
const {chromium} = require('/home/bryan/.agents/skills/playwright-skill/node_modules/playwright');
const base = process.env.CONTRACT_FIXTURE_URL || 'http://127.0.0.1:18769';
(async () => {
 const browser=await chromium.launch({headless:true,executablePath:'/home/bryan/.cache/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell',args:['--no-sandbox']});
 try {
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  const errors=[]; page.on('pageerror', e=>errors.push(e.message));
  let mode='normal'; let requests=0;
  await page.route('**/HopDong/SalaryReviews*',async route=>{
   requests++;
   assert.equal(route.request().headers()['x-requested-with'],'XMLHttpRequest');
   if(mode==='slow') { await new Promise(r=>setTimeout(r,1000)); }
   if(mode==='error') return route.fulfill({status:500,body:'Test server unavailable'});
   if(mode==='empty') { const url=new URL(route.request().url()); url.searchParams.set('empty','true'); const response=await route.fetch({url:url.toString()}); return route.fulfill({response}); }
   return route.continue().catch(() => {});
  });
  await page.goto(base+'/HopDong');
  const opener=page.getByRole('button',{name:'Xem xét lương',exact:true});
  const modal=page.locator('#salaryReviewsModal');
  const originalUrl=page.url();
  async function open() { await opener.click(); await page.waitForFunction(() => { const el=document.getElementById('salaryReviewsModal'); const instance=bootstrap.Modal.getInstance(el); return el.classList.contains('show') && instance && !instance._isTransitioning; }); }
  await open();
  await modal.locator('.salary-review-row').first().waitFor();
  assert.equal(page.url(),originalUrl,'Must not navigate');
  assert.match(await modal.innerText(),/Nguyễn Ngọc Linh/);
  assert.match(await modal.innerText(),/Cần xác minh/);
  assert.match(await modal.innerText(),/Trần Minh Anh/);
  assert.equal(await modal.locator('html,body,header.app-topbar').count(),0);
  await page.screenshot({path:'/tmp/hrm-salary-reviews-desktop.png'});
  await page.keyboard.press('Escape');
  await modal.waitFor({state:'hidden'});
  await page.waitForFunction(() => document.activeElement === document.querySelector('[data-bs-target="#salaryReviewsModal"]'));
  assert(await opener.evaluate(el=>el===document.activeElement),'Return focus');
  mode='error'; await open(); await modal.getByRole('button',{name:'Thử lại'}).waitFor();
  assert.match(await modal.innerText(),/Chưa tải được/);
  mode='normal'; await modal.getByRole('button',{name:'Thử lại'}).click(); await modal.locator('.salary-review-row').first().waitFor();
  await modal.getByRole('button',{name:'Đóng',exact:true}).click(); await modal.waitFor({state:'hidden'});
  mode='empty'; await open(); await modal.locator('[data-reviews-empty]').waitFor();
  assert.match(await modal.innerText(),/Chưa có nhân viên nào cần xét lương/);
  await page.keyboard.press('Escape'); await modal.waitFor({state:'hidden'});
  mode='slow'; await open(); await modal.getByRole('status').waitFor(); await page.keyboard.press('Escape'); await modal.waitFor({state:'hidden'});
  mode='normal'; await page.setViewportSize({width:390,height:844}); await open(); await modal.locator('.salary-review-row').first().waitFor();
  const bounds=await modal.locator('.modal-content').boundingBox(); assert(bounds.x>=0 && bounds.x+bounds.width<=391,'Mobile modal fits');
  assert(await modal.locator('[data-reviews-content]').evaluate(el=>el.scrollWidth<=el.clientWidth+1),'No horizontal list overflow');
  await page.screenshot({path:'/tmp/hrm-salary-reviews-mobile.png'});
  await page.keyboard.press('Escape'); await modal.waitFor({state:'hidden'});
  await page.locator('.modal-backdrop').waitFor({state:'detached'});
  assert.equal(await page.locator('.modal-backdrop').count(),0);
  assert.deepEqual(errors,[]);
  assert(requests>=6);
  console.log('PASS salary review popup: no navigation, real partial, focus/Escape, error/retry, empty/loading/abort, desktop/mobile fit, no JS errors');
 } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exit(1)});
