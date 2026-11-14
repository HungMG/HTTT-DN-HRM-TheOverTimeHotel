(function () {
    'use strict';
    const requests = new WeakMap();
    const money = new Intl.NumberFormat('vi-VN');
    const field = (form, contract, amendment) => form.elements[contract] || form.elements[amendment];

    function formatCommas(val) {
        if (val === null || val === undefined || val === '') return '';
        const digits = String(val).replace(/\D/g, '');
        if (!digits) return '';
        return digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function validateSalaryInput(salary) {
        if (!salary) return;
        const raw = salary.value.replace(/\D/g, '');
        if (!raw) {
            salary.setCustomValidity(salary.required ? 'Vui lòng nhập mức lương.' : '');
            return;
        }
        const val = Number(raw);
        const min = Number(salary.min);
        const max = Number(salary.max);
        if (!Number.isNaN(min) && min > 0 && val < min) {
            salary.setCustomValidity('Mức lương tối thiểu là ' + money.format(min) + ' VNĐ/tháng.');
        } else if (!Number.isNaN(max) && max > 0 && val > max) {
            salary.setCustomValidity('Mức lương tối đa là ' + money.format(max) + ' VNĐ/tháng.');
        } else {
            salary.setCustomValidity('');
        }
    }

    function setEducation(form, education) {
        const select = form?.elements.TrinhDo;
        if (!select) return;
        if (education && !Array.from(select.options).some(option => option.value === education))
            select.add(new Option(education + ' (theo hồ sơ)', education));
        select.value = education;
    }

    async function refresh(form) {
        const hint = form?.querySelector('[data-salary-band]');
        if (!hint) return;
        requests.get(form)?.abort();
        const request = new AbortController();
        requests.set(form, request);
        const salary = field(form, 'Contract.LuongCoBan', 'salary');
        const probation = form.elements['Contract.HinhThucHopDong']?.value === 'THU_VIEC';
        salary.readOnly = probation;
        salary.removeAttribute('max');
        salary.min = '1';
        salary.setCustomValidity('');
        if (probation) salary.value = '';
        const amendment = form.matches('[data-salary-amendment-form]');
        const changingPosition = form.querySelector('[data-amendment-change-position]')?.checked;
        const position = amendment && !changingPosition ? '' : field(form, 'PositionId', 'positionId')?.value;
        const education = field(form, 'TrinhDo', 'education')?.value || '';
        const start = field(form, 'Contract.NgayBatDau', 'effectiveDate')?.value;
        hint.classList.remove('text-danger');
        if ((!position && (!amendment || changingPosition)) || !start || (!amendment && !education)) {
            hint.textContent = amendment ? 'Chọn chức vụ, trình độ và ngày hiệu lực để xem khung lương.' : '';
            return;
        }
        hint.textContent = 'Đang tra khung lương…';
        const query = new URLSearchParams({ education, startDate: start });
        if (position) query.set('positionId', position);
        const employee = field(form, 'EmployeeId', 'employeeId')?.value;
        if (employee && employee !== '0') query.set('employeeId', employee);
        try {
            const response = await fetch('/HopDong/SalaryQuote?' + query, { signal: request.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const json = response.headers.get('content-type')?.includes('application/json');
            const data = json ? await response.json() : { message: await response.text() };
            if (requests.get(form) !== request || !form.isConnected) return;
            if (!response.ok) throw new Error(data.message || 'Không thể tra khung lương. Kiểm tra lại dữ liệu.');
            salary.min = String(probation ? data.probationSalary : data.minimum);
            salary.max = String(probation ? data.probationSalary : data.maximum);
            if (probation) salary.value = formatCommas(data.probationSalary);
            validateSalaryInput(salary);

            if (amendment) {
                const tierName = data.tenureTier || (data.tenureMonths < 12 ? 'Dưới 1 năm' : data.tenureMonths < 36 ? 'Từ 1 đến dưới 3 năm' : 'Từ 3 năm trở lên');
                const tenureText = data.tenureLabel || (data.tenureMonths + ' tháng');
                let infoHtml = `<div class="p-2 mt-2 bg-light rounded border border-secondary-subtle">
                    <div class="fw-semibold text-primary"><i class="bi bi-stars me-1"></i>Đề xuất khung lương theo thâm niên:</div>
                    <div class="text-dark small mt-1">
                        • <strong>Khung áp dụng:</strong> <span class="text-success fw-bold">${money.format(data.minimum)} – ${money.format(data.maximum)} VNĐ/tháng</span>
                    </div>
                    <div class="text-muted small">
                        • <strong>Thâm niên:</strong> ${tenureText} (Bậc: <strong>${tierName}</strong>)
                    </div>`;
                const currentSalaryText = data.currentSalary == null
                    ? 'Chưa xác định' : money.format(data.currentSalary) + ' VNĐ/tháng';
                infoHtml += `<div class="text-muted small">
                    • <strong>Mức lương hiện tại:</strong> ${currentSalaryText}
                </div>`;
                infoHtml += `<div class="mt-2 d-flex flex-wrap gap-2">
                    <button type="button" class="btn btn-outline-success btn-sm py-0 px-2" style="font-size: 0.8rem;" data-apply-suggested="${data.minimum}">
                        Điền mức tối thiểu (${money.format(data.minimum)})
                    </button>
                    <button type="button" class="btn btn-outline-primary btn-sm py-0 px-2" style="font-size: 0.8rem;" data-apply-suggested="${data.maximum}">
                        Điền mức tối đa (${money.format(data.maximum)})
                    </button>
                </div></div>`;
                hint.innerHTML = infoHtml;

                hint.querySelectorAll('[data-apply-suggested]').forEach(btn => {
                    btn.addEventListener('click', e => {
                        e.preventDefault();
                        salary.value = formatCommas(btn.dataset.applySuggested);
                        salary.dispatchEvent(new Event('input', { bubbles: true }));
                    });
                });
            } else {
                hint.textContent = 'Khung lương: ' + money.format(data.minimum) + ' – ' + money.format(data.maximum) + ' VNĐ/tháng · Thâm niên: ' + data.tenureMonths + ' tháng.';
            }
        } catch (error) {
            if (error.name === 'AbortError' || requests.get(form) !== request) return;
            hint.textContent = error.message || 'Không thể tải khung lương. Vui lòng thử lại.';
            hint.classList.add('text-danger');
            salary.setCustomValidity('Chưa tra được khung lương. Kiểm tra chức vụ, trình độ và ngày hiệu lực.');
        }
    }

    async function loadAmendments(region) {
        region.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(region.dataset.salaryAmendmentsUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.redirected) throw new Error('Không thể tải phụ lục. Kiểm tra quyền truy cập và thử lại.');
            const html = await response.text();
            if (!region.isConnected) return;
            region.innerHTML = html;
            init(region);
        } catch (error) {
            region.replaceChildren();
            const message = document.createElement('p');
            message.className = 'text-danger';
            message.textContent = error.message;
            const retry = document.createElement('button');
            retry.type = 'button'; retry.className = 'btn btn-outline-secondary btn-sm'; retry.textContent = 'Tải lại phụ lục';
            retry.addEventListener('click', () => loadAmendments(region));
            region.append(message, retry);
        } finally { region.removeAttribute('aria-busy'); }
    }

    function init(container = document) {
        const forms = container.matches?.('form') ? [container] : container.querySelectorAll('form');
        forms.forEach(form => {
            const salary = field(form, 'Contract.LuongCoBan', 'salary');
            if (salary && salary.value) {
                salary.value = formatCommas(salary.value);
                validateSalaryInput(salary);
            }
            if (form.querySelector('[data-salary-band]')) refresh(form);
        });
        container.querySelectorAll('[data-salary-amendments-url]').forEach(loadAmendments);
    }

    const salarySelector = '#contractLuongCoBan, [id$="contractLuongCoBan"], [data-salary-amendment-form] input[name="salary"], [id$="_salary"]';

    document.addEventListener('input', event => {
        const input = event.target;
        if (!input || !input.matches(salarySelector)) return;
        const cursor = input.selectionStart || 0;
        const oldVal = input.value || '';
        const digitsBefore = oldVal.slice(0, cursor).replace(/\D/g, '').length;
        const digits = oldVal.replace(/\D/g, '').slice(0, 12);
        if (!digits) {
            input.value = '';
            validateSalaryInput(input);
            return;
        }
        const formatted = digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        input.value = formatted;
        let newCursor = formatted.length;
        let count = 0;
        for (let i = 0; i < formatted.length; i++) {
            if (/\d/.test(formatted[i])) count++;
            if (count >= digitsBefore) { newCursor = i + 1; break; }
        }
        if (cursor === 0) newCursor = 0;
        input.setSelectionRange(newCursor, newCursor);
        validateSalaryInput(input);
    });

    document.addEventListener('keydown', event => {
        const input = event.target;
        if (!input || !input.matches(salarySelector)) return;
        if (event.key === 'Backspace' && input.selectionStart === input.selectionEnd) {
            const pos = input.selectionStart;
            if (pos > 0 && input.value[pos - 1] === ',') {
                event.preventDefault();
                const val = input.value;
                input.value = val.slice(0, pos - 2) + val.slice(pos);
                const newPos = Math.max(0, pos - 2);
                input.setSelectionRange(newPos, newPos);
                input.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }
    });

    document.addEventListener('change', async event => {
        const form = event.target.closest('form');
        if (!form?.querySelector('[data-salary-band]')) return;
        if (event.target.name === 'effectiveDate') {
            const val = event.target.value;
            if (val) {
                const parts = val.split('-');
                if (parts.length === 3 && parts[2] !== '01') {
                    event.target.setCustomValidity('Phụ lục chỉ áp dụng ngày 01 của tháng.');
                    event.target.reportValidity();
                } else {
                    event.target.setCustomValidity('');
                }
            }
        }
        if (event.target.matches('[data-amendment-change-position]')) {
            form.elements.positionId.disabled = !event.target.checked;
            form.elements.departmentId.disabled = !event.target.checked;
            refresh(form);
        }
        if (['PositionId', 'TrinhDo', 'Contract.NgayBatDau', 'Contract.HinhThucHopDong', 'effectiveDate', 'positionId'].includes(event.target.name)) refresh(form);
        if (form.matches('[data-salary-amendment-form]') && event.target.name === 'departmentId') {
            const select = form.elements.positionId;
            const department = event.target.value;
            const request = String((Number(form.dataset.departmentRequest) || 0) + 1);
            form.dataset.departmentRequest = request;
            select.replaceChildren(new Option('Đang tải chức vụ…', ''));
            refresh(form);
            try {
                const response = await fetch('/ChucVu/GetByDepartment?departmentId=' + encodeURIComponent(department));
                if (!response.ok) throw new Error();
                const data = await response.json();
                if (form.dataset.departmentRequest !== request) return;
                select.replaceChildren(new Option('-- Chọn chức vụ --', ''));
                data.forEach(item => select.add(new Option(item.tenChucVu, item.maChucVu)));
            } catch {
                if (form.dataset.departmentRequest === request) select.replaceChildren(new Option('Không thể tải chức vụ. Chọn lại phòng ban.', ''));
            }
        }
    });
    document.addEventListener('reset', event => {
        if (event.target.querySelector('[data-salary-band]')) setTimeout(() => refresh(event.target), 0);
    });
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches('[data-salary-amendment-form]')) return;
        event.preventDefault();
        const salaryInput = field(form, 'Contract.LuongCoBan', 'salary');
        if (salaryInput) validateSalaryInput(salaryInput);
        if (form.dataset.submitting === 'true' || !form.reportValidity()) return;
        const error = form.querySelector('[data-amendment-error]');
        const submit = form.querySelector('[type="submit"]');
        error.classList.add('d-none');
        form.dataset.submitting = 'true'; submit.disabled = true; form.setAttribute('aria-busy', 'true');
        try {
            const formData = new FormData(form);
            if (formData.has('salary')) {
                formData.set('salary', formData.get('salary').toString().replace(/,/g, '').trim());
            }
            const response = await fetch(form.action, { method: 'POST', body: formData, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const json = response.headers.get('content-type')?.includes('application/json');
            const data = json ? await response.json() : { message: await response.text() };
            if (!response.ok || !data.success) throw new Error(data.message || 'Không thể lưu phụ lục. Vui lòng thử lại.');
            const region = form.closest('[data-salary-amendments-url]');
            await loadAmendments(region);
            const notice = document.createElement('p'); notice.className = 'alert alert-success'; notice.setAttribute('role', 'status');
            notice.textContent = data.message || 'Đã lưu phụ lục.';
            if (data.promotionUrl) {
                const link = document.createElement('a');
                link.href = data.promotionUrl; link.className = 'd-block mt-2 fw-semibold';
                link.textContent = 'Đề xuất thăng chức sang IT'; notice.append(link);
            }
            region.prepend(notice);
        } catch (failure) {
            error.textContent = failure.message || 'Mất kết nối. Vui lòng thử lại.';
            error.classList.remove('d-none'); error.focus();
        } finally { form.dataset.submitting = 'false'; submit.disabled = false; form.removeAttribute('aria-busy'); }
    });
    window.SalaryContract = { init, refresh, setEducation, formatCommas };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => init());
    else init();
})();
