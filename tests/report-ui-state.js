// Runs the actual filter functions without a server or npm dependencies.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync('src/OvertimeHotel.HRM.WebAdmin/wwwroot/js/baocao.js', 'utf8');
const fields = {ky:{value:'THANG'},nam:{value:'2024'},thang:{value:'2'},quy:{value:'1'},phongBan:{value:'3'}};
const board = {dataset:{ky:'THANG',nam:'2026',thang:'10',quy:'',phongBan:'',tab:'bien-dong'}};
const context = {URL, filterForm:{querySelector:s=>fields[s.match(/name="(\w+)"/)[1]]},
  currentBoard:()=>board, kySelect:fields.ky, syncPeriodFields:()=>{}, normalizeTab:x=>x,
  applyTab:()=>{}, activeTab:'bien-dong', window:{location:{origin:'http://localhost'}},
  FormData:class {forEach(fn){for(const [k,f] of Object.entries(fields))fn(f.value,k);}}};
vm.createContext(context);
vm.runInContext(source.slice(source.indexOf('  function syncFormFromUrl('),source.indexOf('  async function refresh(')),context);
context.syncFormFromUrl(new URL('http://localhost/BaoCao'));
assert.equal(fields.nam.value,'2026','Reset must use applied server defaults');
assert.equal(fields.thang.value,'10');
assert.equal(fields.phongBan.value,'','Reset must clear the department');
assert.equal(fields.quy.value,'');
assert.match(source,/refresh\(new URL\(window.location.href\), false\)/,'Back must not append history');
console.log('PASS report filters restore the actual applied defaults and clear omitted department');
// A tab click during a pending filter change must keep the applied period in history.
const panel={getAttribute:()=> 'hoc-van',classList:{remove(){},add(){}}};
Object.assign(context,{boardHost:{querySelectorAll:()=>[panel]},tabsNav:null,tabInput:null,
  writeStoredTab(){},visiblePanel:()=>panel,renderCharts(){},root:{getAttribute:()=>'/BaoCao'},
  url:()=>new URL('http://localhost/BaoCao'),
  buildUrl:()=>new URL('http://localhost/BaoCao?nam=2027')});
let pushed='';let replaced='';
context.window.history={pushState:(state,title,url)=>{pushed=String(url);},replaceState:(state,title,url)=>{replaced=String(url);}};
context.window.location.href='http://localhost/BaoCao?nam=2027';
vm.runInContext(source.slice(source.indexOf('  function buildCurrentExportUrl('),source.indexOf('  var exportButton')),context);
vm.runInContext(source.slice(source.indexOf('  function applyTab('),source.indexOf('  // Ghép tham số')),context);
fields.nam.value='2027';
context.applyTab('hoc-van',{pushUrl:true});
assert.equal(new URL(pushed).searchParams.get('nam'),'2026','Tab history must not include unapplied filter values');
Object.assign(context,{AbortController,activeAbort:null,setLoading(){},console:{warn(){}},showError(){},fetch:async()=>{throw new Error('offline');}});
vm.runInContext(source.slice(source.indexOf('  async function refresh('),source.indexOf('  // ═══',source.indexOf('  async function refresh('))),context);
(async()=>{
  await context.refresh(new URL('http://localhost/BaoCao?nam=2027'),false);
  assert.equal(new URL(replaced).searchParams.get('nam'),'2026','Failed navigation must restore canonical applied URL');
  assert.equal(new URL(replaced).searchParams.get('tab'),'hoc-van','Failure must preserve the selected tab');
  console.log('PASS pending-filter tab changes and failed navigation retain applied history');
})();
