(() => {
    "use strict";
    const dialog = document.getElementById("accountDetailsModal");
    const content = document.getElementById("accountDetailsModalContent");
    if (!dialog || !content || !window.bootstrap) return;
    const modal = bootstrap.Modal.getOrCreateInstance(dialog);
    let requestId = 0;

    const openDetails = async (row) => {
        const url = row.dataset.accountDetailsUrl;
        if (!url) return;
        const current = ++requestId;
        content.classList.add("account-detail-message");
        content.textContent = "Đang tải thông tin tài khoản...";
        content.setAttribute("aria-busy", "true");
        modal.show();
        try {
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (response.redirected || response.status === 401 || response.status === 403)
                throw new Error("Phiên đăng nhập không còn hợp lệ hoặc bạn không có quyền xem tài khoản.");
            if (response.status === 404) throw new Error("Không tìm thấy tài khoản này.");
            if (!response.ok) throw new Error("Không thể tải thông tin tài khoản. Vui lòng thử lại.");
            const html = await response.text();
            if (current === requestId) {
                content.classList.remove("account-detail-message");
                content.innerHTML = html;
            }
        } catch (error) {
            if (current === requestId) content.textContent = error instanceof Error ? error.message : "Không thể tải thông tin tài khoản.";
        } finally {
            if (current === requestId) content.removeAttribute("aria-busy");
        }
    };
    dialog.addEventListener("hidden.bs.modal", () => { requestId++; });
    document.addEventListener("click", (event) => {
        const row = event.target.closest("[data-account-details-url]");
        if (!row) return;
        if (!event.target.closest("[data-account-details-open]") && event.target.closest("button, a, input, select, textarea, form")) return;
        void openDetails(row);
    });
    document.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        const row = event.target.closest("[data-account-details-url]");
        if (!row || event.target !== row) return;
        event.preventDefault();
        void openDetails(row);
    });
})();
