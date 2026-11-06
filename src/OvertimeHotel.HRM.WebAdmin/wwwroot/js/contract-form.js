(function () {
    'use strict';
    const probation = 'THU_VIEC';
    const oneYear = 'XAC_DINH_THOI_HAN_1_NAM';

    function sync(form, switching = false) {
        const stage = form?.elements['Contract.HinhThucHopDong'];
        const duration = form?.elements['Contract.LoaiHopDong'];
        const end = form?.elements['Contract.NgayKetThuc'];
        if (!stage || !duration || !end) return;
        const isProbation = stage.value === probation;
        const previous = form.dataset.contractStage;
        const description = form.querySelector('[data-contract-probation-duration]');
        if (isProbation) {
            if (duration.value && duration.value !== probation) duration.dataset.officialValue = duration.value;
            duration.value = probation;
            if (switching && previous && previous !== probation) end.value = '';
        } else if (duration.value === probation || !duration.value) {
            duration.value = duration.dataset.officialValue || oneYear;
        }
        duration.hidden = isProbation;
        duration.required = !isProbation;
        if (description) description.hidden = !isProbation;
        const label = form.querySelector('[data-contract-duration-label]');
        if (label) label.htmlFor = isProbation ? description.id : duration.id;
        end.required = isProbation;
        if (isProbation) end.min = form.elements['Contract.NgayBatDau'].value;
        else end.removeAttribute('min');
        const required = form.querySelector('[data-contract-end-required]');
        if (required) required.hidden = !isProbation;
        const hint = form.querySelector('[data-contract-end-hint]');
        if (hint) hint.textContent = isProbation
            ? 'Nhập ngày kết thúc thử việc; hệ thống giữ khoảng thời gian bạn chọn.'
            : 'Tự động tính theo thời hạn 1 năm, 3 năm; để trống nếu không xác định thời hạn.';
        form.dataset.contractStage = stage.value;
    }

    function calculate(form) {
        sync(form);
        if (!form || form.elements['Contract.HinhThucHopDong']?.value === probation) return;
        const start = form.elements['Contract.NgayBatDau'];
        const end = form.elements['Contract.NgayKetThuc'];
        const duration = form.elements['Contract.LoaiHopDong'];
        if (!start?.value || !end || !duration) return;
        const date = new Date(start.value + 'T00:00:00Z');
        if (Number.isNaN(date.getTime())) return;
        const years = { XAC_DINH_THOI_HAN_1_NAM: 1, XAC_DINH_THOI_HAN_3_NAM: 3 }[duration.value];
        if (years) date.setUTCFullYear(date.getUTCFullYear() + years);
        end.value = years ? date.toISOString().slice(0, 10) : '';
    }

    function init(container = document) {
        const forms = container.matches?.('form') ? [container] : container.querySelectorAll('form');
        forms.forEach(form => sync(form));
    }

    document.addEventListener('change', event => {
        const field = event.target;
        const form = field.closest('form');
        if (!form?.elements['Contract.HinhThucHopDong']) return;
        if (field.name === 'Contract.HinhThucHopDong') {
            sync(form, true);
            calculate(form);
        } else if (['Contract.LoaiHopDong', 'Contract.NgayBatDau'].includes(field.name)) calculate(form);
    });
    document.addEventListener('input', event => {
        if (event.target.name === 'Contract.NgayBatDau') calculate(event.target.closest('form'));
    });
    document.addEventListener('reset', event => {
        if (event.target.elements['Contract.HinhThucHopDong'])
            setTimeout(() => { sync(event.target); calculate(event.target); }, 0);
    });
    window.ContractForm = { init, sync, calculate };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => init());
    else init();
})();
