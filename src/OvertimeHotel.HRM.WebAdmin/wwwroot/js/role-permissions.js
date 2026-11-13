(() => {
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
