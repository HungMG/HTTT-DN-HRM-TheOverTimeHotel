// End-to-end integration smoke test for Employee Promotion Workflow
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
        const input = html.match(/<input[^>]*name="__RequestVerificationToken"[^>]*>/)?.[0];
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

console.log("=== BẮT ĐẦU KIỂM THỬ QUY TRÌNH THĂNG CHỨC NHÂN VIÊN ===");

// 1. Đăng nhập Admin, HR, IT
const admin = new Session("admin");
await admin.login(adminPassword);
pass("1.1 Admin đăng nhập thành công");

const hr = new Session("hr_sang");
await hr.login(hrPassword);
pass("1.2 HR đăng nhập thành công");

const it = new Session("it_bao");
await it.login(itPassword);
pass("1.3 IT đăng nhập thành công");

// 2. Phân quyền truy cập form Thăng Chức
const adminPromotePage = await admin.request("/YeuCauTaiKhoan/CreatePromotion");
assert.equal(adminPromotePage.status, 200, "Admin có quyền mở form Thăng chức");
pass("2.1 Admin mở form CreatePromotion thành công");

const hrPromotePage = await hr.request("/YeuCauTaiKhoan/CreatePromotion");
assert.equal(hrPromotePage.status, 200, "HR có quyền mở form Thăng chức");
pass("2.2 HR mở form CreatePromotion thành công");

const itPromotePage = await it.request("/YeuCauTaiKhoan/CreatePromotion");
assert.equal(itPromotePage.status, 302, "IT không có quyền gửi đề xuất thăng chức");
assert.match(itPromotePage.location, /YeuCauTaiKhoan/);
pass("2.3 IT bị chặn truy cập form CreatePromotion và bị redirect");

// 3. Kiểm tra nội dung form CreatePromotion
assert.match(hrPromotePage.html, /Đề Xuất Thăng Chức Lên Quản Lý/, "Tiêu đề trang xác nhận thăng chức lên Quản lý");
assert.match(hrPromotePage.html, /Quản lý \(Manager\)/, "Vai trò đích được khóa cứng là Manager");
assert.match(hrPromotePage.html, /TargetRoleName/, "Có trường ẩn TargetRoleName");
pass("3.1 Form CreatePromotion hiển thị vai trò đích cố định là Manager và không cho đổi vai trò");

// 4. Trích xuất danh sách nhân viên hợp lệ để thăng chức từ dropdown
const employeeOptions = [...hrPromotePage.html.matchAll(/<option\s+value="(\d+)">([^<]+)<\/option>/g)];
console.log(`Tìm thấy ${employeeOptions.length} nhân viên có tài khoản Employee và hợp đồng còn hạn:`);
for (const opt of employeeOptions) {
    console.log(` - ID: ${opt[1]}, Text: ${opt[2].trim()}`);
}
assert(employeeOptions.length > 0, "Phải có ít nhất 1 nhân viên đủ điều kiện thăng chức");

const contractsMatch = hrPromotePage.html.match(/const contracts = (\[[\s\S]*?\]);/);
assert(contractsMatch, "Phải tìm thấy danh sách hợp đồng căn cứ");
const contracts = JSON.parse(contractsMatch[1]);

const targetEmp = employeeOptions[0];
const targetEmpId = parseInt(targetEmp[1], 10);
const targetEmpText = targetEmp[2].trim();
const targetContract = contracts.find(c => c.EmployeeId === targetEmpId);
assert(targetContract, `Nhân viên ${targetEmpText} phải có hợp đồng hợp lệ`);
const targetContractId = targetContract.ContractId;

const usernameMatch = targetEmpText.match(/\((.*?)\)/);
const targetEmpUsername = usernameMatch ? usernameMatch[1] : "";
pass(`4.1 Chọn nhân viên thử nghiệm: ID=${targetEmpId}, Username=${targetEmpUsername}, ContractId=${targetContractId}`);

// 5. HR gửi đề xuất thăng chức
const hrToken = hr.extractToken(hrPromotePage.html);
const submitPromotionRes = await hr.request("/YeuCauTaiKhoan/CreatePromotion", {
    EmployeeId: targetEmpId,
    TargetRoleName: "Manager",
    ContractId: targetContractId,
    Reason: "Đạt KPI xuất sắc quý vừa qua, đề xuất nâng quyền Quản lý",
    __RequestVerificationToken: hrToken
});

assert.equal(submitPromotionRes.status, 302, "HR gửi đề xuất thăng chức thành công, redirect về danh sách");
pass("5.1 HR gửi yêu cầu thăng chức thành công lên hàng đợi");

// 6. Kiểm tra hàng đợi hiển thị đúng badge Thăng chức
const itQueue = await it.request("/YeuCauTaiKhoan");
assert(itQueue.html.includes(targetEmpUsername), "IT thấy yêu cầu thăng chức trong hàng đợi");
assert.match(itQueue.html, /<span[^>]*class="[^"]*badge[^"]*"[^>]*>[\s\S]*?Thăng chức[\s\S]*?<\/span>/, "Bảng hàng đợi hiển thị badge 'Thăng chức'");
pass("6.1 Hàng đợi hiển thị huy hiệu 'Thăng chức' vàng nổi bật");

// Trích xuất RequestId
const reqRegex = new RegExp(`data-request-row="(\\d+)"[\\s\\S]*?${targetEmpUsername}`);
const reqMatch = itQueue.html.match(reqRegex);
assert(reqMatch, "Tìm thấy hàng của yêu cầu vừa gửi");
const requestId = parseInt(reqMatch[1], 10);
pass(`6.2 Lấy được mã yêu cầu thăng chức #${requestId}`);

// 7. IT nhận việc (Claim)
const itToken = it.extractToken(itQueue.html);
const claimRes = await it.request(`/YeuCauTaiKhoan/Claim/${requestId}`, {
    id: requestId,
    __RequestVerificationToken: itToken
}, { "X-Requested-With": "XMLHttpRequest" });
assert.equal(claimRes.status, 200, "IT Claim yêu cầu thành công");
assert.equal(claimRes.json().success, true);
pass("7.1 IT nhận việc (Claim) yêu cầu thăng chức thành công");

// 8. IT kiểm tra giao diện thấy nút 'Duyệt thăng chức'
const queueAfterClaim = await it.request("/YeuCauTaiKhoan");
assert(queueAfterClaim.html.includes("Duyệt thăng chức"), "Nút duyệt hiển thị 'Duyệt thăng chức' thay vì 'Cấp tài khoản'");
pass("8.1 Giao diện hiển thị nút hành động 'Duyệt thăng chức'");

// 9. IT duyệt thăng chức (Complete)
const itTokenAfterClaim = it.extractToken(queueAfterClaim.html);
const completeRes = await it.request(`/YeuCauTaiKhoan/Complete/${requestId}`, {
    id: requestId,
    __RequestVerificationToken: itTokenAfterClaim
}, { "X-Requested-With": "XMLHttpRequest" });

assert.equal(completeRes.status, 200, "Duyệt thăng chức thành công (HTTP 200)");
const completeJson = completeRes.json();
assert.equal(completeJson.success, true);
assert.match(completeJson.message, /thăng chức|nâng quyền/i);
pass("9.1 IT duyệt thăng chức thành công, cập nhật tài khoản sang Manager");

// 10. Kiểm tra trạng thái cuối cùng trên bảng hàng đợi
const queueAfterComplete = await it.request("/YeuCauTaiKhoan");
const rowMatch = queueAfterComplete.html.match(new RegExp(`data-request-row="${requestId}"[\\s\\S]*?<\/tr>`));
console.log(`HTML của hàng #${requestId}:`, rowMatch ? rowMatch[0] : "NOT FOUND");
assert(queueAfterComplete.html.includes("Đã thăng chức @"), "Bảng hiển thị 'Đã thăng chức @...'");
pass("10.1 Bảng hàng đợi hiển thị trạng thái Đã thăng chức thành công");

// 11. Kiểm tra tài khoản trong danh sách UserAccounts: Vai trò đã là Manager
const adminAccountsPage = await admin.request(`/UserAccounts`);
assert(adminAccountsPage.html.includes("Manager"), "Danh sách UserAccounts hiển thị vai trò Manager");
pass("11.1 Tài khoản nhân sự đã được nâng quyền thành công sang Quản lý");

console.log(`\n=== TẤT CẢ ${passed} BƯỚC KIỂM THỬ THĂNG CHỨC ĐỀU THÀNH CÔNG (PASS) ===`);
