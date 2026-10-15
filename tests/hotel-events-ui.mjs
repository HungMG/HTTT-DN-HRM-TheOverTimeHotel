import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { moveDate, calendarUrl, createUrl, handleDayInteraction, validTimeRange, toggleDate, selectionRange, responseError, canCreateSelection } = require('../src/OvertimeHotel.HRM.WebAdmin/wwwroot/js/events.js');

assert.equal(canCreateSelection(['2026-10-15'], '2026-10-16'), false);
assert.equal(canCreateSelection(['2026-10-15', '2026-10-17'], '2026-10-16'), false, 'A mixed selection must not silently create from a past day');
assert.equal(canCreateSelection(['2026-10-16'], '2026-10-16'), true);
assert.equal(canCreateSelection(['2026-10-17', '2026-10-18'], '2026-10-16'), true);
assert.equal(canCreateSelection([], '2026-10-16'), false, 'An empty selection must not create on an unintended day');

const serverError = responseError({ status: 500, ok: false, redirected: false, url: 'http://localhost:5065/SuKien/Create' });
assert.match(serverError, /máy chủ/i);
assert.doesNotMatch(serverError, /phiên đăng nhập/i, 'Server errors must not be reported as expired login');
assert.match(responseError({ status: 200, ok: true, redirected: true, url: 'http://localhost:5065/Account/Login?ReturnUrl=x' }), /đăng nhập/i);
assert.match(responseError({ status: 200, ok: true, redirected: true, url: 'http://localhost:5065/Account/AccessDenied' }), /quyền/i);
assert.match(responseError({ status: 400, ok: false, redirected: false, url: 'http://localhost:5065/SuKien/Create' }), /biểu mẫu/i);
assert.equal(responseError({ status: 200, ok: true, redirected: false, url: 'http://localhost:5065/SuKien/Create' }), null);

assert.deepEqual(toggleDate(['2028-02-29'], '2028-03-01'), ['2028-02-29', '2028-03-01']);
assert.deepEqual(toggleDate(['2028-02-29', '2028-03-01'], '2028-02-29'), ['2028-03-01']);
assert.deepEqual(toggleDate(['2028-02-29'], '2028-02-29'), []);
assert.deepEqual(selectionRange(['2028-03-01', '2028-02-29'], '2026-10-15'), { start: '2028-02-29', end: '2028-03-01' });
assert.deepEqual(selectionRange([], '2026-10-15'), { start: '2026-10-15', end: '2026-10-15' });

// Losing the year rollover or clamping to 28 would break month navigation.
assert.equal(moveDate('2024-01-01', -1), '2023-12-31');
assert.equal(moveDate('2024-12-31', 1), '2025-01-01');
assert.equal(moveDate('2024-02-28', 1), '2024-02-29');
assert.equal(moveDate('2024-02-29', 1), '2024-03-01');
assert.equal(moveDate('2023-02-28', 1), '2023-03-01');
assert.equal(validTimeRange('2024-02-29T23:00', '2024-03-01T01:00'), true);
assert.equal(validTimeRange('2024-02-29T10:00', '2024-02-29T10:00'), false);
assert.equal(validTimeRange('2024-02-29T10:00', '2024-02-29T10:00:00'), false);
assert.equal(validTimeRange('2024-02-29T11:00', '2024-02-29T10:00'), false);
assert.equal(calendarUrl('/SuKien', 2024, 2, '2024-02-29'), '/SuKien?year=2024&month=2&date=2024-02-29');
assert.equal(createUrl('/SuKien/Create', 2024, 2, '2024-02-29'), '/SuKien/Create?date=2024-02-29&returnYear=2024&returnMonth=2&returnDate=2024-02-29');
assert.equal(new URL(createUrl('/SuKien/Create', 2024, 2, '2024-02-29', '2024-03-01'), 'http://localhost').searchParams.get('endDate'), '2024-03-01');
assert.equal(new URL(calendarUrl('/SuKien', 2024, 2, '2024-03-01', ['2024-02-29', '2024-03-01']), 'http://localhost').searchParams.get('dates'), '2024-02-29,2024-03-01');

const cell = { dataset: { date: '2024-02-29' } };
const target = { closest: selector => selector === '[data-calendar-day]' ? cell : null };
const calls = [];
const select = date => calls.push(['select', date]);
const create = date => calls.push(['create', date]);
function event(type, extra = {}) { return { type, target, preventDefault() { this.prevented = true; }, ...extra }; }
handleDayInteraction(event('click'), select, create);
assert.deepEqual(calls.splice(0), [['select', '2024-02-29']]);
handleDayInteraction(event('dblclick'), select, create);
assert.deepEqual(calls.splice(0), [['create', '2024-02-29']]);
for (const key of ['Enter', ' ']) {
    const input = event('keydown', { key });
    handleDayInteraction(input, select, create);
    assert.equal(input.prevented, true);
    assert.deepEqual(calls.splice(0), [['select', '2024-02-29']]);
}
for (const [key, expected] of [['ArrowRight', '2024-03-01'], ['ArrowLeft', '2024-02-28'], ['ArrowUp', '2024-02-22'], ['ArrowDown', '2024-03-07']]) {
    const input = event('keydown', { key });
    handleDayInteraction(input, select, create);
    assert.equal(input.prevented, true);
    assert.deepEqual(calls.splice(0), [['select', expected]]);
}
handleDayInteraction(event('click', { detail: 2 }), select, create);
assert.deepEqual(calls.splice(0), [], 'Second click in a double click must not toggle selection again');
const focusMoves = [];
handleDayInteraction(event('keydown', { key: 'ArrowRight' }), (...args) => focusMoves.push(args), create);
assert.deepEqual(focusMoves, [['2024-03-01', true, false]], 'Arrow navigation must not toggle another date');
const link = { closest: selector => selector === '[data-calendar-day]' ? cell : {} };
for (const type of ['click', 'dblclick', 'keydown']) handleDayInteraction(event(type, { target: link, key: 'Enter' }), select, create);
assert.deepEqual(calls, []);
console.log('Hotel events UI: leap day, multiple selection, range, context, double-click and link isolation passed.');
