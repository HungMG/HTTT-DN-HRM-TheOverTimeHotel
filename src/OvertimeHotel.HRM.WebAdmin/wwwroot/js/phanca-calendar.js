/**
 * PhanCa Calendar Notebook & Agenda Management (5-Star Hotel HRM)
 * Xử lý tương tác Cuốn Lịch Phân Ca 24/7 và Agenda theo phong cách Sự Kiện
 */

(function () {
  'use strict';

  // Khởi tạo và liên kết sự kiện khi DOM sẵn sàng
  document.addEventListener('DOMContentLoaded', function () {
    bindCalendarDayClicks();
    bindMonthNavigation();
    bindViewModeTabs();
  });

  // Chọn ngày trên cuốn lịch và cập nhật Agenda bên phải
  window.selectCalendarDay = function (dateStr) {
    if (!dateStr) return;

    var cell = document.querySelector('.phanca-day-cell[data-date="' + dateStr + '"]');
    if (!cell) return;

    // Nếu click vào ngày thuộc tháng khác, chuyển tháng In-Place
    if (cell.classList.contains('is-other-month')) {
      var parts = dateStr.split('-');
      if (parts.length === 3) {
        var y = parseInt(parts[0], 10);
        var m = parseInt(parts[1], 10);
        loadCalendarMonthInPlace(y, m, dateStr);
        return;
      }
    }

    // Cập nhật trạng thái active trên lưới lịch
    document.querySelectorAll('.phanca-day-cell.is-selected').forEach(function (el) {
      el.classList.remove('is-selected');
    });
    cell.classList.add('is-selected');

    // Cập nhật URL qua history.pushState để có thể copy link mà không reload trang
    var currentUrl = new URL(window.location.href);
    currentUrl.searchParams.set('selectedDate', dateStr);
    currentUrl.searchParams.set('viewMode', 'calendar');
    window.history.pushState({ selectedDate: dateStr }, '', currentUrl.toString());

    // Cập nhật Agenda bên phải
    renderAgendaForDate(dateStr);
  };

  // Render nội dung Agenda cho ngày được chọn từ dữ liệu có sẵn
  function renderAgendaForDate(dateStr) {
    var data = window.phancaCalendarData;
    if (!data || !data.days) return;

    var agendaPanel = document.getElementById('phancaAgendaPanel');
    if (!agendaPanel) return;

    var dateTitle = document.getElementById('agendaDateTitle');
    var dateSubtitle = document.getElementById('agendaDateSubtitle');
    var shiftsContainer = document.getElementById('agendaShiftsContainer');

    // Format ngày hiển thị tiếng Việt
    var d = new Date(dateStr + 'T00:00:00');
    var daysOfWeek = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
    var dayName = daysOfWeek[d.getDay()];
    var formattedDate = dayName + ', ' + String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0') + '/' + d.getFullYear();

    if (dateTitle) dateTitle.textContent = formattedDate;

    var dayShifts = (data.shifts || []).filter(function (s) { return s.ngayLamViec === dateStr; });
    var totalStaff = dayShifts.length;

    if (dateSubtitle) {
      dateSubtitle.textContent = 'Tổng số: ' + totalStaff + ' lượt nhân sự trực trong ngày';
    }

    if (!shiftsContainer) return;

    // Nhóm ca trực
    var caList = data.caList || [
      { maCa: 1, tenCa: 'Ca Sáng (06:00 - 14:00)', gio: '06:00 - 14:00', badgeClass: 'morning' },
      { maCa: 2, tenCa: 'Ca Chiều (14:00 - 22:00)', gio: '14:00 - 22:00', badgeClass: 'afternoon' },
      { maCa: 3, tenCa: 'Ca Đêm (22:00 - 06:00)', gio: '22:00 - 06:00', badgeClass: 'night' }
    ];

    var html = '';
    caList.forEach(function (ca) {
      var empsInCa = dayShifts.filter(function (s) { return s.maCa === ca.maCa; });
      var dotClass = 'phanca-shift-dot-' + (ca.badgeClass || (ca.tenCa.indexOf('Sáng') >= 0 ? 'morning' : (ca.tenCa.indexOf('Chiều') >= 0 ? 'afternoon' : 'night')));
      // Mặc định chỉ mở Ca Sáng, các ca còn lại gấp lại cho nhật ký gọn gàng.
      var startCollapsed = isShiftCollapsed(ca.maCa);

      html += '<div class="phanca-agenda-shift-card' + (startCollapsed ? ' is-collapsed' : '') + '" data-ma-ca="' + ca.maCa + '">';
      html += '  <div class="phanca-agenda-shift-header">';
      html += '    <div class="phanca-agenda-shift-title phanca-agenda-shift-toggle" onclick="toggleAgendaShiftCard(this)" title="Bấm để thu gọn / mở rộng nhóm ca này">';
      html += '      <span class="' + dotClass + '"></span>';
      html += '      <span>' + escapeHtml(ca.tenCa) + '</span>';
      html += '      <span class="badge bg-light text-dark border ms-1" style="font-size:10px;">' + empsInCa.length + ' NV</span>';
      html += '    </div>';
      html += '    <div class="phanca-agenda-shift-actions">';
      html += '      <button type="button" class="phanca-btn-add-to-shift" onclick="openAddShiftModalForDate(\'' + dateStr + '\', ' + ca.maCa + ')">';
      html += '        <i class="bi bi-plus-lg me-1"></i>Thêm NV';
      html += '      </button>';
      html += '      <button type="button" class="phanca-agenda-collapse-btn" onclick="toggleAgendaShiftCard(this)"';
      html += '              title="' + (startCollapsed ? 'Mở rộng' : 'Thu gọn') + ' nhóm ca này" aria-expanded="' + (startCollapsed ? 'false' : 'true') + '">';
      html += '        <i class="bi bi-chevron-down"></i>';
      html += '      </button>';
      html += '    </div>';
      html += '  </div>';

      html += '  <div class="phanca-agenda-emp-list">';
      if (empsInCa.length === 0) {
        html += '    <div class="phanca-agenda-empty-shift">Chưa có nhân sự được phân ca này.</div>';
      } else {
        empsInCa.forEach(function (emp) {
          var hasActiveContract = emp.hasActiveContract !== false;
          var itemClass = 'phanca-agenda-emp-item' + (!hasActiveContract ? ' has-issue' : '');

          html += '    <div class="' + itemClass + '" id="agenda-item-' + emp.maPhanCa + '">';
          html += '      <div class="phanca-agenda-emp-info">';
          html += '        <div class="phanca-agenda-avatar" style="background-color:' + (emp.avatarColor || '#1e3a8a') + ';">';
          html += '          ' + escapeHtml((emp.hoTen || 'NV').slice(-1));
          html += '        </div>';
          html += '        <div>';
          html += '          <div class="phanca-agenda-emp-name">';
          html += '            ' + escapeHtml(emp.hoTen || 'Nhân viên');
          if (!hasActiveContract) {
            html += '            <span class="badge bg-danger ms-1" style="font-size:9px;" title="' + escapeHtml(emp.disabledReason || 'Hết hạn hợp đồng') + '">Hết hạn HĐ</span>';
          }
          html += '          </div>';
          html += '          <div class="phanca-agenda-emp-role">' + escapeHtml(emp.tenChucVu || '') + ' · ' + escapeHtml(emp.tenPhongBan || '') + '</div>';
          html += '        </div>';
          html += '      </div>';
          html += '      <button type="button" class="phanca-agenda-btn-remove" title="Hủy ca trực của nhân viên này" onclick="removeShiftFromAgenda(' + emp.maPhanCa + ', ' + emp.maNhanVien + ', \'' + dateStr + '\')">';
          html += '        <i class="bi bi-x-lg"></i>';
          html += '      </button>';
          html += '    </div>';
        });
      }
      html += '  </div>';
      html += '</div>';
    });

    shiftsContainer.innerHTML = html;
  }

  // Ca Sáng (maCa = 1) là ca duy nhất mở sẵn; Ca Chiều và Ca Đêm gấp lại.
  function isDefaultExpandedShift(maCa) {
    return Number(maCa) === 1;
  }

  // Ghi nhớ ca nào người dùng đã tự thu gọn / mở rộng, để khi nhật ký render lại
  // (sau khi thêm hoặc hủy một ca trực) trạng thái không bị nhảy về mặc định.
  var agendaCollapseState = {};

  function isShiftCollapsed(maCa) {
    var saved = agendaCollapseState[maCa];
    return saved === undefined ? !isDefaultExpandedShift(maCa) : saved;
  }

  // Thu gọn / mở rộng một nhóm ca trong nhật ký. Chỉ ẩn hiện danh sách nhân sự
  // ngay tại thẻ đó (in-place), không nạp lại trang, không mất ngữ cảnh cuộn.
  window.toggleAgendaShiftCard = function (trigger) {
    if (!trigger) return;
    var card = trigger.closest('.phanca-agenda-shift-card');
    if (!card) return;

    var collapsed = card.classList.toggle('is-collapsed');
    var maCa = card.getAttribute('data-ma-ca');
    if (maCa) agendaCollapseState[maCa] = collapsed;

    var btn = card.querySelector('.phanca-agenda-collapse-btn');
    if (btn) {
      btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
      btn.setAttribute('title', (collapsed ? 'Mở rộng' : 'Thu gọn') + ' nhóm ca này');
      var icon = btn.querySelector('.bi');
      if (icon) {
        icon.className = 'bi ' + (collapsed ? 'bi-chevron-down' : 'bi-chevron-up');
      }
    }
  };

  // Gán sự kiện click vào các ô ngày trên lịch
  function bindCalendarDayClicks() {
    document.querySelectorAll('.phanca-day-cell').forEach(function (cell) {
      cell.addEventListener('click', function () {
        var dateStr = cell.getAttribute('data-date');
        if (dateStr) {
          selectCalendarDay(dateStr);
        }
      });
    });
  }

  // Nạp tháng In-Place không làm tải lại trang
  window.loadCalendarMonthInPlace = function (year, month, selectedDate) {
    var overlay = document.getElementById('workspaceLoadingOverlay');
    if (overlay) overlay.classList.remove('d-none');

    // Tháng mới do server render lại nhật ký ở trạng thái mặc định (chỉ mở Ca Sáng),
    // nên xóa trạng thái thu/mở đã nhớ để hai bên không lệch nhau.
    agendaCollapseState = {};

    var url = '/PhanCa?year=' + year + '&month=' + month;
    if (selectedDate) url += '&selectedDate=' + selectedDate;
    url += '&viewMode=calendar';

    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function (res) { return res.text(); })
      .then(function (html) {
        var parser = new DOMParser();
        var doc = parser.parseFromString(html, 'text/html');

        var newSchedule = doc.getElementById('scheduleContentWrapper');
        var curSchedule = document.getElementById('scheduleContentWrapper');

        if (newSchedule && curSchedule) {
          curSchedule.innerHTML = newSchedule.innerHTML;
        }

        // Cập nhật script data
        var newScript = doc.getElementById('phancaCalendarDataScript');
        if (newScript) {
          eval(newScript.textContent);
        }

        // Đồng bộ thanh công cụ nằm NGOÀI vùng nạp cục bộ (nhãn tháng + mũi tên trước/sau),
        // nếu không thanh công cụ vẫn chỉ Tháng 10 trong khi cuốn lịch đã sang Tháng 11.
        syncMonthToolbar(year, month);

        window.history.pushState({ year: year, month: month }, '', url);
        bindCalendarDayClicks();
      })
      .catch(function () {
        window.location.href = url;
      })
      .finally(function () {
        if (overlay) overlay.classList.add('d-none');
      });
  };

  // Thanh công cụ tháng nằm ngoài #scheduleContentWrapper nên không tự cập nhật khi nạp cục bộ.
  // Hàm này giữ nhãn tháng và mũi tên trước/sau khớp với tháng vừa nạp.
  function syncMonthToolbar(year, month) {
    var label = document.getElementById('calMonthLabel');
    if (label) label.textContent = 'Tháng ' + month + ' / ' + year;

    var prev = new Date(year, month - 2, 1);
    var next = new Date(year, month, 1);

    var prevBtn = document.getElementById('btnCalPrevMonth');
    if (prevBtn) {
      prevBtn.setAttribute('data-year', String(prev.getFullYear()));
      prevBtn.setAttribute('data-month', String(prev.getMonth() + 1));
    }

    var nextBtn = document.getElementById('btnCalNextMonth');
    if (nextBtn) {
      nextBtn.setAttribute('data-year', String(next.getFullYear()));
      nextBtn.setAttribute('data-month', String(next.getMonth() + 1));
    }
  }

  // Điều hướng tháng
  function bindMonthNavigation() {
    var prevBtn = document.getElementById('btnCalPrevMonth');
    var nextBtn = document.getElementById('btnCalNextMonth');
    var todayBtn = document.getElementById('btnCalToday');

    if (prevBtn) {
      prevBtn.addEventListener('click', function (e) {
        e.preventDefault();
        var y = parseInt(prevBtn.getAttribute('data-year'), 10);
        var m = parseInt(prevBtn.getAttribute('data-month'), 10);
        loadCalendarMonthInPlace(y, m);
      });
    }

    if (nextBtn) {
      nextBtn.addEventListener('click', function (e) {
        e.preventDefault();
        var y = parseInt(nextBtn.getAttribute('data-year'), 10);
        var m = parseInt(nextBtn.getAttribute('data-month'), 10);
        loadCalendarMonthInPlace(y, m);
      });
    }

    if (todayBtn) {
      todayBtn.addEventListener('click', function (e) {
        e.preventDefault();
        var today = new Date();
        var todayStr = today.toISOString().split('T')[0];
        loadCalendarMonthInPlace(today.getFullYear(), today.getMonth() + 1, todayStr);
      });
    }
  }

  // Chuyển đổi tab chế độ xem (Cuốn Lịch | Ma Trận | Danh Sách)
  function bindViewModeTabs() {
    window.switchCalendarViewTab = function (mode) {
      var calendarContainer = document.getElementById('viewCalendarContainer');
      var matrixContainer = document.getElementById('viewMatrixContainer');
      var listContainer = document.getElementById('viewListContainer');

      var tabCalendar = document.getElementById('tabViewCalendar');
      var tabMatrix = document.getElementById('tabViewMatrix');
      var tabList = document.getElementById('tabViewList');

      [tabCalendar, tabMatrix, tabList].forEach(function (tab) {
        if (tab) tab.classList.remove('active');
      });

      if (calendarContainer) calendarContainer.classList.add('d-none');
      if (matrixContainer) matrixContainer.classList.add('d-none');
      if (listContainer) listContainer.classList.add('d-none');

      if (mode === 'matrix') {
        if (matrixContainer) matrixContainer.classList.remove('d-none');
        if (tabMatrix) tabMatrix.classList.add('active');
      } else if (mode === 'list') {
        if (listContainer) listContainer.classList.remove('d-none');
        if (tabList) tabList.classList.add('active');
      } else {
        if (calendarContainer) calendarContainer.classList.remove('d-none');
        if (tabCalendar) tabCalendar.classList.add('active');
      }

      localStorage.setItem('overtime_phanca_active_tab', mode);
    };

    var savedTab = localStorage.getItem('overtime_phanca_active_tab') || 'calendar';
    if (savedTab !== 'calendar') {
      window.switchCalendarViewTab(savedTab);
    }
  }

  // Xóa ca trực trực tiếp trên Agenda
  window.removeShiftFromAgenda = function (maPhanCa, maNhanVien, ngayLamViec) {
    if (!confirm('Bạn có chắc chắn muốn hủy ca trực này?')) return;

    fetch('/PhanCa/QuickAssignAjax', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ maNhanVien: maNhanVien, ngayLamViec: ngayLamViec, maCa: 0 })
    })
      .then(function (res) { return res.json(); })
      .then(function (res) {
        if (res.success) {
          // Xóa khỏi danh sách shifts trong memory
          if (window.phancaCalendarData && window.phancaCalendarData.shifts) {
            window.phancaCalendarData.shifts = window.phancaCalendarData.shifts.filter(function (s) {
              return s.maPhanCa !== maPhanCa;
            });
          }

          // Cập nhật lại Agenda
          renderAgendaForDate(ngayLamViec);

          // Cập nhật lại chip trên ô lịch
          updateCalendarCellBadge(ngayLamViec);

          if (typeof showQuickToast === 'function') {
            showQuickToast('Đã hủy ca trực thành công.');
          }
        } else {
          alert(res.message || 'Không thể hủy ca trực.');
        }
      })
      .catch(function () {
        alert('Có lỗi xảy ra khi hủy ca trực.');
      });
  };

  // Mở modal thêm nhân viên vào ca
  window.openAddShiftModalForDate = function (dateStr, maCa) {
    if (typeof openQuickAssignModal === 'function') {
      openQuickAssignModal(null, dateStr, maCa);
    } else {
      var modalEl = document.getElementById('modalCreatePhanCa');
      if (modalEl) {
        var modal = bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl);
        var inputDate = document.getElementById('inputNgayLamViecModal');
        var inputCa = document.getElementById('inputMaCaModal');
        if (inputDate) inputDate.value = dateStr;
        if (inputCa) inputCa.value = maCa;
        modal.show();
      }
    }
  };

  function updateCalendarCellBadge(dateStr) {
    var cell = document.querySelector('.phanca-day-cell[data-date="' + dateStr + '"]');
    if (!cell || !window.phancaCalendarData) return;

    var dayShifts = (window.phancaCalendarData.shifts || []).filter(function (s) { return s.ngayLamViec === dateStr; });
    var totalBadge = cell.querySelector('.phanca-day-badge-total');
    if (totalBadge) {
      totalBadge.textContent = dayShifts.length > 0 ? (dayShifts.length + ' ca') : '';
    }
  }

  function escapeHtml(text) {
    if (!text) return '';
    return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;');
  }

})();
