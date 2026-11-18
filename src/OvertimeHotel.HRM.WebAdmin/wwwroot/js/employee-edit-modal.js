(() => {
    "use strict";

    const modalEl = document.getElementById('editEmployeeModal');
    if (!modalEl || !window.bootstrap) return;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const host = modalEl.querySelector('#employeeEditFormPanel');
    const submitBtn = modalEl.querySelector('[data-employee-edit-submit]');
    const modalTitle = modalEl.querySelector('#editEmployeeModalLabel');

    let currentForm = null;
    let isSaving = false;
    let openedFromProfileModal = false;
    let currentEmployeeId = null;

    function showError(message) {
        let alert = host.querySelector('[data-modal-load-error]');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-danger border-0 rounded-4 mb-3';
            alert.setAttribute('role', 'alert');
            alert.dataset.modalLoadError = '';
            host.prepend(alert);
        }
        alert.textContent = message;
    }

    function showToast(message, type = "success") {
        let stack = document.querySelector(".catalog-toast-stack");
        if (!stack) {
            stack = document.createElement("div");
            stack.className = "catalog-toast-stack";
            stack.setAttribute("aria-live", "polite");
            document.body.append(stack);
        }
        const toast = document.createElement("div");
        toast.className = `catalog-toast catalog-toast--${type}`;
        toast.innerHTML = `<i class="bi bi-${type === "success" ? "check-circle-fill" : "exclamation-triangle-fill"}"></i><span>${message}</span>`;
        stack.append(toast);
        setTimeout(() => {
            toast.classList.add("is-fading");
            setTimeout(() => toast.remove(), 400);
        }, 3500);
    }

    function installForm(html, actionUrl) {
        currentForm = document.createElement('form');
        currentForm.method = 'post';
        currentForm.action = actionUrl;
        currentForm.id = 'employeeEditActiveForm';
        currentForm.innerHTML = html;
        host.replaceChildren(currentForm);

        // Re-execute scripts inside form so cascading selects and status hooks bind
        currentForm.querySelectorAll('script').forEach(old => {
            const script = document.createElement('script');
            script.textContent = old.textContent;
            old.replaceWith(script);
        });

        window.EntryValidation?.init();
        if (submitBtn) submitBtn.disabled = false;
    }

    async function loadEditForm(url, employeeName, employeeId) {
        currentEmployeeId = employeeId;
        if (modalTitle) {
            modalTitle.textContent = employeeName
                ? `Chỉnh sửa hồ sơ · ${employeeName}`
                : 'Chỉnh sửa hồ sơ nhân viên';
        }

        if (submitBtn) submitBtn.disabled = true;
        host.innerHTML = `
            <div class="text-center py-5 text-muted">
                <div class="spinner-border text-primary mb-2" role="status"><span class="visually-hidden">Đang tải...</span></div>
                <div class="small">Đang tải biểu mẫu chỉnh sửa...</div>
            </div>`;
        host.setAttribute('aria-busy', 'true');

        modal.show();

        try {
            const fetchUrl = new URL(url, window.location.origin);
            fetchUrl.searchParams.set('popup', 'true');

            const response = await fetch(fetchUrl, {
                headers: {
                    'X-Employee-Modal': '1',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }

            if (!response.ok) {
                throw new Error(`Không thể tải biểu mẫu (Mã lỗi ${response.status}). Vui lòng thử lại.`);
            }

            const html = await response.text();
            installForm(html, fetchUrl.toString());
        } catch (e) {
            host.replaceChildren();
            showError(e.message || 'Không thể tải biểu mẫu chỉnh sửa.');
        } finally {
            host.removeAttribute('aria-busy');
        }
    }

    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('[data-employee-edit-open]');
        if (!trigger) return;
        event.preventDefault();
        event.stopPropagation();

        const editUrl = trigger.dataset.employeeEditUrl;
        if (!editUrl) return;

        const employeeName = trigger.dataset.employeeName || '';
        const employeeIdMatch = editUrl.match(/\/(\d+)(\?|$)/);
        const employeeId = employeeIdMatch ? employeeIdMatch[1] : null;

        // If profile modal is currently open, close it and record that we came from profile
        const profileModalEl = document.getElementById('employeeProfileModal');
        if (profileModalEl && profileModalEl.classList.contains('show')) {
            openedFromProfileModal = true;
            const profileModal = bootstrap.Modal.getInstance(profileModalEl);
            if (profileModal) profileModal.hide();
        } else {
            openedFromProfileModal = false;
        }

        void loadEditForm(editUrl, employeeName, employeeId);
    }, true);

    modalEl.addEventListener('hide.bs.modal', (event) => {
        if (isSaving) {
            event.preventDefault();
        }
    });

    modalEl.addEventListener('hidden.bs.modal', () => {
        currentForm = null;
        host.innerHTML = '';
        if (submitBtn) submitBtn.disabled = true;
    });

    host.addEventListener('submit', async (event) => {
        if (event.target !== currentForm) return;
        if (event.defaultPrevented) return;
        event.preventDefault();

        if (isSaving) return;
        if (typeof currentForm.reportValidity === 'function' && !currentForm.reportValidity()) return;

        isSaving = true;
        if (submitBtn) submitBtn.disabled = true;
        currentForm.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(currentForm.action, {
                method: 'POST',
                body: new FormData(currentForm),
                headers: {
                    'X-Employee-Modal': '1',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }

            if (response.status === 422) {
                const html = await response.text();
                installForm(html, currentForm.action);
                window.EntryValidation?.focusError(currentForm);
                return;
            }

            const contentType = response.headers.get('content-type') || '';
            if (response.ok && contentType.includes('application/json')) {
                const result = await response.json();
                if (!result.success) {
                    throw new Error(result.message || 'Không thể lưu hồ sơ.');
                }

                modal.hide();
                showToast(result.message || 'Đã cập nhật hồ sơ nhân viên thành công.', 'success');

                // Refresh table seamlessly if loadEmployeeTable is available
                if (typeof window.loadEmployeeTable === 'function') {
                    window.loadEmployeeTable({}, false);
                } else {
                    window.location.reload();
                }

                // If opened from profile modal, reopen profile modal with updated data
                if (openedFromProfileModal && currentEmployeeId && typeof window.openEmployeeProfile === 'function') {
                    setTimeout(() => {
                        window.openEmployeeProfile(currentEmployeeId, 'Đã cập nhật thông tin hồ sơ.');
                    }, 400);
                }
            } else {
                throw new Error('Không thể lưu hồ sơ. Vui lòng kiểm tra lại dữ liệu.');
            }
        } catch (e) {
            showError(e.message || 'Đã có lỗi xảy ra khi lưu hồ sơ.');
        } finally {
            isSaving = false;
            if (submitBtn) submitBtn.disabled = false;
            currentForm?.removeAttribute('aria-busy');
        }
    });
})();
