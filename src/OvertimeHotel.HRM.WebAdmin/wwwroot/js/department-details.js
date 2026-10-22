(() => {
  const modal = document.getElementById("departmentDetailsModal");
  if (!modal) return;
  const body = modal.querySelector("[data-department-details-body]");
  let request;

  const message = (container, text, retry) => {
    const paragraph = document.createElement("p");
    paragraph.className = "department-message mb-0";
    paragraph.textContent = text;
    container.replaceChildren(paragraph);
    if (retry) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "btn btn-sm btn-outline-primary mt-2";
      button.textContent = "Thử lại";
      button.addEventListener("click", retry);
      container.append(button);
    }
  };

  const fetchContent = async (url, accept, signal) => {
    const response = await fetch(url, {
      headers: { "X-Requested-With": "XMLHttpRequest", Accept: accept },
      cache: "no-store",
      signal,
    });
    if (response.redirected || response.status === 401 || response.status === 403) {
      throw new Error("Phiên đăng nhập đã hết hạn hoặc bạn không có quyền xem dữ liệu này.");
    }
    if (response.status === 404) throw new Error("Phòng ban hoặc chức vụ không còn tồn tại. Vui lòng tải lại trang.");
    if (!response.ok || !response.headers.get("Content-Type")?.includes(accept)) {
      throw new Error("Không thể tải dữ liệu. Vui lòng thử lại.");
    }
    return response;
  };

  const loadDetails = async (url) => {
    request?.abort();
    const controller = new AbortController();
    request = controller;
    body.setAttribute("aria-busy", "true");
    message(body, "Đang tải chi tiết phòng ban...");
    try {
      const response = await fetchContent(url, "text/html", controller.signal);
      const html = await response.text();
      if (controller.signal.aborted) return;
      const content = new DOMParser().parseFromString(html, "text/html").querySelector("[data-department-content]");
      if (!content) throw new Error("Không thể tải chi tiết phòng ban. Vui lòng thử lại.");
      body.replaceChildren(content);
    } catch (error) {
      if (error.name !== "AbortError") message(body, error.message, () => loadDetails(url));
    } finally {
      if (request === controller) body.removeAttribute("aria-busy");
    }
  };

  const loadEmployees = async (panel) => {
    if (panel.dataset.loaded || panel.getAttribute("aria-busy") === "true") return;
    const list = panel.querySelector("[data-employee-list]");
    if (panel.dataset.canViewEmployees !== "true") {
      message(list, "Bạn không có quyền xem danh sách nhân viên.");
      return;
    }
    const signal = request.signal;
    panel.setAttribute("aria-busy", "true");
    message(list, "Đang tải nhân viên...");
    try {
      const response = await fetchContent(panel.dataset.employeesUrl, "application/json", signal);
      const employees = await response.json();
      if (signal.aborted) return;
      if (!Array.isArray(employees)) throw new Error("Không thể tải danh sách nhân viên.");
      if (employees.length === 0) {
        message(list, "Chưa có nhân viên đang làm việc ở chức vụ này trong phòng ban.");
      } else {
        const items = document.createElement("ul");
        items.className = "department-employees";
        employees.forEach((employee) => {
          const item = document.createElement("li");
          item.className = "department-employee";
          const avatar = document.createElement("span");
          avatar.className = "department-avatar";
          avatar.setAttribute("aria-hidden", "true");
          avatar.textContent = employee.name.trim().split(/\s+/u).slice(-2).map(part => Array.from(part)[0] || "").join("").toLocaleUpperCase("vi");
          const name = document.createElement("span");
          name.className = "department-employee-name";
          name.textContent = employee.name;
          const code = document.createElement("span");
          code.className = "department-employee-code";
          code.textContent = `NV-${String(employee.id).padStart(3, "0")}`;
          item.append(avatar, name, code);
          items.append(item);
        });
        list.replaceChildren(items);
      }
      panel.dataset.loaded = "true";
    } catch (error) {
      if (error.name !== "AbortError") message(list, error.message, () => loadEmployees(panel));
    } finally {
      panel.removeAttribute("aria-busy");
    }
  };

  modal.addEventListener("show.bs.modal", (event) => {
    const url = event.relatedTarget?.dataset.departmentDetails;
    if (url) void loadDetails(url);
  });
  modal.addEventListener("hide.bs.modal", () => request?.abort());
  body.addEventListener("click", (event) => {
    const toggle = event.target.closest("[data-position-toggle]");
    if (!toggle) return;
    const panel = document.getElementById(toggle.getAttribute("aria-controls"));
    const expanded = toggle.getAttribute("aria-expanded") !== "true";
    toggle.setAttribute("aria-expanded", String(expanded));
    panel.hidden = !expanded;
    toggle.querySelector("i").classList.toggle("bi-chevron-up", expanded);
    toggle.querySelector("i").classList.toggle("bi-chevron-down", !expanded);
    if (expanded) void loadEmployees(panel);
  });
})();
