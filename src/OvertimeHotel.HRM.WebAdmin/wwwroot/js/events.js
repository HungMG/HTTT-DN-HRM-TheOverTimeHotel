(function () {
    'use strict';
    function moveDate(value, delta) {
        const date = new Date(value + 'T00:00:00Z');
        date.setUTCDate(date.getUTCDate() + delta);
        return date.toISOString().slice(0, 10);
    }
    function toggleDate(dates, date) {
        return dates.includes(date) ? dates.filter(value => value !== date) : [...dates, date].sort();
    }
    function selectionRange(dates, fallback) {
        const sorted = [...dates].sort();
        return { start: sorted[0] || fallback, end: sorted.at(-1) || fallback };
    }
    function canCreateSelection(dates, today) {
        return dates.length > 0 && dates.every(date => date >= today);
    }
    function calendarUrl(base, year, month, date, dates) {
        const query = new URLSearchParams({ year, month, date });
        if (dates) query.set('dates', dates.join(','));
        return base + '?' + query;
    }
    function createUrl(base, year, month, date, endDate = date) {
        const query = new URLSearchParams({ date, returnYear: year, returnMonth: month, returnDate: date });
        if (endDate !== date) query.set('endDate', endDate);
        return base + '?' + query;
    }
    function validTimeRange(start, end) {
        return !start || !end || new Date(start + '+07:00') < new Date(end + '+07:00');
    }
    function responseError(response) {
        const path = response.url ? new URL(response.url).pathname.toLowerCase() : '';
        if (response.status === 403 || path.endsWith('/account/accessdenied')) return 'Bạn không có quyền thực hiện thao tác sự kiện này.';
        if (response.status === 401 || path.endsWith('/account/login')) return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại rồi thử lại.';
        if (response.status === 400) return 'Phiên biểu mẫu đã thay đổi. Hãy đóng rồi mở lại popup để thử lại.';
        if (response.status === 422) return 'Chỉ được tạo sự kiện từ hôm nay trở đi (giờ Việt Nam). Vui lòng chọn lại ngày.';
        if (!response.ok) return 'Máy chủ gặp lỗi khi xử lý sự kiện. Vui lòng thử lại hoặc báo quản trị viên.';
        if (response.redirected) return 'Phản hồi từ máy chủ không hợp lệ. Vui lòng tải lại trang rồi thử lại.';
        return null;
    }
    function handleDayInteraction(event, select, create) {
        const cell = event.target.closest('[data-calendar-day]');
        if (!cell || event.target.closest('a, button, input, select, textarea')) return;
        if (event.type === 'dblclick') create(cell.dataset.date);
        else if (event.type === 'click' && !(event.detail > 1)) select(cell.dataset.date);
        else if (event.type === 'keydown' && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault();
            select(cell.dataset.date);
        }
        else if (event.type === 'keydown' && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) {
            event.preventDefault();
            const delta = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[event.key];
            select(moveDate(cell.dataset.date, delta), true, false);
        }
    }
    function bindTimeForms(root) {
        root.querySelectorAll('[data-event-time-form]').forEach(form => {
            if (form.dataset.timeBound) return;
            form.dataset.timeBound = 'true';
            const start = form.querySelector('[data-event-start]');
            const end = form.querySelector('[data-event-end]');
            function validate() {
                end.setCustomValidity(validTimeRange(start.value, end.value) ? '' : 'Thời gian kết thúc phải sau thời gian bắt đầu.');
            }
            start.addEventListener('input', validate);
            end.addEventListener('input', validate);
            form.addEventListener('submit', event => {
                validate();
                if (!end.checkValidity()) {
                    event.preventDefault();
                    end.reportValidity();
                }
            });
            validate();
        });
    }
    function bindDepartmentEmployeeFiltering(root) {
        root.querySelectorAll('[data-staff-assignment-form]').forEach(form => {
            if (form.dataset.deptFilterBound) return;
            form.dataset.deptFilterBound = 'true';
            const deptSelect = form.querySelector('[data-department-select]');
            const empSelect = form.querySelector('[data-employee-select]');
            if (!deptSelect || !empSelect) return;

            const allOptions = Array.from(empSelect.querySelectorAll('option')).map(opt => ({
                value: opt.value,
                text: opt.textContent,
                department: opt.dataset.department || '',
                selected: opt.selected
            }));

            function filterEmployees(preserveSelected) {
                const selectedDept = deptSelect.value;
                const currentSelectedValue = preserveSelected ? empSelect.value : '';

                empSelect.innerHTML = '';
                const defaultOpt = document.createElement('option');
                defaultOpt.value = '';
                defaultOpt.textContent = selectedDept ? '-- Chọn nhân viên --' : '-- Vui lòng chọn phòng ban trước --';
                empSelect.appendChild(defaultOpt);

                allOptions.forEach(opt => {
                    if (!opt.value) return;
                    if (!selectedDept || opt.department === selectedDept) {
                        const el = document.createElement('option');
                        el.value = opt.value;
                        el.textContent = opt.text;
                        el.dataset.department = opt.department;
                        if (opt.value === currentSelectedValue) {
                            el.selected = true;
                        }
                        empSelect.appendChild(el);
                    }
                });

                empSelect.disabled = !selectedDept;
                if (!preserveSelected) {
                    empSelect.value = '';
                }
            }

            deptSelect.addEventListener('change', () => {
                filterEmployees(false);
            });

            filterEmployees(true);
        });
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = { moveDate, calendarUrl, createUrl, handleDayInteraction, validTimeRange, toggleDate, selectionRange, responseError, canCreateSelection, bindDepartmentEmployeeFiltering };
    if (typeof document === 'undefined') return;
    document.addEventListener('DOMContentLoaded', function () {
        bindTimeForms(document);
        bindDepartmentEmployeeFiltering(document);
        const calendar = document.querySelector('[data-event-calendar]');
        if (!calendar) return;
        const year = Number(calendar.dataset.year), month = Number(calendar.dataset.month);
        const createButtons = document.querySelectorAll('[data-create-event]');
        const selectedTitle = document.querySelector('[data-selected-title]');
        const selectedCount = document.querySelector('[data-selected-count]');
        const selectedList = document.querySelector('[data-selected-events]');
        const createNote = document.querySelector('[data-create-date-note]');
        const cards = JSON.parse(document.getElementById('event-calendar-data').textContent);
        const datesQuery = new URLSearchParams(window.location.search).get('dates');
        let selectedDates = datesQuery === null ? [calendar.dataset.selected] : [...new Set(datesQuery.split(','))].filter(date =>
            /^\d{4}-\d{2}-\d{2}$/.test(date) && calendar.querySelector('[data-date="' + date + '"]'));
        const dialog = document.querySelector('[data-event-create-dialog]');
        const dialogBody = dialog?.querySelector('[data-event-create-body]');
        let popupRequest;
        let popupSaving = false;
        let popupTrigger;
        function renderSelection(date, focus = false) {
            const selectedCell = calendar.querySelector('[data-date="' + date + '"]');
            if (!selectedCell) {
                const [nextYear, nextMonth] = date.split('-').map(Number);
                if (focus && nextYear >= 1900 && nextYear <= 2100) window.location.assign(calendarUrl(calendar.dataset.indexUrl, nextYear, nextMonth, date));
                return;
            }
            calendar.querySelectorAll('[data-calendar-day]').forEach(cell => {
                const active = selectedDates.includes(cell.dataset.date);
                cell.classList.toggle('is-selected', active);
                cell.setAttribute('aria-selected', String(active));
                cell.tabIndex = cell === selectedCell ? 0 : -1;
            });
            const formatDay = value => value.split('-').reverse().join('/');
            selectedTitle.textContent = selectedDates.length === 1 ? formatDay(selectedDates[0]) : selectedDates.length + ' ngày đã chọn';
            const events = cards.filter(item => item.days.some(day => selectedDates.includes(day)));
            selectedCount.textContent = selectedDates.map(formatDay).join(', ') + (selectedDates.length ? ' · ' : '') + events.length + ' sự kiện';
            selectedList.replaceChildren();
            if (!events.length) {
                const empty = document.createElement('p');
                empty.className = 'event-empty';
                empty.textContent = selectedDates.length ? 'Chưa có sự kiện trong các ngày đã chọn.' : 'Nhấp vào ô ngày để chọn. Có thể chọn nhiều ngày.';
                selectedList.append(empty);
            }
            events.forEach(item => {
                const link = document.createElement('a');
                link.className = 'event-agenda-card';
                link.href = item.url + '&returnDate=' + encodeURIComponent(date);
                const time = document.createElement('span');
                time.className = 'event-eyebrow';
                time.textContent = item.time + ' · ' + item.type;
                const name = document.createElement('strong');
                name.textContent = item.name;
                const meta = document.createElement('span');
                meta.textContent = item.venue + ' · ' + item.count + ' nhân sự';
                const status = document.createElement('span');
                status.className = 'event-status event-status-' + item.status;
                status.textContent = item.statusLabel;
                link.append(time, name, meta, status);
                selectedList.append(link);
            });
            const range = selectionRange(selectedDates, calendar.dataset.today);
            const canCreate = canCreateSelection(selectedDates, calendar.dataset.today);
            createButtons.forEach(button => {
                button.setAttribute('aria-disabled', String(!canCreate));
                button.tabIndex = canCreate ? 0 : -1;
                if (canCreate) button.href = createUrl(calendar.dataset.createUrl, year, month, range.start, range.end);
                else button.removeAttribute('href');
            });
            if (createNote) {
                createNote.hidden = canCreate;
                createNote.textContent = selectedDates.length
                    ? 'Ngày trước hôm nay chỉ xem chi tiết sự kiện. Chọn ngày từ hôm nay trở đi để tạo mới.'
                    : 'Chọn ngày từ hôm nay trở đi để tạo sự kiện.';
            }
            calendar.querySelectorAll('[data-event-link]').forEach(link => {
                const url = new URL(link.href, window.location.origin);
                url.searchParams.set('returnDate', date);
                link.href = url.pathname + url.search;
            });
            history.replaceState(null, '', calendarUrl(calendar.dataset.indexUrl, year, month, date, selectedDates));
            if (focus) selectedCell.focus();
        }
        function select(date, focus = false, toggle = true) {
            if (toggle) selectedDates = toggleDate(selectedDates, date);
            renderSelection(date, focus);
        }
        async function openPopup() {
            if (!dialog || dialog.classList.contains('show') || !canCreateSelection(selectedDates, calendar.dataset.today)) return;
            popupRequest?.abort();
            const request = popupRequest = new AbortController();
            const range = selectionRange(selectedDates, calendar.dataset.today);
            const url = new URL(createUrl(calendar.dataset.createUrl, year, month, range.start, range.end), window.location.origin);
            url.searchParams.set('popup', 'true');
            dialogBody.textContent = 'Đang tải biểu mẫu…';
            dialogBody.setAttribute('aria-busy', 'true');
            popupTrigger = document.activeElement;
            bootstrap.Modal.getOrCreateInstance(dialog).show();
            try {
                const response = await fetch(url, { signal: request.signal, credentials: 'same-origin' });
                const error = responseError(response);
                if (error) throw new Error(error);
                const html = await response.text();
                if (request.signal.aborted) return;
                dialogBody.innerHTML = html;
                bindTimeForms(dialogBody);
            } catch (error) {
                if (error.name !== 'AbortError') dialogBody.textContent = error.message;
            } finally {
                if (!request.signal.aborted) dialogBody.setAttribute('aria-busy', 'false');
            }
        }
        function create(date) {
            if (!createButtons.length || date < calendar.dataset.today) return;
            if (!selectedDates.includes(date)) selectedDates = [...selectedDates, date].sort();
            renderSelection(date);
            void openPopup();
        }
        ['click', 'dblclick', 'keydown'].forEach(type => calendar.addEventListener(type, event => handleDayInteraction(event, select, create)));
        createButtons.forEach(button => button.addEventListener('click', event => {
            event.preventDefault();
            void openPopup();
        }));
        dialog?.addEventListener('hide.bs.modal', event => {
            if (popupSaving) event.preventDefault();
            else popupRequest?.abort();
        });
        dialog?.addEventListener('hidden.bs.modal', () => {
            if (popupTrigger?.isConnected) popupTrigger.focus();
            else calendar.querySelector('[tabindex="0"]')?.focus();
        });
        dialog?.addEventListener('submit', async event => {
            const form = event.target.closest('[data-event-popup-form]');
            if (!form) return;
            event.preventDefault();
            if (popupSaving || !form.reportValidity()) return;
            popupSaving = true;
            const buttons = dialog.querySelectorAll('button');
            buttons.forEach(button => button.disabled = true);
            dialogBody.querySelector('[data-popup-error]')?.remove();
            try {
                const response = await fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' });
                const error = responseError(response);
                if (error) throw new Error(error);
                if (response.headers.get('content-type')?.includes('application/json')) {
                    const result = await response.json();
                    const url = new URL(result.redirectUrl, window.location.origin);
                    if (url.origin !== window.location.origin) throw new Error('Địa chỉ trả về không hợp lệ.');
                    if (Number(url.searchParams.get('year')) === year && Number(url.searchParams.get('month')) === month) url.searchParams.set('dates', selectedDates.join(','));
                    window.location.assign(url.toString());
                } else {
                    dialogBody.innerHTML = await response.text();
                    bindTimeForms(dialogBody);
                    dialogBody.querySelector('.event-validation')?.scrollIntoView({ block: 'nearest' });
                }
            } catch (error) {
                const message = document.createElement('div');
                message.className = 'alert alert-danger';
                message.dataset.popupError = '';
                message.setAttribute('role', 'alert');
                message.textContent = error.message;
                dialogBody.prepend(message);
            } finally {
                popupSaving = false;
                buttons.forEach(button => button.disabled = false);
            }
        });
        renderSelection(calendar.dataset.selected);
    });
})();
