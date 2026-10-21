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
        }
        function selectSection(root, section, focus = false) {
            root.dataset.activeSection = section;
            root.querySelectorAll('[data-details-pane]').forEach(pane => pane.hidden = pane.dataset.detailsPane !== section);
            root.querySelectorAll('[data-details-section]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.detailsSection === section)));
            if (focus) {
                const pane = root.querySelector('[data-details-pane="' + section + '"]');
                pane.tabIndex = -1;
                pane.focus();
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
            const fields = select.closest('[data-assignment-person]').querySelector('[data-custom-role-fields]');
            const custom = fields.querySelector('[data-custom-role-name]');
            const enabled = select.selectedOptions[0]?.hasAttribute('data-custom-role') || false;
            fields.hidden = !enabled;
            custom.disabled = !enabled;
            custom.required = enabled;
        }
        async function readHtml(response) {
            const error = ui.responseError(response);
            if (error) throw new Error(error);
            const html = await response.text();
            const template = document.createElement('template');
            template.innerHTML = html;
            if (!template.content.querySelector('[data-event-details], [data-assignment-popup-form]')) throw new Error('Không thể tải thông tin sự kiện. Vui lòng mở lại popup.');
            return template;
        }
        function replace(body, template, notify = false) {
            body.replaceChildren(template.content);
            bindContent(body, notify);
        }
        function focusMember() {
            if (memberTrigger?.isConnected) memberTrigger.focus();
            else originBody.querySelector('[data-details-section="staff"]')?.focus();
        }
        async function openDetails(link) {
            if (saving) return;
            detailsRequest?.abort();
            const request = detailsRequest = new AbortController();
            originBody = detailsBody;
            trigger = link;
            eventId = link.dataset.eventId;
            detailsBody.textContent = 'Đang tải thông tin sự kiện…';
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
            assignment.querySelector('[data-event-assignment-title]').textContent = 'Phân công nhân viên';
            assignmentBody.textContent = 'Đang tải phân công…';
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
            const picker = button.closest('[data-department-card]').querySelector('[data-member-picker]').cloneNode(true);
            picker.hidden = false;
            picker.removeAttribute('id');
            const search = picker.querySelector('[data-member-search]');
            search.id = 'event-modal-member-search';
            picker.querySelector('label').htmlFor = search.id;
            assignment.querySelector('[data-event-assignment-title]').textContent = 'Chọn nhân viên';
            assignmentBody.replaceChildren(picker);
            assignment.showModal();
            search.focus();
        }
        document.addEventListener('click', event => {
            const link = event.target.closest('[data-event-link]');
            if (link && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
                event.preventDefault();
                void openDetails(link);
                return;
            }
            const close = event.target.closest('[data-assignment-close]');
            if (close) { if (!saving) assignment.close(); return; }
            const next = event.target.closest('[data-continue-assignment]');
            if (next && !next.disabled) { openAssignment(next); return; }
            const root = event.target.closest('[data-event-details]');
            if (!root) return;
            const section = event.target.closest('[data-details-section]');
            if (section) selectSection(root, section.dataset.detailsSection, true);
            const add = event.target.closest('[data-add-member]');
            if (add) openPicker(add);
        }, true);
        document.addEventListener('input', event => {
            if (!event.target.matches('[data-member-search]')) return;
            const picker = event.target.closest('[data-member-picker]');
            const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').toLowerCase();
            const query = normalize(event.target.value.trim());
            const options = [...picker.querySelectorAll('[data-member-name]')];
            options.forEach(option => option.hidden = !normalize(option.dataset.memberName).includes(query));
            picker.querySelector('[data-member-empty]').hidden = options.some(option => !option.hidden);
        });
        document.addEventListener('change', event => {
            if (event.target.matches('[data-choose-member]')) {
                const picker = event.target.closest('[data-member-picker]');
                const count = picker.querySelectorAll('[data-choose-member]:checked').length;
                picker.querySelector('[data-selected-count]').textContent = '(' + count + ')';
                picker.querySelector('[data-continue-assignment]').disabled = count === 0;
            }
            if (event.target.matches('[data-assignment-role]')) {
                toggleCustomRole(event.target);
                const custom = event.target.closest('[data-assignment-person]').querySelector('[data-custom-role-name]');
                if (!custom.disabled) custom.focus();
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
                    if (isAssignment) {
                        saving = false;
                        assignment.close();
                    } else {
                        const status = originBody.querySelector('[role="alert"], [role="status"]');
                        if (status) { status.tabIndex = -1; status.focus(); }
                    }
                } else {
                    replace(body, template);
                    const error = body.querySelector('.validation-summary-errors');
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
