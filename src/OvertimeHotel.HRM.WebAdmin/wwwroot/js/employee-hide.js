(() => {
    const hideModal = document.getElementById('employeeHideModal');
    if (!hideModal) return;
    const bsHideModal = bootstrap.Modal.getOrCreateInstance(hideModal);
    const nameEl = document.getElementById('employeeHideModalName');
    const errorEl = document.getElementById('employeeHideError');
    const confirmBtn = document.getElementById('employeeHideConfirmBtn');
    const cancelBtn = document.getElementById('employeeHideCancelBtn');
    const confirmation = document.getElementById('employeeHideConfirmation');
    const contractWarning = document.getElementById('employeeHideContractWarning');
    const contractLink = document.getElementById('employeeHideContractLink');
    const contractMessage = 'Nhân viên còn hợp đồng đang hoặc sắp có hiệu lực. Vui lòng chấm dứt hợp đồng cần xử lý trước khi đánh dấu nghỉ việc.';
    let currentUrl = null;
    let currentTrigger = null;
    let saving = false;
    let blocked = false;
    let saved = false;
    let profileModal = null;

    function getAntiForgeryToken() {
        return hideModal.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function updateContractWarning(message, url) {
        blocked = Boolean(message);
        confirmation.classList.toggle('d-none', blocked);
        contractWarning.textContent = message || '';
        contractWarning.classList.toggle('d-none', !blocked);
        confirmBtn.classList.toggle('d-none', blocked);
        cancelBtn.textContent = blocked ? 'Đóng' : 'Hủy';
        contractLink.classList.toggle('d-none', !blocked || !url);
        if (blocked && url) contractLink.href = url;
        else contractLink.removeAttribute('href');
    }

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-employee-hide-url]');
        if (!btn || saving) return;
        e.preventDefault();
        e.stopPropagation();
        currentUrl = btn.dataset.employeeHideUrl;
        currentTrigger = btn;
        saved = false;
        profileModal = btn.closest('#employeeProfileModal');
        if (nameEl) nameEl.textContent = btn.dataset.employeeHideName ?? '';
        updateContractWarning(btn.dataset.employeeHideContractBlocked === 'true' ? contractMessage : null,
            btn.dataset.employeeHideContractUrl);
        if (errorEl) {
            errorEl.classList.add('d-none');
            errorEl.textContent = '';
        }
        if (confirmBtn) {
            confirmBtn.disabled = false;
            confirmBtn.innerHTML = '<i class="bi bi-eye-slash me-1" aria-hidden="true"></i>Xác nhận nghỉ việc';
        }
        if (profileModal) {
            hideEmployeeProfileThen(profileModal, () => bsHideModal.show());
        } else {
            bsHideModal.show();
        }
    }, true);

    hideModal.addEventListener('hide.bs.modal', event => { if (saving) event.preventDefault(); });
    hideModal.addEventListener('shown.bs.modal', () => cancelBtn.focus());
    hideModal.addEventListener('hidden.bs.modal', () => {
        if (saved) {
            document.querySelector('[data-employee-hide-notice]')?.focus();
        } else if (profileModal) {
            profileModal.addEventListener('shown.bs.modal', () => currentTrigger?.focus(), { once: true });
            bootstrap.Modal.getOrCreateInstance(profileModal).show();
        } else {
            currentTrigger?.focus();
        }
    });

    if (confirmBtn) {
        confirmBtn.addEventListener('click', async function () {
            if (saving || blocked || !currentUrl) return;
            saving = true;
            confirmBtn.disabled = true;
            confirmBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Đang lưu…';
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
                if (data.blockedByContract) {
                    updateContractWarning(data.message, data.contractUrl);
                    cancelBtn.focus();
                    return;
                }
                if (!res.ok || !data.success) throw new Error(data.message || 'Không thể ẩn hồ sơ nhân viên.');

                saved = true;
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
                const notice = document.querySelector('[data-employee-hide-notice]');
                if (notice) {
                    notice.textContent = data.message;
                    notice.classList.remove('d-none');
                }
            } catch (err) {
                if (errorEl) {
                    errorEl.textContent = err.message || 'Không thể ẩn hồ sơ nhân viên.';
                    errorEl.classList.remove('d-none');
                }
            } finally {
                saving = false;
                confirmBtn.disabled = false;
                confirmBtn.innerHTML = '<i class="bi bi-eye-slash me-1" aria-hidden="true"></i>Xác nhận nghỉ việc';
            }
        });
    }
})();
