# Kế Hoạch Triển Khai Quy Trình Thăng Chức Cho Nhân Viên (Employee Promotion Workflow)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng quy trình thăng chức cho nhân viên với quy tắc nghiệp vụ chặt chẽ: Nhân viên (`Employee`) chỉ có thể được thăng chức lên Quản lý (`Manager`), yêu cầu thăng chức được đẩy vào Hàng đợi tài khoản (`yeu_cau_tai_khoan`) và chờ bộ phận IT hoặc Admin phê duyệt.

**Architecture:** Mở rộng quy trình Hàng đợi cấp tài khoản hiện có (`YeuCauTaiKhoan`), bổ sung phân loại yêu cầu thăng chức (Promotion) bên cạnh cấp mới tài khoản. Tận dụng bảng CSDL hiện có (`yeu_cau_tai_khoan`, `tai_khoan`, `nhan_vien`) mà không làm xáo trộn các ràng buộc CHECK/FK. Kiểm soát nghiệp vụ bằng `LeaveApprovalPolicy`, xử lý hoàn tất (`Complete`) bằng cách cập nhật vai trò `TaiKhoan.MaVaiTro` và ghi nhật ký kiểm toán `NhatKyQuanTri`.

**Tech Stack:** ASP.NET Core 10 MVC, Entity Framework Core, PostgreSQL/Npgsql, Bootstrap 5 & Vanilla JS, xUnit/Console Regression Test Suite.

**Spec:** Yêu cầu từ người dùng ngày 2026-10-19:
- Nhân viên chỉ có thể lên làm quản lý (`Employee -> Manager`).
- Khi yêu cầu tăng chức thì cần gửi yêu cầu ở hàng đợi cấp tài khoản.
- Chờ bộ phận IT hoặc Admin duyệt.

---

## Global Constraints

- **Single Source of Truth:** Toàn bộ quy tắc hợp lệ cho việc thăng chức (người được gửi, vai trò nguồn `Employee`, vai trò đích `Manager`, người có quyền duyệt `Admin`/`IT`) phải được tập trung tại `OvertimeHotel.HRM.Core.Policies.LeaveApprovalPolicy`.
- **Không thay đổi DB Schema trái phép:** Tái sử dụng bảng `yeu_cau_tai_khoan` hiện có với đầy đủ các ràng buộc CHECK (`chk_yeu_cau_da_cap_can_co_tai_khoan`, `chk_yeu_cau_tu_choi_can_co_ghi_chu`). Khi trạng thái là `CHO_IT`/`DANG_XU_LY`, `ma_tai_khoan` là NULL. Khi duyệt `Complete`, `ma_tai_khoan` được gán bằng `account.MaTaiKhoan` và `account.MaVaiTro` được nâng lên `Manager`.
- **Fail-Closed Security:** Mọi thao tác gửi đề xuất phải thuộc thẩm quyền HR hoặc Admin (`CanSubmitAccountRequest`). Mọi thao tác duyệt phải thuộc thẩm quyền IT hoặc Admin (`CanProcessAccountRequest`).
- **Audit Trail:** Bắt buộc ghi lại nhật ký kiểm toán `NhatKyQuanTri` cho cả bước gửi đề xuất thăng chức và bước duyệt/từ chối thăng chức.
- **Tương thích UI:** Giao diện nhất quán với thiết kế luxury hiện có của hệ thống HRM (`luxury-card`, badge vai trò, modal xác nhận).

---

## Review Focus

1. **Chặn thăng chức trái luồng:** Cố tình gửi yêu cầu thăng chức cho nhân viên chưa có tài khoản, hoặc nhân viên đã là Quản lý/Admin/HR/IT, hoặc chọn vai trò đích khác `Manager` (ví dụ cố gắng chọn `Admin`) phải bị chặn triệt để ở cả UI lẫn Backend Policy.
2. **Xung đột yêu cầu mở (Concurrency):** Một nhân viên đang có yêu cầu (dù là cấp mới hay thăng chức) ở trạng thái `CHO_IT` hoặc `DANG_XU_LY` thì không được phép tạo thêm yêu cầu thứ hai.
3. **Tuân thủ CHECK constraint CSDL:** Quá trình lưu và cập nhật `yeu_cau_tai_khoan` không được vi phạm `chk_yeu_cau_da_cap_can_co_tai_khoan` (`(trang_thai = 'DA_CAP') = (ma_tai_khoan IS NOT NULL)`).
4. **Xử lý tài khoản đã có sẵn:** Khi IT/Admin duyệt yêu cầu thăng chức, hệ thống **không** gọi tạo mới user trên Supabase Auth mà chỉ cập nhật vai trò nội bộ `TaiKhoan.MaVaiTro` và đồng bộ metadata nếu cần, tránh lỗi trùng email/tài khoản.
5. **Hiển thị trực quan:** Hàng đợi tài khoản phải hiển thị rõ nhãn phân biệt giữa `Cấp mới tài khoản` và `Thăng chức lên Quản lý`, kèm tên đăng nhập và vai trò hiện tại/mục tiêu.

---

## Tasks

### Task 1: Định nghĩa quy tắc nghiệp vụ thăng chức trong Core Policies & Unit Tests

**Files:**
- Modify: `src/OvertimeHotel.HRM.Core/Policies/LeaveApprovalPolicy.cs`
- Modify: `tests/AccountQueueRules/Program.cs`

**Interfaces:**
- Produces:
  * `LeaveApprovalPolicy.CanPromoteRole(string? currentRole, string? targetRole) -> bool`
  * `LeaveApprovalPolicy.CanSubmitPromotionRequest(string? senderRole) -> bool`

- [ ] **Step 1: Viết test case kiểm tra quy tắc thăng chức trong `tests/AccountQueueRules/Program.cs`**
  Thêm các test case:
  * `CanPromoteRole("Employee", "Manager")` => `true`
  * `CanPromoteRole("Employee", "Admin")` => `false`
  * `CanPromoteRole("Employee", "HR")` => `false`
  * `CanPromoteRole("Employee", "IT")` => `false`
  * `CanPromoteRole("Manager", "Admin")` => `false`
  * `CanPromoteRole("HR", "Manager")` => `false`
  * `CanSubmitPromotionRequest("Admin")` => `true`
  * `CanSubmitPromotionRequest("HR")` => `true`
  * `CanSubmitPromotionRequest("Manager")` => `false`
  * `CanSubmitPromotionRequest("Employee")` => `false`
  * `CanSubmitPromotionRequest("IT")` => `false`

- [ ] **Step 2: Chạy test để xác nhận test thất bại (Red)**
  Run: `dotnet run --project tests/AccountQueueRules/AccountQueueRules.csproj`
  Expected: Lỗi biên dịch do `CanPromoteRole` và `CanSubmitPromotionRequest` chưa tồn tại.

- [ ] **Step 3: Cài đặt logic trong `src/OvertimeHotel.HRM.Core/Policies/LeaveApprovalPolicy.cs`**
  Triển khai các phương thức kiểm tra vai trò chuẩn hóa thông qua `NormalizeRole`.

- [ ] **Step 4: Chạy lại test để xác nhận test vượt qua (Green)**
  Run: `dotnet run --project tests/AccountQueueRules/AccountQueueRules.csproj`
  Expected: All tests PASS.

- [ ] **Step 5: Commit thay đổi**
  Run:
  ```bash
  git add src/OvertimeHotel.HRM.Core/Policies/LeaveApprovalPolicy.cs tests/AccountQueueRules/Program.cs
  git commit -m "feat(core): add employee promotion policy and unit tests"
  ```

---

### Task 2: Mở rộng ViewModels và Hàng đợi tài khoản (`YeuCauTaiKhoan`)

**Files:**
- Modify: `src/OvertimeHotel.HRM.WebAdmin/Models/AccountManagementViewModels.cs`
- Modify: `src/OvertimeHotel.HRM.WebAdmin/Controllers/YeuCauTaiKhoanController.cs`

**Interfaces:**
- Consumes: `LeaveApprovalPolicy.CanPromoteRole`, `LeaveApprovalPolicy.CanSubmitPromotionRequest`
- Produces:
  * `CreatePromotionRequestViewModel`
  * `YeuCauTaiKhoanListItemViewModel.IsPromotion -> bool`
  * `YeuCauTaiKhoanController.CreatePromotion` (GET & POST)
  * `YeuCauTaiKhoanController.Complete` (hỗ trợ nâng cấp vai trò khi nhân viên đã có tài khoản)

- [ ] **Step 1: Khai báo ViewModel `CreatePromotionRequestViewModel` trong `AccountManagementViewModels.cs`**
  Bao gồm:
  * `EmployeeId`: Mã nhân viên cần thăng chức.
  * `EmployeeName`, `CurrentRoleName`: Hiển thị thông tin hồ sơ hiện tại.
  * `ContractId`: Hợp đồng lao động hiệu lực làm căn cứ.
  * `TargetRoleId`: Mã vai trò mục tiêu (khóa cứng mã của vai trò `Manager`).
  * `Reason`: Lý do đề xuất thăng chức.
  * `EligibleEmployees`: Danh sách nhân viên đang có tài khoản vai trò `Employee`, đang làm việc và có hợp đồng hiệu lực.
  * Thêm thuộc tính `IsPromotion` vào `YeuCauTaiKhoanListItemViewModel`.

- [ ] **Step 2: Cài đặt action `CreatePromotion` (GET & POST) trong `YeuCauTaiKhoanController.cs`**
  * `GET`: Kiểm tra quyền `CanSubmitPromotionRequest`. Lọc danh sách nhân viên thỏa mãn điều kiện thăng chức (Đang làm, có hợp đồng hiệu lực, đã có tài khoản `Employee`, không có yêu cầu đang mở). Lấy ID vai trò `Manager`.
  * `POST`: Kiểm tra chặt chẽ điều kiện:
    - Quyền người gửi: Admin hoặc HR.
    - Nhân viên phải đang làm việc, có hợp đồng hiệu lực, tài khoản hiện tại phải là `Employee`.
    - Vai trò mục tiêu phải là `Manager` (`CanPromoteRole("Employee", "Manager")`).
    - Nhân viên không có yêu cầu nào khác đang mở (`CHO_IT` hoặc `DANG_XU_LY`).
    - Lưu bản ghi `YeuCauTaiKhoan` với `TenDangNhap = account.TenDangNhap`, `MaVaiTro = managerRoleId`, `TrangThai = STATUS_CHO_IT`.
    - Ghi nhật ký kiểm toán `CREATE_PROMOTION_REQUEST`.

- [ ] **Step 3: Cập nhật action `Complete` trong `YeuCauTaiKhoanController.cs`**
  * Khi IT hoặc Admin bấm hoàn tất xử lý (`Complete`):
    - Kiểm tra xem nhân viên đã có tài khoản (`TaiKhoan`) chưa.
    - Nếu đã có tài khoản:
      * Cập nhật `account.MaVaiTro = request.MaVaiTro` (nâng lên Quản lý).
      * Cập nhật `request.MaTaiKhoan = account.MaTaiKhoan`.
      * Cập nhật `request.TrangThai = STATUS_DA_CAP`.
      * Cập nhật `request.NgayXuLy = DateTimeOffset.UtcNow`.
      * Ghi nhận audit log `PROMOTE_EMPLOYEE_ROLE`: "Phê duyệt thăng chức nhân viên lên vai trò Quản lý".
    - Nếu chưa có tài khoản: Giữ nguyên logic cấp mới qua Supabase như ban đầu.

- [ ] **Step 4: Cập nhật hàm truy vấn danh sách `Index` trong `YeuCauTaiKhoanController.cs`**
  * Xác định `IsPromotion = item.NhanVien != null && item.NhanVien.TaiKhoan != null && item.TrangThai != STATUS_DA_CAP ? true : ...`
  * Đảm bảo hiển thị chính xác tên vai trò đề xuất (Quản lý).

- [ ] **Step 5: Kiểm tra biên dịch**
  Run: `dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj`
  Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Commit thay đổi**
  Run:
  ```bash
  git add src/OvertimeHotel.HRM.WebAdmin/Models/AccountManagementViewModels.cs src/OvertimeHotel.HRM.WebAdmin/Controllers/YeuCauTaiKhoanController.cs
  git commit -m "feat(accounts): support promotion requests in account queue"
  ```

---

### Task 3: Xây dựng giao diện Đề xuất thăng chức & Hiển thị trên Hàng đợi

**Files:**
- Create: `src/OvertimeHotel.HRM.WebAdmin/Views/YeuCauTaiKhoan/CreatePromotion.cshtml`
- Modify: `src/OvertimeHotel.HRM.WebAdmin/Views/YeuCauTaiKhoan/Index.cshtml`
- Modify: `src/OvertimeHotel.HRM.WebAdmin/Views/YeuCauTaiKhoan/_YeuCauTaiKhoanTable.cshtml`

**Interfaces:**
- Consumes: `CreatePromotionRequestViewModel`, `YeuCauTaiKhoanListItemViewModel`

- [ ] **Step 1: Tạo view `CreatePromotion.cshtml`**
  * Form đề xuất thăng chức:
    - Dropdown chọn nhân viên (chỉ hiển thị nhân viên có vai trò `Employee`).
    - Thẻ hiển thị vai trò hiện tại: `Nhân viên` với badge màu xanh xám.
    - Mũi tên chuyển đổi sang vai trò mới: `Quản lý` với badge vàng luxury (được khóa cứng, hiển thị rõ ràng không thể thay đổi vai trò khác).
    - Hợp đồng lao động căn cứ (tự động chọn hợp đồng hiệu lực của nhân viên).
    - Ô nhập lý do đề xuất thăng chức (bắt buộc nhập để lưu vết kiểm toán).
    - Nút "Gửi Yêu Cầu Thăng Chức" và nút "Quay lại hàng đợi".

- [ ] **Step 2: Cập nhật bảng `_YeuCauTaiKhoanTable.cshtml` và `Index.cshtml`**
  * Thêm nút "Đề xuất thăng chức" cạnh nút "Gửi yêu cầu cấp mới" (chỉ hiển thị cho Admin và HR).
  * Trong bảng danh sách, hiển thị huy hiệu (Badge):
    - `Cấp mới`: badge xanh / thông tin.
    - `Thăng chức`: badge màu vàng hổ phách với biểu tượng thăng tiến (`bi-arrow-up-circle-fill`).
  * Modal/Button xử lý của IT/Admin: khi bấm duyệt thăng chức, hiển thị rõ "Xác nhận duyệt thăng chức cho nhân viên lên Quản lý".

- [ ] **Step 3: Kiểm tra hiển thị & biên dịch**
  Run: `dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj`
  Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit thay đổi**
  Run:
  ```bash
  git add src/OvertimeHotel.HRM.WebAdmin/Views/YeuCauTaiKhoan/
  git commit -m "feat(views): add promotion request UI and queue integration"
  ```

---

### Task 4: Kiểm thử tích hợp toàn diện & Xác minh quy trình hoàn chỉnh

**Files:**
- Modify: `tests/AccountQueueIntegration/Program.cs` (hoặc tạo kịch bản kiểm thử tích hợp)

- [ ] **Step 1: Viết kịch bản kiểm thử quy trình thăng chức khép kín**
  * Kịch bản 1: HR gửi yêu cầu thăng chức cho nhân viên có tài khoản `Employee` => Tạo thành công bản ghi với trạng thái `CHO_IT`.
  * Kịch bản 2: Nhân viên đã là `Manager` hoặc chưa có tài khoản cố tình nộp đơn thăng chức => Bị từ chối.
  * Kịch bản 3: IT nhận việc (`Claim`) => Chuyển sang `DANG_XU_LY`.
  * Kịch bản 4: IT phê duyệt (`Complete`) => Tài khoản nhân viên chuyển thành `Manager`, bản ghi thành `DA_CAP`, `NhatKyQuanTri` được tạo.
  * Kịch bản 5: Sau khi thăng chức thành công, thử mở trang chỉnh sửa tài khoản (`UserAccounts/Edit`) kiểm tra vai trò `Manager` được giữ nguyên và bị khóa cứng.

- [ ] **Step 2: Chạy kiểm thử tự động**
  Run: `dotnet run --project tests/AccountQueueRules/AccountQueueRules.csproj`
  Expected: PASS tất cả các policy check.

- [ ] **Step 3: Kiểm tra trực quan trên trình duyệt (E2E Verification)**
  * Truy cập `http://localhost:5065/YeuCauTaiKhoan` với tài khoản HR/Admin.
  * Bấm nút `Đề xuất thăng chức`, kiểm tra dropdown và vai trò mục tiêu bị khóa cứng ở `Quản lý`.
  * Gửi yêu cầu và kiểm tra hàng đợi hiển thị đúng nhãn `Thăng chức`.
  * Đăng nhập IT/Admin bấm `Nhận việc` và `Cấp tài khoản / Duyệt thăng chức`.
  * Xác nhận vai trò tài khoản đổi thành Quản lý.

- [ ] **Step 4: Commit và hoàn tất**
  Run:
  ```bash
  git commit -am "test: verify complete employee promotion lifecycle"
  ```
