(() => {
    "use strict";

    document.querySelectorAll("[data-account-toast]").forEach((toast) => {
        let closeTimer;

        const closeToast = () => {
            window.clearTimeout(closeTimer);
            toast.classList.remove("is-visible");
            toast.classList.add("is-leaving");
            window.setTimeout(() => toast.remove(), 260);
        };

        toast.querySelector("[data-account-toast-close]")?.addEventListener("click", closeToast);
        window.requestAnimationFrame(() => toast.classList.add("is-visible"));
        closeTimer = window.setTimeout(closeToast, 5200);
    });

    const dialog = document.querySelector("[data-account-confirm]");
    if (!dialog) {
        return;
    }

    const title = dialog.querySelector("[data-account-confirm-title]");
    const message = dialog.querySelector("[data-account-confirm-message]");
    const accountName = dialog.querySelector("[data-account-confirm-name]");
    const icon = dialog.querySelector(".account-confirm-icon i");
    const submitButton = dialog.querySelector("[data-account-confirm-submit]");
    const submitLabel = submitButton?.querySelector("span");
    let pendingForm = null;

    const closeDialog = () => {
        pendingForm = null;
        if (dialog.open) {
            dialog.close();
        }
    };

    document.querySelectorAll(".account-toggle-form").forEach((form) => {
        form.addEventListener("submit", (event) => {
            event.preventDefault();
            pendingForm = form;

            const isLock = form.dataset.accountMode === "lock";
            title.textContent = isLock ? "Khóa tài khoản?" : "Mở khóa tài khoản?";
            message.textContent = isLock
                ? "Người dùng sẽ không thể đăng nhập cho đến khi tài khoản được mở khóa."
                : "Người dùng sẽ có thể đăng nhập và sử dụng quyền được cấp trở lại.";
            accountName.textContent = form.dataset.accountName || "Tài khoản";
            icon.className = isLock ? "bi bi-person-lock" : "bi bi-person-check";
            submitLabel.textContent = isLock ? "Khóa tài khoản" : "Mở khóa";
            submitButton.classList.toggle("is-unlock", !isLock);

            if (typeof dialog.showModal === "function") {
                dialog.showModal();
            } else if (window.confirm(title.textContent)) {
                form.submit();
            }
        });
    });

    dialog.querySelectorAll("[data-account-confirm-cancel]").forEach((button) => {
        button.addEventListener("click", closeDialog);
    });

    submitButton?.addEventListener("click", () => {
        const form = pendingForm;
        closeDialog();
        form?.submit();
    });

    dialog.addEventListener("click", (event) => {
        if (event.target === dialog) {
            closeDialog();
        }
    });
})();
