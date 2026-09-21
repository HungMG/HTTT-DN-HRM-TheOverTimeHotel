/**
 * The OverTime Hotel HRM System · UI Motion & PowerPoint Slide Transitions
 * Chuyển thể hiệu ứng chuyển động mượt mà, cảm giác nhấn tactile đầm tay
 * và hiệu ứng các khung bảng nhảy vào nhẹ nhàng kèm chữ BlurText từ mờ mờ hiện rõ ra.
 * Tinh chỉnh chuẩn phong cách Luxury Hotel, không phụ thuộc thư viện ngoài, 60 FPS.
 */

(function () {
  'use strict';

  // -------------------------------------------------------------
  // 1. THANH TIẾN TRÌNH VÀNG HOÀNG GIA & CHUYỂN TRANG MƯỢT (SPA FEEL)
  // -------------------------------------------------------------
  let progressBar = document.getElementById('hotel-page-progress');
  if (!progressBar) {
    progressBar = document.createElement('div');
    progressBar.id = 'hotel-page-progress';
    document.body.prepend(progressBar);
  }

  // Khi tải trang xong: lướt đầy 100% rồi ẩn nhẹ
  window.addEventListener('DOMContentLoaded', function () {
    if (progressBar) {
      progressBar.classList.add('active');
      progressBar.style.width = '100%';
      setTimeout(function () {
        progressBar.style.opacity = '0';
        setTimeout(function () {
          progressBar.classList.remove('active');
          progressBar.style.width = '0%';
        }, 220);
      }, 200);
    }
  });

  // Hỗ trợ BFCache (khi user bấm nút Back/Forward của trình duyệt)
  window.addEventListener('pageshow', function (event) {
    const main = document.querySelector('main[role="main"]') || document.body;
    main.classList.remove('hotel-page-leaving');
    if (progressBar) {
      progressBar.classList.remove('active');
      progressBar.style.opacity = '0';
      progressBar.style.width = '0%';
    }
  });

  // Bắt sự kiện click vào các liên kết nội bộ để tạo hiệu ứng chuyển slide êm
  document.addEventListener('click', function (e) {
    if (e.defaultPrevented) return;
    const link = e.target.closest('a');
    if (!link) return;

    const href = link.getAttribute('href');
    if (!href) return;

    // Bỏ qua các link đặc biệt
    if (
      href.startsWith('#') ||
      href.startsWith('javascript:') ||
      href.startsWith('mailto:') ||
      href.startsWith('tel:') ||
      link.target === '_blank' ||
      link.hasAttribute('download') ||
      link.hasAttribute('data-catalog-reset') ||
      link.hasAttribute('data-catalog-modal') ||
      link.hasAttribute('data-bs-toggle') ||
      link.hasAttribute('data-catalog-export') ||
      link.hasAttribute('data-catalog-print') ||
      e.ctrlKey || e.metaKey || e.shiftKey || e.altKey
    ) {
      return;
    }

    // Kiểm tra liên kết cùng domain
    const targetUrl = new URL(link.href, window.location.origin);
    if (targetUrl.origin !== window.location.origin) return;

    // Bỏ qua nếu nhấn lại đúng URL hiện tại
    if (targetUrl.pathname === window.location.pathname && targetUrl.search === window.location.search) {
      return;
    }

    // Kích hoạt hiệu ứng chuyển slide
    e.preventDefault();

    if (progressBar) {
      progressBar.style.opacity = '1';
      progressBar.classList.add('active');
      progressBar.style.width = '65%';
    }

    const main = document.querySelector('main[role="main"]') || document.body;
    main.classList.add('hotel-page-leaving');

    // Các khung bảng lướt nhẹ ra sau trong 180ms trước khi đổi trang
    setTimeout(function () {
      if (progressBar) {
        progressBar.style.width = '95%';
      }
      window.location.href = link.href;
    }, 180);
  });

  // -------------------------------------------------------------
  // 2. HIỆU ỨNG BLURTEXT: CHỮ TRƯỢT TỪ MỜ LÊN HIỆN RÕ SẮC NÉT (TỪ REACT-BITS)
  // -------------------------------------------------------------
  function applyBlurTextEffect(container) {
    const main = container || document.querySelector('main[role="main"]') || document.body;
    if (!main) return;

    // 1. Tiêu đề lớn: Tách từng từ lướt mờ -> rõ
    const heroTitles = main.querySelectorAll(
      'h1, .display-6, .display-5, .account-command-copy h1, .inspector-card-heading h2, .account-audit-title h2'
    );
    heroTitles.forEach(function (title) {
      if (title.getAttribute('data-blur-text-ready')) return;
      title.setAttribute('data-blur-text-ready', 'true');

      const childNodes = Array.from(title.childNodes);
      title.innerHTML = '';
      let wordIndex = 0;

      childNodes.forEach(function (node) {
        if (node.nodeType === Node.TEXT_NODE) {
          const words = node.textContent.split(/(\s+)/);
          words.forEach(function (w) {
            if (w.trim().length > 0) {
              const span = document.createElement('span');
              span.className = 'hotel-blur-word';
              span.style.animationDelay = (0.04 + wordIndex * 0.04).toFixed(2) + 's';
              span.textContent = w;
              title.appendChild(span);
              wordIndex++;
            } else if (w.length > 0) {
              title.appendChild(document.createTextNode(w));
            }
          });
        } else {
          const wrapper = document.createElement('span');
          wrapper.className = 'hotel-blur-word';
          wrapper.style.animationDelay = (0.04 + wordIndex * 0.04).toFixed(2) + 's';
          wrapper.appendChild(node.cloneNode(true));
          title.appendChild(wrapper);
          wordIndex++;
        }
      });
    });

    // 2. Các đoạn mô tả, phụ đề: Lướt từ mờ lên rõ
    const subtitles = main.querySelectorAll(
      '.hero-luxury-banner p, .lead, .kpi-card .small, .account-command-copy p, .account-eyebrow, .account-audit-title small, .inspector-card-heading small, .account-panel-heading span, .identity-summary-copy small'
    );
    subtitles.forEach(function (el, idx) {
      if (el.getAttribute('data-blur-text-ready')) return;
      el.setAttribute('data-blur-text-ready', 'true');
      el.classList.add('hotel-blur-text');
      el.style.animationDelay = (0.12 + Math.min(idx * 0.03, 0.35)).toFixed(2) + 's';
    });

    // 3. Các con số thống kê Dashboard & Quản trị: Số lướt từ mờ lên rõ sắc nét
    const statsValues = main.querySelectorAll(
      '.kpi-card h2, .kpi-card .fs-3, .kpi-card .display-6, .motion-stat-value, .account-audit-count, .account-environment-pill strong'
    );
    statsValues.forEach(function (val, idx) {
      if (val.getAttribute('data-blur-text-ready')) return;
      val.setAttribute('data-blur-text-ready', 'true');
      val.classList.add('hotel-blur-text');
      val.style.animationDelay = (0.08 + idx * 0.05).toFixed(2) + 's';
    });
  }

  // -------------------------------------------------------------
  // 3. HIỆU ỨNG KHUNG BẢNG LƯỚT VÀO NHẸ NHÀNG, ĐẦM TAY (GENTLE LUXURY FLOAT)
  // -------------------------------------------------------------
  function applyPowerPointEntrance(container) {
    const main = container || document.querySelector('main[role="main"]') || document.body;
    if (!main) return;

    // 1. Banner trên cùng: Thả lướt êm ái từ trên xuống
    const banners = main.querySelectorAll('.hero-luxury-banner, .catalog-hero, .account-command-bar');
    banners.forEach(function (banner, idx) {
      banner.classList.add('pp-banner-anim');
      banner.style.animationDelay = (idx * 0.06) + 's';
    });

    // 2. Các ô KPI cards, module cards, khung quản trị: Lướt nhẹ từ dưới lên theo nhịp so le
    const boxes = main.querySelectorAll(
      '.kpi-card, .module-card, .card:not(.hero-luxury-banner), .shift-timeline-item, .motion-stat, .inspector-card, .account-outline-panel, .account-preview-panel, .account-audit-panel'
    );
    boxes.forEach(function (box, idx) {
      box.classList.add('pp-box-anim');
      const delay = 0.05 + Math.min(idx * 0.05, 0.4);
      box.style.animationDelay = delay.toFixed(2) + 's';
    });

    // 3. Các bảng dữ liệu (Data Table, Grid, Form sections): Trượt lướt êm ái
    const tables = main.querySelectorAll(
      '.table-responsive, table, .catalog-table-container, form:not(.account-toggle-form), .nav-pills, .account-catalog-stage, .account-table-shell'
    );
    tables.forEach(function (tbl, idx) {
      tbl.classList.add('pp-table-anim');
      const delay = 0.12 + Math.min(idx * 0.06, 0.35);
      tbl.style.animationDelay = delay.toFixed(2) + 's';
    });

    // 4. Các dòng nhật ký quản trị: Trượt vào so le nhịp nhàng
    const auditItems = main.querySelectorAll('.account-audit-item:not(.is-new)');
    auditItems.forEach(function (item, idx) {
      item.classList.add('pp-box-anim');
      const delay = 0.15 + Math.min(idx * 0.04, 0.45);
      item.style.animationDelay = delay.toFixed(2) + 's';
    });

    // 5. Kích hoạt hiệu ứng BlurText cho chữ
    applyBlurTextEffect(main);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => applyPowerPointEntrance());
  } else {
    applyPowerPointEntrance();
  }

  // -------------------------------------------------------------
  // 4. HIỆU ỨNG NHẤN CHUỘT SLOW-MO & GỢN SÓNG ÁNH SÁNG (TACTILE RIPPLE)
  // -------------------------------------------------------------
  function triggerSlowMoRipple(e) {
    const target = e.target.closest(
      '.btn, .btn-slowmo, .nav-pills .nav-link, .dropdown-item, .module-card, .kpi-card, ' +
      '.account-primary-action, .account-icon-action, .account-row-actions a, .account-row-actions button, ' +
      '.account-filter-actions button, .account-filter-actions a, .account-form-actions button, .account-form-actions a, ' +
      '.account-confirm-actions button, .role-legend-item, .password-visibility, .account-audit-tools button, ' +
      '.account-audit-clear, .account-audit-change summary, .motion-stat, .account-popup button'
    );
    if (!target) return;

    // Đảm bảo phần tử cha có position để ripple định vị chuẩn
    const style = window.getComputedStyle(target);
    if (style.position === 'static') {
      target.style.position = 'relative';
    }

    const rect = target.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height) * 1.35;
    const x = e.clientX - rect.left - size / 2;
    const y = e.clientY - rect.top - size / 2;

    const ripple = document.createElement('span');
    ripple.className = 'hotel-slowmo-ripple';
    ripple.style.width = size + 'px';
    ripple.style.height = size + 'px';
    ripple.style.left = x + 'px';
    ripple.style.top = y + 'px';

    target.appendChild(ripple);

    // Tự động dọn dẹp sau khi animation kết thúc (620ms)
    setTimeout(function () {
      if (ripple.parentNode) {
        ripple.parentNode.removeChild(ripple);
      }
    }, 650);
  }

  // Bắt sự kiện pointerdown để phản hồi tức thì với tốc độ khung hình cao
  document.addEventListener('pointerdown', triggerSlowMoRipple, { passive: true });

  // -------------------------------------------------------------
  // 5. XUẤT API GLOBAL CHO CÁC MODULE KHÁC DÙNG CHUNG (TSK-13, TSK-16...)
  // -------------------------------------------------------------
  window.HotelMotion = {
    applyEntrance: applyPowerPointEntrance,
    applyBlurText: applyBlurTextEffect,
    triggerRipple: triggerSlowMoRipple
  };

})();
