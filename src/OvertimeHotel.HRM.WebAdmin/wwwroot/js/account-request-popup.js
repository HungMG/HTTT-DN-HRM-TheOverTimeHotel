window.initAccountRequestPopup = (container, onSuccess, onError) => {
                const empSelect = container.querySelector("#popupEmployeeId");
                const roleSelect = container.querySelector("#popupRoleId");
                const contractSelect = container.querySelector("[data-contract-select]");
                const usernameInput = container.querySelector("[data-username-input]");
                const submitBtn = container.querySelector("#popupBtnSubmit");
                const roleNotice = container.querySelector("#popupRoleNotice");
                const posText = container.querySelector("#popupPositionText");
                const roleBadge = container.querySelector("#popupRoleBadge");
                const noContractAlert = container.querySelector("#popupNoContractAlert");
                const contractHelpText = container.querySelector("#popupContractHelpText");
                const form = container.querySelector("[data-create-request-form]");
                const roleRestriction = container.querySelector("[data-role-restriction]");
                const roleFilter = container.querySelector("[data-role-filter]");
                const roleFilterEmpty = container.querySelector("[data-role-filter-empty]");

                let roleMap = {};
                try {
                    const raw = container.querySelector("#popup-employee-role-data")?.textContent;
                    if (raw) roleMap = JSON.parse(raw);
                } catch (e) {
                    console.warn("Failed to parse popup role data", e);
                }

                const stripDiacritics = (value) =>
                    value
                        .normalize("NFD")
                        .replace(/[\u0300-\u036f]/g, "")
                        .replace(/đ/g, "d")
                        .replace(/Đ/g, "D");

                const suggestUsername = () => {
                    if (!empSelect || !usernameInput || usernameInput.value.trim()) return;
                    const option = empSelect.selectedOptions[0];
                    if (!option || !option.value) return;

                    const fullName = option.text.split("·")[0].trim();
                    const parts = fullName.split(/\s+/).filter(Boolean);
                    if (parts.length === 0) return;
                    const last = parts[parts.length - 1];
                    const first = parts[0];
                    const initial = stripDiacritics(first).charAt(0).toLowerCase();
                    usernameInput.value = `${initial}.${stripDiacritics(last).toLowerCase().replace(/\s+/g, "")}`;
                };

                const updateRole = () => {
                    const empId = Number(empSelect?.value || 0);
                    const selectedContract = contractSelect?.selectedOptions[0];
                    let roleId = 0;
                    let posName = "";
                    let roleDisplay = "";
                    let roleName = "";

                    if (selectedContract && selectedContract.value && selectedContract.dataset.roleId) {
                        roleId = Number(selectedContract.dataset.roleId);
                        const pos = selectedContract.dataset.position || "";
                        const dept = selectedContract.dataset.department || "";
                        posName = pos && dept ? `${pos} · ${dept}` : (pos || dept);
                        roleDisplay = selectedContract.dataset.roleDisplay || selectedContract.dataset.roleName || "";
                        roleName = selectedContract.dataset.roleName || "";
                    } else if (empId && roleMap[empId]) {
                        const info = roleMap[empId];
                        roleId = info.roleId || info.RoleId;
                        const pos = info.positionName || info.PositionName || "";
                        const dept = info.departmentName || info.DepartmentName || "";
                        posName = pos && dept ? `${pos} · ${dept}` : (pos || dept);
                        roleDisplay = info.roleDisplayName || info.RoleDisplayName || "";
                        roleName = info.roleName || info.RoleName || "";
                    }

                    const restricted = form?.dataset?.employeeOnly === "true" && roleName && roleName !== "Employee" &&
                        !(roleName === "Manager" && form?.dataset?.managerAllowed === "true");
                    if (roleRestriction) roleRestriction.hidden = !restricted;
                    if (submitBtn && restricted) submitBtn.disabled = true;

                    const roleDisplayName = container.querySelector("#popupRoleDisplayName");
                    if (roleSelect) {
                        roleSelect.value = roleId ? String(roleId) : "";
                    }
                    if (roleDisplayName) {
                        roleDisplayName.textContent = roleDisplay || (roleId ? "Đã xác định theo chức vụ và phòng ban" : "Tự động theo chức vụ và phòng ban");
                    }
                    if (roleBadge) {
                        if (roleDisplay) {
                            roleBadge.textContent = roleDisplay;
                            roleBadge.style.display = "inline-block";
                        } else {
                            roleBadge.style.display = "none";
                        }
                    }

                    if (roleNotice) {
                        if (roleId && posName.trim()) {
                            roleNotice.style.display = "block";
                            if (posText) posText.textContent = posName;
                            if (roleBadge) roleBadge.textContent = roleDisplay;
                        } else {
                            roleNotice.style.display = "none";
                        }
                    }
                };

                const filterContracts = () => {
                    if (!contractSelect) return;
                    const empId = empSelect?.value || "";
                    let visibleCount = 0;
                    let firstValid = null;

                    Array.from(contractSelect.options).forEach((opt) => {
                        if (!opt.value) return;
                        const matches = !!empId && opt.dataset.employeeId === empId;
                        opt.hidden = !matches;
                        opt.disabled = !matches;
                        if (matches) {
                            visibleCount++;
                            if (!firstValid) firstValid = opt;
                        }
                    });

                    if (!empId) {
                        contractSelect.disabled = true;
                        contractSelect.selectedIndex = 0;
                        if (noContractAlert) {
                            noContractAlert.classList.add("d-none");
                            noContractAlert.classList.remove("d-flex");
                        }
                        if (contractHelpText) contractHelpText.classList.remove("d-none");
                        if (submitBtn) submitBtn.disabled = true;
                        updateRole();
                        return;
                    }

                    if (visibleCount === 0) {
                        contractSelect.disabled = true;
                        contractSelect.selectedIndex = 0;
                        if (noContractAlert) {
                            noContractAlert.classList.remove("d-none");
                            noContractAlert.classList.add("d-flex");
                        }
                        if (contractHelpText) contractHelpText.classList.add("d-none");
                        if (submitBtn) submitBtn.disabled = true;
                    } else {
                        contractSelect.disabled = false;
                        if (noContractAlert) {
                            noContractAlert.classList.add("d-none");
                            noContractAlert.classList.remove("d-flex");
                        }
                        if (contractHelpText) contractHelpText.classList.remove("d-none");

                        if (visibleCount === 1) {
                            if (firstValid) contractSelect.value = firstValid.value;
                            if (submitBtn) submitBtn.disabled = false;
                        } else {
                            const cur = contractSelect.selectedOptions[0];
                            if (!cur || cur.dataset.employeeId !== empId) contractSelect.selectedIndex = 0;
                            if (submitBtn) submitBtn.disabled = !contractSelect.value;
                        }
                    }
                    updateRole();
                };

                const filterEmployees = () => {
                    if (!roleFilter || !empSelect) return;
                    let visibleCount = 0;
                    for (const option of empSelect.options) {
                        if (!option.value) continue;
                        const info = roleMap[option.value];
                        const name = info?.roleName || info?.RoleName || "";
                        const matches = !roleFilter.value || name === roleFilter.value;
                        option.hidden = !matches;
                        option.disabled = !matches;
                        if (matches) visibleCount++;
                    }
                    if (roleFilterEmpty) roleFilterEmpty.hidden = visibleCount > 0;
                    const selected = Array.from(empSelect.options).find(option => option.value === empSelect.value);
                    if (selected?.disabled) {
                        empSelect.value = "";
                        if (usernameInput) usernameInput.value = "";
                    }
                    filterContracts();
                };

                roleFilter?.addEventListener("change", filterEmployees);

                empSelect?.addEventListener("change", () => {
                    if (usernameInput) usernameInput.value = "";
                    suggestUsername();
                    filterContracts();
                });

                contractSelect?.addEventListener("change", () => {
                    if (submitBtn && empSelect?.value) {
                        submitBtn.disabled = !contractSelect.value;
                    }
                    updateRole();
                });

                form?.addEventListener("submit", async (e) => {
                    e.preventDefault();
                    if (submitBtn) submitBtn.disabled = true;
                    try {
                        const response = await fetch(form.action, {
                            method: "POST",
                            headers: { "X-Requested-With": "XMLHttpRequest" },
                            body: new FormData(form)
                        });
                        const contentType = response.headers.get("content-type") || "";
                        if (contentType.includes("application/json")) {
                            const result = await response.json();
                            if (result.success) {
                                await onSuccess(result.message);
                                return;
                            }
                            throw new Error(result.message || "Không thể gửi yêu cầu.");
                        }
                        if (!response.ok) throw new Error("Không thể gửi yêu cầu.");
                        const html = await response.text();
                        container.innerHTML = html;
                        window.initAccountRequestPopup(container, onSuccess, onError);
                    } catch (err) {
                        onError(err instanceof Error ? err.message : "Không thể gửi yêu cầu.");
                    } finally {
                        if (submitBtn) submitBtn.disabled = false;
                    }
                });

                suggestUsername();
                if (roleFilter) filterEmployees();
                else filterContracts();
            };
