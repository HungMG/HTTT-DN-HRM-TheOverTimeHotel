(function () {
    'use strict';
    const modal = document.getElementById('salaryReviewsModal');
    if (!modal) return;
    const content = modal.querySelector('[data-reviews-content]');
    let request;
    let opener;
    async function load() {
        request?.abort();
        const current = new AbortController();
        request = current;
        content.setAttribute('aria-busy', 'true');
        content.innerHTML = '<div class="salary-review-state text-center" role="status"><span class="spinner-border spinner-border-sm text-success me-2" aria-hidden="true"></span>Đang tải danh sách nhân viên…</div>';
        try {
            const response = await fetch(modal.dataset.reviewsUrl, { signal: current.signal, headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' } });
            if (response.redirected || response.status === 401 || response.status === 403)
                throw new Error('Phiên đăng nhập đã thay đổi hoặc bạn không có quyền xem. Vui lòng tải lại trang và đăng nhập.');
            if (!response.ok) throw new Error('Chưa tải được danh sách. Vui lòng thử lại.');
            const html = await response.text();
            if (request !== current || current.signal.aborted) return;
            // Do not embed a full error/login page inside the modal.
            if (!html.includes('salary-review-list') && !html.includes('data-reviews-empty'))
                throw new Error('Không nhận được danh sách hợp lệ. Vui lòng thử lại.');
            content.innerHTML = html;
        } catch (error) {
            if (current.signal.aborted || request !== current) return;
            content.replaceChildren();
            const state = document.createElement('div'); state.className = 'salary-review-state text-center';
            const message = document.createElement('p'); message.className = 'text-muted'; message.setAttribute('role', 'alert'); message.textContent = error.message || 'Mất kết nối. Vui lòng thử lại.';
            const retry = document.createElement('button'); retry.type = 'button'; retry.className = 'btn btn-outline-primary rounded-pill px-4'; retry.textContent = 'Thử lại'; retry.addEventListener('click', load);
            state.append(message, retry); content.append(state);
        } finally { if (request === current) content.removeAttribute('aria-busy'); }
    }
    modal.addEventListener('show.bs.modal', event => { opener = event.relatedTarget || document.activeElement; load(); });
    modal.addEventListener('hide.bs.modal', () => { request?.abort(); content.removeAttribute('aria-busy'); });
    modal.addEventListener('hidden.bs.modal', () => { if (opener?.isConnected) opener.focus(); });
})();
