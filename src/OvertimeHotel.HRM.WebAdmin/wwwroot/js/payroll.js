// Popup chi tiết phiếu lương nhiều tab — The OverTime Hotel HRM.
// Nguyên tắc: nạp thân phiếu tại chỗ qua fetch, giữ nguyên header popup và không
// bao giờ rời trang danh sách (Zero Full-Page Reload cho thay đổi trạng thái trong trang).
(function () {
    "use strict";

    var DETAIL_PARTIAL_URL = "/BangLuong/DetailPartial";

    // ------------------------------------------------------------------
    // LỌC / TÌM KIẾM TẠI CHỖ: chỉ nạp lại khối danh sách lương bên dưới.
    // Header, breadcrumb, stepper, thẻ KPI và chính bộ lọc giữ nguyên —
    // không có lần reload toàn trang nào (rule 4 coding-style).
    // ------------------------------------------------------------------
    function initListRefresh() {
        var filterForm = document.querySelector("[data-payroll-filter-form]");
        var listRegion = document.querySelector("[data-payroll-list]");
        if (!filterForm || !listRegion) return;

        var results = listRegion.querySelector("[data-payroll-results]");
        var loading = listRegion.querySelector("[data-payroll-loading]");
        var countBadge = document.querySelector("[data-payroll-count]");
        var clearBtn = document.querySelector("[data-payroll-clear]");
        var kpiRow = document.querySelector("[data-payroll-kpi]");
        if (!results) return;

        var activeRequest = null;

        function buildFilterUrl() {
            var basePath = filterForm.getAttribute("action") || window.location.pathname;
            var url = new URL(basePath, window.location.origin);
            new FormData(filterForm).forEach(function (value, key) {
                var normalized = String(value).trim();
                if (normalized) url.searchParams.set(key, normalized);
            });
            return url;
        }

        function setLoading(isLoading) {
            if (loading) {
                // Overlay mờ dần bằng opacity nên phải bỏ `hidden` trước khi bật
                // class is-visible, và chỉ ẩn lại sau khi transition kết thúc.
                if (isLoading) {
                    loading.hidden = false;
                    void loading.offsetWidth;
                }
                loading.classList.toggle("is-visible", isLoading);
                loading.setAttribute("aria-hidden", isLoading ? "false" : "true");
                if (!isLoading) {
                    window.setTimeout(function () {
                        if (!loading.classList.contains("is-visible")) loading.hidden = true;
                    }, 240);
                }
            }
            results.classList.toggle("is-updating", isLoading);
        }

        /**
         * Đếm lại 4 thẻ KPI theo số mới. applyCountUp của site.js không xuất ra
         * window, nên gọi thẳng window.animateCountUp và tự chạy hào quang vàng kim.
         */
        function refreshKpi(nextDocument) {
            if (!kpiRow) return;
            var nextKpis = nextDocument.querySelectorAll("[data-payroll-kpi] [data-count-to]");
            var currentKpis = kpiRow.querySelectorAll("[data-count-to]");
            currentKpis.forEach(function (card, index) {
                var next = nextKpis[index];
                if (!next) return;
                var from = card.getAttribute("data-count-to");
                var to = next.getAttribute("data-count-to");
                // Số không đổi thì để yên, tránh nhấp nháy vô cớ.
                if (from === to) return;
                card.setAttribute("data-count-to", to);
                var suffix = next.textContent.slice(next.textContent.replace(/\D+$/, "").length);
                // animateCountUp lấy hậu tố đơn vị (" người", " đ") và giá trị chốt
                // cuối từ chính textContent lúc gọi, nên phải đặt sẵn giá trị MỚI kèm
                // hậu tố trước, rồi mới cho số chạy từ giá trị cũ sang giá trị mới.
                card.textContent = to + suffix;
                if (window.animateCountUp) window.animateCountUp(card, from, to, 700);
                var box = card.closest(".payroll-kpi-card");
                if (box && window.setTimeout) {
                    box.classList.add("kpi-pulse-active");
                    window.setTimeout(function () { box.classList.remove("kpi-pulse-active"); }, 900);
                }
            });
        }

        function syncClearButton() {
            if (!clearBtn) return;
            // Chỉ xét đúng hai trường lọc của người dùng. Không quét cả FormData vì
            // form còn mang thang/nam ẩn — chúng luôn khác rỗng nên nút X sẽ không
            // bao giờ ẩn được.
            var select = filterForm.querySelector("select[name='department']");
            var keyword = filterForm.querySelector("input[name='keyword']");
            var hasFilter =
                (select && select.value.trim() !== "") ||
                (keyword && keyword.value.trim() !== "");
            clearBtn.classList.toggle("d-none", !hasFilter);
        }

        function refreshResults(url) {
            if (activeRequest) activeRequest.abort();
            var controller = new AbortController();
            activeRequest = controller;
            var signal = controller.signal;

            setLoading(true);

            return fetch(url.toString(), {
                headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" },
                credentials: "same-origin",
                signal: signal
            })
                .then(function (response) {
                    if (!response.ok) throw new Error("Không thể cập nhật danh sách lương.");
                    return response.text();
                })
                .then(function (html) {
                    // HTML trả về là dữ liệu, không phải lệnh.
                    var nextDocument = new DOMParser().parseFromString(html, "text/html");
                    var nextResults = nextDocument.querySelector("[data-payroll-results]");
                    if (!nextResults) throw new Error("Dữ liệu trả về không hợp lệ.");

                    // Đồng bộ lại select phòng ban (danh mục có thể vừa đổi) mà không
                    // dựng lại form — giữ nguyên con trỏ và trạng thái focus của người dùng.
                    var nextSelect = nextDocument.querySelector("[data-payroll-filter-form] select[name='department']");
                    var currentSelect = filterForm.querySelector("select[name='department']");
                    if (nextSelect && currentSelect) currentSelect.innerHTML = nextSelect.innerHTML;

                    var keywordInput = filterForm.querySelector("input[name='keyword']");
                    if (keywordInput) keywordInput.value = url.searchParams.get("keyword") || "";

                    results.innerHTML = nextResults.innerHTML;

                    var nextBadge = nextDocument.querySelector("[data-payroll-count]");
                    if (countBadge && nextBadge) countBadge.textContent = nextBadge.textContent.trim();

                    // Nút X của danh sách cũ nằm trong form (không bị thay) — chỉ cần
                    // bật/tắt theo việc có bộ lọc hay không.
                    syncClearButton();

                    window.history.replaceState({}, "", url.toString());

                    results.classList.remove("payroll-list-enter");
                    void results.offsetWidth;
                    results.classList.add("payroll-list-enter");

                    refreshKpi(nextDocument);
                })
                .catch(function (error) {
                    if (error && error.name === "AbortError") return;
                    // Mất kết nối thì quay về hành vi cũ: điều hướng thật để không
                    // hiển thị danh sách cũ mà người dùng tưởng là mới.
                    window.location.href = url.toString();
                })
                .finally(function () {
                    if (activeRequest === controller) {
                        activeRequest = null;
                        setLoading(false);
                    }
                });
        }

        // Chọn phòng ban: onchange gọi requestSubmit() -> submit, chặn ở đây để nạp tại chỗ.
        filterForm.addEventListener("submit", function (event) {
            event.preventDefault();
            refreshResults(buildFilterUrl());
        });

        // Nút xóa bộ lọc: xóa ngay trên form rồi nạp tại chỗ, không rời trang.
        if (clearBtn) {
            clearBtn.addEventListener("click", function (event) {
                event.preventDefault();
                var select = filterForm.querySelector("select[name='department']");
                var keyword = filterForm.querySelector("input[name='keyword']");
                if (select) select.value = "";
                if (keyword) keyword.value = "";
                refreshResults(buildFilterUrl());
            });
        }
    }

    function init() {
        var modal = document.getElementById("payslipDetailModal");
        if (!modal) return;

        var body = modal.querySelector("[data-payslip-body]");
        var loading = modal.querySelector("[data-payslip-loading]");
        var errorBox = modal.querySelector("[data-payslip-error]");
        var headerName = modal.querySelector("[data-payslip-name]");
        var headerCode = modal.querySelector("[data-payslip-code]");
        var headerDept = modal.querySelector("[data-payslip-dept]");
        var headerRole = modal.querySelector("[data-payslip-role]");
        var avatar = modal.querySelector("[data-payslip-avatar]");
        var printBtn = modal.querySelector("[data-payslip-print]");
        var pageLink = modal.querySelector("[data-payslip-page-link]");
        if (!body || !printBtn) return;

        var bootstrapModal = window.bootstrap && window.bootstrap.Modal
            ? new window.bootstrap.Modal(modal)
            : null;

        var activeRequest = null;

        function setHeaderFromRow(row) {
            var name = row.getAttribute("data-payslip-name") || "Phiếu lương";
            headerName.textContent = name;
            headerCode.textContent = row.getAttribute("data-payslip-code") || "—";
            headerDept.textContent = row.getAttribute("data-payslip-dept") || "";
            headerRole.textContent = row.getAttribute("data-payslip-role") || "";
            avatar.textContent = name.trim().split(" ").pop().charAt(0).toUpperCase();

            var printUrl = row.getAttribute("data-payslip-print");
            if (printUrl) {
                printBtn.setAttribute("data-payslip-href", printUrl);
                printBtn.classList.remove("d-none");
            } else {
                printBtn.classList.add("d-none");
            }

            var pageUrl = row.getAttribute("data-payslip-page");
            if (pageLink && pageUrl) {
                pageLink.setAttribute("href", pageUrl);
                pageLink.classList.remove("d-none");
            } else if (pageLink) {
                pageLink.classList.add("d-none");
            }
        }

        function showLoading() {
            loading.hidden = false;
            body.hidden = true;
            body.innerHTML = "";
            errorBox.classList.add("d-none");
            errorBox.textContent = "";
        }

        function openDetail(row) {
            var id = row.getAttribute("data-payslip-id");
            if (!id) return;

            setHeaderFromRow(row);
            showLoading();
            if (bootstrapModal) bootstrapModal.show();

            if (activeRequest) activeRequest.abort();
            var controller = new AbortController();
            activeRequest = controller;

            fetch(DETAIL_PARTIAL_URL + "?id=" + encodeURIComponent(id), {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin",
                signal: controller.signal
            })
                .then(function (response) {
                    if (!response.ok) throw new Error("HTTP " + response.status);
                    return response.text();
                })
                .then(function (html) {
                    // Trang trả về là HTML phiếu lương; nội dung là dữ liệu, không phải lệnh.
                    body.innerHTML = html;
                    // Thẻ <script> chèn qua innerHTML không tự chạy — chép lại thủ công.
                    body.querySelectorAll("script").forEach(function (old) {
                        var s = document.createElement("script");
                        if (old.src) s.src = old.src;
                        else s.textContent = old.textContent;
                        old.replaceWith(s);
                    });
                    body.hidden = false;
                    loading.hidden = true;
                    activeRequest = null;
                })
                .catch(function (err) {
                    if (err && err.name === "AbortError") return;
                    loading.hidden = true;
                    errorBox.textContent = "Không tải được chi tiết phiếu lương. Vui lòng thử lại.";
                    errorBox.classList.remove("d-none");
                    activeRequest = null;
                });
        }

        // Mở popup khi bấm vào dòng hoặc tên nhân viên (chặn điều hướng mặc định).
        document.addEventListener("click", function (event) {
            var trigger = event.target.closest("[data-payslip-id]");
            if (!trigger) return;
            if (event.target.closest(".btn, button")) return;
            event.preventDefault();
            openDetail(trigger);
        });

        printBtn.addEventListener("click", function () {
            var href = printBtn.getAttribute("data-payslip-href");
            if (href) window.open(href, "_blank", "noopener");
        });

        // Chuyển tab tại chỗ trong popup.
        modal.addEventListener("click", function (event) {
            var tab = event.target.closest(".payslip-tab");
            if (!tab) return;
            var target = tab.getAttribute("aria-controls");
            var pane = target ? modal.querySelector("#" + CSS.escape(target)) : null;
            if (!pane) return;

            modal.querySelectorAll(".payslip-tab").forEach(function (t) {
                var active = t === tab;
                t.classList.toggle("is-active", active);
                t.setAttribute("aria-selected", active ? "true" : "false");
                t.tabIndex = active ? 0 : -1;
            });
            modal.querySelectorAll(".payslip-pane").forEach(function (p) {
                var active = p === pane;
                p.classList.toggle("is-active", active);
                if (active) p.removeAttribute("hidden");
                else p.setAttribute("hidden", "");
            });
        });

        // Đóng popup thì xả nội dung để lần mở sau nạp lại số liệu mới.
        modal.addEventListener("hidden.bs.modal", function () {
            if (activeRequest) {
                activeRequest.abort();
                activeRequest = null;
            }
            body.innerHTML = "";
            body.hidden = false;
            loading.hidden = true;
            errorBox.classList.add("d-none");
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            initListRefresh();
            init();
        });
    } else {
        initListRefresh();
        init();
    }
})();
