(() => {
    const dialog = document.getElementById("rolePermissionsModal");
    const content = document.getElementById("rolePermissionsContent");
    if (!dialog || !content || !window.bootstrap) return;

    const modal = bootstrap.Modal.getOrCreateInstance(dialog);
    let endpoint;
    let busy = false;

    async function load(url, options) {
        if (busy) return;
        busy = true;
        content.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(url, options);
            if (!response.ok) throw new Error();
            const html = await response.text();
            const parsed = new DOMParser().parseFromString(html, "text/html");
            if (!parsed.querySelector("#rolePermissionsForm")) throw new Error();
            content.innerHTML = html;
        } catch {
            let alert = content.querySelector("[data-role-load-error]");
            if (!alert) {
                alert = document.createElement("div");
                alert.className = "alert alert-danger";
                alert.setAttribute("role", "alert");
                alert.dataset.roleLoadError = "";
                content.prepend(alert);
            }
            alert.textContent = "Không tải hoặc lưu được phân quyền. Vui lòng thử lại hoặc đăng nhập lại.";
        } finally {
            busy = false;
            content.removeAttribute("aria-busy");
        }
    }

    document.querySelectorAll("[data-role-permissions-open]").forEach(link => {
        link.addEventListener("click", event => {
            event.preventDefault();
            endpoint = new URL(link.href);
            endpoint.searchParams.set("popup", "true");
            content.textContent = "Đang tải phân quyền...";
            modal.show();
            load(endpoint);
        });
    });

    content.addEventListener("change", event => {
        if (event.target.matches('select[name="RoleId"]')) {
            const url = new URL(endpoint);
            url.searchParams.set("roleId", event.target.value);
            load(url);
        }
        if (event.target.matches(".permission-checkbox")) updateCard(event.target);
    });

    content.addEventListener("click", event => {
        const button = event.target.closest("#selectAllPermissionsBtn, #deselectAllPermissionsBtn");
        if (!button) return;
        content.querySelectorAll(".permission-checkbox").forEach(checkbox => {
            checkbox.checked = button.id === "selectAllPermissionsBtn";
            updateCard(checkbox);
        });
    });

    content.addEventListener("submit", event => {
        if (event.target.id !== "rolePermissionsForm") return;
        event.preventDefault();
        load(event.target.action, { method: "POST", body: new FormData(event.target) });
    });

    function updateCard(checkbox) {
        const card = checkbox.closest("label");
        if (!card) return;
        card.classList.toggle("bg-primary-subtle", checkbox.checked);
        card.classList.toggle("border-primary-subtle", checkbox.checked);
        card.classList.toggle("bg-light", !checkbox.checked);
        card.classList.toggle("bg-opacity-50", !checkbox.checked);
        card.classList.toggle("border-light-subtle", !checkbox.checked);
    }
})();
