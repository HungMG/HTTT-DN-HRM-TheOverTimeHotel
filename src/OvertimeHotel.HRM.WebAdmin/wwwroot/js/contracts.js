(function () {
    'use strict';
    const modal = document.getElementById('terminateContractModal');
    if (!modal) return;
    const form = modal.querySelector('form');
    const headers = { 'X-Requested-With': 'XMLHttpRequest' };
    const dateInput = form.elements.NgayChamDut;
    const typeSelect = form.elements.LoaiChamDut;
    const reason = form.elements.LyDoChamDut;
    let opener;

    const fmt = value => value ? value.split('-').reverse().join('/') : 'Không thời hạn';
    const today = () => {
        const d = new Date();
        return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    };

    function responseError(response) {
        if (response.status === 401 || response.redirected) return 'Phiên đăng nhập đã thay đổi. Vui lòng tải lại trang và đăng nhập lại.';
        if (response.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
        if (response.status === 404) return 'Hợp đồng không còn tồn tại. Vui lòng tải lại danh sách.';
        if (response.status === 400) return 'Biểu mẫu đã hết hạn hoặc dữ liệu không hợp lệ. Hãy đóng rồi mở lại popup.';
        return null;
    }

    function showError(message) {
        const error = form.querySelector('[data-contract-form-error]');
        error.textContent = message;
        error.classList.remove('d-none');
        error.setAttribute('tabindex', '-1');
        error.focus();
    }

    function setBusy(busy) {
        form.dataset.submitting = busy ? 'true' : 'false';
        form.setAttribute('aria-busy', String(busy));
        modal.querySelectorAll('button').forEach(button => { button.disabled = busy; });
        form.querySelector('button[type="submit"]').textContent = busy ? 'Đang xử lý…' : 'Xác nhận chấm dứt';
    }

    function syncReasonRequired() {
        const required = typeSelect.value === 'KHAC';
        reason.required = required;
        form.querySelector('[data-terminate-reason-required]').classList.toggle('d-none', !required);
    }

    function open(button) {
        opener = button;
        const d = button.dataset;
        const state = d.contractState;
        form.reset();
        form.action = d.contractTerminateUrl;
        form.querySelector('[data-terminate-number]').textContent = d.contractNumber;
        form.querySelector('[data-terminate-employee]').textContent = d.contractEmployee;
        form.querySelector('[data-terminate-period]').textContent = fmt(d.contractStart) + ' → ' + fmt(d.contractEnd);
        form.querySelector('[data-contract-form-error]').classList.add('d-none');

        const note = form.querySelector('[data-terminate-note]');
        note.textContent = d.contractHasOther === 'true'
            ? 'Nhân viên vẫn còn hợp đồng khác nên sẽ KHÔNG chuyển sang nghỉ việc. Thao tác không thể hoàn tác.'
            : 'Nhân viên sẽ chuyển sang trạng thái nghỉ việc. Thao tác không thể hoàn tác.';

        const hint = form.querySelector('[data-terminate-date-hint]');
        if (state === 'DA_HET_HAN') {
            dateInput.value = d.contractEnd; dateInput.readOnly = true;
            hint.textContent = 'Hợp đồng đã hết hạn: ngày chấm dứt là ngày kết thúc hợp đồng.';
            typeSelect.value = 'HET_HAN_KHONG_TAI_KY';
        } else if (state === 'CHUA_HIEU_LUC') {
            dateInput.value = d.contractStart; dateInput.readOnly = true;
            hint.textContent = 'Hợp đồng chưa bắt đầu: ngày chấm dứt là ngày bắt đầu hợp đồng.';
        } else {
            dateInput.readOnly = false;
            dateInput.min = d.contractStart;
            dateInput.max = d.contractEnd && d.contractEnd < today() ? d.contractEnd : today();
            dateInput.value = today();
            hint.textContent = 'Chọn ngày nghỉ thực tế, từ ' + fmt(d.contractStart) + ' đến hôm nay.';
        }
        syncReasonRequired();
        bootstrap.Modal.getOrCreateInstance(modal).show();
    }

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-contract-terminate-url]');
        if (!button) return;
        event.preventDefault();
        open(button);
    });

    typeSelect.addEventListener('change', syncReasonRequired);

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (form.dataset.submitting === 'true' || !form.reportValidity()) return;
        setBusy(true);
        try {
            const response = await fetch(form.action, { method: 'POST', headers, body: new FormData(form) });
            const error = responseError(response);
            if (error) throw new Error(error);
            const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok || !data?.success) throw new Error(data?.message || 'Không thể chấm dứt hợp đồng. Vui lòng thử lại.');
            setBusy(false);
            bootstrap.Modal.getOrCreateInstance(modal).hide();
            const refreshed = await window.loadContractTable({}, false);
            const notice = document.querySelector('[data-contract-notice]');
            notice.textContent = refreshed ? data.message : data.message + ' Danh sách chưa cập nhật được; vui lòng tải lại trang.';
            notice.className = 'alert ' + (refreshed ? 'alert-success' : 'alert-warning');
            notice.focus();
        } catch (error) { showError(error.message || 'Mất kết nối. Vui lòng thử lại.'); }
        finally { if (form.isConnected) setBusy(false); }
    });

    modal.addEventListener('hide.bs.modal', event => {
        if (form.dataset.submitting === 'true') event.preventDefault();
    });
    modal.addEventListener('hidden.bs.modal', () => { if (opener?.isConnected) opener.focus(); });

    // =========================================================================
    // MODAL GỢI Ý ĐỀ XUẤT THĂNG CHỨC SANG IT
    // =========================================================================
    const promptModalEl = document.getElementById('promotionPromptModal');
    if (promptModalEl) {
        const empId = promptModalEl.dataset.employeeId;
        const contractId = promptModalEl.dataset.contractId;
        if (empId && contractId) {
            bootstrap.Modal.getOrCreateInstance(promptModalEl).show();
        }

        const submitPromptBtn = promptModalEl.querySelector('#btnSubmitPromotionPrompt');
        submitPromptBtn?.addEventListener('click', async () => {
            submitPromptBtn.disabled = true;
            submitPromptBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Đang gửi…';
            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const fd = new FormData();
                fd.append('employeeId', empId);
                fd.append('contractId', contractId);
                if (token) fd.append('__RequestVerificationToken', token);

                const res = await fetch('/YeuCauTaiKhoan/QuickSubmitPromotion', {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: fd
                });
                const data = await res.json();
                if (!res.ok || !data.success) throw new Error(data.message || 'Không thể gửi yêu cầu.');

                bootstrap.Modal.getOrCreateInstance(promptModalEl).hide();
                const notice = document.querySelector('[data-contract-notice]');
                if (notice) {
                    notice.textContent = data.message;
                    notice.className = 'alert alert-success';
                    notice.classList.remove('d-none');
                }
            } catch (err) {
                const errDiv = promptModalEl.querySelector('[data-prompt-error]');
                if (errDiv) {
                    errDiv.textContent = err.message;
                    errDiv.classList.remove('d-none');
                }
                submitPromptBtn.disabled = false;
                submitPromptBtn.innerHTML = '<i class="bi bi-send-fill me-1"></i> Gửi yêu cầu sang IT ngay';
            }
        });
    }
})();
