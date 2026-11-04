/*
 * ═══════════════════════════════════════════════════════════════════════════
 *  PHÂN HỆ BÁO CÁO & THỐNG KÊ QUẢN TRỊ (TSK-24) — The OverTime Hotel
 * ═══════════════════════════════════════════════════════════════════════════
 *
 *  Module này giữ đúng quy tắc "Zero Full-Page Reload for In-Page State Changes"
 *  (rules/coding-style.md mục 4):
 *
 *   • Đổi KỲ / PHÒNG BAN -> fetch AJAX, chỉ hoán đổi vùng [data-baocao-board].
 *                           Thanh tiêu đề, bộ lọc và thanh tab đứng yên.
 *   • Đổi TAB            -> không gọi mạng. Cả sáu khối đã render sẵn trong DOM,
 *                           JS chỉ ẩn/hiện và dựng biểu đồ của khối vừa lộ ra,
 *                           nên chuyển tab là tức thì.
 *   • Cả hai đường đều cập nhật history.pushState để URL dán được cho đồng nghiệp.
 *
 *  Dữ liệu biểu đồ đi kèm HTML trong một thẻ <script type="application/json">,
 *  nên sau mỗi lần hoán đổi không phát sinh thêm request nào.
 */
(function () {
  "use strict";

  var root = document.querySelector("[data-baocao-root]");
  if (!root) return;

  var boardHost = root.querySelector("[data-baocao-board-host]");
  var filterForm = root.querySelector("[data-baocao-filter]");
  var tabsNav = root.querySelector("[data-baocao-tabs]");
  var tabInput = root.querySelector("[data-baocao-tab-input]");
  var loadingBar = root.querySelector("[data-baocao-loading]");
  var kySelect = root.querySelector("[data-baocao-ky]");
  var monthField = root.querySelector("[data-baocao-month-field]");
  var quarterField = root.querySelector("[data-baocao-quarter-field]");

  if (!boardHost) return;

  var VALID_TABS = ["bien-dong", "co-cau", "hoc-van", "quy-luong", "chuyen-can", "su-kien"];
  var STORAGE_KEY = "overtime.baocao.tab";
  var MONEY_DIVISOR = 1000000;

  // Nhớ tab đang xem giữa các lần quay lại (rules/coding-style.md mục 4).
  // Bọc try/catch vì chế độ riêng tư của một số trình duyệt ném lỗi khi đọc
  // localStorage — không được để việc đó làm sập cả trang báo cáo.
  function readStoredTab() {
    try {
      return window.localStorage.getItem(STORAGE_KEY) || "";
    } catch (error) {
      console.warn("[BaoCao] Không đọc được localStorage:", error);
      return "";
    }
  }

  function writeStoredTab(value) {
    try {
      window.localStorage.setItem(STORAGE_KEY, value);
    } catch (error) {
      console.warn("[BaoCao] Không ghi được localStorage:", error);
    }
  }

  function normalizeTab(value) {
    var key = String(value || "").trim().toLowerCase();
    return VALID_TABS.indexOf(key) >= 0 ? key : "";
  }

  function availableTabs() {
    if (!tabsNav) return [];
    return Array.prototype.map.call(
      tabsNav.querySelectorAll("[data-baocao-tab]"),
      function (button) {
        return button.getAttribute("data-baocao-tab");
      }
    );
  }

  function url(attribute, element) {
    var value = element.getAttribute(attribute) || window.location.pathname;
    return new URL(value, window.location.origin);
  }

  // ── Trạng thái của module ────────────────────────────────────────────────
  var activeTab = "bien-dong";
  var chartData = {};
  var chartInstances = new Map();
  var activeAbort = null;

  // ═══════════════════════════════════════════════════════════════════════
  //  DỰNG BIỂU ĐỒ
  // ═══════════════════════════════════════════════════════════════════════

  // Đọc thẳng token màu từ :root để biểu đồ không lệch tông với phần còn lại
  // của giao diện khi bảng màu đổi (xem khối :root của site.css).
  function cssVar(name, fallback) {
    var value = getComputedStyle(document.documentElement).getPropertyValue(name);
    value = String(value || "").trim();
    return value || fallback;
  }

  var COLORS = {
    royal: cssVar("--hotel-royal", "#1e3a8a"),
    gold: cssVar("--hotel-gold", "#d4af37"),
    up: "#15803d",
    down: "#b91c1c",
    muted: "#94a3b8"
  };

  var PIE_PALETTE = [
    COLORS.royal, COLORS.gold, "#0ea5e9", "#14b8a6",
    "#8b5cf6", "#f97316", "#ec4899", "#64748b"
  ];

  function vnNumber(value) {
    var number = Number(value) || 0;
    return number.toLocaleString("vi-VN");
  }

  function millions(value) {
    return Math.round((Number(value) || 0) / MONEY_DIVISOR * 10) / 10;
  }

  // Nhãn trục tung cho tiền: quy về đơn vị triệu đồng cho khỏi tràn chữ số.
  function moneyAxisLabel(value) {
    return vnNumber(value) + " tr";
  }

  function baseGrid() {
    return { left: 8, right: 16, top: 48, bottom: 8, containLabel: true };
  }

  function legend() {
    return { top: 8, icon: "roundRect", itemWidth: 12, itemHeight: 8, textStyle: { color: "#475569" } };
  }

  // Lát cắt tròn dùng chung cho mọi biểu đồ cơ cấu.
  //
  // Hai chế độ, chọn theo SỐ LÁT CẮT:
  //
  //   • Ít lát (<= 6): nhãn nằm quanh vành tròn, mỗi nhãn một dòng.
  //   • Nhiều lát (> 6): TẮT nhãn quanh vành, dồn toàn bộ tên + tỷ lệ xuống
  //     chú giải dọc bên phải. Phòng ban và chức vụ có tên tiếng Việt rất dài
  //     (vd "Bộ phận Công nghệ thông tin (IT - Information Technology)");
  //     nhãn hai dòng quanh vành tròn rộng ~380px chắc chắn chồng lên nhau và
  //     đè cả lên chú giải — ECharts có avoidLabelOverlap cũng không đủ chỗ.
  //     Đưa vào danh sách chú giải thì xếp dọc, không bao giờ đè.
  function pieOption(items, options) {
    var config = options || {};
    var rows = Array.isArray(items) ? items : [];
    var total = rows.reduce(function (sum, item) {
      return sum + (Number(item && item.GiaTri) || 0);
    }, 0);

    // Bảng tra tên -> tỷ lệ, để chú giải hiển thị kèm phần trăm như nhãn cũ.
    var percentByName = {};
    rows.forEach(function (item) {
      var value = Number(item && item.GiaTri) || 0;
      var percent = total > 0 ? Math.round((value / total) * 1000) / 10 : 0;
      percentByName[String(item.Nhan || "—")] = percent;
    });

    // Donut hai lát như "Theo giới tính" vẫn nên có nhãn: chỉ 2 chỗ, không đè.
    var dense = rows.length > 6;

    var baseLegend = {
      icon: "circle",
      itemWidth: 10,
      itemHeight: 10,
      textStyle: { color: "#475569" }
    };

    return {
      color: PIE_PALETTE,
      tooltip: {
        trigger: "item",
        confine: true,
        formatter: function (params) {
          // Tên đầy đủ luôn có ở đây, kể cả khi chú giải đã rút gọn chữ.
          return params.name + ": " + vnNumber(params.value) +
            (config.unit ? " " + config.unit : "") + " (" + params.percent + "%)";
        }
      },
      legend: dense
        ? Object.assign({}, baseLegend, {
            type: "scroll",
            orient: "vertical",
            right: 0,
            top: 12,
            bottom: 12,
            itemGap: 7,
            textStyle: {
              color: "#475569",
              fontSize: 11,
              width: 196,
              overflow: "truncate"
            },
            formatter: function (name) {
              return name + " · " + percentByName[name] + "%";
            }
          })
        : Object.assign({}, baseLegend, { bottom: 0 }),
      series: [
        {
          type: "pie",
          // Chừa chỗ hai bên cho nhãn khi có nhãn; khi dồn sang chú giải thì
          // đẩy vành tròn sang trái nhường nửa phải cho danh sách.
          radius: dense
            ? (config.donut ? ["42%", "64%"] : ["0%", "62%"])
            : (config.donut ? ["44%", "58%"] : ["0%", "56%"]),
          center: dense ? ["28%", "50%"] : ["50%", "45%"],
          avoidLabelOverlap: true,
          minAngle: 3,
          itemStyle: { borderColor: "#ffffff", borderWidth: 2, borderRadius: 6 },
          label: {
            show: !dense,
            formatter: function (params) {
              // Một dòng để hai nhãn cạnh nhau không đè lên nhau.
              return params.name + " " + Math.round(params.percent) + "%";
            },
            color: "#334155",
            fontSize: 11,
            lineHeight: 15
          },
          // Khi đã tắt nhãn thì đường dẫn nhãn cũng phải tắt, nếu không ECharts
          // vẫn vẽ các vạch gãy trỏ ra ngoài và trông như nhãn bị mất chữ.
          labelLine: { show: !dense, length: 10, length2: 6 },
          data: rows.map(function (item) {
            return { name: String(item.Nhan || "—"), value: Number(item.GiaTri) || 0 };
          })
        }
      ]
    };
  }

  // Cột ngang: hợp với tên phòng ban vì nhãn dài vẫn đọc được đủ chữ.
  function horizontalBarOption(items, options) {
    var config = options || {};
    var rows = (Array.isArray(items) ? items : []).slice();

    // Trục phạm trù của ECharts vẽ từ dưới lên; đảo lại để phần tử lớn nhất nằm trên.
    rows.reverse();

    return {
      grid: baseGrid(),
      tooltip: {
        trigger: "axis",
        axisPointer: { type: "shadow" },
        confine: true,
        valueFormatter: function (value) {
          return vnNumber(value) + (config.suffix || "");
        }
      },
      xAxis: {
        type: "value",
        axisLabel: { color: "#64748b", formatter: config.money ? moneyAxisLabel : "{value}" },
        splitLine: { lineStyle: { color: "#e2e8f0" } }
      },
      yAxis: {
        type: "category",
        data: rows.map(function (item) {
          return String(item.Nhan || "—");
        }),
        axisLabel: { color: "#334155", fontSize: 11, width: 130, overflow: "truncate" },
        axisTick: { show: false },
        axisLine: { lineStyle: { color: "#e2e8f0" } }
      },
      series: [
        {
          type: "bar",
          barMaxWidth: 22,
          itemStyle: { color: config.color || COLORS.royal, borderRadius: [0, 6, 6, 0] },
          data: rows.map(function (item) {
            return Number(config.value(item)) || 0;
          })
        }
      ]
    };
  }

  // Bảng cấu hình cho từng khung biểu đồ trong _BaoCaoBoard.
  // Mọi hàm đọc từ `chartData` — payload JSON của board đang hiển thị.
  var CHART_BUILDERS = {
    "bien-dong-series": function () {
      var rows = (chartData.BienDong && chartData.BienDong.Series) || [];
      return {
        grid: baseGrid(),
        tooltip: { trigger: "axis", confine: true },
        legend: legend(),
        xAxis: {
          type: "category",
          data: rows.map(function (row) { return row.Nhan; }),
          axisLabel: { color: "#64748b" },
          axisLine: { lineStyle: { color: "#e2e8f0" } }
        },
        yAxis: [
          {
            type: "value",
            name: "Người",
            nameTextStyle: { color: "#94a3b8" },
            axisLabel: { color: "#64748b" },
            splitLine: { lineStyle: { color: "#e2e8f0" } }
          },
          {
            type: "value",
            name: "Quân số",
            nameTextStyle: { color: "#94a3b8" },
            axisLabel: { color: "#64748b" },
            splitLine: { show: false }
          }
        ],
        series: [
          {
            name: "Tuyển mới",
            type: "bar",
            barMaxWidth: 18,
            itemStyle: { color: COLORS.up, borderRadius: [4, 4, 0, 0] },
            data: rows.map(function (row) { return row.TuyenMoi; })
          },
          {
            name: "Thôi việc",
            type: "bar",
            barMaxWidth: 18,
            itemStyle: { color: COLORS.down, borderRadius: [4, 4, 0, 0] },
            data: rows.map(function (row) { return row.NghiViec; })
          },
          {
            name: "Quân số cuối tháng",
            type: "line",
            yAxisIndex: 1,
            smooth: true,
            symbolSize: 6,
            lineStyle: { width: 3, color: COLORS.gold },
            itemStyle: { color: COLORS.gold },
            areaStyle: { opacity: 0.08, color: COLORS.gold },
            data: rows.map(function (row) { return row.CuoiKy; })
          }
        ]
      };
    },

    "bien-dong-phongban": function () {
      var rows = (chartData.BienDong && chartData.BienDong.TheoPhongBan) || [];
      return horizontalBarOption(
        rows.map(function (row) {
          return { Nhan: row.PhongBan, GiaTri: row.QuanSo };
        }),
        { color: COLORS.royal, suffix: " người", value: function (item) { return item.GiaTri; } }
      );
    },

    "co-cau-phongban": function () {
      return pieOption(chartData.CoCau && chartData.CoCau.TheoPhongBan, { unit: "người" });
    },

    "co-cau-chucvu": function () {
      return pieOption(chartData.CoCau && chartData.CoCau.TheoChucVu, { unit: "người", donut: true });
    },

    "co-cau-gioitinh": function () {
      return pieOption(chartData.CoCau && chartData.CoCau.TheoGioiTinh, { unit: "người", donut: true });
    },

    "hoc-van-trinhdo": function () {
      var rows = (chartData.HocVanThamNien && chartData.HocVanThamNien.TheoTrinhDo) || [];
      return horizontalBarOption(rows, {
        color: COLORS.royal,
        suffix: " người",
        value: function (item) { return item.GiaTri; }
      });
    },

    "hoc-van-thamnien": function () {
      return pieOption(chartData.HocVanThamNien && chartData.HocVanThamNien.TheoThamNien, {
        unit: "người",
        donut: true
      });
    },

    "hoc-van-mucluong": function () {
      var rows = (chartData.HocVanThamNien && chartData.HocVanThamNien.TheoMucLuong) || [];
      return horizontalBarOption(rows, {
        color: COLORS.gold,
        suffix: " người",
        value: function (item) { return item.GiaTri; }
      });
    },

    "quy-luong-series": function () {
      var rows = (chartData.QuyLuong && chartData.QuyLuong.Series) || [];
      return {
        grid: baseGrid(),
        tooltip: {
          trigger: "axis",
          confine: true,
          valueFormatter: function (value) {
            return vnNumber(value) + " triệu đồng";
          }
        },
        legend: legend(),
        xAxis: {
          type: "category",
          data: rows.map(function (row) { return row.Nhan; }),
          axisLabel: { color: "#64748b" },
          axisLine: { lineStyle: { color: "#e2e8f0" } }
        },
        yAxis: {
          type: "value",
          name: "Triệu đồng",
          nameTextStyle: { color: "#94a3b8" },
          axisLabel: { color: "#64748b", formatter: moneyAxisLabel },
          splitLine: { lineStyle: { color: "#e2e8f0" } }
        },
        series: [
          {
            name: "Định mức tham chiếu",
            type: "bar",
            barMaxWidth: 20,
            itemStyle: { color: COLORS.muted, borderRadius: [4, 4, 0, 0] },
            data: rows.map(function (row) { return millions(row.NganSach); })
          },
          {
            name: "Thực chi",
            type: "bar",
            barMaxWidth: 20,
            itemStyle: { color: COLORS.royal, borderRadius: [4, 4, 0, 0] },
            data: rows.map(function (row) { return millions(row.ThucChi); })
          }
        ]
      };
    },

    "quy-luong-phongban": function () {
      var rows = (chartData.QuyLuong && chartData.QuyLuong.TheoPhongBan) || [];
      return horizontalBarOption(
        rows.map(function (row) {
          return { Nhan: row.PhongBan, GiaTri: millions(row.ThucChi) };
        }),
        {
          color: COLORS.gold,
          suffix: " triệu đồng",
          value: function (item) { return item.GiaTri; }
        }
      );
    },

    "chuyen-can-series": function () {
      var rows = (chartData.ChuyenCan && chartData.ChuyenCan.Series) || [];
      return {
        grid: baseGrid(),
        tooltip: { trigger: "axis", confine: true },
        legend: legend(),
        xAxis: {
          type: "category",
          data: rows.map(function (row) { return row.Nhan; }),
          axisLabel: { color: "#64748b" },
          axisLine: { lineStyle: { color: "#e2e8f0" } }
        },
        yAxis: [
          {
            type: "value",
            name: "%",
            min: 0,
            max: 100,
            nameTextStyle: { color: "#94a3b8" },
            axisLabel: { color: "#64748b" },
            splitLine: { lineStyle: { color: "#e2e8f0" } }
          },
          {
            type: "value",
            name: "Lượt",
            nameTextStyle: { color: "#94a3b8" },
            axisLabel: { color: "#64748b" },
            splitLine: { show: false }
          }
        ],
        series: [
          {
            name: "Tỷ lệ chuyên cần",
            type: "line",
            smooth: true,
            symbolSize: 6,
            lineStyle: { width: 3, color: COLORS.up },
            itemStyle: { color: COLORS.up },
            areaStyle: { opacity: 0.08, color: COLORS.up },
            data: rows.map(function (row) { return row.TyLeChuyenCan; })
          },
          {
            name: "Lượt vi phạm",
            type: "bar",
            yAxisIndex: 1,
            barMaxWidth: 18,
            itemStyle: { color: COLORS.down, borderRadius: [4, 4, 0, 0], opacity: 0.75 },
            data: rows.map(function (row) { return row.ViPham; })
          }
        ]
      };
    },

    "chuyen-can-phongban": function () {
      var rows = (chartData.ChuyenCan && chartData.ChuyenCan.TheoPhongBan) || [];
      return horizontalBarOption(
        rows.map(function (row) {
          return { Nhan: row.Nhan, GiaTri: row.TyLe };
        }),
        {
          color: COLORS.up,
          suffix: "% chuyên cần",
          value: function (item) { return item.GiaTri; }
        }
      );
    },

    "su-kien-loai": function () {
      return pieOption(chartData.SuKien && chartData.SuKien.TheoLoai, { unit: "sự kiện" });
    },

    "su-kien-danhgia": function () {
      return pieOption(chartData.SuKien && chartData.SuKien.TheoDanhGia, { unit: "lượt" });
    }
  };

  // Đọc payload JSON của board đang hiển thị.
  // Controller serialize bằng bộ mã hoá MẶC ĐỊNH của System.Text.Json nên ký tự "<"
  // đã bị thoát sang dạng thực thể Unicode (mã điểm mã 003C) — nội dung trong thẻ
  // script luôn là JSON hợp lệ và không thể đóng thẻ để chèn mã.
  function readChartData() {
    var node = boardHost.querySelector("[data-baocao-chart-data]");
    if (!node) {
      console.warn("[BaoCao] Không tìm thấy khối dữ liệu biểu đồ.");
      return {};
    }
    try {
      return JSON.parse(node.textContent || "{}");
    } catch (error) {
      console.warn("[BaoCao] Dữ liệu biểu đồ không phải JSON hợp lệ:", error);
      return {};
    }
  }

  function currentBoard() {
    return boardHost.querySelector("[data-baocao-board]");
  }

  function visiblePanel() {
    return boardHost.querySelector('[data-baocao-panel="' + activeTab + '"]');
  }

  function disposeCharts() {
    chartInstances.forEach(function (instance) {
      try {
        instance.dispose();
      } catch (error) {
        console.warn("[BaoCao] Không huỷ được biểu đồ:", error);
      }
    });
    chartInstances.clear();
  }

  // Dựng biểu đồ cho các khung đang thực sự nhìn thấy.
  //
  // Chỉ dựng khi khung đã lộ ra: ECharts đọc kích thước phần tử ngay lúc khởi tạo,
  // nên dựng sẵn một khung còn `hidden` sẽ cho ra canvas 0×0 và cảnh báo của thư viện.
  // Khởi tạo muộn + giữ lại instance (không huỷ khi đổi tab) vừa đúng kích thước
  // vừa khỏi dựng lại mỗi lần người dùng quay về tab cũ.
  function renderCharts() {
    if (!window.echarts) {
      // CDN bị chặn hoặc mất mạng: báo rõ thay vì để khung trống không lời giải thích.
      Array.prototype.forEach.call(boardHost.querySelectorAll(".baocao-chart"), function (el) {
        el.classList.add("is-unavailable");
        el.textContent = "Không tải được thư viện biểu đồ (ECharts). Số liệu vẫn xem được ở bảng bên dưới.";
      });
      return;
    }

    var panel = visiblePanel();
    if (!panel) return;

    Array.prototype.forEach.call(panel.querySelectorAll(".baocao-chart"), function (el) {
      var key = el.getAttribute("data-chart");
      var builder = CHART_BUILDERS[key];
      if (!key || !builder) return;

      var instance = chartInstances.get(key);
      if (!instance || instance.isDisposed()) {
        instance = window.echarts.init(el, null, { renderer: "canvas" });
        chartInstances.set(key, instance);
      }
      instance.setOption(builder(), true);
      instance.resize();
    });
  }

  // ═══════════════════════════════════════════════════════════════════════
  //  CHUYỂN TAB (phía client, KHÔNG gọi mạng)
  // ═══════════════════════════════════════════════════════════════════════

  function applyTab(tab, options) {
    var config = options || {};
    var panels = boardHost.querySelectorAll("[data-baocao-panel]");
    var hasPanel = false;

    Array.prototype.forEach.call(panels, function (panel) {
      var isTarget = panel.getAttribute("data-baocao-panel") === tab;
      if (isTarget) hasPanel = true;
      panel.hidden = !isTarget;
    });

    // Tab được yêu cầu không tồn tại — ví dụ tài khoản không có quyền xem quỹ lương
    // nên khối 10.4 không được render: lùi về tab đầu tiên đang có mặt.
    if (!hasPanel) {
      var fallback = availableTabs()[0];
      if (fallback && fallback !== tab) {
        applyTab(fallback, { persist: config.persist, pushUrl: config.pushUrl });
      }
      return;
    }

    activeTab = tab;
    var appliedBoard = currentBoard();
    if (appliedBoard) appliedBoard.dataset.tab = tab;

    if (tabsNav) {
      Array.prototype.forEach.call(tabsNav.querySelectorAll("[data-baocao-tab]"), function (button) {
        var isActive = button.getAttribute("data-baocao-tab") === tab;
        button.classList.toggle("is-active", isActive);
        button.setAttribute("aria-pressed", isActive ? "true" : "false");
      });
    }

    if (tabInput) tabInput.value = tab;
    if (config.persist !== false) writeStoredTab(tab);

    var panel = visiblePanel();
    if (panel) {
      panel.classList.remove("in-place-enter");
      void panel.offsetWidth;
      panel.classList.add("in-place-enter");
    }

    renderCharts();

    if (config.pushUrl) {
      window.history.pushState({ baocaoTab: tab }, "", buildCurrentExportUrl(root.getAttribute("data-baocao-endpoint") || window.location.pathname));
    }
  }

  // Ghép tham số bộ lọc hiện tại vào URL. Ô đang bị `disabled` (tháng khi xem theo
  // quý, hoặc ngược lại) tự động bị FormData bỏ qua — đúng như mong muốn.
  function buildUrl(base) {
    var result = base instanceof URL ? base : new URL(String(base), window.location.origin);
    if (filterForm) {
      new FormData(filterForm).forEach(function (value, key) {
        var normalized = String(value).trim();
        if (normalized) {
          result.searchParams.set(key, normalized);
        } else {
          result.searchParams.delete(key);
        }
      });
    }
    if (activeTab) result.searchParams.set("tab", activeTab);
    return result;
  }

  // ═══════════════════════════════════════════════════════════════════════
  //  NẠP LẠI VÙNG DỮ LIỆU TẠI CHỖ (AJAX)
  // ═══════════════════════════════════════════════════════════════════════

  function setLoading(isLoading) {
    if (loadingBar) loadingBar.hidden = !isLoading;
    boardHost.setAttribute("aria-busy", isLoading ? "true" : "false");
    boardHost.classList.toggle("is-loading", isLoading);
  }

  function showError(message) {
    var toast = document.createElement("div");
    toast.className = "baocao-toast";
    toast.setAttribute("role", "alert");
    toast.textContent = message || "Không thể cập nhật báo cáo.";
    boardHost.appendChild(toast);
    window.setTimeout(function () {
      toast.classList.add("is-leaving");
      window.setTimeout(function () { toast.remove(); }, 240);
    }, 4600);
  }

  function replaceBoard(nextBoard) {
    var oldBoard = currentBoard();
    var adopted = document.adoptNode(nextBoard);

    // Huỷ biểu đồ cũ TRƯỚC khi gỡ DOM: ECharts vẫn giữ con trỏ tới canvas đã bị bỏ,
    // không huỷ sẽ rò rỉ và làm canvas mới không vẽ được.
    disposeCharts();

    if (oldBoard) {
      oldBoard.replaceWith(adopted);
    } else {
      boardHost.appendChild(adopted);
    }

    chartData = readChartData();

    adopted.classList.remove("in-place-enter");
    void adopted.offsetWidth;
    adopted.classList.add("in-place-enter");

    // Đếm số + lướt vào + hào quang vàng kim cho đúng vùng vừa thay (rule 5.1 - 5.4).
    // Phần tử mới chưa có data-count-done nên HotelMotion đếm lại từ 0.
    window.HotelMotion?.applyEntrance?.(adopted);
  }

  // Server-rendered state is authoritative, including omitted/reset parameters.
  function syncFormFromUrl(targetUrl, preserveTab) {
    if (!filterForm) return;
    var board = currentBoard();
    var data = board ? board.dataset : {};
    ["ky", "nam", "thang", "quy", "phongBan"].forEach(function (key) {
      var field = filterForm.querySelector('select[name="' + key + '"]');
      if (field) field.value = data[key] !== undefined ? data[key] : (targetUrl.searchParams.get(key) || "");
    });
    syncPeriodFields();
    var tab = normalizeTab(preserveTab ? activeTab : (data.tab || targetUrl.searchParams.get("tab")));
    if (tab) applyTab(tab, { persist: true, pushUrl: false });
  }

  async function refresh(targetUrl, pushHistory) {
    if (typeof pushHistory === "undefined") pushHistory = true;
    var next = targetUrl instanceof URL ? targetUrl : new URL(String(targetUrl), window.location.origin);

    if (activeAbort) activeAbort.abort();
    activeAbort = new AbortController();
    var signal = activeAbort.signal;

    setLoading(true);
    try {
      var response = await fetch(next.toString(), {
        headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" },
        signal: signal
      });

      if (!response.ok) {
        throw new Error("Máy chủ trả về mã " + response.status + ".");
      }

      var html = await response.text();
      if (signal.aborted) return;
      var parsed = new DOMParser().parseFromString(html, "text/html");
      var nextBoard = parsed.querySelector("[data-baocao-board]");
      if (!nextBoard) {
        throw new Error("Dữ liệu trả về không đúng cấu trúc.");
      }

      replaceBoard(nextBoard);
      syncFormFromUrl(next, pushHistory);
      if (pushHistory) {
        window.history.pushState({}, "", buildCurrentExportUrl(root.getAttribute("data-baocao-endpoint") || window.location.pathname));
      }
    } catch (error) {
      if (error && error.name === "AbortError") return;
      syncFormFromUrl(new URL(window.location.href), true);
      window.history.replaceState({}, "", buildCurrentExportUrl(root.getAttribute("data-baocao-endpoint") || window.location.pathname));
      console.warn("[BaoCao] Lỗi nạp lại vùng dữ liệu:", error);
      showError(error instanceof Error ? error.message : null);
    } finally {
      if (activeAbort && activeAbort.signal === signal) {
        setLoading(false);
      }
    }
  }

  // ═══════════════════════════════════════════════════════════════════════
  //  GẮN SỰ KIỆN
  // ═══════════════════════════════════════════════════════════════════════

  // Ô tháng và ô quý chỉ hiện đúng một ô theo kiểu kỳ đang chọn.
  function syncPeriodFields() {
    if (!kySelect) return;
    var kind = String(kySelect.value || "THANG").toUpperCase();

    if (monthField) {
      var showMonth = kind === "THANG";
      monthField.hidden = !showMonth;
      var monthSelect = monthField.querySelector("select");
      if (monthSelect) monthSelect.disabled = !showMonth;
    }
    if (quarterField) {
      var showQuarter = kind === "QUY";
      quarterField.hidden = !showQuarter;
      var quarterSelect = quarterField.querySelector("select");
      if (quarterSelect) quarterSelect.disabled = !showQuarter;
    }
  }

  if (tabsNav) {
    tabsNav.addEventListener("click", function (event) {
      var button = event.target.closest("[data-baocao-tab]");
      if (!button || !tabsNav.contains(button)) return;
      event.preventDefault();
      applyTab(normalizeTab(button.getAttribute("data-baocao-tab")), { persist: true, pushUrl: true });
    });
  }

  if (filterForm) {
    // Chặn submit mặc định: mọi thay đổi bộ lọc đều đi qua AJAX, không nạp lại trang.
    filterForm.addEventListener("submit", function (event) {
      event.preventDefault();
      refresh(buildUrl(url("action", filterForm)));
    });

    filterForm.addEventListener("change", function (event) {
      if (event.target === kySelect) {
        syncPeriodFields();
      }
      // Đổi bất kỳ ô lọc nào cũng cập nhật ngay, không cần bấm "Áp dụng".
      refresh(buildUrl(url("action", filterForm)));
    });
  }

  var resetLink = root.querySelector("[data-baocao-reset]");
  if (resetLink) {
    resetLink.addEventListener("click", function (event) {
      event.preventDefault();
      refresh(new URL(
        resetLink.getAttribute("href") || root.getAttribute("data-baocao-endpoint"),
        window.location.origin
      ), true);
    });
  }

  // Export and history use server-applied board filters, never pending form edits.
  function buildCurrentExportUrl(baseEndpoint) {
    var result = new URL(baseEndpoint, window.location.origin);
    var board = currentBoard();
    var data = board ? board.dataset : {};
    ["ky", "nam", "thang", "quy", "phongBan"].forEach(function (key) {
      if (data[key]) result.searchParams.set(key, data[key]);
    });
    if (activeTab) {
      result.searchParams.set("tab", activeTab);
    }
    return result.toString();
  }

  var exportButton = root.querySelector("[data-baocao-export]");
  if (exportButton) {
    exportButton.addEventListener("click", function () {
      var endpoint = root.getAttribute("data-baocao-export-endpoint") || "/BaoCao/Export";
      window.location.href = buildCurrentExportUrl(endpoint);
    });
  }

  var exportExcelButton = root.querySelector("[data-baocao-export-excel]");
  if (exportExcelButton) {
    exportExcelButton.addEventListener("click", function () {
      var endpoint = root.getAttribute("data-baocao-export-excel-endpoint") || "/BaoCao/ExportExcel";
      window.location.href = buildCurrentExportUrl(endpoint);
    });
  }

  var printButton = root.querySelector("[data-baocao-print]");
  if (printButton) {
    printButton.addEventListener("click", function () {
      var endpoint = root.getAttribute("data-baocao-print-endpoint") || "/BaoCao/Print";
      window.open(buildCurrentExportUrl(endpoint), "_blank");
    });
  }

  // ═══════════════════════════════════════════════════════════════════════
  //  DRILL-DOWN MODAL CHI TIẾT ĐỐI CHỨNG (Task 9)
  // ═══════════════════════════════════════════════════════════════════════
  function exportModalTableToCsv(container, drillType) {
    var table = container.querySelector("table");
    if (!table) return;
    var rows = table.querySelectorAll("tr");
    var csvContent = "\uFEFF"; // UTF-8 BOM
    Array.prototype.forEach.call(rows, function (row) {
      if (row.hidden) return;
      var cells = row.querySelectorAll("th, td");
      var line = [];
      Array.prototype.forEach.call(cells, function (cell) {
        var text = (cell.innerText || cell.textContent || "").trim();
        // Chống chèn công thức CSV Injection
        if (/^[=+@\-\t\r]/.test(text)) {
          text = "'" + text;
        }
        line.push('"' + text.replace(/"/g, '""') + '"');
      });
      if (line.length > 0) {
        csvContent += line.join(",") + "\r\n";
      }
    });

    var blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    var link = document.createElement("a");
    var filename = "BaoCaoChiTiet_" + drillType + "_" + new Date().toISOString().slice(0, 10) + ".csv";
    link.href = URL.createObjectURL(blob);
    link.setAttribute("download", filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  async function openDrillDown(drillType) {
    var modalEl = document.getElementById("baocaoDetailsModal");
    var contentEl = document.getElementById("baocaoDetailsContent");
    if (!modalEl || !contentEl) return;

    // Teleport modal ra trực tiếp document.body để không bị giữ trong stacking context
    // của .in-place-container (will-change: transform) nằm dưới .modal-backdrop.
    if (modalEl.parentElement !== document.body) {
      document.body.appendChild(modalEl);
    }

    contentEl.innerHTML = '<div class="p-5 text-center text-muted"><div class="spinner-border text-primary mb-3" role="status"></div><div class="fw-semibold">Đang nạp dữ liệu chi tiết...</div></div>';

    var modalInstance = null;
    if (window.bootstrap && window.bootstrap.Modal) {
      modalInstance = window.bootstrap.Modal.getOrCreateInstance(modalEl);
      modalInstance.show();
    } else {
      modalEl.classList.add("show");
      modalEl.style.display = "block";
    }

    try {
      var detailsEndpoint = root.getAttribute("data-baocao-details-endpoint") || "/BaoCao/Details";
      var fetchUrl = new URL(detailsEndpoint, window.location.origin);
      fetchUrl = new URL(buildCurrentExportUrl(detailsEndpoint));
      fetchUrl.searchParams.set("type", drillType);

      var resp = await fetch(fetchUrl.toString(), {
        headers: { "X-Requested-With": "XMLHttpRequest", Accept: "text/html" }
      });
      if (!resp.ok) {
        throw new Error("Máy chủ trả về mã " + resp.status);
      }
      var html = await resp.text();
      contentEl.innerHTML = html;

      var detailSearch = contentEl.querySelector("[data-baocao-details-search]");
      if (detailSearch) {
        detailSearch.addEventListener("input", function () {
          var query = detailSearch.value.trim().toLocaleLowerCase("vi");
          contentEl.querySelectorAll("tbody tr").forEach(function (row) {
            row.hidden = !row.textContent.toLocaleLowerCase("vi").includes(query);
          });
        });
      }
      var detailSwitch = contentEl.querySelector("[data-baocao-details-switch]");
      if (detailSwitch) detailSwitch.addEventListener("click", function () {
        openDrillDown(detailSwitch.getAttribute("data-baocao-details-switch"));
      });

      var modalExportBtn = contentEl.querySelector("[data-baocao-export-details]");
      if (modalExportBtn) {
        modalExportBtn.addEventListener("click", function () {
          exportModalTableToCsv(contentEl, drillType);
        });
      }
    } catch (err) {
      contentEl.innerHTML = '<div class="modal-header border-0 pb-0"><h5 class="modal-title fw-bold text-danger"><i class="bi bi-exclamation-triangle-fill me-2"></i>Không thể tải chi tiết</h5><button type="button" class="btn-close" data-bs-dismiss="modal"></button></div><div class="modal-body text-danger py-4">' + (err.message || "Đã xảy ra lỗi khi tải dữ liệu.") + '</div><div class="modal-footer border-0"><button type="button" class="btn btn-sm btn-outline-secondary" data-bs-dismiss="modal">Đóng</button></div>';
    }
  }

  // Bắt sự kiện click drill-down trên toàn bộ bảng báo cáo
  root.addEventListener("click", function (event) {
    var trigger = event.target.closest("[data-baocao-drill]");
    if (!trigger) return;
    event.preventDefault();
    var drillType = trigger.getAttribute("data-baocao-drill");
    if (drillType) {
      openDrillDown(drillType);
    }
  });

  // Nút quay lại / tiến của trình duyệt: nạp lại đúng vùng dữ liệu theo URL mới (không push duplicate).
  window.addEventListener("popstate", function () {
    refresh(new URL(window.location.href), false);
  });

  // Đổi kích thước cửa sổ: chỉ co giãn biểu đồ đang hiển thị.
  var resizeTimer = null;
  window.addEventListener("resize", function () {
    if (resizeTimer) window.clearTimeout(resizeTimer);
    resizeTimer = window.setTimeout(function () {
      var panel = visiblePanel();
      if (!panel) return;
      Array.prototype.forEach.call(panel.querySelectorAll(".baocao-chart"), function (el) {
        var instance = chartInstances.get(el.getAttribute("data-chart"));
        if (instance && !instance.isDisposed()) instance.resize();
      });
    }, 180);
  });

  // ═══════════════════════════════════════════════════════════════════════
  //  KHỞI ĐỘNG
  // ═══════════════════════════════════════════════════════════════════════
  function boot() {
    syncPeriodFields();
    chartData = readChartData();

    // Tab trên URL (?tab=) thắng localStorage để deep-link luôn mở đúng báo cáo;
    // URL không nêu tab thì mới nhớ lại lần xem trước.
    var available = availableTabs();
    var requested = normalizeTab(root.getAttribute("data-baocao-tab"));
    if (available.indexOf(requested) < 0) requested = normalizeTab(readStoredTab());
    if (available.indexOf(requested) < 0) requested = available[0] || "bien-dong";

    applyTab(requested, { persist: true, pushUrl: false });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot);
  } else {
    boot();
  }
})();
