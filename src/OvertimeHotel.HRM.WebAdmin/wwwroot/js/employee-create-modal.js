(() => {
    const modal = document.getElementById('createEmployeeModal');
    if (!modal) return;
    const host = modal.querySelector('#employeeFormPanel');
    const submit = modal.querySelector('[data-employee-create-submit]');
    let form = null;
    let saving = false;

    function error(message) {
        let alert = host.querySelector('[data-modal-load-error]');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-danger';
            alert.setAttribute('role', 'alert');
            alert.dataset.modalLoadError = '';
            host.prepend(alert);
        }
        alert.textContent = message;
    }

    function install(html) {
        form = document.createElement('form');
        form.method = 'post';
        form.action = host.dataset.createUrl;
        form.id = 'employeeActiveForm';
        form.innerHTML = html;
        host.replaceChildren(form);
        form.querySelectorAll('script').forEach(old => {
            const script = document.createElement('script');
            script.textContent = old.textContent;
            old.replaceWith(script);
        });
        window.EntryValidation?.init();
        submit.disabled = false;
    }

    modal.addEventListener('show.bs.modal', async () => {
        if (form) return;
        submit.disabled = true;
        host.textContent = 'Đang tải biểu mẫu…';
        host.setAttribute('aria-busy', 'true');
        try {
            const url = new URL(host.dataset.createUrl, location.origin);
            url.searchParams.set('entryMode', 'FROM_CONTRACT');
            const response = await fetch(url, { headers: { 'X-Employee-Modal': '1' } });
            if (response.redirected || !response.ok) throw new Error('Không thể tải biểu mẫu. Vui lòng tải lại trang và đăng nhập lại nếu cần.');
            install(await response.text());
        } catch (e) { host.replaceChildren(); error(e.message || 'Không thể tải biểu mẫu.'); }
        finally { host.removeAttribute('aria-busy'); }
    });
    modal.addEventListener('hide.bs.modal', event => { if (saving) event.preventDefault(); });
    host.addEventListener('submit', async event => {
        if (event.target !== form) return;
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (saving || !form.reportValidity()) return;
        saving = true;
        submit.disabled = true;
        form.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), headers: { 'X-Employee-Modal': '1' } });
            if (response.redirected) throw new Error('Phiên làm việc có thể đã hết hạn. Vui lòng tải lại trang và đăng nhập lại.');
            if (response.status === 422) {
                install(await response.text());
                window.EntryValidation?.focusError(form);
            } else if (response.ok && (response.headers.get('content-type') || '').includes('application/json')) {
                const result = await response.json();
                if (!result.success) throw new Error(result.message || 'Không thể lưu hồ sơ.');
                window.location.reload();
            } else throw new Error('Không thể lưu hồ sơ. Vui lòng thử lại.');
        } catch (e) { error(e.message || 'Không thể lưu hồ sơ. Vui lòng thử lại.'); }
        finally { saving = false; submit.disabled = false; form.removeAttribute('aria-busy'); }
    });
})();
