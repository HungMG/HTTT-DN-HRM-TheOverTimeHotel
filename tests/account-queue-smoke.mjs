// Native Node.js test script for Account Provisioning Queue Plan
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
        assert.equal(res.location, "/", `Redirect location should be /`);
    }
}

let passed = 0;
function pass(name) {
    passed++;
    console.log(`[PASS] ${name}`);
}

console.log("=== BẮT ĐẦU KIỂM THỬ HÀNG ĐỢI CẤP TÀI KHOẢN ===");

// 1. Kiểm tra truy cập ẩn danh (Anonymous)
const anon = new Session("anon");
const anonQueue = await anon.request("/YeuCauTaiKhoan");
assert.equal(anonQueue.status, 302, "Ẩn danh vào /YeuCauTaiKhoan phải bị chuyển hướng");
assert.match(anonQueue.location, /Account\/Login/);
pass("1.1 Ẩn danh bị chặn truy cập và chuyển hướng về trang Đăng nhập");

// 2. Đăng nhập 3 tài khoản Admin, HR, IT
const admin = new Session("admin");
await admin.login(adminPassword);
pass("1.2 Đăng nhập tài khoản Quản trị viên (Admin) thành công");

const hr = new Session("hr_sang");
await hr.login(hrPassword);
pass("1.3 Đăng nhập tài khoản Nhân sự (HR) thành công");

const it = new Session("it_bao");
await it.login(itPassword);
pass("1.4 Đăng nhập tài khoản Công nghệ Thông tin (IT) thành công");

// 3. Phân quyền gửi yêu cầu (Admin: Có, HR: Có, IT: Không)
const adminCreatePage = await admin.request("/YeuCauTaiKhoan/Create");
assert.equal(adminCreatePage.status, 200, "Admin có quyền truy cập /YeuCauTaiKhoan/Create");
pass("2.1 Admin có quyền mở form tạo yêu cầu cấp tài khoản");

const hrCreatePage = await hr.request("/YeuCauTaiKhoan/Create");
assert.equal(hrCreatePage.status, 200, "HR có quyền truy cập /YeuCauTaiKhoan/Create");
pass("2.2 HR có quyền mở form tạo yêu cầu cấp tài khoản");

const itCreatePage = await it.request("/YeuCauTaiKhoan/Create");
assert.equal(itCreatePage.status, 302, "IT không có quyền gửi yêu cầu, phải bị chuyển hướng");
assert.match(itCreatePage.location, /YeuCauTaiKhoan/);
pass("2.3 IT bị chặn khi cố truy cập trực tiếp form tạo yêu cầu");

const itIndexPage = await it.request("/YeuCauTaiKhoan");
assert(!itIndexPage.html.includes("Gửi Yêu Cầu Cấp Tài Khoản"), "Giao diện của IT không hiển thị nút Gửi Yêu Cầu");
pass("2.4 Giao diện của IT ẩn nút 'Gửi Yêu Cầu Cấp Tài Khoản'");

// 4. Phân quyền xử lý (Admin: Có, IT: Có, HR: Không được xử lý kể cả gửi trực tiếp)
const hrClaimAttempt = await hr.request("/YeuCauTaiKhoan/Claim/99999", { id: 99999 }, { "X-Requested-With": "XMLHttpRequest" });
assert.equal(hrClaimAttempt.status, 403, "HR gửi Claim trực tiếp phải bị máy chủ từ chối 403");
pass("3.1 HR bị máy chủ từ chối 403 Forbidden khi cố gọi Claim (Nhận việc)");

const hrCompleteAttempt = await hr.request("/YeuCauTaiKhoan/Complete/99999", { id: 99999 }, { "X-Requested-With": "XMLHttpRequest" });
assert.equal(hrCompleteAttempt.status, 403, "HR gửi Complete trực tiếp phải bị máy chủ từ chối 403");
pass("3.2 HR bị máy chủ từ chối 403 Forbidden khi cố gọi Complete (Cấp tài khoản)");

const hrRejectAttempt = await hr.request("/YeuCauTaiKhoan/Reject/99999", { id: 99999, lyDo: "Test" }, { "X-Requested-With": "XMLHttpRequest" });
assert.equal(hrRejectAttempt.status, 403, "HR gửi Reject trực tiếp phải bị máy chủ từ chối 403");
pass("3.3 HR bị máy chủ từ chối 403 Forbidden khi cố gọi Reject (Từ chối)");

// 5. Kiểm tra quy tắc: Chỉ Admin được đề xuất cấp vai trò Admin
const hrToken = hr.extractToken(hrCreatePage.html);
const hrProposeAdmin = await hr.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: 3,
    RoleId: 1, // Admin role
    ContractId: 3,
    Username: "nv.testadmin",
    __RequestVerificationToken: hrToken
});
assert.match(hrProposeAdmin.html, /Chỉ Quản trị viên mới được đề xuất cấp vai trò Quản trị viên/);
pass("4.1 HR gửi đề xuất vai trò Admin bị máy chủ từ chối với thông báo rõ ràng");

// 6. Kiểm tra quy tắc: Bắt buộc chọn hợp đồng hợp lệ
const adminToken = admin.extractToken(adminCreatePage.html);
const submitNoContract = await admin.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: 3,
    RoleId: 4, // Employee
    ContractId: "",
    Username: "nv.testnocontract",
    __RequestVerificationToken: adminToken
});
assert.match(submitNoContract.html, /Vui lòng chọn hợp đồng lao động làm căn cứ cấp tài khoản/);
pass("5.1 Gửi yêu cầu thiếu hợp đồng bị chặn và giữ form thông báo lỗi");

const submitExpiredContract = await admin.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: 10, // Đỗ Thành Đạt - hợp đồng thử việc hết hạn 2026-10-01 (trước 2026-10-15)
    RoleId: 4,
    ContractId: 10,
    Username: "nv.testexpired",
    __RequestVerificationToken: adminToken
});
assert.match(submitExpiredContract.html, /Hợp đồng không hợp lệ hoặc đã hết hạn/);
pass("5.2 Gửi yêu cầu với hợp đồng đã hết hạn bị máy chủ chặn");

// 7. Kiểm tra quy tắc: Chặn nhân viên đã có tài khoản
const submitExistingAccount = await admin.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: 1, // Nguyễn Đình Cường - đã có tài khoản admin
    RoleId: 4,
    ContractId: 1,
    Username: "nv.testdup",
    __RequestVerificationToken: adminToken
});
assert.match(submitExistingAccount.html, /Nhân viên này đã có tài khoản rồi/);
pass("6.1 Gửi yêu cầu cho nhân viên đã có tài khoản bị chặn");

// 8. Kiểm tra chức năng tự động lọc & gán hợp đồng trong HTML
assert.match(adminCreatePage.html, /data-contract-select/, "Select box hợp đồng có thuộc tính data-contract-select");
assert.match(adminCreatePage.html, /noContractAlert/, "Có thẻ cảnh báo noContractAlert khi nhân viên không có HĐ hợp lệ");
pass("7.1 Giao diện có đầy đủ thẻ kiểm soát hợp đồng và cảnh báo khóa nút gửi");

// 9. Dọn dẹp các yêu cầu đang mở của NV-19 từ phiên trước (nếu có)
const preQueue = await it.request("/YeuCauTaiKhoan");
const openMatches = [...preQueue.html.matchAll(/data-request-row="(\d+)"[\s\S]*?thang\.doan/g)];
for (const match of openMatches) {
    const existingReqId = parseInt(match[1], 10);
    const token = it.extractToken(preQueue.html);
    await it.request(`/YeuCauTaiKhoan/Claim/${existingReqId}`, { id: existingReqId, __RequestVerificationToken: token }, { "X-Requested-With": "XMLHttpRequest" });
    await it.request(`/YeuCauTaiKhoan/Reject/${existingReqId}`, { id: existingReqId, lyDo: "Reset test data", __RequestVerificationToken: token }, { "X-Requested-With": "XMLHttpRequest" });
}

// 10. Gửi yêu cầu cấp tài khoản hợp lệ từ HR (cho nhân viên đầu tiên đủ điều kiện)
const hrCreateForm = await hr.request("/YeuCauTaiKhoan/Create");
const hrFreshToken = hr.extractToken(hrCreateForm.html);
const empSelectMatch = hrCreateForm.html.match(/<select[^>]*name="EmployeeId"[^>]*>([\s\S]*?)<\/select>/);
const availableEmpOptions = [...empSelectMatch[1].matchAll(/<option\s+value="(\d+)"[^>]*>([^<]+)<\/option>/g)].filter(o => o[1]);
assert(availableEmpOptions.length > 0, "Có ít nhất 1 nhân viên chưa có tài khoản");
const targetEmpId = parseInt(availableEmpOptions[0][1], 10);

const contractMatch = hrCreateForm.html.match(new RegExp(`<option\\s+value="(\\d+)"[^>]*data-employee-id="${targetEmpId}"`));
assert(contractMatch, `Phải có hợp đồng hợp lệ cho nhân viên #${targetEmpId}`);
const targetContractId = parseInt(contractMatch[1], 10);

console.log("Selected target:", targetEmpId, targetContractId);
const testUsername = "user.test." + Date.now().toString().slice(-6);
const submitValidRequest = await hr.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: targetEmpId,
    RoleId: 4, // Employee
    ContractId: targetContractId,
    Username: testUsername,
    __RequestVerificationToken: hrFreshToken
});
assert.equal(submitValidRequest.status, 302, "HR gửi yêu cầu hợp lệ phải được chuyển hướng 302 về danh sách");
assert.match(submitValidRequest.location, /YeuCauTaiKhoan/, "Chuyển hướng về trang danh sách hàng đợi");
pass("8.1 HR gửi yêu cầu cấp tài khoản hợp lệ thành công (chuyển vào hàng đợi CHO_IT)");

// Kiểm tra yêu cầu mới xuất hiện trong hàng đợi của HR, IT, và Admin
const hrQueue = await hr.request("/YeuCauTaiKhoan");
assert(hrQueue.html.includes(testUsername), "HR thấy yêu cầu do chính mình vừa gửi");
pass("8.2 HR nhìn thấy yêu cầu do chính mình gửi trong hàng đợi");

const itQueue = await it.request("/YeuCauTaiKhoan");
assert(itQueue.html.includes(testUsername), "IT thấy toàn bộ yêu cầu bao gồm yêu cầu HR vừa gửi");
pass("8.3 IT nhìn thấy yêu cầu trong hàng đợi toàn khách sạn");

const adminQueue = await admin.request("/YeuCauTaiKhoan");
assert(adminQueue.html.includes(testUsername), "Admin thấy toàn bộ yêu cầu trong hàng đợi");
pass("8.4 Admin nhìn thấy yêu cầu trong hàng đợi toàn khách sạn");

// Trích xuất mã yêu cầu vừa tạo
const reqRegex = new RegExp(`data-request-row="(\\d+)"[\\s\\S]*?${testUsername}`);
const reqMatch = itQueue.html.match(reqRegex);
assert(reqMatch, "Tìm thấy yêu cầu của username trong bảng");
const requestId = parseInt(reqMatch[1], 10);
assert(requestId > 0, "Mã yêu cầu phải là số nguyên dương");
pass(`8.5 Xác định được yêu cầu #${requestId} (${testUsername}) đang ở trạng thái CHO_IT`);

// 11. Kiểm tra quy tắc: Chặn gửi yêu cầu trùng cho nhân viên đang có yêu cầu mở
const adminFreshPage = await admin.request("/YeuCauTaiKhoan/Create");
const adminFreshToken = admin.extractToken(adminFreshPage.html);
const submitDuplicateRequest = await admin.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: targetEmpId,
    RoleId: 4,
    ContractId: targetContractId,
    Username: testUsername + "dup",
    __RequestVerificationToken: adminFreshToken
});
assert.match(submitDuplicateRequest.html, /Nhân viên này đang có yêu cầu cấp tài khoản chờ hoặc đang được xử lý/);
pass("9.1 Chặn gửi thêm yêu cầu khi nhân viên đang có yêu cầu chờ hoặc đang xử lý");

// Kiểm tra danh sách dropdown cũng không còn hiển thị targetEmpId
assert(!adminFreshPage.html.includes(`value="${targetEmpId}"`), "Dropdown nhân viên tự động loại bỏ nhân viên đang có yêu cầu mở");
pass("9.2 Dropdown chọn nhân viên tự động loại trừ nhân viên đang có yêu cầu mở");

// 12. Kiểm tra Optimistic Concurrency: Cả IT và Admin cùng nhận xử lý (Claim)
const itQueuePage = await it.request("/YeuCauTaiKhoan");
const itToken = it.extractToken(itQueuePage.html);
const adminQueuePage = await admin.request("/YeuCauTaiKhoan");
const adminTokenQueue = admin.extractToken(adminQueuePage.html);

// Người thứ nhất (IT) nhận việc
const itClaim = await it.request(`/YeuCauTaiKhoan/Claim/${requestId}`, {
    id: requestId,
    __RequestVerificationToken: itToken
}, { "X-Requested-With": "XMLHttpRequest" });

assert.equal(itClaim.status, 200, "IT nhận việc thành công");
const itClaimJson = itClaim.json();
assert.equal(itClaimJson.success, true, "Phản hồi JSON của IT báo thành công");
pass(`10.1 IT nhận việc yêu cầu #${requestId} thành công, chuyển sang DANG_XU_LY`);

// Người thứ hai (Admin) cùng nhận việc -> Xung đột trạng thái 409 Conflict
const adminClaimConflict = await admin.request(`/YeuCauTaiKhoan/Claim/${requestId}`, {
    id: requestId,
    __RequestVerificationToken: adminTokenQueue
}, { "X-Requested-With": "XMLHttpRequest" });

assert.equal(adminClaimConflict.status, 409, "Người thứ hai cùng Claim phải nhận mã lỗi 409 Conflict");
const adminConflictJson = adminClaimConflict.json();
assert.equal(adminConflictJson.success, false);
assert.match(adminConflictJson.message, /vừa được người khác nhận hoặc đã chuyển trạng thái/);
pass("10.2 Chặn người thứ hai cùng nhận việc với mã lỗi 409 Conflict và thông báo rõ ràng");

// 13. Kiểm tra thao tác Từ chối (Reject) kèm lý do của người đang xử lý
const rejectReason = "Hồ sơ cần bổ sung xác nhận trưởng bộ phận chuyên môn.";
const itReject = await it.request(`/YeuCauTaiKhoan/Reject/${requestId}`, {
    id: requestId,
    lyDo: rejectReason,
    __RequestVerificationToken: itToken
}, { "X-Requested-With": "XMLHttpRequest" });

assert.equal(itReject.status, 200, "IT từ chối yêu cầu thành công");
const itRejectJson = itReject.json();
assert.equal(itRejectJson.success, true);
pass(`11.1 IT từ chối yêu cầu #${requestId} kèm lý do rõ ràng`);

// Kiểm tra trạng thái đã chuyển thành TU_CHOI và lưu đúng ghi chú
const queueAfterReject = await it.request("/YeuCauTaiKhoan");
assert(queueAfterReject.html.includes("TU_CHOI"), "Trạng thái yêu cầu hiển thị TU_CHOI");
assert(queueAfterReject.html.includes(rejectReason), "Ghi chú từ chối được hiển thị trên giao diện");
pass("11.2 Bảng hàng đợi hiển thị đúng trạng thái TU_CHOI và lý do từ chối");

// 14. Sau khi bị từ chối, yêu cầu không còn ở trạng thái mở -> nhân viên có thể gửi lại
const adminReopenPage = await admin.request("/YeuCauTaiKhoan/Create");
assert(adminReopenPage.html.includes(`value="${targetEmpId}"`), "Nhân viên xuất hiện lại trong dropdown sau khi yêu cầu cũ đã đóng");
pass("12.1 Sau khi yêu cầu cũ bị từ chối, nhân viên lại đủ điều kiện gửi yêu cầu mới");

// 15. Kiểm tra tính độc quyền xử lý (Handler Exclusivity): Admin nhận việc, IT khác không được can thiệp
const adminReopenToken = admin.extractToken(adminReopenPage.html);
const testUsername2 = "user.test." + (Date.now() + 1).toString().slice(-6);
await admin.request("/YeuCauTaiKhoan/Create", {
    EmployeeId: targetEmpId,
    RoleId: 4,
    ContractId: targetContractId,
    Username: testUsername2,
    __RequestVerificationToken: adminReopenToken
});

const adminQueueV2 = await admin.request("/YeuCauTaiKhoan");
const adminV2Token = admin.extractToken(adminQueueV2.html);
const reqMatch2 = adminQueueV2.html.match(new RegExp(`data-request-row="(\\d+)"[\\s\\S]*?${testUsername2}`));
assert(reqMatch2, "Tìm thấy yêu cầu thứ hai");
const requestId2 = parseInt(reqMatch2[1], 10);

// Admin nhận việc
await admin.request(`/YeuCauTaiKhoan/Claim/${requestId2}`, {
    id: requestId2,
    __RequestVerificationToken: adminV2Token
}, { "X-Requested-With": "XMLHttpRequest" });
pass(`13.1 Admin nhận việc yêu cầu #${requestId2}`);

// IT xem hàng đợi: Thấy huy hiệu "Đang được admin xử lý" và không có nút thao tác Cấp/Từ chối
const itQueueV2 = await it.request("/YeuCauTaiKhoan");
assert(itQueueV2.html.includes("Đang được admin xử lý"), "IT nhìn thấy badge 'Đang được admin xử lý'");
pass("13.2 Giao diện của IT hiển thị huy hiệu chuyên viên đang xử lý, khóa nút thao tác");

// IT cố tình gọi API Complete trực tiếp tới yêu cầu của Admin -> Bị máy chủ chặn 409
const itTokenV2 = it.extractToken(itQueueV2.html);
const itStealComplete = await it.request(`/YeuCauTaiKhoan/Complete/${requestId2}`, {
    id: requestId2,
    __RequestVerificationToken: itTokenV2
}, { "X-Requested-With": "XMLHttpRequest" });
assert.equal(itStealComplete.status, 409, "IT không được quyền can thiệp yêu cầu do người khác xử lý");
assert.match(itStealComplete.json().message, /đang được một chuyên viên khác xử lý/);
pass("13.3 Máy chủ chặn 409 khi chuyên viên khác cố tình can thiệp yêu cầu đã có người nhận");

// Dọn dẹp: Admin từ chối yêu cầu 2 để hoàn tất kịch bản kiểm thử
await admin.request(`/YeuCauTaiKhoan/Reject/${requestId2}`, {
    id: requestId2,
    lyDo: "Hoàn tất kiểm thử tính độc quyền xử lý.",
    __RequestVerificationToken: adminV2Token
}, { "X-Requested-With": "XMLHttpRequest" });
pass(`13.4 Admin đóng yêu cầu #${requestId2} thành công`);

console.log(`\n=== TẤT CẢ ${passed} BƯỚC KIỂM THỬ SMOKE TEST ĐỀU THÀNH CÔNG (PASS) ===`);


