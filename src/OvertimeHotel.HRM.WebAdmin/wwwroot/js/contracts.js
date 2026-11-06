(function () {
    'use strict';
    const editModal = document.getElementById('editContractModal');
    const deleteModal = document.getElementById('deleteContractModal');
    if (!editModal || !deleteModal) return;
    const content = document.getElementById('editContractModalContent');
    const headers = { 'X-Requested-With': 'XMLHttpRequest' };
    let editRequest;
    let opener;

    function responseError(response) {
        if (response.status === 401 || response.redirected) return 'Phiên đăng nhập đã thay đổi. Vui lòng tải lại trang và đăng nhập lại.';
        if (response.status === 403) return 'Bạn không có quyền thực hiện thao tác này.';
        if (response.status === 404) return 'Hợp đồng không còn tồn tại. Vui lòng tải lại danh sách.';
        if (response.status === 400) return 'Biểu mẫu đã hết hạn hoặc dữ liệu không hợp lệ. Hãy đóng rồi mở lại popup.';
        return null;
    }

    function showError(form, message) {
        const error = form.querySelector('[data-contract-form-error]');
        error.textContent = message;
        error.classList.remove('d-none');
        error.setAttribute('tabindex', '-1');
        error.focus();
    }

    async function refreshList(message) {
        const refreshed = await window.loadContractTable({}, false);
        const notice = document.querySelector('[data-contract-notice]');
        notice.textContent = refreshed ? message : message + ' Danh sách chưa cập nhật được; vui lòng tải lại trang.';
        notice.className = 'alert ' + (refreshed ? 'alert-success' : 'alert-warning');
        notice.focus();
    }

    function setBusy(form, busy) {
        form.dataset.submitting = busy ? 'true' : 'false';
        form.setAttribute('aria-busy', String(busy));
        const modal = form.closest('.modal');
        modal.querySelectorAll('button').forEach(button => { button.disabled = busy; });
        form.querySelector('button[type="submit"]').textContent = busy ? 'Đang xử lý…' :
            (form.matches('[data-contract-edit-form]') ? 'Lưu thay đổi' : 'Xóa hợp đồng');
    }

    async function openEdit(button) {
        opener = button;
        editRequest?.abort();
        editRequest = new AbortController();
        const request = editRequest;
        content.innerHTML = '<div class="modal-header px-4"><h2 class="modal-title h5" id="editContractModalLabel">Chỉnh sửa hợp đồng</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button></div><div class="modal-body p-4 text-secondary" role="status">Đang tải thông tin hợp đồng…</div>';
        bootstrap.Modal.getOrCreateInstance(editModal).show();
        try {
            const response = await fetch(button.dataset.contractEditUrl, { headers, signal: request.signal });
            const error = responseError(response);
            if (error || !response.ok) throw new Error(error || 'Không thể tải hợp đồng. Vui lòng thử lại.');
            const html = await response.text();
            if (request.signal.aborted) return;
            content.innerHTML = html;
            window.ContractForm.init(content);
            content.querySelector('input:not([type="hidden"]):not([readonly]), select')?.focus();
        } catch (error) {
            if (error.name === 'AbortError') return;
            const body = content.querySelector('.modal-body');
            body.setAttribute('role', 'alert');
            body.textContent = error.message;
        }
    }

    document.addEventListener('click', async event => {
        const editButton = event.target.closest('[data-contract-edit-url]');
        if (editButton) {
            event.preventDefault();
            openEdit(editButton);
            return;
        }
        const deleteButton = event.target.closest('[data-contract-delete-url]');
        if (deleteButton) {
            event.preventDefault();
            opener = deleteButton;
            const form = deleteModal.querySelector('form');
            form.action = deleteButton.dataset.contractDeleteUrl;
            form.querySelector('[data-contract-delete-number]').textContent = deleteButton.dataset.contractNumber;
            form.querySelector('[data-contract-delete-employee]').textContent = deleteButton.dataset.contractEmployee;
            form.querySelector('[data-contract-form-error]').classList.add('d-none');
            bootstrap.Modal.getOrCreateInstance(deleteModal).show();
        }
        const generate = event.target.closest('[data-contract-generate="true"]');
        if (generate) await generateNumber(generate.closest('form'));
        const print = event.target.closest('[data-contract-print]');
        if (print && print.closest('[data-contract-edit-form]')) window.printContractDocument(print.closest('form'));
    });

    async function generateNumber(form) {
        try {
            const response = await fetch('/HopDong/GenerateContractNumber?hinhThuc=' + encodeURIComponent(form.elements['Contract.HinhThucHopDong'].value), { headers });
            const error = responseError(response);
            if (error || !response.ok) throw new Error(error || 'Không thể tạo mã hợp đồng. Vui lòng thử lại.');
            const data = await response.json();
            if (!data.contractNumber) throw new Error('Không nhận được mã hợp đồng mới. Vui lòng thử lại.');
            form.elements['Contract.SoHopDong'].value = data.contractNumber;
        } catch (error) { showError(form, error.message); }
    }

    editModal.addEventListener('change', async event => {
        const field = event.target;
        const form = field.closest('form');
        if (!form) return;
        if (field.name === 'DepartmentId') {
            const positions = form.elements.PositionId;
            positions.replaceChildren(new Option('Đang tải chức vụ…', ''));
            positions.disabled = true;
            const departmentId = field.value;
            try {
                const response = await fetch('/ChucVu/GetByDepartment?departmentId=' + encodeURIComponent(departmentId), { headers });
                const error = responseError(response);
                if (error || !response.ok) throw new Error(error || 'Không thể tải chức vụ. Hãy chọn lại phòng ban.');
                const data = await response.json();
                if (field.value !== departmentId) return;
                positions.replaceChildren(new Option('-- Chọn chức vụ --', ''));
                data.forEach(item => positions.add(new Option(item.tenChucVu + (item.tenPhongBan ? ' (' + item.tenPhongBan + ')' : ' (Chung)'), item.maChucVu)));
            } catch (error) {
                if (field.value !== departmentId) return;
                positions.replaceChildren(new Option('Không thể tải chức vụ', ''));
                showError(form, error.message);
            } finally {
                if (field.value === departmentId) positions.disabled = false;
            }
        }
        if (field.name === 'Contract.HinhThucHopDong') await generateNumber(form);
    });

    document.addEventListener('submit', async event => {
        const form = event.target.closest('[data-contract-edit-form], [data-contract-delete-form]');
        if (!form) return;
        event.preventDefault();
        if (form.dataset.submitting === 'true' || !form.reportValidity()) return;
        setBusy(form, true);
        try {
            const response = await fetch(form.action, { method: 'POST', headers, body: new FormData(form) });
            const error = responseError(response);
            if (error) throw new Error(error);
            if (response.status === 422 && form.matches('[data-contract-edit-form]')) {
                content.innerHTML = await response.text();
                window.ContractForm.init(content);
                const validation = content.querySelector('.field-validation-error, .validation-summary-errors');
                if (validation) { validation.tabIndex = -1; validation.focus(); }
                return;
            }
            const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok || !data?.success) throw new Error(data?.message || 'Không thể lưu thay đổi. Vui lòng thử lại.');
            const modal = form.closest('.modal');
            setBusy(form, false);
            bootstrap.Modal.getOrCreateInstance(modal).hide();
            await refreshList(data.message);
        } catch (error) { showError(form, error.message || 'Mất kết nối. Vui lòng thử lại.'); }
        finally { if (form.isConnected) setBusy(form, false); }
    });

    [editModal, deleteModal].forEach(modal => {
        modal.addEventListener('hide.bs.modal', event => {
            if (modal.querySelector('[data-submitting="true"]')) event.preventDefault();
        });
        modal.addEventListener('hidden.bs.modal', () => {
            if (modal === editModal) { editRequest?.abort(); content.replaceChildren(); }
            if (opener?.isConnected) opener.focus();
        });
    });
    deleteModal.addEventListener('shown.bs.modal', () => deleteModal.querySelector('[data-bs-dismiss="modal"].btn').focus());
})();
