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

  var AVATAR_COLORS = ['#1E3A8A', '#D97706', '#059669', '#7C3AED', '#DB2777', '#2563EB', '#0891B2'];

  function getAvatarColor(emp) {
    if (emp && emp.avatarColor && emp.avatarColor.toLowerCase() !== '#1e3a8a') {
      return emp.avatarColor;
    }
    var id = (emp && Number(emp.maNhanVien)) || 0;
    return AVATAR_COLORS[Math.abs(id) % AVATAR_COLORS.length];
  }

  // Render nội dung Agenda cho ngày được chọn từ dữ liệu có sẵn
  function renderAgendaForDate(dateStr) {
    var data = window.phancaCalendarData;
    if (!data || !data.days) return;

    var agendaPanel = document.getElementById('phancaAgendaPanel');
    if (!agendaPanel) return;

    var dateTitle = document.getElementById('agendaDateTitle');
    var dateSubtitle = document.getElementById('agendaDateSubtitle');
    var shiftsContainer = document.getElementById('agendaShiftsContainer');

    // Format ngày hiển thị tiếng Việt chuẩn không phụ thuộc múi giờ
    var parts = dateStr.split('-');
    var y = parseInt(parts[0], 10);
    var m = parseInt(parts[1], 10);
    var day = parseInt(parts[2], 10);
    var d = new Date(y, m - 1, day);
    var daysOfWeek = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
    var dayName = daysOfWeek[d.getDay()];
    var formattedDate = dayName + ', ' + String(day).padStart(2, '0') + '/' + String(m).padStart(2, '0') + '/' + y;

    if (dateTitle) dateTitle.textContent = formattedDate;

    var dayShifts = (data.shifts || []).filter(function (s) { return s.ngayLamViec === dateStr; });
    var totalStaff = dayShifts.length;

    if (dateSubtitle) {
      dateSubtitle.textContent = 'Tổng số: ' + totalStaff + ' lượt nhân sự trực trong ngày';
    }

    // Cập nhật nút + Thêm Ca Trực trên tiêu đề Agenda sang ngày mới
    var caList = data.caList || [];
    var addMainShiftBtn = document.querySelector('.phanca-agenda-header button[onclick*="openAddShiftModalForDate"]');
    var caMacDinh = (caList.length > 0 && caList[0].maCa) ? caList[0].maCa : 0;
    if (addMainShiftBtn) {
      addMainShiftBtn.setAttribute('onclick', 'openAddShiftModalForDate(\'' + dateStr + '\', ' + caMacDinh + ')');
    }

    if (!shiftsContainer) return;

    var html = '';
    var tongCaThieuTrongNgay = 0;
    var isSelectedDate = dateStr === data.selectedDate;
    caList.forEach(function (ca) {
      var empsInCa = dayShifts.filter(function (s) { return s.maCa === ca.maCa; });
      // Sắc thái lấy thẳng từ máy chủ (CaLamViec.SacThaiCa), không dò tên ca:
      // tên ca là dữ liệu người dùng sửa được, dò chuỗi thì ca mới rơi vào
      // nhánh mặc định và hiện màu ca đêm.
      var dotClass = 'phanca-shift-dot-' + (ca.sacThai || 'morning');
      var startCollapsed = isShiftCollapsed(ca.maCa, caList);

      // Kiểm tra đối chiếu định biên (PRD 3.5)
      var dinhBienList = [];
      var rules = data.dinhBienRules || [];
      rules.forEach(function (r) {
        var toiThieu = 0;
        if (isSelectedDate && r.mucNgayTheoCa && (r.mucNgayTheoCa[ca.maCa] !== undefined || r.mucNgayTheoCa[String(ca.maCa)] !== undefined)) {
          toiThieu = r.mucNgayTheoCa[ca.maCa] !== undefined ? r.mucNgayTheoCa[ca.maCa] : r.mucNgayTheoCa[String(ca.maCa)];
        } else if (r.mucNenTheoCa && (r.mucNenTheoCa[ca.maCa] !== undefined || r.mucNenTheoCa[String(ca.maCa)] !== undefined)) {
          toiThieu = r.mucNenTheoCa[ca.maCa] !== undefined ? r.mucNenTheoCa[ca.maCa] : r.mucNenTheoCa[String(ca.maCa)];
        }
        toiThieu = parseInt(toiThieu || '0', 10);

        if (toiThieu > 0) {
          var hienCo = empsInCa.filter(function (e) {
            if (e.trangThai === 'VANG_MAT') return false;
            var matchRole = Number(e.maChucVu) === Number(r.maChucVu);
            var matchDept = !r.maPhongBan || Number(e.maPhongBan) === Number(r.maPhongBan);
            return matchRole && matchDept;
          }).length;
          var conThieu = Math.max(0, toiThieu - hienCo);
          dinhBienList.push({
            tenChucVu: r.tenChucVu,
            tenPhongBan: r.tenPhongBan,
            soNguoiHienCo: hienCo,
            soNguoiToiThieu: toiThieu,
            conThieu: conThieu
          });
        }
      });
      var thieuList = dinhBienList.filter(function (d) { return d.conThieu > 0; });
      var tongConThieu = thieuList.reduce(function (sum, d) { return sum + d.conThieu; }, 0);
      if (tongConThieu > 0) {
        tongCaThieuTrongNgay++;
      }

      html += '<div class="phanca-agenda-shift-card' + (startCollapsed ? ' is-collapsed' : '') + '" data-ma-ca="' + ca.maCa + '">';
      html += '  <div class="phanca-agenda-shift-header">';
      html += '    <div class="phanca-agenda-shift-title phanca-agenda-shift-toggle" onclick="toggleAgendaShiftCard(this)" title="Bấm để thu gọn / mở rộng nhóm ca này">';
      html += '      <span class="' + dotClass + '"></span>';
      html += '      <span>' + escapeHtml(ca.tenCa) + '</span>';
      html += '      <span class="badge bg-light text-dark border ms-1" style="font-size:10px;">' + empsInCa.length + ' NV</span>';
      if (tongConThieu > 0) {
        html += '      <span class="badge bg-danger ms-1" style="font-size:10px;" title="Thiếu ' + tongConThieu + ' người so với định biên tối thiểu">';
        html += '        <i class="bi bi-exclamation-triangle-fill me-1"></i>Thiếu ' + tongConThieu;
        html += '      </span>';
      }
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
      if (thieuList.length > 0) {
        html += '    <div class="phanca-dinh-bien-warning">';
        html += '      <i class="bi bi-shield-exclamation"></i>';
        html += '      <span>';
        html += '        Thiếu định biên: ';
        thieuList.forEach(function (d) {
          var deptSuffix = d.tenPhongBan ? (' · ' + escapeHtml(d.tenPhongBan)) : '';
          html += '<span class="phanca-dinh-bien-item">' + escapeHtml(d.tenChucVu) + deptSuffix + ' ' + d.soNguoiHienCo + '/' + d.soNguoiToiThieu + '</span> ';
        });
        html += '      </span>';
        html += '    </div>';
      }
      if (empsInCa.length === 0) {
        html += '    <div class="phanca-agenda-empty-shift">Chưa có nhân sự được phân ca này.</div>';
      } else {
        empsInCa.forEach(function (emp) {
          var hasActiveContract = emp.hasActiveContract !== false;
          var itemClass = 'phanca-agenda-emp-item' + (!hasActiveContract ? ' has-issue' : '');

          html += '    <div class="' + itemClass + '" id="agenda-item-' + emp.maPhanCa + '">';
          html += '      <div class="phanca-agenda-emp-info">';
          html += '        <div class="phanca-agenda-avatar" style="background-color:' + getAvatarColor(emp) + ';">';
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

    // Cập nhật số ca thiếu trên badge nút "Định Biên Ca"
    var badgeTongThieu = document.getElementById('dinhBienTongThieuBadge');
    if (badgeTongThieu) {
      if (tongCaThieuTrongNgay > 0) {
        badgeTongThieu.textContent = tongCaThieuTrongNgay;
        badgeTongThieu.classList.remove('d-none');
        badgeTongThieu.setAttribute('title', 'Có ' + tongCaThieuTrongNgay + ' ca trong ngày đang thiếu người so với định biên');
      } else {
        badgeTongThieu.classList.add('d-none');
      }
    }
  }

  // Ca đầu tiên trong danh mục là ca mở sẵn; các ca còn lại gấp lại cho nhật ký
  // gọn gàng. Trước đây chỗ này khoá cứng maCa === 1 — đúng với bộ ba Sáng/Chiều/
  // Đêm nhưng sai ngay khi danh mục đổi thứ tự hoặc thêm ca mới.
  function isDefaultExpandedShift(maCa, caList) {
    if (!caList || caList.length === 0) return false;
    return Number(maCa) === Number(caList[0].maCa);
  }

  // Ghi nhớ ca nào người dùng đã tự thu gọn / mở rộng, để khi nhật ký render lại
  // (sau khi thêm hoặc hủy một ca trực) trạng thái không bị nhảy về mặc định.
  var agendaCollapseState = {};

  function isShiftCollapsed(maCa, caList) {
    var saved = agendaCollapseState[maCa];
    return saved === undefined ? !isDefaultExpandedShift(maCa, caList) : saved;
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

  // Chuyển đổi tab chế độ xem (Cuốn Lịch | Ma Trận | Danh Sách | Nhu Cầu Sự Kiện)
  function bindViewModeTabs() {
    window.switchCalendarViewTab = function (mode) {
      var calendarContainer = document.getElementById('viewCalendarContainer');
      var matrixContainer = document.getElementById('viewMatrixContainer');
      var listContainer = document.getElementById('viewListContainer');
      var eventDemandContainer = document.getElementById('viewEventDemandContainer');

      var tabCalendar = document.getElementById('tabViewCalendar');
      var tabMatrix = document.getElementById('tabViewMatrix');
      var tabList = document.getElementById('tabViewList');
      var tabEventDemand = document.getElementById('tabViewEventDemand');

      [tabCalendar, tabMatrix, tabList, tabEventDemand].forEach(function (tab) {
        if (tab) tab.classList.remove('active');
      });

      if (calendarContainer) calendarContainer.classList.add('d-none');
      if (matrixContainer) matrixContainer.classList.add('d-none');
      if (listContainer) listContainer.classList.add('d-none');
      if (eventDemandContainer) eventDemandContainer.classList.add('d-none');

      if (mode === 'matrix') {
        if (matrixContainer) matrixContainer.classList.remove('d-none');
        if (tabMatrix) tabMatrix.classList.add('active');
      } else if (mode === 'list') {
        if (listContainer) listContainer.classList.remove('d-none');
        if (tabList) tabList.classList.add('active');
      } else if (mode === 'eventDemand') {
        if (eventDemandContainer) eventDemandContainer.classList.remove('d-none');
        if (tabEventDemand) tabEventDemand.classList.add('active');
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
    const changeReason = prompt('Lý do hủy ca (tối đa 500 ký tự). Phân công sự kiện từ ca này cần xác nhận lại:');
    if (!changeReason?.trim() || changeReason.trim().length > 500) return;

    fetch('/PhanCa/QuickAssignAjax', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
      body: JSON.stringify({ maNhanVien: maNhanVien, ngayLamViec: ngayLamViec, maCa: 0, changeReason: changeReason.trim() })
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
