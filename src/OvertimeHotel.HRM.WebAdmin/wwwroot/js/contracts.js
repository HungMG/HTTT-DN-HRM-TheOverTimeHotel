(function () {
    'use strict';
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
