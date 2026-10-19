# Quy định nhân sự và lương thưởng sự kiện — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tính đủ nhân sự theo quy mô khách và tính thù lao từng người theo loại sự kiện, phòng ban, thời lượng và kết quả tham gia.

**Architecture:** Mở rộng quy tắc và dịch vụ sự kiện hiện có; dùng một bảng định mức trong Core, có phiên bản bất biến. Server tính nhân sự và tiền; giao diện hiển thị kết quả và cho người quản lý chọn nhân viên. Tái sử dụng bảng sự kiện, phân công, ngân sách, giao dịch, kiểm tra phiên bản và nhật ký.

**Tech Stack:** ASP.NET Core MVC/.NET 10, EF Core/Npgsql/PostgreSQL, Razor, Bootstrap, JavaScript hiện có; không thêm thư viện.

**Spec:** Yêu cầu người dùng ngày 16/10/2026; hiện trạng trong `docs/hotel-events.md`; PRD/02 mục 9.2, 9.4, 9.5. Các quy tắc nghiệp vụ đề xuất nằm ngay trong tài liệu này.

**Trạng thái:** Bản đề xuất để duyệt. Chưa triển khai code, chạy migration hoặc xác nhận các mức tiền là chính sách của khách sạn.

## Global Constraints

- Giữ đúng 5 loại sự kiện đang hỗ trợ; không khôi phục `BAO_TRI_NANG_CAP`.
- Admin/HR tạo, sửa sự kiện và nghiệm thu; Manager chỉ thao tác sự kiện được giao. Không mở rộng quyền hiện tại.
- Thời gian hiển thị UTC+7, lưu UTC; khoảng thời gian `[bắt đầu, kết thúc)`.
- Số tiền dùng `decimal` và `numeric(15,2)`; kết quả làm tròn 2 số thập phân, `MidpointRounding.AwayFromZero`.
- Không tự sinh tăng ca hoặc thay đổi lương hợp đồng. Khoản này là thù lao/phụ cấp sự kiện ngoài lương cơ bản.
- Không đổi số tiền lịch sử của sự kiện đã nghiệm thu, đã hủy hoặc đã kết chuyển.
- Migration chỉ bổ sung dữ liệu/cột; không tự chạy khi khởi động và không tự áp dụng lên database đang dùng trong bước viết plan.
- Các tỷ lệ nhân sự, đơn giá và thưởng 20% dưới đây là số đề xuất để minh họa nghiệp vụ, cần được duyệt trước triển khai.

## Review Focus

1. Người được phân công hai nửa thời gian không được cộng thành hai người cùng trực; kiểm tra độ phủ theo thời gian ở Task 1 và Task 3.
2. Nhân viên chuyển phòng ban sau phân công không được đổi đơn giá hoặc làm biến dạng báo cáo cũ; snapshot ở Task 2 và kiểm tra Task 3.
3. Tăng khách hoặc thay định mức không được ghi lại tiền cũ âm thầm; kiểm tra lưu, phiên bản và sự kiện cũ ở Task 2–3.
4. Đánh giá xuất sắc không được làm phát sinh khoản chi vượt ngân sách; dự phòng thưởng từ lúc phân công ở Task 1 và Task 3.
5. Nhân sự vượt nhu cầu vẫn phải được trả theo đóng góp đã xác nhận; không tự hạ tiền từng người để ép vừa ngân sách, kiểm tra Task 3–4.

## 1. Kết quả kiểm tra hiện trạng

| Nội dung | Hiện có | Khoảng thiếu cần xử lý |
|---|---|---|
| Loại sự kiện | Tiệc cưới, Hội nghị MICE, Gala Dinner, Festival ẩm thực, Đoàn VIP | PRD còn nhắc bảo trì; code và migration mới chỉ còn 5 loại |
| Phòng ban bắt buộc | Đã quy định riêng cho từng loại | Mỗi phòng ban chỉ cần ít nhất 1 phân công là được coi đủ |
| Số khách | `DuAn.SoKhachDuKien` | Chưa tham gia công thức định biên, đang cho phép 0 |
| Tiền nhân viên | `PhuCapDuKien`, `SoTienPhuCap` | Nhập tay; chưa có đơn giá theo sự kiện/phòng ban |
| Đánh giá | Hoàn thành, xuất sắc, không tham gia | Chưa tính tiền tự động theo đánh giá; đã bắt không tham gia nhận 0 |
| Ngân sách | Kiểm tra dự kiến và thực tế không vượt ngân sách | Chưa có dự phòng phần thưởng xuất sắc |
| Chốt sự kiện | HR/Admin nghiệm thu và khóa | Giữ nguyên cơ chế này |
| Phiếu lương | Có `MaChiTiet`, `TrangThaiChiTra` để liên kết | Chưa thấy luồng kết chuyển trong dịch vụ sự kiện; tài liệu giai đoạn 1 cũng xác nhận để giai đoạn sau |

Nguồn kiểm tra: `HotelEventRules.cs`, `HotelEventService.cs`, `HotelEventContracts.cs`, `HotelEventViewModels.cs`, `Views/SuKien/Details.cshtml`, hai model `DuAn`/`PhanCongDuAn`, migration sự kiện và các test hiện có. Chỉ kiểm tra repository, chưa đối chiếu dữ liệu database đang chạy.

## 2. Quy định nghiệp vụ đề xuất

### 2.1. Nhân sự theo quy mô

Mỗi loại sự kiện có bảng số khách tối đa do 1 nhân viên của từng phòng ban phục vụ:

`Số người cần của phòng ban = max(1, ceil(Số khách dự kiến / Khách trên mỗi nhân viên))`

Chỉ tính các phòng ban bắt buộc của loại sự kiện. Một phòng ban dư người không bù cho phòng ban khác đang thiếu. Tăng khách có thể giữ nguyên nhân sự trong cùng ngưỡng; vượt ngưỡng sẽ tăng số người.

| Loại sự kiện | Tiền sảnh | F&B | Bếp | Buồng phòng | Kỹ thuật | An ninh | IT |
|---|---:|---:|---:|---:|---:|---:|---:|
| Tiệc cưới | 100 | 20 | 50 | 100 | 200 | 100 | — |
| Hội nghị MICE | 75 | 40 | 80 | — | 100 | 100 | 100 |
| Gala Dinner | 100 | 20 | 50 | — | 100 | 100 | — |
| Festival ẩm thực | — | 25 | 30 | 100 | 150 | 100 | — |
| Đoàn VIP | 20 | 10 | 25 | 15 | — | 20 | — |

Đơn vị: **khách/nhân viên**. Dấu `—` nghĩa là phòng ban không thuộc danh sách phân công của loại đó theo quy tắc hiện tại.

Ví dụ tiệc cưới:

| Khách | Tiền sảnh | F&B | Bếp | Buồng phòng | Kỹ thuật | An ninh | Tổng cần cùng lúc |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 1 | 5 | 2 | 1 | 1 | 1 | 11 |
| 300 | 3 | 15 | 6 | 3 | 2 | 3 | 32 |
| 600 | 6 | 30 | 12 | 6 | 3 | 6 | 63 |

- Có thể lưu kế hoạch với 0 khách khi chưa chốt; hiển thị “Chưa xác định quy mô”, không tính đủ nhân sự và không cho bắt đầu.
- Số khách âm, không nguyên hoặc lớn hơn `int.MaxValue` bị từ chối; tính làm tròn bằng số nguyên mở rộng để tránh tràn số.
- Đề xuất dùng số khách cao điểm làm đầu vào. Với nhiều ngày, định mức áp dụng trong toàn bộ khoảng sự kiện; cho nhiều nhân viên thay phiên, kiểm tra từng đoạn giữa các mốc bắt đầu/kết thúc phân công. Đây là giả định cần duyệt nếu nghiệp vụ chỉ cần một số phòng ban trong một phần thời gian.
- Không tự chọn hoặc điều động người. Quản lý chọn người đáp ứng hợp đồng và lịch; khi thiếu nhân viên khả dụng phải báo số còn thiếu.
- Cho phép phân công vượt số cần nếu còn ngân sách; hiển thị số dư. Không cắt phân công tự động khi giảm khách.
- Chặn bắt đầu nếu có bất kỳ đoạn thời gian nào thiếu nhân sự. Khi đang diễn ra, chỉ được gỡ phân công chưa bắt đầu và việc gỡ không làm thiếu định biên ở đoạn thời gian còn lại; phải có người thay thế trước. Phân công đã bắt đầu phải giữ lại để xác nhận thời lượng và trả tiền, kể cả khi người đó đã trực xong.
- Người quản lý sự kiện chỉ được tính vào nhân sự khi có phân công hợp lệ tại phòng ban phù hợp.

### 2.2. Thù lao theo đầu người trong từng phòng ban

**Đã được người dùng xác nhận:** “Mỗi nhân viên thực tế tham gia nhận mức thưởng theo phòng ban”. Áp dụng đơn giá theo loại sự kiện và phòng ban tại thời điểm phân công; tổng phòng ban là tổng quyền lợi của những người thực tế tham gia. Cách quy đổi theo giờ/phút dưới đây là đề xuất của người lập plan, chưa được xác nhận.

| Loại sự kiện | Tiền sảnh | F&B | Bếp | Buồng phòng | Kỹ thuật | An ninh | IT |
|---|---:|---:|---:|---:|---:|---:|---:|
| Tiệc cưới | 40.000 | 50.000 | 60.000 | 35.000 | 55.000 | 40.000 | — |
| Hội nghị MICE | 45.000 | 45.000 | 55.000 | — | 60.000 | 45.000 | 65.000 |
| Gala Dinner | 45.000 | 55.000 | 65.000 | — | 65.000 | 45.000 | — |
| Festival ẩm thực | — | 50.000 | 65.000 | 40.000 | 55.000 | 45.000 | — |
| Đoàn VIP | 60.000 | 65.000 | 75.000 | 55.000 | — | 65.000 | — |

Đơn vị: **VNĐ/người/giờ**; là mức minh họa do người lập plan đề xuất, chưa phải mức đang có trong hệ thống. Dùng theo giờ để cùng quy tắc xử lý được sự kiện ngắn, qua đêm và nhiều ngày.

| Kết quả nghiệm thu | Hệ số chi trả |
|---|---:|
| Chưa đánh giá | Chưa được chốt tiền |
| Hoàn thành | 1,0 |
| Xuất sắc | 1,2 |
| Không tham gia | 0 |

`Tiền dự kiến = round(Đơn giá snapshot × Phút phân công / 60, 2)`

`Tiền thực tế = round(Đơn giá snapshot × Phút thực tế được xác nhận / 60 × Hệ số đánh giá, 2)`

`Dự phòng tối đa = round(Đơn giá snapshot × Phút phân công / 60 × 1,2, 2)`

- Không làm tròn giờ lên; chỉ nhận thời điểm phân công có độ chính xác đến phút. Số phút thực tế phải là số nguyên từ 0 đến số phút phân công; hoàn thành/xuất sắc cần lớn hơn 0, không tham gia phải bằng 0.
- Manager được giao hoặc HR/Admin xác nhận số phút thực tế và đánh giá; server tự tính tiền. Số phút là xác nhận thủ công có nhật ký, chưa phải dữ liệu chấm công tự động.
- Người vắng nhận 0; không phân phối lại tiền của người vắng cho người khác.
- Tổng dự phòng tối đa phải nằm trong ngân sách trước khi thêm người hoặc giảm ngân sách. Khi nghiệm thu, tổng thực tế phải nằm trong ngân sách.
- Tổng tiền phòng ban là tổng tiền từng người đã làm tròn, không lấy đơn giá nhân tổng biên chế. Không trả cho người không có phân công.
- Ví dụ: 5 nhân viên F&B tiệc cưới, mỗi người làm đủ 4 giờ, đều hoàn thành: mỗi người 200.000đ, cả nhóm 1.000.000đ. Nếu một người xuất sắc, người đó nhận 240.000đ, cả nhóm nhận 1.040.000đ. Dự phòng trước sự kiện là 1.200.000đ.
- Dùng `PhuCapDuKien` cho tiền cơ bản dự kiến và `SoTienPhuCap` cho tổng thực tế gồm thưởng. Hiển thị riêng phần cơ bản/phần thưởng từ công thức; chưa tách thêm một dòng thưởng vào phiếu lương vì dễ cộng trùng.

### 2.3. Quy tắc bảo toàn dữ liệu

- Bản đầu dùng bảng định mức trong code, phiên bản `1`; thay đổi định mức phải thêm phiên bản mới, giữ nguyên phiên bản đã sử dụng. Chưa tạo màn hình quản trị cấu hình riêng.
- Sự kiện mới lưu `PhienBanDinhMuc = 1`; sự kiện cũ để `null` và giữ cách nhập tiền hiện tại, hiển thị “Quy định cũ”.
- Lưu `MaPhongBanSuKien` và `DonGiaGio` tại phân công để giữ phòng ban/đơn giá lúc giao việc. Nhân viên chuyển phòng ban sau đó không làm đổi dữ liệu này.
- Chỉ HR/Admin chuyển sự kiện cũ sang phiên bản 1 khi còn lên kế hoạch và chưa có phân công; kiểm tra số khách và ngân sách trước khi lưu. Không âm thầm định giá lại phân công cũ.
- Cho đổi loại sự kiện khi đang lên kế hoạch và chưa có phân công. Có phân công thì phải gỡ/chọn lại trước khi đổi để tránh giữ nhầm đơn giá.
- Khi tăng/giảm khách ở giai đoạn kế hoạch, tính lại nhu cầu, giữ phân công và tiền đã ghi, báo thiếu/dư rõ ràng.
- Với phiên bản 1, không cho hủy kiểu xóa tiền khi có phân công đã bắt đầu. Giữ phân công, chuyển chờ nghiệm thu sau khi kết thúc và xác nhận số phút thực tế; chỉ được hủy và đưa tiền về 0 khi chưa có phân công nào bắt đầu. Không sửa hồi tố quy tắc hủy của dữ liệu legacy.
- Giữ giao dịch, khóa ghi, chống ghi đè `Version`, kiểm tra tài khoản hiện thời và nhật ký hiện tại.

## 3. Các bước triển khai

### Task 1: Quy tắc định biên và tính tiền dùng chung

**Files:** Create `src/OvertimeHotel.HRM.Core/Policies/HotelEventCompensationRules.cs`; Modify `src/OvertimeHotel.HRM.Core/Policies/HotelEventRules.cs`; Test `tests/HotelEvents/Program.cs`.

**Interfaces:** `GetRule(int policyVersion, string eventType, string departmentCode) -> EventDepartmentRule`; record `EventDepartmentRule(string DepartmentCode, int GuestsPerEmployee, decimal HourlyRate)`; `RequiredCount(int guests, int guestsPerEmployee) -> int`; `CalculateAllowance(decimal rate, int minutes, string rating) -> decimal`; `CalculateMaximumAllowance(decimal rate, int minutes) -> decimal`; `GetStaffingSummary(DuAn item) -> EventStaffingSummary`. Giữ overload cũ chỉ phục vụ sự kiện legacy.

- [ ] Viết test đỏ: đúng 27 tổ hợp loại/phòng ban, đơn giá và tỷ lệ theo hai bảng; loại/phòng ban/phiên bản lạ bị từ chối; `RequiredCount(20,20)==1`, `(21,20)==2`, `(300,20)==15`; 0 khách cho trạng thái chưa xác định; giá trị âm bị từ chối.
- [ ] Thêm test tiền: `CalculateAllowance(50000m,240,"HOAN_THANH")==200000m`, xuất sắc `240000m`, vắng `0m`, 1 phút hoàn thành `833.33m`; chưa đánh giá không được tính thực tế; dữ liệu âm, vượt giới hạn tiền bị từ chối.
- [ ] Thêm test độ phủ: 2 người mỗi người trực nửa buổi chỉ đáp ứng nhu cầu 1 người; nhu cầu 2 phải báo thiếu. Hai đoạn nối tiếp không tạo khoảng hở; một đoạn hở 1 phút phải báo thiếu. Nhóm dư người không bù nhóm khác.
- [ ] Chạy `dotnet run --project tests/HotelEvents/HotelEvents.csproj`, xác nhận test mới đỏ; triển khai catalog phiên bản 1 và quy tắc tính để toàn bộ test xanh.
- [ ] Mở rộng `EventDepartmentStatus` bằng `RequiredCount`, `MissingCount`, `ExcessCount`; `AssignedCount` là số người khác nhau đã giao, `IsSufficient` dựa trên độ phủ. `MissingCount` là thiếu lớn nhất trong các đoạn, `ExcessCount` là dư lớn nhất; thêm `MinimumConcurrentCount` để UI không nhầm tổng người với người cùng trực. Giữ `TotalRequired` là số phòng ban để không đổi nghĩa caller cũ.
- [ ] Commit phần quy tắc và test khi triển khai thực tế.

### Task 2: Lưu phiên bản, đơn giá và thời lượng xác nhận

**Files:** Modify `src/OvertimeHotel.HRM.Core/Models/DuAn.cs`, `src/OvertimeHotel.HRM.Core/Models/PhanCongDuAn.cs`, `src/OvertimeHotel.HRM.Data/Context/HotelEventConfiguration.cs`; Create `src/OvertimeHotel.HRM.Data/migrations/20261016_event_staffing_compensation.sql`; Test `tests/HotelEventsIntegration/Program.cs`.

**Interfaces:** `DuAn.PhienBanDinhMuc: int?`; `PhanCongDuAn.MaPhongBanSuKien: string?`, `DonGiaGio: decimal?`, `SoPhutThucTe: int?`. Nullable cho dữ liệu legacy, bắt buộc đầy đủ trong service cho phiên bản 1.

- [ ] Viết test đỏ cho roundtrip các trường mới và sự kiện legacy; test snapshot phòng ban/đơn giá không đổi sau khi nhân viên chuyển phòng ban.
- [ ] Thêm mapping và migration chạy lặp an toàn: cột nullable, đơn giá `numeric(15,2)` không âm, số phút không âm, mã phòng ban đúng độ dài; không backfill tiền hoặc đổi trạng thái cũ.
- [ ] Áp dụng hai lần trên database test có sự kiện cũ; xác nhận số tiền, phân công, trạng thái và tham chiếu phiếu lương cũ không đổi.
- [ ] Chạy test tích hợp với `EVENT_TEST_CONNECTION` trỏ database test đã áp dụng migration; commit model/mapping/migration và test sau khi xanh.

### Task 3: Áp dụng vào lưu, phân công, bắt đầu và nghiệm thu

**Files:** Modify `src/OvertimeHotel.HRM.Data/Services/HotelEventContracts.cs`, `IHotelEventService.cs`, `HotelEventService.cs`; Test `tests/HotelEventsIntegration/Program.cs`.

**Interfaces:** thêm `EventReviewInput.ActualMinutes: int?`; thêm `Task ApplyPolicyAsync(EventActor actor, int id, long version)` vào interface/service. Giữ `Allowance`/`ActualAllowance` chỉ cho legacy; sự kiện phiên bản 1 luôn tính lại trên server, không lấy tiền do client gửi.

- [ ] Viết test đỏ: sự kiện mới dùng phiên bản 1; giả mạo tiền phía client không thay đổi số tính; Manager không được đổi quy định; người không có quyền không đọc/ghi được sự kiện.
- [ ] Test 300 khách tiệc cưới có 1 người/phòng ban vẫn không bắt đầu được; đầy đủ độ phủ từng phòng ban mới bắt đầu được. Tăng khách phải báo thiếu; giảm khách không tự xóa người hoặc giảm tiền.
- [ ] Test phân công/gỡ người khi đang diễn ra không làm thủng độ phủ còn lại; kiểm tra chỉ đoạn chưa kết thúc bằng mốc `now` cố định trong test. Chỉ gỡ phân công chưa bắt đầu; từ chối xóa phân công đã bắt đầu, đã trực xong, đã đánh giá hoặc kết chuyển để giữ quyền nhận tiền. Hủy trước khi có người bắt đầu đưa tiền về 0; hủy khi đã có người bắt đầu bị từ chối và hướng dẫn nghiệm thu thời lượng thực tế.
- [ ] Test dự phòng vượt ngân sách bị từ chối; đủ dự phòng thì toàn bộ người được đánh giá xuất sắc vẫn trong ngân sách. Thêm người vượt định biên nhưng trong ngân sách vẫn nhận đủ đơn giá.
- [ ] Test số phút thực tế null/âm/vượt phân công, sai đánh giá, tiền vượt giới hạn bị từ chối; không tham gia bắt buộc 0 phút/0 tiền; sau nghiệm thu không sửa được. Phân công dài vẫn tính theo phút, không nhân thêm số ngày lần nữa.
- [ ] Cập nhật các method hiện hữu: `SaveAsync`, `AssignAsync`, `RemoveAssignmentAsync`, `ChangeStatusAsync`, `ReviewAsync`, `AcceptAsync`; dùng chung quy tắc Task 1, snapshot Task 2 và giao dịch hiện tại. `ApplyPolicyAsync` chỉ HR/Admin, kế hoạch, chưa phân công; ghi audit cùng giao dịch.
- [ ] Các test cũ đang dựa vào tiền nhập tay dùng fixture legacy rõ ràng; tạo fixture phiên bản 1 cho luồng mới, không hạ kỳ vọng test bảo mật/ngân sách/lịch sử.
- [ ] Chạy toàn bộ suite tích hợp; kiểm tra hai yêu cầu đồng thời vượt ngân sách hoặc gỡ người không vượt qua khóa và version. Commit khi xanh.

### Task 4: Hiển thị định mức và bảng tiền theo phòng ban

**Files:** Modify `src/OvertimeHotel.HRM.WebAdmin/Controllers/SuKienController.cs`, `Models/HotelEventViewModels.cs`, `Views/SuKien/_EventForm.cshtml`, `Views/SuKien/Details.cshtml`, `wwwroot/js/events.js`, `wwwroot/css/events.css`; Test `tests/hotel-events-ui.mjs`; Create `tests/hotel-events-compensation.py` theo mẫu Playwright hiện có.

**Interfaces:** `EventDetailsViewModel.StaffingSummary` dùng overload `DuAn`; thêm `DepartmentCosts: IReadOnlyList<EventDepartmentCost>` với record `EventDepartmentCost(string Code, int ParticipantCount, decimal Planned, decimal Reserved, decimal Actual)` trong file ViewModel. Dữ liệu server tính bằng service/rules, UI không quyết định tiền.

- [ ] Thêm test đỏ cho bảng 300 khách: cần 15 F&B; hiện có 5 người phải báo thiếu 10, đồng thời chỉ rõ đoạn thiếu nếu phân công không phủ đủ giờ.
- [ ] Form tạo/sửa hiển thị quy mô, nhu cầu và bảng đơn giá phiên bản 1; chi tiết có các cột phòng ban, cần cùng lúc, đã phân công, trực ít nhất, thiếu/dư, tiền dự kiến, dự phòng, thực tế. Kế hoạch 0 khách báo chưa xác định.
- [ ] Form phân công hiển thị đơn giá và tiền dự kiến tự tính; form đánh giá nhập phút thực tế và kết quả, hiển thị phần cơ bản/thưởng/tổng. Với legacy giữ input tiền hiện tại và nhãn quy định cũ.
- [ ] Dùng kết quả render/submit server làm nguồn chính; nếu có preview JS thì server trả kết quả tính, không nhân đôi catalog/công thức tiền trong JavaScript. Preview lỗi phải báo chưa tải được, không hiện số 0 như đã tính.
- [ ] Thêm nút áp dụng quy định cho sự kiện cũ đủ điều kiện; nút bắt đầu bị khóa kèm lý do, server vẫn kiểm tra khi POST trực tiếp.
- [ ] Test trình duyệt: đổi quy mô, vắng mặt, xuất sắc, tổng phòng ban, giả mạo tiền, lỗi giữ input/context, dùng bàn phím, màn hình 390px và desktop. Chạy `node tests/hotel-events-ui.mjs` và `python tests/hotel-events-compensation.py http://localhost:5077` với web build mới và fixture thử.
- [ ] Commit giao diện và test khi xanh.

### Task 5: Hồi quy, cập nhật tài liệu và bàn giao

**Files:** Modify `docs/hotel-events.md`; cập nhật phần loại sự kiện ở `PRD/02_Phan_Tich_Chuc_Nang_BFD.md`, `PRD/03_So_Do_Ngu_Canh_Va_DFD_Muc_1.md`, `PRD/04_Thiet_Ke_CSDL_17_Bang.md` để khớp 5 loại; không sửa hồi tố migration đã chạy.

- [ ] Chạy `dotnet run --project tests/HotelEvents/HotelEvents.csproj` và `node tests/hotel-events-ui.mjs`; kỳ vọng mọi case PASS, exit 0.
- [ ] Chạy `dotnet run --project tests/HotelEventsIntegration/HotelEventsIntegration.csproj` trên database test; kỳ vọng mọi case PASS, dữ liệu fixture được dọn theo runner.
- [ ] Chạy `dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj --no-restore -p:UseAppHost=false -o .probe/event-compensation-build`; kỳ vọng 0 lỗi, xử lý mọi warning mới do thay đổi này.
- [ ] Trên preview riêng chứa build mới, chạy lại test popup/ngày hiện có và test compensation; không khởi động lại web người dùng trong bước xác minh preview.
- [ ] Cập nhật tài liệu công thức, đơn vị, phiên bản, legacy và cách đọc tổng theo phòng ban; ghi kết quả thực chạy và các giới hạn. Commit tài liệu và kiểm tra `git diff --check`.

## 4. Tiêu chí nghiệm thu

- 5 loại sự kiện có đầy đủ tỷ lệ nhân sự và đơn giá của đúng phòng ban liên quan.
- Sự kiện ít khách cần ít người hơn theo ngưỡng; 100/300/600 khách tiệc cưới lần lượt cần 11/32/63 người cùng lúc theo bảng đề xuất.
- Không thể bắt đầu hoặc gỡ người đang cần nếu gây thiếu độ phủ; dư ở phòng ban khác không bù được.
- Hai người cùng phòng ban, cùng thời lượng và đánh giá nhận cùng số tiền; thời lượng khác nhau tính theo phút xác nhận.
- Bảng tổng phòng ban khớp tổng từng người; không tham gia nhận 0; xuất sắc cộng 20%; tổng tiền không vượt ngân sách.
- Client không sửa được tiền server tính; lịch sử không đổi khi đổi phòng ban/đơn giá ở phiên bản sau; bảo mật và khóa nghiệm thu giữ nguyên.
- Luồng sự kiện cũ và popup/lịch đang có vẫn hoạt động; mọi test liên quan chạy trên bản build mới.

## 5. Phạm vi và các quyết định cần duyệt

**Đã xác nhận:** mỗi nhân viên thực tế tham gia nhận mức thưởng theo phòng ban.

**Cần duyệt:** tính theo giờ/phút; hai bảng tỷ lệ/đơn giá; thưởng xuất sắc 20%; mức độ phủ yêu cầu suốt thời gian sự kiện; cách áp dụng cho sự kiện cũ. Chưa triển khai khi mới chỉ xác nhận cách hiểu theo đầu người.

Plan này hoàn tất quy định, định biên và số tiền được nghiệm thu tại phân hệ sự kiện. **Kết chuyển tự động vào phiếu lương là bước tiếp theo**, vì chưa có luồng tính lương/kết chuyển hoàn chỉnh trong phần code đã kiểm tra. Khi lập plan bước đó phải chốt kỳ nhận tiền, xử lý kỳ đã khóa, chống kết chuyển trùng bằng `MaChiTiet` và giữ snapshot tên khoản/số tiền; không chỉ cộng thẳng vào `TongThuong`.

Chưa bao gồm chia quỹ doanh thu/service charge, thuế/bảo hiểm, tự động điều động người, kết nối chấm công, cấu hình định mức qua màn hình hoặc phân bổ nhu cầu riêng cho từng giai đoạn sự kiện. Chỉ bổ sung nếu người dùng xác nhận có nhu cầu.
