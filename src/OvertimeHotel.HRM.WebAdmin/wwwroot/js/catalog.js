(() => {
  "use strict";

  const page = document.querySelector(".catalog-page");
  if (!page) return;

  const reduceMotion = window.matchMedia(
    "(prefers-reduced-motion: reduce)"
  ).matches;
  const filterForm = page.querySelector(".catalog-toolbar form[method='get']");
  const tableContainer =
    page.querySelector("[data-catalog-results]") ||
    page.querySelector(".table-responsive");
  const loadingOverlay = page.querySelector("[data-catalog-loading]");
  let searchTimer;

  const animateCountUp = (element, startValue, targetValue, duration = 420) => {
    if (!element || reduceMotion || startValue === targetValue) {
      if (element) element.textContent = targetValue;
      return;
    }

    const startedAt = performance.now();
    const update = (now) => {
      const progress = Math.min((now - startedAt) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      element.textContent = Math.round(
        startValue + (targetValue - startValue) * eased
      );
      if (progress < 1) requestAnimationFrame(update);
    };
    requestAnimationFrame(update);
  };

  const pulseCount = (badge) => {
    if (!badge || reduceMotion) return;
    badge.classList.remove("catalog-count-pulse");
    void badge.offsetWidth;
    badge.classList.add("catalog-count-pulse");
    window.setTimeout(() => badge.classList.remove("catalog-count-pulse"), 420);
  };

  const updateCountBadge = (currentBadge, nextBadge) => {
    if (!currentBadge || !nextBadge) return;
    const currentNumber = Number(
      currentBadge.textContent.match(/\d+/)?.[0] || 0
    );
    const nextNumber = Number(nextBadge.textContent.match(/\d+/)?.[0] || 0);
    currentBadge.innerHTML = nextBadge.innerHTML;
    const textNode = Array.from(currentBadge.childNodes).find(
      (node) => node.nodeType === Node.TEXT_NODE && /\d+/.test(node.textContent)
    );
    if (textNode) {
      const suffix = textNode.textContent.replace(/\d+/, "");
      const counter = document.createElement("span");
      counter.dataset.catalogCountValue = "";
      textNode.replaceWith(counter, document.createTextNode(suffix));
      animateCountUp(counter, currentNumber, nextNumber);
    }
    pulseCount(currentBadge);
  };

  const setLoading = (isLoading) => {
    loadingOverlay?.classList.toggle("is-visible", isLoading);
    tableContainer?.classList.toggle("is-updating", isLoading);
    filterForm?.querySelectorAll("input, select, button").forEach((control) => {
      control.disabled = isLoading;
    });
  };

  const showToast = (message, type = "success") => {
    let stack = document.querySelector(".catalog-toast-stack");
    if (!stack) {
      stack = document.createElement("div");
      stack.className = "catalog-toast-stack";
      stack.setAttribute("aria-live", "polite");
      document.body.append(stack);
    }

    const toast = document.createElement("div");
    toast.className = `catalog-toast catalog-toast--${type}`;
    const icon = document.createElement("i");
    icon.className =
      type === "success" ? "bi bi-check2-circle" : "bi bi-exclamation-triangle";
    const text = document.createElement("strong");
    text.textContent = message;
    const close = document.createElement("button");
    close.type = "button";
    close.setAttribute("aria-label", "Đóng thông báo");
    close.innerHTML = '<i class="bi bi-x-lg"></i>';
    toast.append(icon, text, close);
    stack.prepend(toast);

    const dismiss = () => {
      toast.classList.add("is-leaving");
      window.setTimeout(() => toast.remove(), reduceMotion ? 0 : 220);
    };
    close.addEventListener("click", dismiss);
    requestAnimationFrame(() => toast.classList.add("is-visible"));
    window.setTimeout(dismiss, 4600);
  };

  const buildFilterUrl = () => {
    const url = new URL(
      filterForm?.action || window.location.href,
      window.location.origin
    );
    if (!filterForm) return url;
    new FormData(filterForm).forEach((value, key) => {
      const normalized = String(value).trim();
      if (normalized) url.searchParams.set(key, normalized);
      else url.searchParams.delete(key);
    });
    return url;
  };

  const refreshResults = async (url = buildFilterUrl()) => {
    if (!tableContainer) {
      window.location.href = url.toString();
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(url, {
        headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" },
      });
      if (!response.ok) throw new Error("Không thể cập nhật danh mục.");

      const documentResult = new DOMParser().parseFromString(
        await response.text(),
        "text/html"
      );
      const nextContainer =
        documentResult.querySelector("[data-catalog-results]") ||
        documentResult.querySelector(".catalog-page .table-responsive");
      const nextBadge = documentResult.querySelector("[data-catalog-count]");

      if (!nextContainer) throw new Error("Dữ liệu trả về không hợp lệ.");
      tableContainer.innerHTML = nextContainer.innerHTML;

      const currentBadge = page.querySelector("[data-catalog-count]");
      updateCountBadge(currentBadge, nextBadge);

      window.history.replaceState({}, "", url);
      tableContainer.classList.remove("catalog-results-enter");
      void tableContainer.offsetWidth;
      tableContainer.classList.add("catalog-results-enter");
      window.HotelMotion?.applyEntrance?.(tableContainer);
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "Không thể cập nhật danh mục.",
        "danger"
      );
    } finally {
      setLoading(false);
    }
  };

  filterForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    void refreshResults();
  });

  filterForm?.querySelectorAll("select").forEach((select) => {
    select.addEventListener("change", () => void refreshResults());
  });

  const searchInput = filterForm?.querySelector("input[name='keyword']");
  searchInput?.addEventListener("input", () => {
    window.clearTimeout(searchTimer);
    searchTimer = window.setTimeout(() => void refreshResults(), 320);
  });

  page.querySelectorAll("[data-catalog-reset]").forEach((reset) => {
    reset.addEventListener("click", (event) => {
      if (!filterForm) return;
      event.preventDefault();
      filterForm.reset();
      filterForm
        .querySelectorAll("input:not([type='hidden']), select")
        .forEach((control) => {
          control.value = "";
        });
      void refreshResults(new URL(reset.href, window.location.origin));
    });
  });

  page.querySelectorAll("[data-catalog-toast]").forEach((alert) => {
    const message = alert
      .querySelector("[data-catalog-toast-message]")
      ?.textContent?.trim();
    if (message) showToast(message, alert.dataset.catalogToast || "success");
    alert.remove();
  });

  // ==========================================
  // SMART FORM INTERACTIONS (SHIFTS & PAYROLL)
  // ==========================================
  const initFormInteractions = (container) => {
    if (!container) return;

    // 1. Ca làm việc: Auto Night Shift + Duration + Live Preview
    const shiftStart = container.querySelector("[data-shift-start-input]");
    const shiftEnd = container.querySelector("[data-shift-end-input]");
    const shiftNight = container.querySelector("[data-shift-night-switch]");
    const shiftName = container.querySelector("[data-shift-name-input]");
    const shiftHint = container.querySelector("#shiftAutoNightHint");
    const previewBox = container.querySelector("[data-shift-preview]");

    const updateShiftPreview = () => {
      if (!previewBox) return;
      const startVal = shiftStart?.value || "06:00";
      const endVal = shiftEnd?.value || "14:00";
      const nameVal = shiftName?.value?.trim() || "Ca làm việc mới";

      const [startH, startM] = startVal.split(":").map(Number);
      const [endH, endM] = endVal.split(":").map(Number);

      const startMin = (startH || 0) * 60 + (startM || 0);
      const endMin = (endH || 0) * 60 + (endM || 0);

      let diffMin = endMin - startMin;
      let isOvernight = false;

      if (diffMin < 0) {
        diffMin += 24 * 60;
        isOvernight = true;
      }

      if (isOvernight && shiftNight && !shiftNight.checked) {
        shiftNight.checked = true;
        if (shiftHint) shiftHint.classList.remove("d-none");
      } else if (!isOvernight && shiftHint) {
        shiftHint.classList.add("d-none");
      }

      const isNightActive = shiftNight?.checked || isOvernight;
      const hours = (diffMin / 60).toFixed(1);

      const durEl = previewBox.querySelector("[data-preview-duration]");
      if (durEl) durEl.innerHTML = `<i class="bi bi-hourglass-split me-1"></i>${hours} giờ`;

      const titleEl = previewBox.querySelector("[data-preview-title]");
      if (titleEl) titleEl.textContent = nameVal;

      const timeEl = previewBox.querySelector("[data-preview-time]");
      if (timeEl) timeEl.textContent = `${startVal} — ${endVal}`;

      const badgeEl = previewBox.querySelector("[data-preview-badge]");
      const iconEl = previewBox.querySelector("[data-preview-icon]");

      if (isNightActive) {
        if (badgeEl) badgeEl.innerHTML = `<span class="badge bg-dark text-warning border border-warning"><i class="bi bi-moon-stars-fill me-1"></i>Ca đêm · +30%</span>`;
        if (iconEl) {
          iconEl.className = "catalog-icon catalog-icon--night";
          iconEl.innerHTML = `<i class="bi bi-moon-stars-fill"></i>`;
        }
      } else {
        const isAfternoon = nameVal.toLowerCase().includes("chiều") || (startH >= 12 && startH < 18);
        if (badgeEl) badgeEl.innerHTML = `<span class="badge bg-light text-dark border"><i class="bi bi-sun-fill me-1 text-warning"></i>Trong ngày</span>`;
        if (iconEl) {
          iconEl.className = isAfternoon ? "catalog-icon catalog-icon--afternoon" : "catalog-icon catalog-icon--morning";
          iconEl.innerHTML = `<i class="bi ${isAfternoon ? "bi-sunset-fill" : "bi-sunrise-fill"}"></i>`;
        }
      }
    };

    if (shiftStart && shiftEnd) {
      shiftStart.addEventListener("input", updateShiftPreview);
      shiftEnd.addEventListener("input", updateShiftPreview);
      shiftName?.addEventListener("input", updateShiftPreview);
      shiftNight?.addEventListener("change", updateShiftPreview);
      updateShiftPreview();
    }

    // 2. Khoản lương: Quick Currency Chips + Live Preview
    const payName = container.querySelector("[data-payroll-name-input]");
    const payType = container.querySelector("[data-payroll-type-select]");
    const payAmount = container.querySelector("[data-payroll-amount-input]");
    const payPreview = container.querySelector("[data-payroll-preview]");
    const chipsContainer = container.querySelector("[data-payroll-chips]");

    const updatePayrollPreview = () => {
      if (!payPreview) return;
      const typeVal = payType?.value || "PHU_CAP";
      const nameVal = payName?.value?.trim() || "Khoản lương mới";
      const numVal = Number(payAmount?.value || 0);

      const isBonus = typeVal === "THUONG";
      const isDeduction = typeVal === "KHAU_TRU";

      const titleEl = payPreview.querySelector("[data-preview-pay-title]");
      if (titleEl) titleEl.textContent = nameVal;

      const badgeEl = payPreview.querySelector("[data-preview-type-badge]");
      if (badgeEl) {
        if (isBonus) {
          badgeEl.className = "badge bg-success-subtle text-success border border-success-subtle";
          badgeEl.innerHTML = `<i class="bi bi-gift-fill me-1"></i>Thưởng khích lệ (+)`;
        } else if (isDeduction) {
          badgeEl.className = "badge bg-danger-subtle text-danger border border-danger-subtle";
          badgeEl.innerHTML = `<i class="bi bi-dash-circle-fill me-1"></i>Khấu trừ / Kỷ luật (-)`;
        } else {
          badgeEl.className = "badge bg-primary-subtle text-primary border border-primary-subtle";
          badgeEl.innerHTML = `<i class="bi bi-wallet2 me-1"></i>Phụ cấp công việc (+)`;
        }
      }

      const iconEl = payPreview.querySelector("[data-preview-pay-icon]");
      if (iconEl) {
        iconEl.className = isBonus ? "catalog-icon catalog-icon--bonus" : isDeduction ? "catalog-icon catalog-icon--deduction" : "catalog-icon catalog-icon--allowance";
        iconEl.innerHTML = `<i class="bi ${isBonus ? "bi-gift-fill" : isDeduction ? "bi-dash-circle-fill" : "bi-wallet2"}"></i>`;
      }

      const signEl = payPreview.querySelector("[data-preview-sign]");
      if (signEl) signEl.textContent = isDeduction ? "-" : "+";

      const valEl = payPreview.querySelector("[data-preview-value]");
      if (valEl) valEl.textContent = numVal.toLocaleString("vi-VN");

      const boxEl = payPreview.querySelector("[data-preview-amount-box]");
      if (boxEl) {
        boxEl.className = `catalog-amount-box ${isBonus ? "text-success" : isDeduction ? "text-danger" : "text-primary"}`;
      }
    };

    chipsContainer?.querySelectorAll("[data-chip-amount]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const amt = Number(btn.dataset.chipAmount || 0);
        if (payAmount) {
          payAmount.value = amt;
          payAmount.dispatchEvent(new Event("input", { bubbles: true }));
        }
      });
    });

    payName?.addEventListener("input", updatePayrollPreview);
    payType?.addEventListener("change", updatePayrollPreview);
    payAmount?.addEventListener("input", updatePayrollPreview);
    if (payAmount) updatePayrollPreview();
  };

  // Khởi tạo form interactions nếu đang mở trang Create/Edit độc lập (không qua modal)
  initFormInteractions(document);

  // ==========================================
  // DYNAMIC AJAX MODAL EDITOR
  // ==========================================
  let editorDialog = document.querySelector("[data-catalog-editor]");
  if (!editorDialog) {
    editorDialog = document.createElement("dialog");
    editorDialog.className = "catalog-editor-dialog";
    editorDialog.dataset.catalogEditor = "";
    editorDialog.innerHTML = `
      <div class="catalog-editor-card">
        <div class="catalog-editor-header">
          <div><small>Cập nhật danh mục · The OverTime Hotel</small><h2 data-editor-title>Thông tin danh mục</h2></div>
          <button type="button" data-editor-close aria-label="Đóng"><i class="bi bi-x-lg"></i></button>
        </div>
        <div class="catalog-editor-loading" data-editor-loading>
          <span class="spinner-border text-primary"></span><strong>Đang tải biểu mẫu...</strong>
        </div>
        <div class="catalog-editor-body" data-editor-body></div>
      </div>`;
    document.body.append(editorDialog);
  }

  const editorBody = editorDialog.querySelector("[data-editor-body]");
  const editorLoading = editorDialog.querySelector("[data-editor-loading]");
  const setEditorLoading = (isLoading) => {
    editorLoading?.classList.toggle("is-visible", isLoading);
    editorBody?.classList.toggle("is-loading", isLoading);
  };

  const bindEditorForm = () => {
    const form = editorBody?.querySelector("form");
    if (!form) return;

    // Kích hoạt Smart Form logic (Preview, Chips, Night detection) bên trong modal
    initFormInteractions(editorBody);

    form.querySelectorAll("a[href]").forEach((link) => {
      if (link.textContent.includes("Quay lại")) {
        link.addEventListener("click", (event) => {
          event.preventDefault();
          editorDialog.close();
        });
      }
    });

    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      setEditorLoading(true);
      try {
        const response = await fetch(form.action, {
          method: form.method || "POST",
          body: new FormData(form),
          headers: { "X-Requested-With": "XMLHttpRequest" },
        });
        const html = await response.text();
        if (response.redirected) {
          editorDialog.close();
          showToast("Dữ liệu danh mục đã được lưu thành công.", "success");
          await refreshResults(buildFilterUrl());
          return;
        }
        const result = new DOMParser().parseFromString(html, "text/html");
        const returnedForm = result.querySelector(".catalog-form-card");
        if (!returnedForm) throw new Error("Không thể lưu dữ liệu danh mục.");
        editorBody.innerHTML = "";
        editorBody.append(returnedForm);
        bindEditorForm();
      } catch (error) {
        showToast(
          error instanceof Error ? error.message : "Không thể lưu dữ liệu.",
          "danger"
        );
      } finally {
        setEditorLoading(false);
      }
    });
  };

  const openEditor = async (link) => {
    const title = editorDialog.querySelector("[data-editor-title]");
    if (title)
      title.textContent =
        link.dataset.catalogModalTitle || "Thông tin danh mục";
    if (editorBody) editorBody.innerHTML = "";
    editorDialog.showModal();
    setEditorLoading(true);
    try {
      const response = await fetch(link.href, {
        headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" },
      });
      if (!response.ok) throw new Error("Không thể tải biểu mẫu.");
      const result = new DOMParser().parseFromString(
        await response.text(),
        "text/html"
      );
      const form = result.querySelector(".catalog-form-card");
      if (!form || !editorBody)
        throw new Error("Biểu mẫu trả về không hợp lệ.");
      editorBody.append(form);
      bindEditorForm();
      window.HotelMotion?.applyEntrance?.(editorBody);
      form
        .querySelector("input:not([type='hidden']), select, textarea")
        ?.focus();
    } catch (error) {
      editorDialog.close();
      showToast(
        error instanceof Error ? error.message : "Không thể tải biểu mẫu.",
        "danger"
      );
    } finally {
      setEditorLoading(false);
    }
  };

  document.addEventListener("click", (event) => {
    const link = event.target.closest("[data-catalog-modal]");
    if (!link) return;
    event.preventDefault();
    void openEditor(link);
  });

  editorDialog
    .querySelector("[data-editor-close]")
    ?.addEventListener("click", () => editorDialog.close());
  editorDialog.addEventListener("click", (event) => {
    if (event.target === editorDialog) editorDialog.close();
  });

  // ==========================================
  // CONFIRM DELETE DIALOG
  // ==========================================
  let confirmDialog = document.querySelector("[data-catalog-confirm]");
  if (!confirmDialog) {
    confirmDialog = document.createElement("dialog");
    confirmDialog.className = "catalog-confirm-dialog";
    confirmDialog.dataset.catalogConfirm = "";
    confirmDialog.innerHTML = `
            <div class="catalog-confirm-card">
                <button type="button" class="catalog-confirm-close" data-confirm-cancel aria-label="Đóng"><i class="bi bi-x-lg"></i></button>
                <span class="catalog-confirm-icon"><i class="bi bi-trash3"></i></span>
                <small>Xác nhận bảo mật danh mục</small>
                <h2>Xóa mục danh mục?</h2>
                <p>Thao tác này sẽ xóa vĩnh viễn và bị từ chối nếu dữ liệu đang liên kết với nhân sự hoặc lịch trực.</p>
                <div class="catalog-confirm-target"><i class="bi bi-database"></i><strong data-confirm-name>Mục danh mục</strong></div>
                <div class="catalog-confirm-actions">
                    <button type="button" data-confirm-cancel>Giữ lại</button>
                    <button type="button" class="is-danger" data-confirm-submit><i class="bi bi-trash3"></i> Xác nhận xóa</button>
                </div>
            </div>`;
    document.body.append(confirmDialog);
  }

  let pendingDeleteForm = null;
  document.addEventListener("submit", (event) => {
    const form = event.target.closest(".catalog-delete-form");
    if (!form || form.dataset.confirmed === "true") return;
    event.preventDefault();
    pendingDeleteForm = form;
    const name = form.dataset.catalogName || "Mục danh mục";
    const nameElement = confirmDialog.querySelector("[data-confirm-name]");
    if (nameElement) nameElement.textContent = name;
    if (typeof confirmDialog.showModal === "function")
      confirmDialog.showModal();
    else if (
      window.confirm(`Xóa “${name}”? Thao tác này không thể hoàn tác.`)
    ) {
      form.dataset.confirmed = "true";
      form.submit();
    }
  });

  const closeDialog = () => {
    pendingDeleteForm = null;
    if (confirmDialog.open) confirmDialog.close();
  };

  confirmDialog.querySelectorAll("[data-confirm-cancel]").forEach((button) => {
    button.addEventListener("click", closeDialog);
  });
  confirmDialog
    .querySelector("[data-confirm-submit]")
    ?.addEventListener("click", () => {
      const form = pendingDeleteForm;
      closeDialog();
      if (form) {
        form.dataset.confirmed = "true";
        form.submit();
      }
    });
  confirmDialog.addEventListener("click", (event) => {
    if (event.target === confirmDialog) closeDialog();
  });

  // ==========================================
  // EXPORT CSV & PRINT TABLE UTILITIES
  // ==========================================
  const exportTableToCsv = (type = "DanhMuc") => {
    const table = document.querySelector(".catalog-table");
    if (!table) {
      showToast("Không tìm thấy bảng dữ liệu để xuất.", "danger");
      return;
    }

    const rows = [];
    const ths = Array.from(table.querySelectorAll("thead th"));
    // Bỏ cột cuối cùng là cột Thao tác
    const headerRow = ths.slice(0, ths.length - 1).map((th) => `"${th.innerText.trim().replace(/"/g, '""')}"`);
    rows.push(headerRow.join(","));

    table.querySelectorAll("tbody tr").forEach((tr) => {
      if (tr.querySelector(".catalog-empty")) return;
      const tds = Array.from(tr.querySelectorAll("td"));
      if (tds.length <= 1) return;
      const rowData = tds.slice(0, tds.length - 1).map((td) => {
        const text = td.innerText.replace(/\s+/g, " ").trim();
        return `"${text.replace(/"/g, '""')}"`;
      });
      rows.push(rowData.join(","));
    });

    if (rows.length <= 1) {
      showToast("Không có dữ liệu trong bảng để xuất.", "danger");
      return;
    }

    const csvContent = "\uFEFF" + rows.join("\r\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    const dateStr = new Date().toISOString().slice(0, 10);
    link.setAttribute("href", url);
    link.setAttribute("download", `TheOverTimeHotel_${type}_${dateStr}.csv`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    showToast(`Đã xuất danh sách ${type} ra file Excel/CSV thành công!`, "success");
  };

  document.addEventListener("click", (e) => {
    const exportBtn = e.target.closest("[data-catalog-export]");
    if (exportBtn) {
      e.preventDefault();
      exportTableToCsv(exportBtn.dataset.catalogExport || "DanhMuc");
    }

    const printBtn = e.target.closest("[data-catalog-print]");
    if (printBtn) {
      e.preventDefault();
      window.print();
    }
  });
})();
