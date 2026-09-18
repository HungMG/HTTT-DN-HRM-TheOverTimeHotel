(() => {
    "use strict";

    const toastStack = document.querySelector(".account-popup-stack");

    const activateToast = (toast) => {
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
    };

    document.querySelectorAll("[data-account-toast]").forEach(activateToast);

    const showToast = (message, type = "success") => {
        if (!toastStack) return;

        const toast = document.createElement("div");
        toast.className = `account-popup account-popup--${type === "success" ? "success" : "danger"}`;
        toast.dataset.accountToast = "";
        toast.setAttribute("role", type === "success" ? "status" : "alert");

        const iconWrap = document.createElement("div");
        iconWrap.className = "account-popup-icon";
        const icon = document.createElement("i");
        icon.className = type === "success" ? "bi bi-check2" : "bi bi-exclamation-lg";
        iconWrap.append(icon);

        const copy = document.createElement("div");
        copy.className = "account-popup-copy";
        const label = document.createElement("small");
        label.textContent = type === "success" ? "Thao tác thành công" : "Không thể thực hiện";
        const text = document.createElement("strong");
        text.textContent = message;
        copy.append(label, text);

        const close = document.createElement("button");
        close.type = "button";
        close.dataset.accountToastClose = "";
        close.setAttribute("aria-label", "Đóng thông báo");
        const closeIcon = document.createElement("i");
        closeIcon.className = "bi bi-x-lg";
        close.append(closeIcon);

        const progress = document.createElement("span");
        progress.className = "account-popup-progress";
        toast.append(iconWrap, copy, close, progress);
        toastStack.prepend(toast);
        activateToast(toast);
    };

    // 1. Hiệu ứng đếm số CountUp (từ react-bits) khi số liệu KPI cập nhật
    const animateCountUp = (element, startValue, targetValue, duration = 450) => {
        if (!element) return;
        if (startValue === targetValue) {
            element.textContent = targetValue;
            return;
        }

        const startTime = performance.now();
        element.setAttribute("data-count", targetValue);

        const updateCount = (currentTime) => {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            const easeProgress = 1 - Math.pow(1 - progress, 3);
            const currentCount = Math.round(startValue + (targetValue - startValue) * easeProgress);

            element.textContent = currentCount;

            if (progress < 1) {
                requestAnimationFrame(updateCount);
            } else {
                element.textContent = targetValue;
            }
        };

        requestAnimationFrame(updateCount);
    };

    // 2. Hiệu ứng phát quang vàng kim (Pulse) lướt nhẹ qua 4 thẻ KPI
    const pulseKpiCards = () => {
        const stats = document.querySelectorAll(".motion-stat");
        stats.forEach((item, idx) => {
            window.setTimeout(() => {
                item.classList.add("kpi-pulse-active");
                window.setTimeout(() => {
                    item.classList.remove("kpi-pulse-active");
                }, 380);
            }, idx * 60);
        });
    };

    const updateStatistics = (data) => {
        const total = Number(document.querySelector("[data-account-total]")?.textContent || 0);
        const activeValue = document.querySelector("[data-account-active]");
        const lockedValue = document.querySelector("[data-account-locked]");
        const activeTrack = document.querySelector("[data-account-active-track]");
        const lockedTrack = document.querySelector("[data-account-locked-track]");

        if (activeValue && data.activeAccounts !== undefined) {
            const oldActive = Number(activeValue.textContent || 0);
            animateCountUp(activeValue, oldActive, data.activeAccounts);
        }
        if (lockedValue && data.lockedAccounts !== undefined) {
            const oldLocked = Number(lockedValue.textContent || 0);
            animateCountUp(lockedValue, oldLocked, data.lockedAccounts);
        }
        if (activeTrack && data.activeAccounts !== undefined) {
            activeTrack.style.width = `${total > 0 ? Math.round(data.activeAccounts * 100 / total) : 0}%`;
        }
        if (lockedTrack && data.lockedAccounts !== undefined) {
            lockedTrack.style.width = `${total > 0 ? Math.round(data.lockedAccounts * 100 / total) : 0}%`;
        }

        pulseKpiCards();
    };

    const appendTextWithIcon = (parent, iconClass, value) => {
        const element = document.createElement("span");
        const icon = document.createElement("i");
        icon.className = `bi ${iconClass}`;
        element.append(icon, document.createTextNode(value));
        parent.append(element);
    };

    const vietnamDate = (isoValue) => {
        const parts = new Intl.DateTimeFormat("en-CA", {
            timeZone: "Asia/Ho_Chi_Minh",
            year: "numeric",
            month: "2-digit",
            day: "2-digit"
        }).formatToParts(new Date(isoValue));
        const getPart = (type) => parts.find((part) => part.type === type)?.value;
        return `${getPart("year")}-${getPart("month")}-${getPart("day")}`;
    };

    const matchesAuditDateFilter = (log) => {
        if (!log?.occurredAt) return false;
        const workspace = document.querySelector("[data-account-workspace]");
        const date = vietnamDate(log.occurredAt);
        const from = workspace?.dataset.auditFrom;
        const to = workspace?.dataset.auditTo;
        return (!from || date >= from) && (!to || date <= to);
    };

    const localizeAuditValue = (value) => value
        ?.replaceAll("Vai trò: Admin", "Vai trò: Quản trị viên")
        .replaceAll("Vai trò: HR", "Vai trò: Nhân sự")
        .replaceAll("Vai trò: Manager", "Vai trò: Quản lý")
        .replaceAll("Vai trò: Employee", "Vai trò: Nhân viên");

    const auditFilter = document.querySelector("[data-account-audit-filter]");

    const setAuditDateLimits = () => {
        if (!auditFilter) return;
        const fromInput = auditFilter.querySelector("[name='auditFrom']");
        const toInput = auditFilter.querySelector("[name='auditTo']");
        if (fromInput) fromInput.max = toInput?.value || "";
        if (toInput) toInput.min = fromInput?.value || "";
    };

    const updateAuditFilterUrl = (formData) => {
        const url = new URL(window.location.href);
        ["auditFrom", "auditTo"].forEach((name) => {
            const value = String(formData.get(name) || "");
            if (value) url.searchParams.set(name, value);
            else url.searchParams.delete(name);
        });
        window.history.replaceState({}, "", `${url.pathname}${url.search}${url.hash}`);
    };

    const updateAuditDateState = (formData) => {
        const workspace = document.querySelector("[data-account-workspace]");
        const from = String(formData.get("auditFrom") || "");
        const to = String(formData.get("auditTo") || "");
        if (workspace) {
            workspace.dataset.auditFrom = from;
            workspace.dataset.auditTo = to;
        }
        document.querySelectorAll(".account-filter-form input[name='auditFrom']").forEach((input) => input.value = from);
        document.querySelectorAll(".account-filter-form input[name='auditTo']").forEach((input) => input.value = to);
        const clearButton = auditFilter?.querySelector("[data-account-audit-clear]");
        clearButton?.classList.toggle("is-disabled", !from && !to);
        clearButton?.setAttribute("aria-disabled", String(!from && !to));
    };

    const setAuditFilterLoading = (isLoading) => {
        if (!auditFilter) return;
        auditFilter.classList.toggle("is-loading", isLoading);
        auditFilter.querySelectorAll("button, input").forEach((control) => control.disabled = isLoading);
    };

    const loadAuditLogs = async () => {
        if (!auditFilter) return;
        const formData = new FormData(auditFilter);
        const endpoint = auditFilter.dataset.auditEndpoint;
        if (!endpoint) return;

        const requestUrl = new URL(endpoint, window.location.origin);
        ["auditFrom", "auditTo"].forEach((name) => {
            const value = String(formData.get(name) || "");
            if (value) requestUrl.searchParams.set(name, value);
        });

        const auditOverlay = document.getElementById("auditLoadingOverlay");
        if (auditOverlay) auditOverlay.classList.remove("d-none");
        setAuditFilterLoading(true);
        try {
            const response = await fetch(requestUrl, {
                headers: { "Accept": "text/html", "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) {
                const error = await response.json().catch(() => null);
                throw new Error(error?.message || "Không thể lọc nhật ký quản trị.");
            }

            const content = document.querySelector("[data-account-audit-content]");
            if (!content) return;
            content.innerHTML = await response.text();
            content.classList.remove("in-place-enter");
            void content.offsetWidth;
            content.classList.add("in-place-enter");

            window.HotelMotion?.applyEntrance?.(content);
            const count = document.querySelector("[data-account-audit-count]");
            if (count) count.textContent = response.headers.get("X-Audit-Count") || "0";
            updateAuditDateState(formData);
            updateAuditFilterUrl(formData);
        } catch (error) {
            showToast(error instanceof Error ? error.message : "Không thể lọc nhật ký quản trị.", "danger");
        } finally {
            if (auditOverlay) auditOverlay.classList.add("d-none");
            setAuditFilterLoading(false);
        }
    };

    auditFilter?.addEventListener("submit", (event) => {
        event.preventDefault();
        void loadAuditLogs();
    });

    auditFilter?.querySelectorAll("input[type='date']").forEach((input) => input.addEventListener("change", setAuditDateLimits));

    auditFilter?.querySelector("[data-account-audit-clear]")?.addEventListener("click", (event) => {
        event.preventDefault();
        if (event.currentTarget.getAttribute("aria-disabled") === "true") return;
        const fromInput = auditFilter.querySelector("[name='auditFrom']");
        const toInput = auditFilter.querySelector("[name='auditTo']");
        if (fromInput) fromInput.value = "";
        if (toInput) toInput.value = "";
        setAuditDateLimits();
        void loadAuditLogs();
    });

    const prependAuditLog = (log) => {
        const list = document.querySelector("[data-account-audit-list]");
        if (!list || !log || !matchesAuditDateFilter(log)) return;

        document.querySelector("[data-account-audit-empty]")?.remove();
        const presentation = {
            LOCK_ACCOUNT: ["is-lock", "bi-person-lock"],
            UNLOCK_ACCOUNT: ["is-unlock", "bi-person-check"],
            RESET_PASSWORD: ["is-password", "bi-key"],
            CREATE_ACCOUNT: ["is-create", "bi-person-plus"]
        }[log.actionCode] || ["is-update", "bi-sliders"];

        const item = document.createElement("article");
        item.className = `account-audit-item ${presentation[0]} is-new pp-box-anim`;

        const iconWrap = document.createElement("div");
        iconWrap.className = "account-audit-icon";
        const icon = document.createElement("i");
        icon.className = `bi ${presentation[1]}`;
        iconWrap.append(icon);

        const content = document.createElement("div");
        content.className = "account-audit-content";
        const meta = document.createElement("div");
        meta.className = "account-audit-meta";
        const action = document.createElement("strong");
        action.textContent = log.actionName;
        const time = document.createElement("time");
        time.dateTime = log.occurredAt;
        time.textContent = log.displayTime;
        meta.append(action, time);

        const description = document.createElement("p");
        description.textContent = log.description;
        const subject = document.createElement("div");
        subject.className = "account-audit-subject";
        appendTextWithIcon(subject, "bi-person-gear", log.actorName);
        const arrow = document.createElement("i");
        arrow.className = "bi bi-arrow-right";
        subject.append(arrow);
        appendTextWithIcon(subject, "bi-at", log.targetUsername);
        if (log.ipAddress) {
            const ip = document.createElement("small");
            ip.textContent = `IP ${log.ipAddress}`;
            subject.append(ip);
        }
        content.append(meta, description, subject);

        if (log.reason) {
            const reason = document.createElement("div");
            reason.className = "account-audit-reason";
            const reasonIcon = document.createElement("i");
            reasonIcon.className = "bi bi-chat-quote";
            const reasonText = document.createElement("span");
            const reasonLabel = document.createElement("b");
            reasonLabel.textContent = "Lý do: ";
            reasonText.append(reasonLabel, document.createTextNode(log.reason));
            reason.append(reasonIcon, reasonText);
            content.append(reason);
        }

        if (log.oldValue || log.newValue) {
            const details = document.createElement("details");
            details.className = "account-audit-change";
            const summary = document.createElement("summary");
            summary.textContent = "Xem dữ liệu thay đổi";
            const change = document.createElement("div");
            const createValue = (label, value) => {
                const wrap = document.createElement("span");
                const caption = document.createElement("small");
                caption.textContent = label;
                wrap.append(caption, document.createTextNode(value || "—"));
                return wrap;
            };
            const changeArrow = document.createElement("i");
            changeArrow.className = "bi bi-arrow-right";
            change.append(createValue("Trước", localizeAuditValue(log.oldValue)), changeArrow, createValue("Sau", localizeAuditValue(log.newValue)));
            details.append(summary, change);
            content.append(details);
        }

        item.append(iconWrap, content);
        list.prepend(item);
        window.setTimeout(() => item.classList.remove("is-new"), 600);
        while (list.children.length > 12) list.lastElementChild?.remove();

        const count = document.querySelector("[data-account-audit-count]");
        if (count) count.textContent = list.children.length;
    };

    const ensureEmptyAccountRow = () => {
        const body = document.querySelector(".account-table tbody");
        if (!body || body.querySelector("[data-account-row]") || body.querySelector("[data-account-empty-row]")) return;

        const row = document.createElement("tr");
        row.dataset.accountEmptyRow = "";
        const cell = document.createElement("td");
        cell.colSpan = 6;
        const empty = document.createElement("div");
        empty.className = "account-empty-state";
        const iconWrap = document.createElement("div");
        const icon = document.createElement("i");
        icon.className = "bi bi-person-x";
        iconWrap.append(icon);
        const title = document.createElement("strong");
        title.textContent = "Không có tài khoản phù hợp";
        const description = document.createElement("span");
        description.textContent = "Thử thay đổi bộ lọc hoặc tạo một tài khoản mới.";
        empty.append(iconWrap, title, description);
        cell.append(empty);
        row.append(cell);
        body.append(row);
    };

    const updateAccountRow = (form, data) => {
        const row = document.querySelector(`[data-account-row="${data.accountId}"]`);
        const state = row?.querySelector("[data-account-state]");
        const button = form.querySelector("button[type='submit']");
        const icon = button?.querySelector("i");

        row?.classList.toggle("is-locked", !data.isActive);
        if (state) {
            state.className = `state-chip ${data.isActive ? "state-chip--active" : "state-chip--locked"}`;
            state.replaceChildren();
            const dot = document.createElement("i");
            state.append(dot, document.createTextNode(data.isActive ? "Hoạt động" : "Đã khóa"));
            state.dataset.accountState = "";
        }
        form.dataset.accountMode = data.isActive ? "lock" : "unlock";
        if (button) button.title = data.isActive ? "Khóa tài khoản" : "Mở khóa tài khoản";
        if (icon) icon.className = `bi ${data.isActive ? "bi-pause-fill" : "bi-play-fill"}`;

        updateStatistics(data);
        prependAuditLog(data.auditLog);

        const statusFilter = document.querySelector("[data-account-workspace]")?.dataset.statusFilter;
        if (statusFilter && statusFilter !== String(data.isActive)) {
            row?.classList.add("is-removing");
            window.setTimeout(() => {
                row?.remove();
                const visibleRows = document.querySelectorAll("[data-account-row]").length;
                const resultCount = document.querySelector("[data-account-result-count]");
                if (resultCount) resultCount.textContent = visibleRows;
                ensureEmptyAccountRow();
            }, 260);
        }
    };

    const submitToggleForm = async (form) => {
        const button = form.querySelector("button[type='submit']");
        button?.classList.add("is-loading");
        if (button) button.disabled = true;
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            const contentType = response.headers.get("content-type") || "";
            const data = contentType.includes("application/json") ? await response.json() : null;
            if (!response.ok || !data?.success) throw new Error(data?.message || "Không thể cập nhật trạng thái tài khoản.");

            updateAccountRow(form, data);
            showToast(data.message, "success");
        } catch (error) {
            showToast(error instanceof Error ? error.message : "Không thể cập nhật trạng thái tài khoản.", "danger");
        } finally {
            button?.classList.remove("is-loading");
            if (button && document.body.contains(button)) button.disabled = false;
        }
    };

    const dialog = document.querySelector("[data-account-confirm]");
    const title = dialog?.querySelector("[data-account-confirm-title]");
    const message = dialog?.querySelector("[data-account-confirm-message]");
    const accountName = dialog?.querySelector("[data-account-confirm-name]");
    const icon = dialog?.querySelector(".account-confirm-icon i");
    const submitButton = dialog?.querySelector("[data-account-confirm-submit]");
    const submitLabel = submitButton?.querySelector("span");
    const reasonInput = dialog?.querySelector("[data-account-confirm-reason]");
    const reasonError = dialog?.querySelector("[data-account-confirm-reason-error]");
    let pendingForm = null;

    const closeDialog = () => {
        pendingForm = null;
        reasonInput?.classList.remove("is-invalid");
        reasonError?.classList.remove("is-visible");
        if (dialog?.open) dialog.close();
    };

    const bindToggleForms = () => {
        document.querySelectorAll(".account-toggle-form").forEach((form) => {
            if (form.dataset.bound === "true") return;
            form.dataset.bound = "true";
            form.addEventListener("submit", (event) => {
                event.preventDefault();
                pendingForm = form;
                const isLock = form.dataset.accountMode === "lock";
                if (title) title.textContent = isLock ? "Khóa tài khoản?" : "Mở khóa tài khoản?";
                if (message) message.textContent = isLock
                    ? "Người dùng sẽ không thể đăng nhập cho đến khi tài khoản được mở khóa."
                    : "Người dùng sẽ có thể đăng nhập và sử dụng quyền được cấp trở lại.";
                if (accountName) accountName.textContent = form.dataset.accountName || "Tài khoản";
                if (icon) icon.className = isLock ? "bi bi-person-lock" : "bi bi-person-check";
                if (submitLabel) submitLabel.textContent = isLock ? "Khóa tài khoản" : "Mở khóa";
                if (submitButton) submitButton.classList.toggle("is-unlock", !isLock);
                if (reasonInput) reasonInput.value = "";

                if (dialog && typeof dialog.showModal === "function") {
                    dialog.showModal();
                    window.setTimeout(() => reasonInput?.focus(), 80);
                } else {
                    const reason = window.prompt("Nhập lý do thực hiện:");
                    if (reason?.trim()) {
                        const reasonVal = form.querySelector("[data-account-reason-value]");
                        if (reasonVal) reasonVal.value = reason.trim();
                        void submitToggleForm(form);
                    }
                }
            });
        });
    };

    if (dialog) {
        dialog.querySelectorAll("[data-account-confirm-cancel]").forEach((button) => button.addEventListener("click", closeDialog));

        submitButton?.addEventListener("click", () => {
            const reason = reasonInput?.value.trim() || "";
            if (!reason) {
                reasonInput?.classList.add("is-invalid");
                reasonError?.classList.add("is-visible");
                reasonInput?.focus();
                return;
            }

            const form = pendingForm;
            const formReason = form?.querySelector("[data-account-reason-value]");
            if (formReason) formReason.value = reason;
            closeDialog();
            if (form) void submitToggleForm(form);
        });

        reasonInput?.addEventListener("input", () => {
            reasonInput.classList.remove("is-invalid");
            reasonError?.classList.remove("is-visible");
        });

        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) closeDialog();
        });
    }

    // =========================================================================
    // ✨ IN-PLACE FILTERING (ZERO FULL-PAGE RELOAD - RULES/CODING-STYLE.MD)
    // =========================================================================
    const filterForm = document.querySelector("[data-account-filter-form]");
    const tableContainer = document.getElementById("accountTableContainer");
    let searchDebounceTimer = null;

    const loadAccountsTable = async (overrideUrl = null, replaceHistory = true) => {
        if (!tableContainer) return;

        let requestUrl;
        if (overrideUrl) {
            requestUrl = new URL(overrideUrl, window.location.origin);
        } else if (filterForm) {
            requestUrl = new URL(filterForm.action || window.location.href, window.location.origin);
            const formData = new FormData(filterForm);
            for (const [key, val] of formData.entries()) {
                const strVal = String(val).trim();
                if (strVal) {
                    requestUrl.searchParams.set(key, strVal);
                } else {
                    requestUrl.searchParams.delete(key);
                }
            }
        } else {
            return;
        }

        const overlay = document.getElementById("accountLoadingOverlay");
        if (overlay) overlay.classList.remove("d-none");

        try {
            const response = await fetch(requestUrl.toString(), {
                headers: {
                    "Accept": "text/html",
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                throw new Error("Không thể tải danh sách tài khoản.");
            }

            const html = await response.text();
            tableContainer.innerHTML = html;

            // Kích hoạt hiệu ứng chuyển động mượt mà In-Place
            tableContainer.classList.remove("in-place-enter");
            void tableContainer.offsetWidth; // force reflow
            tableContainer.classList.add("in-place-enter");

            // Cập nhật số liệu từ Headers
            const activeH = response.headers.get("X-Active-Accounts");
            const lockedH = response.headers.get("X-Locked-Accounts");
            if (activeH !== null && lockedH !== null) {
                updateStatistics({
                    activeAccounts: Number(activeH),
                    lockedAccounts: Number(lockedH)
                });
            }

            // Đồng bộ URL mà không reload trang (Zero Full-Page Reload)
            if (replaceHistory) {
                window.history.replaceState({}, "", requestUrl.toString());
            }

            // Gán lại sự kiện cho các hàng mới nạp
            bindToggleForms();

            // Hiệu ứng PowerPoint Entrance & BlurText cho bảng mới
            window.HotelMotion?.applyEntrance?.(tableContainer);
            window.HotelMotion?.applyBlurText?.(tableContainer);

        } catch (error) {
            showToast(error instanceof Error ? error.message : "Lỗi khi lọc tài khoản.", "danger");
        } finally {
            if (overlay) overlay.classList.add("d-none");
        }
    };

    filterForm?.addEventListener("submit", (e) => {
        e.preventDefault();
        void loadAccountsTable();
    });

    const searchInput = filterForm?.querySelector("[data-account-search-input]");
    searchInput?.addEventListener("input", () => {
        window.clearTimeout(searchDebounceTimer);
        searchDebounceTimer = window.setTimeout(() => {
            void loadAccountsTable();
        }, 300);
    });

    filterForm?.querySelector("[data-account-role-select]")?.addEventListener("change", () => {
        const selectedVal = filterForm.querySelector("[data-account-role-select]").value;
        document.querySelectorAll("[data-role-filter]").forEach((item) => {
            item.classList.toggle("is-selected", item.dataset.roleFilter === selectedVal);
        });
        void loadAccountsTable();
    });

    filterForm?.querySelector("[data-account-status-select]")?.addEventListener("change", () => {
        void loadAccountsTable();
    });

    document.querySelector("[data-account-filter-reset]")?.addEventListener("click", (e) => {
        e.preventDefault();
        if (filterForm) {
            filterForm.querySelectorAll("input[type='text'], select").forEach((control) => {
                if (control.name !== "auditFrom" && control.name !== "auditTo") {
                    control.value = "";
                }
            });
            document.querySelectorAll("[data-role-filter]").forEach((item) => {
                item.classList.remove("is-selected");
            });
        }
        void loadAccountsTable();
    });

    document.querySelectorAll("[data-role-filter]").forEach((item) => {
        item.addEventListener("click", () => {
            const roleVal = item.dataset.roleFilter;
            const roleSelect = filterForm?.querySelector("[data-account-role-select]");
            if (!roleSelect) return;

            if (roleSelect.value === roleVal) {
                roleSelect.value = "";
                item.classList.remove("is-selected");
            } else {
                roleSelect.value = roleVal;
                document.querySelectorAll("[data-role-filter]").forEach((r) => r.classList.remove("is-selected"));
                item.classList.add("is-selected");
            }
            void loadAccountsTable();
        });
    });

    // Khởi tạo ban đầu
    bindToggleForms();
})();
