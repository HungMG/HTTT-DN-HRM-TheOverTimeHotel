# TSK-20 — Công phép và giao diện Mobile

## Phạm vi đã triển khai

Màn hình **Công & nghỉ phép** tổng hợp theo tháng từ `don_tu`, `loai_don`, `phan_ca` và `cham_cong`. Khi duyệt đơn thành công ở TSK-19, lần tải bảng công tiếp theo tự sử dụng trạng thái mới đã lưu trong Supabase. Các thay đổi lịch ca hoặc trạng thái đơn cũng phản ánh ở lần tải tiếp theo.

Không thêm bảng, không migration, không tạo giờ check-in/check-out cho ngày nghỉ, không ghi lại phiếu lương đã chốt. Kết quả là dữ liệu tạm tính cho TSK-21; khóa sổ và lưu snapshot thuộc TSK-22.

## Quy tắc

1. Chỉ tính đơn `DA_DUYET` với `LoaiDon.CoHuongLuong == true`, theo cấu hình loại đơn thực tế trong CSDL, không dựa vào mã loại đơn cố định.
2. Khoảng nghỉ bao gồm `TuNgay` và `DenNgay`, được giới hạn trong kỳ đang xem. Ngày phải có ít nhất một phân ca còn hợp lệ (`DA_PHAN`, `HOAN_THANH`, `VANG`) mới được cộng công phép. Chủ nhật có phân ca vẫn được tính.
3. Đơn đã duyệt nhưng ngày không có lịch ca vẫn hiển thị để giải thích vì sao công bằng 0.
4. Một ngày được tính tối đa 1 công dù có nhiều ca hoặc nhiều đơn nghỉ trùng nhau. Giữ danh sách tất cả mã đơn nguồn để đối chiếu.
5. Công thực tế cần có cả giờ vào/ra hợp lệ (giờ ra lớn hơn giờ vào), trạng thái `DUNG_GIO`, `DI_TRE`, `VE_SOM` hoặc `TRE_VA_SOM`. Ca đêm thuộc `NgayLamViec`, kể cả khi check-out sang tháng sau.
6. Ngày vừa có công thực tế vừa có phép hưởng lương được tính ở công thực tế, không cộng thêm công phép. Bất kỳ giờ vào/ra nào trùng ngày có phép đều được đánh dấu cần đối chiếu; bản ghi chưa đủ check-out chưa tính công thực tế nhưng công phép hợp lệ vẫn được ghi nhận một lần.
7. `TotalCreditedDays = WorkedDays + PaidLeaveDays`. Công phép không sinh OT, giờ làm đêm, phụ cấp ăn ca hay giờ quẹt thẻ. Những khoản phụ cấp cần đi làm thực tế phải lấy riêng từ chấm công khi làm TSK-21.
8. Bảng công gồm cả phép đã duyệt cho ngày sắp tới trong tháng và được ghi rõ là tạm tính. Đây là tổng hợp theo ngày nguyên; nghỉ nửa ngày/giờ chưa có trong schema đơn từ hiện tại.

## Mã nguồn và tích hợp

- `Core/Services/AttendanceSummaryCalculator.cs`: phép tính thuần, không thay đổi dữ liệu đầu vào.
- `Data/Services/IAttendanceSummaryService.cs` và `AttendanceSummaryService.cs`: đọc EF Core/Supabase theo nhân viên và khoảng ngày (tối đa 366 ngày).
- `Core/DTOs/AttendanceSummaryDto.cs`: tổng công và chi tiết ngày/ca/đơn nguồn.
- `MobileApp/Services/IMobileAttendanceSummaryService.cs` và `MobileAttendanceSummaryService.cs`: đọc trực tiếp Supabase PostgREST bằng JWT hiện tại, phân trang lịch ca và đơn nghỉ, rồi dùng bộ tính công trong Core. Tách riêng với `IAttendanceService` của module quẹt thẻ mới; app không phụ thuộc địa chỉ API WebAdmin.
- `MobileApp/Services/AttendanceSummaryPostgrstMapper.cs`: đọc JSON snake_case và quan hệ nhúng dạng object/array, không trả tổng 0 khi phản hồi lỗi hoặc thiếu cấu hình loại đơn.
- `WebAdmin/Controllers/Api/AttendanceController.cs`: endpoint `GET /api/mobile/attendance/summary?year=2026&month=10&employeeId=5`.
- Bỏ `employeeId` để xem chính mình. API dùng JWT scheme `MobileBearer` và claim `ma_nhan_vien` từ Supabase hook (không parse UUID `sub` thành mã tài khoản); tài khoản, vai trò và phòng ban được kiểm tra lại từ DB. Employee chỉ xem bản thân, Manager được xem nhân viên cùng phòng ban, HR/Admin xem toàn khách sạn. Vai trò không hợp lệ bị từ chối. Mobile kiểm tra phạm vi để hiển thị lỗi; RLS trên Supabase chịu trách nhiệm chặn truy cập dữ liệu thực tế.
- API trả 400 khi tham số không hợp lệ, 401 khi phiên/tài khoản không hợp lệ, 403 khi không có quyền, 503 khi không truy cập được CSDL; không trả số công giả khi lỗi kết nối.
- `MobileApp/Views/AttendanceSummaryPage.xaml`: giao diện dùng màu xanh navy/xanh lá, thẻ bo góc, font SegoeUI và thao tác quay lại theo các trang sẵn có.

Trong TSK-21, inject `IAttendanceSummaryService` và gọi:

```csharp
var attendance = await attendanceSummaryService.GetAsync(employeeId, fromDate, toDate, cancellationToken);
// attendance.TotalCreditedDays: số ngày công cho thành phần lương ngày.
// attendance.WorkedDays: số ngày có chấm công thực tế.
// attendance.Days: chi tiết ngày, phân ca, mã đơn và các ngày cần đối chiếu.
```

TSK-21/22 cần xử lý các ngày `NeedsReview` trước khi chốt lương và lưu snapshot khi khóa sổ, thay vì sửa kết quả kỳ đã chốt theo dữ liệu đang thay đổi.

## Cách sử dụng Mobile

1. Trang chủ theo vai trò (Nhân viên/Quản lý/Nhân sự) → thẻ **Công & nghỉ phép** → **Xem bảng công** để xem công cá nhân. Dùng cấu trúc trang mới, không khôi phục `MainPage` đã bị remote xóa.
2. Người duyệt mở chi tiết đơn → **Xem công & nghỉ phép của nhân viên**. Màn hình mở đúng nhân viên và tháng bắt đầu của đơn.
3. Dùng tháng trước/tháng sau hoặc **Về tháng hiện tại**. Bộ lọc **Có đơn phép** giữ cả ngày có đơn đã duyệt nhưng chưa có lịch ca.
4. Kéo xuống hoặc bấm tải lại để cập nhật sau khi duyệt đơn/thay lịch ca. Mở lại trang cũng tải mới.
5. Thẻ từng ngày hiển thị loại công, số công, lịch ca, mã đơn/loại đơn/khoảng nghỉ/ngày duyệt. Ngày trùng với chấm công có cảnh báo.
6. Khi mất mạng/lỗi máy chủ, màn hình hiển thị lỗi và nút thử lại; khi hết phiên, có nút đăng nhập lại. Không giữ số công cũ của kỳ trước khi tải kỳ mới bị lỗi.

## Chế độ Demo

`DemoAttendanceSummaryService` dùng cùng danh sách đơn trong bộ nhớ của singleton `DemoLeaveApprovalService`, cùng bộ tính công với API thật và cùng quy tắc phân quyền. Duyệt một đơn ở demo rồi mở bảng công sẽ thấy kết quả mới. Lịch mẫu là ca sáng thứ Hai–thứ Bảy; ca và quẹt thẻ hôm nay của chính mình lấy từ `DemoAttendanceService` mới để khớp màn hình chấm công. Có banner dữ liệu mẫu. Demo không gọi Supabase.

Luồng thử: tài khoản `manager` → đơn của nhân viên `employee` (đơn #101) → xem công trước duyệt → quay lại duyệt → mở lại từ danh sách đã xử lý → xem công sau duyệt. Ngày có phân ca trong khoảng đơn được cộng, Chủ nhật của lịch demo không được cộng.

## Kiểm tra

Chạy kiểm tra nghiệp vụ độc lập, không kết nối DB:

```powershell
dotnet run --project tests/AttendanceRules/AttendanceRules.csproj
```

24 trường hợp bao gồm đơn chưa duyệt/từ chối/không lương, ngày không có ca, đơn trùng, công thực tế trùng phép, chấm công thiếu giờ, ca đêm qua tháng, tháng nhuận, thay đổi trạng thái/lịch ca, tính lại không cộng dồn, dữ liệu API JSON và phạm vi phân quyền. Bốn kiểm tra tích hợp mới xác nhận JSON Supabase object/array, quan hệ `cham_cong`, phép hưởng lương từ danh mục và báo lỗi khi dữ liệu không hợp lệ.

Build:

```powershell
dotnet build src/OvertimeHotel.HRM.WebAdmin/OvertimeHotel.HRM.WebAdmin.csproj
dotnet build src/OvertimeHotel.HRM.MobileApp/OvertimeHotel.HRM.MobileApp.csproj -f net10.0-windows10.0.19041.0
dotnet build src/OvertimeHotel.HRM.MobileApp/OvertimeHotel.HRM.MobileApp.csproj -f net10.0-android
```

Chạy các build lần lượt vì cùng tham chiếu output của Core.

Đã kiểm tra ngày 07/10/2026: WebAdmin, Mobile Windows và Mobile Android đều build thành công, 0 cảnh báo, 0 lỗi. Mã Android trong safe area, status bar và log provider được giới hạn bằng `#if ANDROID` để không gây lỗi Windows. Service chấm công đọc đúng quan hệ `cham_cong` của PostgREST.

## Rà soát checkout Android ngày 09/10/2026

Bản tích hợp vào checkout mới nhất được kiểm tra lại và sửa ba phần còn thiếu:

- Đăng ký `DemoLeaveApprovalService` một lần rồi dùng cùng singleton cho màn duyệt đơn và bảng công; tránh lỗi không resolve được dependency hoặc dùng hai danh sách đơn khác nhau.
- Thêm nút **Xem công & nghỉ phép của nhân viên** tại chi tiết đơn và truyền đúng mã nhân viên/tháng bắt đầu của đơn vào route.
- Bổ sung endpoint `summary` và đăng ký `IAttendanceSummaryService` trong WebAdmin, khớp phạm vi đã mô tả ở trên.

Kết quả đã thực hiện trên `mobile-latest-build`:

- `tests/AttendanceRules`: 24/24 kiểm tra nghiệp vụ và ánh xạ PostgREST đạt.
- `tests/AttendanceFlow -- --live`: 13/13 kiểm tra luồng đạt. Gồm HR duyệt/từ chối trong bộ nhớ Demo, singleton dùng chung, tải lại không cộng dồn, phân quyền, tháng không hợp lệ, hủy tải và đăng xuất; phần Cloud đăng nhập tài khoản mẫu rồi chỉ đọc dữ liệu Supabase.
- Nhân viên và HR xem cùng nhân viên trên Cloud nhận cùng số công và số ngày. Đổi kỳ không giữ dữ liệu tháng trước. Danh sách HR có thông tin tên/phòng ban cần cho việc duyệt.
- Build Android tự chứa (`EmbedAssembliesIntoApk=true`) và WebAdmin thành công, 0 cảnh báo/0 lỗi. APK đã được cài cập nhật bằng `adb install -r`.

Chạy kiểm tra luồng offline:

```powershell
dotnet run --project tests/AttendanceFlow/AttendanceFlow.csproj
```

Kiểm tra đọc Cloud dùng `-- --live` và biến môi trường `TSK20_TEST_PASSWORD` của tài khoản mẫu trong `LoginViewModel`. Không ghi token vào file/log. Kiểm tra Cloud không duyệt/từ chối, tạo, sửa hoặc xóa đơn thật.

**Giới hạn nghiệm thu:** thao tác duyệt/từ chối trên Cloud thật chưa được thực hiện trong lần rà soát này; luồng thay đổi trạng thái và cập nhật bảng công được kiểm tra trong Demo. Endpoint WebAdmin đã build nhưng chưa chạy kiểm tra HTTP với DB. Giao diện cuối trên điện thoại cần mở khóa thiết bị; chữ lớn, mất mạng và phiên hết hạn trên UI chưa được nghiệm thu. Build và kiểm tra dịch vụ không thay thế các bước này.
