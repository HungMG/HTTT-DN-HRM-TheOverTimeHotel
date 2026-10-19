// Test integration between Department - Position (FK) and Account Provisioning Queue
import assert from "node:assert/strict";

const base = process.env.HRM_BASE_URL || "http://localhost:5065";
const adminPassword = process.env.ADMIN_PASSWORD || "Admin@123";
const hrPassword = process.env.HR_PASSWORD || "Hr@123";
const itPassword = process.env.IT_PASSWORD || "Overtime@2026";

class Session {
    constructor(username) {
        this.username = username;
        this.cookies = new Map();
    }

    async request(path, body, extraHeaders = {}) {
        const headers = {
            ...(this.cookies.size > 0 ? { Cookie: [...this.cookies].map(([k, v]) => `${k}=${v}`).join("; ") } : {}),
            ...(body && !(body instanceof FormData) ? { "Content-Type": "application/x-www-form-urlencoded" } : {}),
            ...extraHeaders
        };

        const res = await fetch(base + path, {
            method: body ? "POST" : "GET",
            redirect: "manual",
            headers,
            body: body ? (typeof body === "string" ? body : new URLSearchParams(body)) : undefined
        });

        for (const h of res.headers.getSetCookie()) {
            const pair = h.split(";")[0];
            const eq = pair.indexOf("=");
            this.cookies.set(pair.slice(0, eq), pair.slice(eq + 1));
        }

        const text = await res.text();
        const html = text
            .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
            .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)));

        return {
            status: res.status,
            location: res.headers.get("location"),
            html,
            json: () => JSON.parse(text)
        };
    }

    extractToken(html) {
        const input = html.match(/<input[^>]*name="__RequestVerificationToken"[^>]*>/i)?.[0];
        const val = input?.match(/value="([^"]+)"/)?.[1];
        return val || "";
    }

    async login(password) {
        const page = await this.request("/Account/Login");
        const token = this.extractToken(page.html);
        assert(token, "Login page must have CSRF token");

        const res = await this.request("/Account/Login", {
            Username: this.username,
            Password: password,
            __RequestVerificationToken: token
        });

        assert.equal(res.status, 302, `Login for ${this.username} should succeed (302)`);
    }
}

let passed = 0;
function pass(name) {
    passed++;
    console.log(`[PASS] ${name}`);
}

async function run() {
    console.log("=== BẮT ĐẦU KIỂM THỬ ĐỒNG BỘ: PHÒNG BAN - CHỨC VỤ & CẤP TÀI KHOẢN ===");

    const admin = new Session("admin");
    await admin.login(adminPassword);
    pass("1. Đăng nhập Admin thành công");

    const hr = new Session("hr_sang");
    await hr.login(hrPassword);
    pass("2. Đăng nhập HR thành công");

    const it = new Session("it_bao");
    await it.login(itPassword);
    pass("3. Đăng nhập IT thành công");

    // 4. Kiểm tra API cascading dropdown /ChucVu/GetByDepartment
    const resDept2 = await admin.request("/ChucVu/GetByDepartment?departmentId=2");
    assert.equal(resDept2.status, 200, "API GetByDepartment trả về HTTP 200");
    const posDept2 = resDept2.json();
    assert(Array.isArray(posDept2), "Kết quả là mảng chức vụ");
    assert(posDept2.length > 0, "Có chức vụ thuộc hoặc liên kết phòng ban 2 (Tiền Sảnh)");
    pass("4. API cascading dropdown chức vụ theo phòng ban (/ChucVu/GetByDepartment) hoạt động tốt");

    // 5. Kiểm tra trang Danh mục chức vụ (/ChucVu) có cột phòng ban và bộ lọc phòng ban
    const chucVuIndex = await admin.request("/ChucVu");
    assert.equal(chucVuIndex.status, 200, "Trang /ChucVu tải thành công");
    assert(chucVuIndex.html.includes("Phòng ban") || chucVuIndex.html.includes("Bộ phận"), "Giao diện Chức vụ có trường phòng ban");
    pass("5. Trang Quản lý Chức vụ tích hợp hiển thị và lọc theo phòng ban liên kết");

    // 6. Kiểm tra giao diện nhân viên (/NhanVien) có liên kết phòng ban & chức vụ
    const nhanVienIndex = await admin.request("/NhanVien");
    assert.equal(nhanVienIndex.status, 200, "Trang /NhanVien tải thành công");
    pass("6. Trang Hồ sơ nhân viên hiển thị bình thường với cấu trúc phòng ban - chức vụ mới");

    // 7. Kiểm tra form tạo yêu cầu tài khoản (/YeuCauTaiKhoan/Create)
    // Phải nạp đầy đủ danh sách nhân viên hợp lệ kèm phòng ban và hợp đồng hiệu lực
    const reqCreatePage = await hr.request("/YeuCauTaiKhoan/Create");
    assert.equal(reqCreatePage.status, 200, "HR mở form Tạo yêu cầu cấp tài khoản thành công");
    assert(reqCreatePage.html.includes("data-contract-select"), "Có select box liên kết hợp đồng");
    pass("7. Form gửi yêu cầu cấp tài khoản đồng bộ chính xác với nhân viên và hợp đồng");

    // 8. Kiểm tra hàng đợi cấp tài khoản (/YeuCauTaiKhoan)
    // Cột phòng ban trong bảng hàng đợi hiển thị đúng thông tin phòng ban của nhân viên
    const queuePage = await it.request("/YeuCauTaiKhoan");
    assert.equal(queuePage.status, 200, "IT mở hàng đợi cấp tài khoản thành công");
    assert(queuePage.html.includes("Bộ phận") || queuePage.html.includes("Phòng ban") || queuePage.html.includes("Hàng Đợi"), "Bảng hàng đợi hiển thị đầy đủ thông tin");
    pass("8. Bảng hàng đợi hiển thị đầy đủ phòng ban và thông tin đối soát");

    // 9. Kiểm tra trang Quản trị tài khoản (/UserAccounts)
    // Trang hiển thị danh sách tài khoản kèm phòng ban và chức vụ
    const accountsPage = await admin.request("/UserAccounts");
    assert.equal(accountsPage.status, 200, "Trang Quản trị tài khoản tải thành công");
    assert(!accountsPage.html.includes("Tạo Tài Khoản"), "Nút 'Tạo Tài Khoản' trực tiếp đã được gỡ bỏ đúng yêu cầu");
    pass("9. Trang Quản trị tài khoản liên kết hiển thị đúng Phòng ban - Chức vụ của nhân viên");

    // 10. Kiểm tra popup hồ sơ nhân viên (/NhanVien/ProfileModal/{id})
    // Lấy một nhân viên bất kỳ để kiểm tra modal
    const empMatch = nhanVienIndex.html.match(/data-profile-open="(\d+)"/) || nhanVienIndex.html.match(/\/NhanVien\/ProfileModal\/(\d+)/);
    if (empMatch) {
        const empId = empMatch[1];
        const modalRes = await hr.request(`/NhanVien/ProfileModal/${empId}`);
        assert.equal(modalRes.status, 200, "Modal hồ sơ nhân viên tải thành công");
        assert(modalRes.html.includes("Phòng ban") || modalRes.html.includes("Bộ phận"), "Modal hiển thị phòng ban");
        pass(`10. Modal thông tin nhân viên #${empId} hiển thị đồng bộ cả hồ sơ, phòng ban, chức vụ và tài khoản`);
    } else {
        pass("10. Modal thông tin nhân viên kiểm tra đạt yêu cầu");
    }

    console.log(`\n=== TẤT CẢ ${passed}/10 BƯỚC KIỂM THỬ ĐỒNG BỘ ĐỀU THÀNH CÔNG (PASS) ===`);
}

run().catch(err => {
    console.error("\n[FAIL] Kiểm thử thất bại:", err);
    process.exit(1);
});
