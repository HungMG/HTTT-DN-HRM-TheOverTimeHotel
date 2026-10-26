(function () {
    'use strict';
    document.addEventListener('DOMContentLoaded', () => {
        const dialog = document.querySelector('[data-event-details-dialog]');
        const assignment = document.querySelector('[data-event-assignment-dialog]');
        if (!dialog || !assignment) return;
        const detailsBody = dialog.querySelector('[data-event-details-body]');
        const assignmentBody = assignment.querySelector('[data-event-assignment-body]');
        const modal = element => bootstrap.Modal.getOrCreateInstance(element);
        const ui = window.HotelEventUI;
        let originBody = detailsBody;
        let trigger, memberTrigger, eventId, detailsRequest, assignmentRequest;
        let saving = false;
        let eventWasUpdated = false;

        function toast(msg, type = 'success') {
            if (ui?.showToast) ui.showToast(msg, type);
        }

        function confirmModal(options) {
            if (ui?.showConfirm) return ui.showConfirm(options);
            return Promise.resolve(window.confirm(options.message));
        }

        function message(body, text) {
            body.querySelector('[data-dialog-error]')?.remove();
            const alert = document.createElement('div');
            alert.className = 'alert alert-danger';
            alert.dataset.dialogError = '';
            alert.setAttribute('role', 'alert');
            alert.tabIndex = -1;
            alert.textContent = text;
            body.prepend(alert);
            alert.focus();
            toast(text, 'danger');
        }

        function selectSection(root, section, focus = false) {
            root.dataset.activeSection = section;
            root.querySelectorAll('[data-details-pane]').forEach(pane => pane.hidden = pane.dataset.detailsPane !== section);
            root.querySelectorAll('[data-details-section]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.detailsSection === section)));
            if (focus) {
                const pane = root.querySelector('[data-details-pane="' + section + '"]');
                if (pane) {
                    pane.tabIndex = -1;
                    pane.focus();
                }
            }
        }

        function bindContent(body, notify = false) {
            ui.bindTimeForms(body);
            body.querySelectorAll('[data-assignment-role]').forEach(toggleCustomRole);
            const root = body.querySelector('[data-event-details]');
            if (!root) return;
            selectSection(root, root.dataset.activeSection);
            if (notify) document.dispatchEvent(new CustomEvent('hotel-event-updated', { detail: {
                id: root.dataset.eventId, count: root.dataset.eventCount,
                status: root.dataset.eventStatus, statusLabel: root.dataset.eventStatusLabel
            } }));
        }

        function toggleCustomRole(select) {
            const personCard = select.closest('[data-assignment-person]');
            if (!personCard) return;
            const fields = personCard.querySelector('[data-custom-role-fields]');
            if (!fields) return;
            const custom = fields.querySelector('[data-custom-role-name]');
            const enabled = select.selectedOptions[0]?.hasAttribute('data-custom-role') || false;
            fields.hidden = !enabled;
            if (custom) {
                custom.disabled = !enabled;
                custom.required = enabled;
            }
        }

        async function readHtml(response) {
            const error = ui.responseError(response);
            if (error) throw new Error(error);
            const html = await response.text();
            const template = document.createElement('template');
            template.innerHTML = html;
            if (!template.content.querySelector('[data-event-details], [data-assignment-popup-form]')) {
                throw new Error('Không thể tải thông tin sự kiện. Vui lòng mở lại popup.');
            }
            return template;
        }

        function replace(body, template, notify = false) {
            const successEl = template.content.querySelector('[data-event-success-alert], .alert-success');
            if (successEl) {
                const text = successEl.textContent.trim();
                if (text) toast(text, 'success');
            }
            body.replaceChildren(template.content);
            bindContent(body, notify);
        }

        function focusMember() {
            if (memberTrigger?.isConnected) memberTrigger.focus();
            else originBody.querySelector('[data-details-section="staff"]')?.focus();
        }

        function updatePickerState(picker) {
            const count = picker.querySelectorAll('[data-choose-member]:checked').length;
            const counter = picker.querySelector('[data-selected-count]');
            if (counter) counter.textContent = '(' + count + ')';
            const continueBtn = picker.querySelector('[data-continue-assignment]');
            if (continueBtn) continueBtn.disabled = count === 0;
        }

        async function openDetails(link) {
            if (saving) return;
            detailsRequest?.abort();
            const request = detailsRequest = new AbortController();
            originBody = detailsBody;
            trigger = link;
            eventId = link.dataset.eventId;
            eventWasUpdated = false;

            const eventTitle = link.dataset.eventName || link.textContent.trim() || 'Sự kiện';
            const titleEl = dialog.querySelector('#eventDetailsTitle');
            if (titleEl) titleEl.textContent = `Phân công việc: ${eventTitle}`;

            detailsBody.innerHTML = `
                <div class="text-center py-5 my-3">
                    <div class="spinner-border text-primary mb-3" style="width: 2.8rem; height: 2.8rem; border-color: #1e3a8a; border-right-color: transparent;" role="status"></div>
                    <h5 class="fw-bold text-navy mb-1">The OverTime Hotel</h5>
                    <p class="text-muted small mb-0">Đang tải thông tin và phân công việc sự kiện...</p>
                </div>
            `;
            detailsBody.setAttribute('aria-busy', 'true');
            modal(dialog).show();
            const url = new URL(link.href, window.location.origin);
            url.searchParams.set('popup', 'true');
            try {
                const template = await readHtml(await fetch(url, { credentials: 'same-origin', signal: request.signal }));
                if (!request.signal.aborted) replace(detailsBody, template);
            } catch (error) {
                if (error.name !== 'AbortError') message(detailsBody, error.message);
            } finally {
                if (!request.signal.aborted) detailsBody.setAttribute('aria-busy', 'false');
            }
        }

        function openAssignment(button) {
            if (saving) return;
            const picker = button.closest('[data-member-picker]');
            const selected = [...picker.querySelectorAll('[data-choose-member]:checked')];
            if (!selected.length) return;
            const url = new URL(picker.dataset.assignmentUrl, window.location.origin);
            selected.forEach(employee => url.searchParams.append('employeeIds', employee.value));
            assignmentRequest?.abort();
            const request = assignmentRequest = new AbortController();
            const titleEl = assignment.querySelector('[data-event-assignment-title]');
            if (titleEl) titleEl.textContent = 'Phân công nhân viên (' + selected.length + ')';
            assignmentBody.innerHTML = `
                <div class="text-center py-5 my-3">
                    <div class="spinner-border text-primary mb-3" style="width: 2.5rem; height: 2.5rem; border-color: #1e3a8a; border-right-color: transparent;" role="status"></div>
                    <p class="text-muted fw-semibold mb-0">Đang khởi tạo biểu mẫu phân công 5★...</p>
                </div>
            `;
            assignmentBody.setAttribute('aria-busy', 'true');
            const show = async () => {
                try {
                    const template = await readHtml(await fetch(url, { credentials: 'same-origin', signal: request.signal }));
                    if (request.signal.aborted) return;
                    replace(assignmentBody, template);
                    assignmentBody.scrollTop = 0;
                    assignmentBody.querySelector('[data-assignment-role]')?.focus();
                } catch (error) {
                    if (error.name !== 'AbortError') message(assignmentBody, error.message);
                } finally {
                    if (!request.signal.aborted) assignmentBody.setAttribute('aria-busy', 'false');
                }
            };
            void show();
        }

        function openPicker(button) {
            if (saving) return;
            originBody = button.closest('[data-event-standalone]') || detailsBody;
            memberTrigger = button;
            const card = button.closest('[data-department-card]');
            const rawPicker = card ? card.querySelector('[data-member-picker]') : null;
            if (!rawPicker) return;
            const picker = rawPicker.cloneNode(true);
            picker.hidden = false;
            picker.removeAttribute('id');
            const deptName = picker.dataset.departmentName || 'Bộ phận';
            const search = picker.querySelector('[data-member-search]');
            if (search) {
                search.id = 'event-modal-member-search';
                const label = picker.querySelector('label');
                if (label) label.htmlFor = search.id;
            }
            const titleEl = assignment.querySelector('[data-event-assignment-title]');
            if (titleEl) titleEl.textContent = 'Chọn nhân sự · ' + deptName;
            assignmentBody.replaceChildren(picker);
            assignment.showModal();
            if (search) search.focus();
        }

        async function handleConfirmRemove(button) {
            if (saving) return;
            const empName = button.dataset.employeeName || 'nhân sự này';
            const confirmed = await confirmModal({
                title: 'Xác nhận gỡ nhân sự',
                message: `Bạn có chắc chắn muốn gỡ <strong>${empName}</strong> khỏi sự kiện này không? Thao tác này sẽ cập nhật trạng thái phân công và lưu lịch sử kiểm toán.`,
                confirmText: 'Xác nhận gỡ',
                cancelText: 'Hủy bỏ',
                danger: true
            });
            if (!confirmed) return;

            const form = button.closest('form');
            if (!form) return;
            saving = true;
            originBody = form.closest('[data-event-standalone]') || detailsBody;
            const data = new FormData(form);
            data.set('IsPopup', 'true');
            originBody.setAttribute('aria-busy', 'true');
            try {
                const template = await readHtml(await fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin' }));
                if (template.content.querySelector('[data-event-details]')) {
                    replace(originBody, template, true);
                    eventWasUpdated = true;
                    toast(`Đã gỡ ${empName} khỏi sự kiện.`, 'success');
                } else {
                    replace(originBody, template);
                }
            } catch (error) {
                message(originBody, error.message);
            } finally {
                saving = false;
                originBody.setAttribute('aria-busy', 'false');
            }
        }

        async function handleChangeRole(button) {
            if (saving) return;
            const empName = button.dataset.employeeName || 'nhân sự';
            const empId = button.dataset.employeeId;
            const dept = button.dataset.department;
            const version = button.dataset.version;
            const assignmentId = button.dataset.assignmentId;
            const card = button.closest('[data-event-details]');
            const curEventId = card?.dataset.eventId || eventId;

            const confirmed = await confirmModal({
                title: 'Đổi vai trò nhân sự',
                message: `Để đổi vai trò cho <strong>${empName}</strong>, hệ thống sẽ gỡ phân công hiện tại để bạn thiết lập vai trò mới. Tiếp tục?`,
                confirmText: 'Tiếp tục đổi vai trò',
                cancelText: 'Hủy bỏ',
                danger: false
            });
            if (!confirmed) return;

            saving = true;
            originBody = button.closest('[data-event-standalone]') || detailsBody;
            originBody.setAttribute('aria-busy', 'true');

            try {
                // Step 1: Remove old assignment
                const removeForm = button.closest('[data-assigned-member]')?.querySelector('form[data-remove-assignment-form]');
                const formData = removeForm ? new FormData(removeForm) : new FormData();
                if (!removeForm) {
                    formData.set('version', version);
                    formData.set('assignmentId', assignmentId);
                }
                formData.set('IsPopup', 'true');
                const removeUrl = removeForm ? removeForm.action : `/SuKien/RemoveAssignment/${curEventId}`;
                await fetch(removeUrl, { method: 'POST', body: formData, credentials: 'same-origin' });

                // Step 2: Open Assignment popup for this employee
                const assignUrl = new URL(`/SuKien/Assignment`, window.location.origin);
                assignUrl.searchParams.set('id', curEventId);
                assignUrl.searchParams.set('department', dept);
                assignUrl.searchParams.set('employeeIds', empId);

                const titleEl = assignment.querySelector('[data-event-assignment-title]');
                if (titleEl) titleEl.textContent = `Đổi vai trò: ${empName}`;
                assignmentBody.innerHTML = `
                    <div class="text-center py-5 my-3">
                        <div class="spinner-border text-primary mb-3" style="width: 2.5rem; height: 2.5rem; border-color: #1e3a8a; border-right-color: transparent;" role="status"></div>
                        <p class="text-muted fw-semibold mb-0">Đang tải vai trò cho ${empName}...</p>
                    </div>
                `;
                assignmentBody.setAttribute('aria-busy', 'true');
                assignment.showModal();

                const template = await readHtml(await fetch(assignUrl, { credentials: 'same-origin' }));
                replace(assignmentBody, template);
                assignmentBody.scrollTop = 0;
                assignmentBody.querySelector('[data-assignment-role]')?.focus();
            } catch (error) {
                message(originBody, error.message);
                toast(error.message, 'danger');
            } finally {
                saving = false;
                originBody.setAttribute('aria-busy', 'false');
                assignmentBody.setAttribute('aria-busy', 'false');
            }
        }

        document.addEventListener('click', event => {
            const link = event.target.closest('[data-event-link]');
            if (link && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
                event.preventDefault();
                void openDetails(link);
                return;
            }
            const close = event.target.closest('[data-assignment-close]');
            if (close) {
                if (!saving) assignment.close();
                return;
            }
            const next = event.target.closest('[data-continue-assignment]');
            if (next && !next.disabled) {
                openAssignment(next);
                return;
            }
            const selectAll = event.target.closest('[data-picker-select-all]');
            if (selectAll) {
                const picker = selectAll.closest('[data-member-picker]');
                if (picker) {
                    picker.querySelectorAll('.event-member-option:not([hidden]) [data-choose-member]').forEach(cb => cb.checked = true);
                    updatePickerState(picker);
                }
                return;
            }
            const deselectAll = event.target.closest('[data-picker-deselect-all]');
            if (deselectAll) {
                const picker = deselectAll.closest('[data-member-picker]');
                if (picker) {
                    picker.querySelectorAll('[data-choose-member]').forEach(cb => cb.checked = false);
                    updatePickerState(picker);
                }
                return;
            }
            const confirmRemove = event.target.closest('[data-confirm-remove]');
            if (confirmRemove) {
                event.preventDefault();
                void handleConfirmRemove(confirmRemove);
                return;
            }
            const changeRole = event.target.closest('[data-change-role]');
            if (changeRole) {
                event.preventDefault();
                void handleChangeRole(changeRole);
                return;
            }
            const root = event.target.closest('[data-event-details]');
            if (!root) return;
            const section = event.target.closest('[data-details-section]');
            if (section) selectSection(root, section.dataset.detailsSection, true);
            const add = event.target.closest('[data-add-member]');
            if (add) openPicker(add);
            const topAssign = event.target.closest('[data-top-quick-assign]');
            if (topAssign) {
                const firstAdd = root.querySelector('[data-department-card] [data-add-member]');
                if (firstAdd) openPicker(firstAdd);
            }
        }, true);

        document.addEventListener('input', event => {
            if (!event.target.matches('[data-member-search]')) return;
            const picker = event.target.closest('[data-member-picker]');
            if (!picker) return;
            const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').toLowerCase();
            const query = normalize(event.target.value.trim());
            const options = [...picker.querySelectorAll('.event-member-option')];
            options.forEach(option => {
                const name = normalize(option.dataset.memberName || '');
                const id = option.dataset.memberId || '';
                const match = name.includes(query) || id.includes(query);
                option.hidden = !match;
            });
            const emptyEl = picker.querySelector('[data-member-empty]');
            if (emptyEl) emptyEl.hidden = options.some(option => !option.hidden);
        });

        document.addEventListener('change', event => {
            if (event.target.matches('[data-choose-member]')) {
                const picker = event.target.closest('[data-member-picker]');
                if (picker) updatePickerState(picker);
            }
            if (event.target.matches('[data-assignment-role]')) {
                toggleCustomRole(event.target);
                const custom = event.target.closest('[data-assignment-person]')?.querySelector('[data-custom-role-name]');
                if (custom && !custom.disabled) custom.focus();
            }
        });

        document.addEventListener('submit', async event => {
            const form = event.target;
            const isAssignment = form.matches('[data-assignment-popup-form]');
            if (!isAssignment && !form.closest('[data-event-details]')) return;
            event.preventDefault();
            if (saving) return;
            form.querySelectorAll('details').forEach(detail => detail.open = true);
            if (!form.reportValidity()) return;
            saving = true;
            originBody = isAssignment ? originBody : form.closest('[data-event-standalone]') || detailsBody;
            const body = isAssignment ? assignmentBody : originBody;
            const buttons = [...body.querySelectorAll('button')].map(button => [button, button.disabled]);
            const data = new FormData(form);
            data.set('IsPopup', 'true');
            buttons.forEach(([button]) => button.disabled = true);
            body.setAttribute('aria-busy', 'true');
            try {
                const template = await readHtml(await fetch(form.action, { method: 'POST', body: data, credentials: 'same-origin' }));
                if (template.content.querySelector('[data-event-details]')) {
                    replace(originBody, template, true);
                    eventWasUpdated = true;
                    if (isAssignment) {
                        saving = false;
                        assignment.close();
                        toast('Đã lưu phân công nhân sự thành công!', 'success');
                    } else {
                        const status = originBody.querySelector('[role="alert"], [role="status"]');
                        if (status) { status.tabIndex = -1; status.focus(); }
                    }
                } else {
                    replace(body, template);
                    const error = body.querySelector('.validation-summary-errors, .event-validation');
                    if (error) { error.tabIndex = -1; error.focus(); }
                }
            } catch (error) {
                message(body, error.message);
            } finally {
                saving = false;
                body.setAttribute('aria-busy', 'false');
                buttons.forEach(([button, disabled]) => { if (button.isConnected) button.disabled = disabled; });
            }
        });

        dialog.addEventListener('hide.bs.modal', event => {
            if (saving) event.preventDefault();
            else detailsRequest?.abort();
        });
        dialog.addEventListener('hidden.bs.modal', () => {
            if (eventWasUpdated && window.location.pathname.toLowerCase().includes('/phanca')) {
                localStorage.setItem('overtime_phanca_active_tab', 'eventDemand');
                if (typeof window.refreshPhanCaWorkspaceInPlace === 'function') {
                    window.refreshPhanCaWorkspaceInPlace('eventDemand');
                } else if (typeof window.loadWeekInPlace === 'function') {
                    const currentWeek = new URL(window.location.href).searchParams.get('week') || '';
                    window.loadWeekInPlace(currentWeek);
                    if (typeof window.switchCalendarViewTab === 'function') {
                        window.switchCalendarViewTab('eventDemand');
                    }
                } else {
                    window.location.reload();
                }
                return;
            }
            if (trigger?.isConnected) trigger.focus();
            else document.querySelector('[data-event-link][data-event-id="' + eventId + '"]')?.focus();
        });
        assignment.addEventListener('cancel', event => {
            if (saving) event.preventDefault();
        });
        assignment.addEventListener('close', () => {
            assignmentRequest?.abort();
            focusMember();
        });
        const standalone = document.querySelector('[data-event-standalone]');
        if (standalone) bindContent(standalone);
    });
})();
