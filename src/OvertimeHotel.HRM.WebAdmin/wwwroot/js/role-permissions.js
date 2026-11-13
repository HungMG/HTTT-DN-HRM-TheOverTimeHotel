(() => {
    window.showRolePermissionsNotice = (message, isError = false) => {
        const notice = document.createElement('dialog');
        notice.className = 'border-0 rounded-4 shadow-lg p-0';
        notice.style.cssText = 'max-width: 420px; width: calc(100% - 32px);';
        notice.setAttribute('aria-labelledby', 'rolePermissionsNoticeTitle');
        notice.innerHTML = `<div class="p-4 text-center">
            <i class="bi ${isError ? 'bi-exclamation-circle-fill text-danger' : 'bi-check-circle-fill text-success'}" style="font-size: 2.5rem;"></i>
            <h2 id="rolePermissionsNoticeTitle" class="fs-5 fw-bold mt-3">${isError ? 'Không thể lưu' : 'Thông báo'}</h2>
            <p class="text-secondary mb-4" data-notice-message></p>
            <button type="button" class="btn btn-primary px-4">Đóng</button>
        </div>`;
        notice.querySelector('[data-notice-message]').textContent = message;
        notice.querySelector('button').addEventListener('click', () => notice.close());
        notice.addEventListener('close', () => {
            notice.remove();
            if (!isError) {
                const content = document.querySelector('#rolePermissionsModal.show #rolePermissionsContent');
                if (content) content.scrollTo({ top: 0, behavior: 'smooth' });
                else window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        });
        (document.querySelector('#rolePermissionsModal.show') || document.body).appendChild(notice);
        notice.showModal();
    };

    const saveMessage = document.querySelector('[data-role-save-message]');
    if (saveMessage) window.showRolePermissionsNotice(saveMessage.textContent.trim());

    function updateCard(box) {
        const card = box.closest('label');
        if (!card) return;
        for (const c of ['bg-primary-subtle', 'border-primary-subtle']) card.classList.toggle(c, box.checked);
        for (const c of ['bg-light', 'bg-opacity-50', 'border-light-subtle']) card.classList.toggle(c, !box.checked);
    }
    document.addEventListener('change', event => {
        const box = event.target;
        if (!box.matches('.permission-checkbox')) return;
        const form = box.closest('form');
        if (box.dataset.code === 'REPORT_VIEW') form.querySelector('[data-report-changed]').value = 'true';
        const boxes = [...form.querySelectorAll('.permission-checkbox')];
        if (box.checked && box.dataset.requires) {
            const view = boxes.find(item => item.dataset.code === box.dataset.requires);
            if (view) { view.checked = true; updateCard(view); }
        }
        if (!box.checked && boxes.some(item => item.checked && item.dataset.requires === box.dataset.code)) {
            box.checked = true;
            alert('Hãy bỏ quyền thao tác hoặc duyệt trước khi bỏ quyền xem.');
        }
        updateCard(box);
    });
    document.addEventListener('click', event => {
        const button = event.target.closest('#selectAllPermissionsBtn, #deselectAllPermissionsBtn');
        if (!button) return;
        button.closest('form').querySelectorAll('.permission-checkbox:not(:disabled)').forEach(box => {
            const next = button.id === 'selectAllPermissionsBtn';
            if (box.dataset.code === 'REPORT_VIEW' && box.checked !== next)
                box.closest('form').querySelector('[data-report-changed]').value = 'true';
            box.checked = next;
            updateCard(box);
        });
    });
})();
