(() => {
    const modal = document.getElementById('employeeDeleteModal');
    if (!modal) return;
    const form = document.getElementById('employeeDeleteForm');
    const submit = form.querySelector('[data-delete-submit]');
    const token = form.elements.namedItem('confirmationToken');
    const error = form.querySelector('[data-delete-error]');
    let trigger, saving = false;
    const dialog = bootstrap.Modal.getOrCreateInstance(modal);

    function showError(message) {
        error.textContent = message;
        error.classList.remove('d-none');
    }
    async function readResponse(response) {
        if (response.status === 401 || (response.redirected && new URL(response.url).pathname.toLowerCase().endsWith('/account/login')))
            throw new Error('Phiên làm việc có thể đã hết hạn. Hãy tải lại trang và đăng nhập để tiếp tục.');
        if (response.status === 403)
            throw new Error('Tài khoản hiện tại không có quyền xóa hồ sơ nhân viên.');
        if (!response.headers.get('content-type')?.includes('application/json')) {
            if (response.status === 404)
                throw new Error('Không tìm thấy chức năng trên máy chủ (404). Hãy tải lại trang và thử lại.');
            throw new Error(`Máy chủ trả về phản hồi không hợp lệ (${response.status}). Hãy tải lại trang và thử lại.`);
        }
        return response.json();
    }
    document.addEventListener('click', event => {
        const button = event.target.closest('[data-employee-delete-name]');
        if (!button || saving) return;
        event.preventDefault();
        event.stopPropagation();
        trigger = button;
        form.reset();
        form.action = button.dataset.employeeDeleteUrl;
        token.value = button.dataset.employeeDeleteToken;
        form.querySelector('[data-delete-employee-name]').textContent = button.dataset.employeeDeleteName;
        error.classList.add('d-none');
        submit.disabled = false;
        dialog.show();
    });
    modal.addEventListener('shown.bs.modal', () => form.querySelector('[data-delete-cancel]').focus());
    modal.addEventListener('hide.bs.modal', event => { if (saving) event.preventDefault(); });
    modal.addEventListener('hidden.bs.modal', () => { trigger?.focus(); });
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (saving || !token.value) return;
        saving = true;
        submit.disabled = true;
        submit.textContent = 'Đang xóa…';
        error.classList.add('d-none');
        form.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), headers: { Accept: 'application/json' } });
            const data = await readResponse(response);
            if (!response.ok || !data.success) throw new Error(data.message || 'Không thể xóa nhân viên.');
            saving = false;
            trigger = null;
            dialog.hide();
            // Reuse the employee workspace's existing AJAX refresh, retaining all filters.
            const filters = document.querySelector('.employee-workspace .catalog-filter-form');
            filters.requestSubmit();
            const notice = document.querySelector('[data-employee-delete-notice]');
            notice.textContent = data.message;
            notice.classList.remove('d-none');
            notice.focus();
        } catch (failure) {
            showError((failure instanceof TypeError ? 'Mất kết nối; chưa xác nhận được kết quả xóa.' : failure.message)
                + ' Đóng rồi mở lại cảnh báo để kiểm tra trước khi thử lại.');
        } finally {
            saving = false;
            token.value = '';
            submit.disabled = true;
            submit.textContent = 'Xác nhận xóa';
            form.setAttribute('aria-busy', 'false');
        }
    });
})();
