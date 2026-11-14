(() => {
    const hideModal = document.getElementById('employeeHideModal');
    if (!hideModal) return;
    const bsHideModal = bootstrap.Modal.getOrCreateInstance(hideModal);
    const nameEl = document.getElementById('employeeHideModalName');
    const errorEl = document.getElementById('employeeHideError');
    const confirmBtn = document.getElementById('employeeHideConfirmBtn');
    let currentUrl = null;
    let currentTrigger = null;
    let saving = false;

    function getAntiForgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-employee-hide-url]');
        if (!btn || saving) return;
        e.preventDefault();
        e.stopPropagation();
        currentUrl = btn.dataset.employeeHideUrl;
        currentTrigger = btn;
        if (nameEl) nameEl.textContent = btn.dataset.employeeHideName ?? '';
        if (errorEl) {
            errorEl.classList.add('d-none');
            errorEl.textContent = '';
        }
        if (confirmBtn) {
            confirmBtn.disabled = false;
            confirmBtn.innerHTML = '<i class="bi bi-eye-slash me-1"></i>Xác nhận ẩn';
        }
        bsHideModal.show();
    }, true);

    hideModal.addEventListener('hide.bs.modal', event => { if (saving) event.preventDefault(); });
    hideModal.addEventListener('hidden.bs.modal', () => { currentTrigger?.focus(); });

    if (confirmBtn) {
        confirmBtn.addEventListener('click', async function () {
            if (saving || !currentUrl) return;
            saving = true;
            confirmBtn.disabled = true;
            confirmBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Đang ẩn…';
            if (errorEl) errorEl.classList.add('d-none');

            try {
                const formData = new FormData();
                formData.append('__RequestVerificationToken', getAntiForgeryToken());
                const res = await fetch(currentUrl, {
                    method: 'POST',
                    body: formData,
                    headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' }
                });
                if (res.status === 401 || (res.redirected && new URL(res.url).pathname.toLowerCase().endsWith('/account/login')))
                    throw new Error('Phiên làm việc có thể đã hết hạn. Hãy tải lại trang và đăng nhập lại.');
                if (res.status === 403)
                    throw new Error('Tài khoản hiện tại không có quyền thực hiện thao tác này.');
                const data = await res.json();
                if (!data.success) throw new Error(data.message || 'Không thể ẩn hồ sơ nhân viên.');

                saving = false;
                bsHideModal.hide();

                // Làm mới bảng danh sách nhân viên
                const filters = document.querySelector('.employee-workspace .catalog-filter-form');
                if (filters && typeof filters.requestSubmit === 'function') {
                    filters.requestSubmit();
                } else if (typeof loadEmployeeTable === 'function') {
                    loadEmployeeTable({}, false);
                }

                // Hiển thị thông báo toast thành công
                const notice = document.querySelector('[data-employee-delete-notice]');
                if (notice) {
                    notice.textContent = data.message;
                    notice.classList.remove('d-none');
                    notice.focus();
                }
            } catch (err) {
                if (errorEl) {
                    errorEl.textContent = err.message || 'Không thể ẩn hồ sơ nhân viên.';
                    errorEl.classList.remove('d-none');
                }
            } finally {
                saving = false;
                confirmBtn.disabled = false;
                confirmBtn.innerHTML = '<i class="bi bi-eye-slash me-1"></i>Xác nhận ẩn';
            }
        });
    }
})();
