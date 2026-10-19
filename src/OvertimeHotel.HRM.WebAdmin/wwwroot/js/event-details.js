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
        let switching = false;

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
            const roleSelect = body.querySelector('[data-assignment-role]');
            if (roleSelect) toggleCustomRole(roleSelect);
            const root = body.querySelector('[data-event-details]');
            if (!root) return;
            selectSection(root, root.dataset.activeSection);
            if (notify) document.dispatchEvent(new CustomEvent('hotel-event-updated', { detail: {
                id: root.dataset.eventId, count: root.dataset.eventCount,
                status: root.dataset.eventStatus, statusLabel: root.dataset.eventStatusLabel
            } }));
        }
        function toggleCustomRole(select) {
            const fields = select.form.querySelector('[data-custom-role-fields]');
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
        function returnToDetails() {
            if (originBody === detailsBody) {
                dialog.addEventListener('shown.bs.modal', focusMember, { once: true });
                modal(dialog).show();
            } else focusMember();
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
            originBody = button.closest('[data-event-standalone]') || detailsBody;
            memberTrigger = button;
            assignmentRequest?.abort();
            const request = assignmentRequest = new AbortController();
            assignmentBody.textContent = 'Đang tải phân công…';
            assignmentBody.setAttribute('aria-busy', 'true');
            const show = async () => {
                modal(assignment).show();
                try {
                    const template = await readHtml(await fetch(button.dataset.assignmentUrl, { credentials: 'same-origin', signal: request.signal }));
                    if (request.signal.aborted) return;
                    replace(assignmentBody, template);
                    assignmentBody.querySelector('[autofocus]')?.focus();
                } catch (error) {
                    if (error.name !== 'AbortError') message(assignmentBody, error.message);
                } finally {
                    if (!request.signal.aborted) assignmentBody.setAttribute('aria-busy', 'false');
                }
            };
            if (dialog.classList.contains('show')) {
                switching = true;
                dialog.addEventListener('hidden.bs.modal', show, { once: true });
                modal(dialog).hide();
            } else void show();
        }
        document.addEventListener('click', event => {
            const link = event.target.closest('[data-event-link]');
            if (link && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
                event.preventDefault();
                void openDetails(link);
                return;
            }
            const root = event.target.closest('[data-event-details]');
            if (!root) return;
            const section = event.target.closest('[data-details-section]');
            if (section) selectSection(root, section.dataset.detailsSection, true);
            const add = event.target.closest('[data-add-member]');
            if (add) {
                const picker = add.closest('[data-department-card]').querySelector('[data-member-picker]');
                picker.hidden = !picker.hidden;
                add.setAttribute('aria-expanded', String(!picker.hidden));
                if (!picker.hidden) picker.querySelector('input').focus();
            }
            const member = event.target.closest('[data-choose-member]');
            if (member && !member.disabled) openAssignment(member);
        }, true);
        document.addEventListener('input', event => {
            if (!event.target.matches('[data-member-search]')) return;
            const picker = event.target.closest('[data-member-picker]');
            const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').toLowerCase();
            const query = normalize(event.target.value.trim());
            const options = [...picker.querySelectorAll('[data-choose-member]')];
            options.forEach(option => option.hidden = !normalize(option.dataset.memberName).includes(query));
            picker.querySelector('[data-member-empty]').hidden = options.some(option => !option.hidden);
        });
        document.addEventListener('change', event => {
            if (event.target.matches('[data-assignment-role]')) {
                toggleCustomRole(event.target);
                const custom = event.target.form.querySelector('[data-custom-role-name]');
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
                        modal(assignment).hide();
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
            else if (!switching) detailsRequest?.abort();
        });
        dialog.addEventListener('hidden.bs.modal', () => {
            if (switching) { switching = false; return; }
            if (trigger?.isConnected) trigger.focus();
            else document.querySelector('[data-event-link][data-event-id="' + eventId + '"]')?.focus();
        });
        assignment.addEventListener('hide.bs.modal', event => {
            if (saving) event.preventDefault();
            else assignmentRequest?.abort();
        });
        assignment.addEventListener('hidden.bs.modal', returnToDetails);
        const standalone = document.querySelector('[data-event-standalone]');
        if (standalone) bindContent(standalone);
    });
})();
