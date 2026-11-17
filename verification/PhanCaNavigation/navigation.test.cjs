const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const storage = new Map([['overtime_phanca_active_tab', 'matrix']]);
const handlers = {};
function element(attrs = {}) { return { attrs, value:'', classList: { add(){}, remove(){}, toggle(){} }, addEventListener(name, fn){this[name]=fn;}, getAttribute(k){return this.attrs[k]}, setAttribute(k,v){this.attrs[k]=v} }; }
const elements = {
 btnCalPrevMonth: element({'data-year':'2026','data-month':'9'}),
 btnCalNextMonth: element({'data-year':'2026','data-month':'11'}),
 btnCalToday: element(), filterPhongBan: element(), filterCa: element(),
 tabViewCalendar: element(), tabViewMatrix: element(), tabViewList: element(),
 viewCalendarContainer: element(), viewMatrixContainer: element(), viewListContainer: element(),
 matrixWeekFilter: element()
};
elements.filterPhongBan.value='9'; elements.filterCa.value='2';
let navigated='';
const location={origin:'http://localhost:5065',href:'http://localhost:5065/PhanCa?year=2026&month=10&viewMode=matrix',assign(url){navigated=url;}};
const window={location,history:{state:null,replaceState(){}},phancaCalendarData:{year:2026,month:10,selectedDate:'2026-10-01'}};
const document={addEventListener(name,fn){handlers[name]=fn;},getElementById(id){return elements[id]||null;},querySelectorAll(){return []}};
const localStorage={getItem(k){return storage.get(k)||null},setItem(k,v){storage.set(k,v)}};
const context={document,localStorage,URL,Date,console,...window}; context.window=context;
vm.runInNewContext(fs.readFileSync('src/OvertimeHotel.HRM.WebAdmin/wwwroot/js/phanca-calendar.js','utf8'),context);
handlers.DOMContentLoaded();
elements.btnCalPrevMonth.click({preventDefault(){}});
let result=new URL(navigated); assert.equal(result.searchParams.get('year'),'2026'); assert.equal(result.searchParams.get('month'),'9'); assert.equal(result.searchParams.get('viewMode'),'matrix'); assert.equal(result.searchParams.get('phongBanId'),'9'); assert.equal(result.searchParams.get('caId'),'2');
context.selectMatrixWeek('2026-10-26'); result=new URL(navigated); assert.equal(result.searchParams.get('selectedDate'),'2026-10-26'); assert.equal(result.searchParams.get('month'),'10'); assert.equal(result.searchParams.get('viewMode'),'matrix');
const previous=navigated;context.selectMatrixWeek('invalid');assert.equal(navigated,previous);
context.phancaWeekContextStale=true;context.switchCalendarViewTab('matrix');result=new URL(navigated);assert.equal(result.searchParams.get('selectedDate'),'2026-10-01'); assert.equal(result.searchParams.get('viewMode'),'matrix');
console.log('Matrix month navigation, week selection, filters and stale context: passed');
