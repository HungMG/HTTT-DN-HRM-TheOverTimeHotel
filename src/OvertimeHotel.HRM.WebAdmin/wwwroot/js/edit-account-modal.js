(() => {
    "use strict";

    window.initEditAccountForm = (root = document) => {
        const scope = root instanceof Element || root instanceof Document ? root : document;
        scope.querySelectorAll(".password-toggle").forEach((btn) => {
            if (btn.dataset.bound === "true") return;
            btn.dataset.bound = "true";
            btn.addEventListener("click", () => {
                const group = btn.closest(".input-group");
                const input = group?.querySelector(".password-field");
                const icon = btn.querySelector("i");
                if (!input || !icon) return;

                if (input.type === "password") {
                    input.type = "text";
                    icon.className = "bi bi-eye-slash";
                } else {
                    input.type = "password";
                    icon.className = "bi bi-eye";
                }
            });
        });
    };

    const dialog = document.getElementById("editAccountModal");
    const content = document.getElementById("editAccountModalContent");
    const subtitle = document.getElementById("editAccountModalSubtitle");
    if (!dialog || !content || !window.bootstrap) return;

    const modal = bootstrap.Modal.getOrCreateInstance(dialog);
    let busy = false;

    const showLoadError = (message) => {
        let alert = content.querySelector("[data-edit-account-error]");
        if (!alert) {
            alert = document.createElement("div");
            alert.className = "alert alert-danger border-0 rounded-4";
            alert.setAttribute("role", "alert");
            alert.dataset.editAccountError = "";
            content.prepend(alert);
        }
        alert.textContent = message;
    };

    const loadForm = async (url) => {
        if (busy) return;
        busy = true;
        content.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(url, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }
            if (!response.ok) throw new Error("Không tải được biểu mẫu chỉnh sửa tài khoản.");

            const html = await response.text();
            const parsed = new DOMParser().parseFromString(html, "text/html");
            if (!parsed.querySelector("#editAccountForm")) {
                throw new Error("Dữ liệu biểu mẫu không hợp lệ.");
            }
            content.innerHTML = html;
            window.initEditAccountForm(content);
            content.querySelector("#Username")?.focus();
        } catch (error) {
            content.replaceChildren();
            showLoadError(error instanceof Error ? error.message : "Không tải được biểu mẫu. Vui lòng thử lại.");
        } finally {
            busy = false;
            content.removeAttribute("aria-busy");
        }
    };

    dialog.addEventListener("hide.bs.modal", (event) => {
        if (busy) event.preventDefault();
    });

    document.addEventListener("click", (event) => {
        const trigger = event.target.closest("[data-account-edit-open]");
        if (!(trigger instanceof HTMLElement) || !trigger.dataset.accountEditUrl) return;

        event.preventDefault();
        const url = new URL(trigger.dataset.accountEditUrl, window.location.origin);
        url.searchParams.set("popup", "true");

        if (subtitle) {
            const name = trigger.dataset.accountName?.trim();
            subtitle.textContent = name
                ? `Cập nhật thông tin đăng nhập cho ${name}`
                : "Điều chỉnh thông tin đăng nhập và đặt lại mật khẩu";
        }

        content.innerHTML = `<div class="text-center py-5 text-muted">
            <div class="spinner-border text-primary mb-2" role="status"><span class="visually-hidden">Đang tải...</span></div>
            <div class="small">Đang tải thông tin tài khoản...</div>
        </div>`;
        modal.show();
        void loadForm(url.toString());
    });

    content.addEventListener("submit", async (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.id !== "editAccountForm") return;
        event.preventDefault();
        if (busy) return;

        const submitButton = form.querySelector("[data-edit-account-submit]");
        busy = true;
        if (submitButton instanceof HTMLButtonElement) submitButton.disabled = true;
        content.setAttribute("aria-busy", "true");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }

            if (response.status === 422) {
                const html = await response.text();
                content.innerHTML = html;
                window.initEditAccountForm(content);
                return;
            }

            const contentType = response.headers.get("content-type") || "";
            if (!response.ok || !contentType.includes("application/json")) {
                throw new Error("Không thể lưu thay đổi tài khoản. Vui lòng thử lại.");
            }

            const result = await response.json();
            if (!result?.success) {
                throw new Error(result?.message || "Không thể lưu thay đổi tài khoản.");
            }

            busy = false;
            modal.hide();
            document.dispatchEvent(new CustomEvent("account:updated", { detail: result }));
        } catch (error) {
            showLoadError(error instanceof Error ? error.message : "Không thể lưu thay đổi tài khoản.");
        } finally {
            busy = false;
            content.removeAttribute("aria-busy");
            const currentSubmit = content.querySelector("[data-edit-account-submit]");
            if (currentSubmit instanceof HTMLButtonElement) currentSubmit.disabled = false;
        }
    });
})();
