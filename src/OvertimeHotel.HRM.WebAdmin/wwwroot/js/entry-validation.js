(function () {
    'use strict';

    function message(input) {
        const validity = input.validity;
        if (input.name === 'Employee.DienThoai' && (validity.valueMissing || validity.patternMismatch || validity.tooLong))
            return 'Nhập số di động 10 chữ số bắt đầu bằng 03, 05, 07, 08 hoặc 09; hoặc +84 và 9 chữ số. Ví dụ: 0912345678.';
        if ((input.name === 'Employee.EmailCaNhan' || input.name === 'PersonalEmail') && validity.valueMissing)
            return 'Vui lòng nhập email cá nhân, ví dụ ten@gmail.com.';
        if (validity.valueMissing) {
            const labels = { ContractId: 'hợp đồng', EmployeeName: 'họ và tên', DepartmentId: 'phòng ban', PositionId: 'chức vụ',
                TrinhDo: 'trình độ chuyên môn', 'Contract.NgayBatDau': 'ngày bắt đầu hợp đồng', 'Contract.NgayKetThuc': 'ngày kết thúc hợp đồng',
                'Contract.LoaiHopDong': 'thời hạn hợp đồng', 'Contract.HinhThucHopDong': 'giai đoạn hợp đồng', 'Contract.LuongCoBan': 'mức lương',
                'Employee.NgaySinh': 'ngày sinh', 'Employee.GioiTinh': 'giới tính', 'Employee.TrinhDo': 'trình độ chuyên môn',
                'Employee.DiaChi': 'địa chỉ', 'Employee.SoNguoiPhuThuoc': 'số người phụ thuộc' };
            const label = labels[input.name] || 'thông tin này';
            return input.tagName === 'SELECT' || input.type === 'date' ? `Vui lòng chọn ${label}.` : `Vui lòng nhập ${label}.`;
        }
        if (validity.typeMismatch && input.type === 'email') return 'Email phải có dạng ten@gmail.com.';
        if (validity.patternMismatch) return input.title || 'Thông tin chưa đúng định dạng được hướng dẫn.';
        if (validity.badInput || validity.stepMismatch) return 'Vui lòng nhập một số nguyên hợp lệ.';
        if (validity.rangeUnderflow || validity.rangeOverflow) {
            if (input.type === 'date') {
                if (input.name !== 'Employee.NgaySinh') return 'Vui lòng chọn ngày hợp lệ.';
                const limit = new Date(); const month = limit.getMonth();
                limit.setFullYear(limit.getFullYear() - 18);
                if (limit.getMonth() !== month) limit.setDate(0);
                const cutoff = `${limit.getFullYear()}-${String(limit.getMonth()+1).padStart(2,'0')}-${String(limit.getDate()).padStart(2,'0')}`;
                return input.value > cutoff ? 'Nhân viên phải đủ 18 tuổi trở lên.' : 'Ngày sinh phải trước ngày vào làm trên hợp đồng.';
            }
            return `Nhập số từ ${input.min} đến ${input.max}.`;
        }
        if (validity.tooLong) return `Nhập tối đa ${input.maxLength} ký tự.`;
        return input.validationMessage;
    }

    function feedback(input) {
        const span = Array.from(input.form.querySelectorAll('[data-valmsg-for]'))
            .find(item => item.dataset.valmsgFor === input.name);
        if (!span) return;
        const invalid = !input.validity.valid;
        const previousMessage = span.textContent.trim();
        if (!invalid && previousMessage) {
            const summary = input.form.querySelector('[data-entry-validation]');
            Array.from(summary?.querySelectorAll('li') || []).find(item => item.textContent.trim() === previousMessage)?.remove();
            if (summary && !Array.from(summary.querySelectorAll('li')).some(item => item.textContent.trim())) {
                summary.classList.remove('validation-summary-errors');
                summary.classList.add('validation-summary-valid');
            }
        }
        span.textContent = invalid ? message(input) : '';
        span.classList.toggle('field-validation-error', invalid);
        span.classList.toggle('field-validation-valid', !invalid);
        input.classList.toggle('input-validation-error', invalid);
        input.classList.toggle('is-invalid', invalid);
        input.setAttribute('aria-invalid', String(invalid));
    }

    function focusError(form) {
        const input = Array.from(form.elements).find(item => item.getAttribute('aria-invalid') === 'true' && item.type !== 'hidden' && !item.disabled && !item.readOnly && item.getClientRects().length);
        (input || form.querySelector('.validation-summary-errors'))?.focus();
        if (input && form.querySelector('[data-native-tooltip]')) input.reportValidity();
    }

    function syncBirthLimit(form) {
        const birth = form.elements['Employee.NgaySinh'];
        const joined = form.elements['Employee.NgayVaoLam'];
        if (!birth) return;
        const maxAgeDate = new Date();
        const month = maxAgeDate.getMonth();
        maxAgeDate.setFullYear(maxAgeDate.getFullYear() - 18);
        if (maxAgeDate.getMonth() !== month) maxAgeDate.setDate(0);
        const localDate = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
        let max = localDate(maxAgeDate);
        if (joined?.value) {
            const dayBeforeJoining = new Date(joined.value + 'T00:00:00');
            dayBeforeJoining.setDate(dayBeforeJoining.getDate() - 1);
            const joiningLimit = localDate(dayBeforeJoining);
            if (joiningLimit < max) max = joiningLimit;
        }
        birth.max = max;
    }

    function init() {
        document.querySelectorAll('[data-entry-validation]').forEach((summary, index) => {
            const form = summary.closest('form');
            if (!form || form.dataset.entryValidationReady) return;
            form.dataset.entryValidationReady = 'true';
            const tooltip = summary.hasAttribute('data-native-tooltip');
            if (tooltip) form.noValidate = true;
            form.querySelectorAll('[data-valmsg-for]').forEach((span, fieldIndex) => {
                span.id ||= `entry-error-${index}-${fieldIndex}`;
                const input = form.elements.namedItem(span.dataset.valmsgFor);
                if (!input?.setAttribute) return;
                const describedBy = new Set((input.getAttribute('aria-describedby') || '').split(' ').filter(Boolean));
                describedBy.add(span.id);
                input.setAttribute('aria-describedby', Array.from(describedBy).join(' '));
                const invalid = span.classList.contains('field-validation-error');
                if (tooltip) {
                    span.hidden = true;
                    if (invalid && !input.readOnly && !input.disabled) input.setCustomValidity(span.textContent.trim());
                }
                input.setAttribute('aria-invalid', String(invalid));
                input.classList.toggle('is-invalid', invalid);
                span.setAttribute('aria-live', 'polite');
            });
            syncBirthLimit(form);
            form.addEventListener('invalid', event => {
                const input = event.target;
                if (!tooltip) { feedback(input); return; }
                if (!input.validity.customError) input.setCustomValidity(message(input));
                input.setAttribute('aria-invalid', 'true');
                input.classList.add('is-invalid');
            }, true);
            ['input', 'change'].forEach(name => form.addEventListener(name, event => {
                syncBirthLimit(form);
                if (tooltip && event.target.setCustomValidity) {
                    event.target.setCustomValidity('');
                    if (event.target.validity.valid) {
                        event.target.setAttribute('aria-invalid', 'false');
                        event.target.classList.remove('is-invalid', 'input-validation-error');
                        const span = [...form.querySelectorAll('[data-valmsg-for]')].find(item => item.dataset.valmsgFor === event.target.name);
                        const previous = span?.textContent.trim();
                        if (previous) {
                            [...summary.querySelectorAll('li')].find(item => item.textContent.trim() === previous)?.remove();
                            span.textContent = '';
                            if (![...summary.querySelectorAll('li')].some(item => item.textContent.trim())) {
                                summary.classList.replace('validation-summary-errors', 'validation-summary-valid');
                            }
                        }
                    }
                } else if (event.target.getAttribute('aria-invalid') === 'true') feedback(event.target);
            }));
            // Also enforce native constraints when another validator sets novalidate.
            form.addEventListener('submit', event => {
                if (!form.reportValidity()) {
                    event.preventDefault();
                    if (!tooltip) focusError(form);
                }
            });
            if (!form.closest('.modal') && summary.classList.contains('validation-summary-errors')) focusError(form);
        });
    }

    window.EntryValidation = { focusError, init };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
