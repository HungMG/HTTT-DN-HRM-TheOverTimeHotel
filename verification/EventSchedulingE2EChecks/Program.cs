using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.Data.Services;

Console.WriteLine("======================================================================");
Console.WriteLine("    BẮT ĐẦU CHUỖI KIỂM THỬ E2E: PHÂN CA SỰ KIỆN & RÀNG BUỘC MỚI");
Console.WriteLine("======================================================================\n");

int passed = 0;
int total = 0;

void Assert(bool condition, string testName, string? detail = null)
{
    total++;
    if (condition)
    {
        passed++;
        Console.WriteLine($"  [PASS] {testName}");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAIL] {testName} {(detail != null ? $"--> {detail}" : "")}");
        Console.ResetColor();
        throw new Exception($"Test failed: {testName}");
    }
}

// -------------------------------------------------------------------------------------------------
// 1. KIỂM THỬ CỔNG MỞ PHÂN CA QUẢN LÝ (Thứ Sáu 00:00 – Chủ Nhật 16:00, giờ VN)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("--- 1. Kiểm thử Ràng buộc Cổng mở phân ca Quản lý (Thứ Sáu 00:00 – Chủ Nhật 16:00) ---");
var vnOffset = ShiftSchedulingPolicy.VietnamOffset;

// Thứ 6: 00:00, 12:00, 23:59 VN -> Mở
var fri0000 = new DateTimeOffset(2026, 11, 6, 0, 0, 0, vnOffset);
var fri1200 = new DateTimeOffset(2026, 11, 6, 12, 0, 0, vnOffset);
Assert(ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(fri0000), "Thứ Sáu 00:00 VN: Cổng MỞ");
Assert(ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(fri1200), "Thứ Sáu 12:00 VN: Cổng MỞ");

// Thứ 7: Cả ngày -> Mở
var sat1800 = new DateTimeOffset(2026, 11, 7, 18, 0, 0, vnOffset);
Assert(ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(sat1800), "Thứ Bảy 18:00 VN: Cổng MỞ");

// Chủ Nhật: trước 16:00 -> Mở; từ 16:00 -> ĐÓNG
var sun1559 = new DateTimeOffset(2026, 11, 8, 15, 59, 59, vnOffset);
var sun1600 = new DateTimeOffset(2026, 11, 8, 16, 0, 0, vnOffset);
var sun1601 = new DateTimeOffset(2026, 11, 8, 16, 1, 0, vnOffset);
var sun2000 = new DateTimeOffset(2026, 11, 8, 20, 0, 0, vnOffset);
Assert(ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(sun1559), "Chủ Nhật 15:59 VN: Cổng MỞ");
Assert(!ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(sun1600), "Chủ Nhật 16:00 VN: Cổng ĐÓNG CHÍNH XÁC");
Assert(!ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(sun1601), "Chủ Nhật 16:01 VN: Cổng ĐÓNG");
Assert(!ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(sun2000), "Chủ Nhật 20:00 VN: Cổng ĐÓNG");

// Thứ Hai đến Thứ Năm -> Đóng
var mon0900 = new DateTimeOffset(2026, 11, 2, 9, 0, 0, vnOffset);
var thu1400 = new DateTimeOffset(2026, 11, 5, 14, 0, 0, vnOffset);
Assert(!ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(mon0900), "Thứ Hai 09:00 VN: Cổng ĐÓNG");
Assert(!ShiftSchedulingPolicy.IsManagerSchedulingWindowOpen(thu1400), "Thứ Năm 14:00 VN: Cổng ĐÓNG");


// -------------------------------------------------------------------------------------------------
// 2. KIỂM THỬ KHUNG GIỜ CÔNG BỐ LỊCH TUẦN (Chủ Nhật 17:00, giãn cách 1h sau khi đóng phân ca)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("\n--- 2. Kiểm thử Mốc Công bố Lịch tuần (Chủ Nhật 17:00, 1h giãn cách) ---");
var nextWeekStart = new DateOnly(2026, 11, 9); // Thứ Hai tuần tới

// Chủ Nhật 16:30 (nằm trong 1h giãn cách): Chưa được công bố định kỳ
var sun1630 = new DateTimeOffset(2026, 11, 8, 16, 30, 0, vnOffset);
Assert(!ShiftSchedulingPolicy.IsWeeklyPublishSchedule(sun1630, nextWeekStart), "Chủ Nhật 16:30 VN: Trong 1h đệm review, chưa tới giờ công bố định kỳ");

// Chủ Nhật 17:00 (đúng giờ công bố định kỳ): Hợp lệ
var sun1700 = new DateTimeOffset(2026, 11, 8, 17, 0, 0, vnOffset);
var sun1800 = new DateTimeOffset(2026, 11, 8, 18, 0, 0, vnOffset);
Assert(ShiftSchedulingPolicy.IsWeeklyPublishSchedule(sun1700, nextWeekStart), "Chủ Nhật 17:00 VN: ĐÚNG mốc công bố định kỳ");
Assert(ShiftSchedulingPolicy.IsWeeklyPublishSchedule(sun1800, nextWeekStart), "Chủ Nhật 18:00 VN: Hợp lệ công bố định kỳ");

// Ngày khác công bố: Bắt buộc coi là ngoài mốc định kỳ (cần lý do)
Assert(!ShiftSchedulingPolicy.IsWeeklyPublishSchedule(mon0900, nextWeekStart), "Thứ Hai công bố: Phải có lý do bổ sung ngoài mốc");


// -------------------------------------------------------------------------------------------------
// 3. KIỂM THỬ HƯỚNG A: TỰ CHỐT THEO GIỜ KẾT THÚC CA NGUỒN KHI QUÊN QUẸT RA
// -------------------------------------------------------------------------------------------------
Console.WriteLine("\n--- 3. Kiểm thử Hướng A: Tự chốt công sự kiện theo ca nguồn khi quên checkout ---");
var shiftStart = new DateTimeOffset(2026, 11, 7, 6, 0, 0, vnOffset);
var shiftEnd = new DateTimeOffset(2026, 11, 7, 14, 0, 0, vnOffset);
var assignStart = new DateTimeOffset(2026, 11, 7, 8, 0, 0, vnOffset);
var assignEnd = new DateTimeOffset(2026, 11, 7, 12, 0, 0, vnOffset);

// Case 3.1: Quẹt thẻ đầy đủ (Checkin 05:55, Checkout 14:05)
var checkInNormal = new DateTimeOffset(2026, 11, 7, 5, 55, 0, vnOffset);
var checkOutNormal = new DateTimeOffset(2026, 11, 7, 14, 5, 0, vnOffset);
var resNormal = HourlyEventRules.ResolveActualWindow(assignStart, assignEnd, shiftStart, shiftEnd, checkInNormal, checkOutNormal);
Assert(resNormal.HasValue, "Quẹt thẻ đủ: Tính được thời gian thực tế");
Assert(resNormal!.Value.Start == assignStart && resNormal.Value.End == assignEnd, "Thời gian đúng từ 08:00 đến 12:00");
Assert(!resNormal.Value.TuChotTheoCa, "Quẹt thẻ đủ: TuChotTheoCa = FALSE");

// Case 3.2: QUÊN CHECKOUT (Checkin 06:00, Checkout = NULL) --> Hướng A tự chốt theo shiftEnd 14:00
var resMissingCheckout = HourlyEventRules.ResolveActualWindow(assignStart, assignEnd, shiftStart, shiftEnd, checkInNormal, null);
Assert(resMissingCheckout.HasValue, "Quên Checkout: Hướng A KHÔNG bị mất công, tính được thời gian thực tế");
Assert(resMissingCheckout!.Value.Start == assignStart && resMissingCheckout.Value.End == assignEnd, "Thời gian sự kiện được chốt đủ 08:00 - 12:00 theo ca nguồn");
Assert(resMissingCheckout.Value.TuChotTheoCa, "Quên Checkout: Cờ TuChotTheoCa = TRUE chính xác");

// Case 3.3: Quên Check-in (Checkin = NULL, Checkout = 14:00) --> Không tính được
var resMissingCheckIn = HourlyEventRules.ResolveActualWindow(assignStart, assignEnd, shiftStart, shiftEnd, null, checkOutNormal);
Assert(!resMissingCheckIn.HasValue, "Không có Checkin: Trả về NULL (không tính công sự kiện)");

// Case 3.4: Check-in sau khi sự kiện đã kết thúc (Checkin 12:30)
var lateCheckIn = new DateTimeOffset(2026, 11, 7, 12, 30, 0, vnOffset);
var resLate = HourlyEventRules.ResolveActualWindow(assignStart, assignEnd, shiftStart, shiftEnd, lateCheckIn, checkOutNormal);
Assert(!resLate.HasValue, "Check-in sau giờ sự kiện kết thúc: Trả về NULL");


// -------------------------------------------------------------------------------------------------
// 4. KIỂM THỬ TRỰC TIẾP TRÊN CSDL THỰC TẾ SUPABASE CLOUD (E2E Integration)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("\n--- 4. Kiểm thử Tích hợp E2E trực tiếp trên CSDL Supabase Cloud ---");

var connStr = "Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.soethtbttktnwtxyxjrj;Password=Hungpro123@;SSL Mode=Require;Trust Server Certificate=true;No Reset On Close=true;";
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connStr)
    .Options;

using (var db = new AppDbContext(dbOptions))
{
    var service = new HotelEventService(db);
    var actor = new EventActor(1, 1, "Admin", "Nguyễn Đình Cường");

    // Lấy sự kiện test 1369 đã tạo cho ngày 07/11/2026
    var evt1369 = await service.GetAsync(actor, 1369);
    Assert(evt1369 != null, "Lấy được sự kiện E2E #1369 trên Supabase Cloud");

    // Kiểm tra cấu hình địa điểm đã được cố định là Khách sạn
    Assert(evt1369!.DiaDiem == "Khách sạn The Overtime", "Địa điểm sự kiện được khóa cố định là 'Khách sạn The Overtime'");

    // TEST 4.1: Chặn phân công bắt đầu trong quá khứ (Start < UtcNow)
    bool pastBlocked = false;
    try
    {
        var pastInput = new EventAssignmentInput
        {
            EmployeeId = 4, // Châu Quốc Bảo (Lễ tân)
            Role = "LE_TAN",
            Start = DateTime.UtcNow.AddMinutes(-45), // 45 phút trước
            End = DateTime.UtcNow.AddHours(2),
            Version = evt1369.Version
        };
        await service.AssignAsync(actor, 1369, pastInput);
    }
    catch (EventOperationException ex)
    {
        pastBlocked = ex.Field == "Assignment.Start" && ex.Message.Contains("Không thể gán phân công cho thời gian đã qua");
    }
    Assert(pastBlocked, "Chặn phân công có giờ bắt đầu trong quá khứ (Field: Assignment.Start)");

    // Lấy vai trò chuẩn của bộ phận Tiền sảnh cho sự kiện Hội nghị MICE
    var deptCode = "TIEN_SANH";
    var roles = HotelEventRoleRules.GetRoles(evt1369!.LoaiDuAn, deptCode, 0);
    Assert(roles.Count > 0, "Có danh mục vai trò cho TIEN_SANH trong HOI_NGHI_MICE");
    var testRole = roles[0].Name; // "Tiếp nhận đăng ký"
    Console.WriteLine($"  [DEBUG] Sử dụng vai trò: '{testRole}' cho bộ phận {deptCode}");

    // Đảm bảo nhu cầu sự kiện cho bộ phận TIEN_SANH với vai trò testRole là đúng 1 người
    var currentNeed = evt1369.NhuCaus.FirstOrDefault(n => n.MaPhongBan == deptCode && n.VaiTro == testRole);
    if (currentNeed == null)
    {
        db.NhuCauSuKiens.Add(new NhuCauSuKien
        {
            MaDuAn = 1369,
            MaPhongBan = deptCode,
            VaiTro = testRole,
            SoNguoi = 1,
            LaMau = false
        });
        await db.SaveChangesAsync();
        evt1369 = (await service.GetAsync(actor, 1369))!;
    }
    else if (currentNeed.SoNguoi != 1)
    {
        var needEntity = await db.NhuCauSuKiens.FindAsync(currentNeed.MaNhuCau);
        if (needEntity != null)
        {
            needEntity.SoNguoi = 1;
            await db.SaveChangesAsync();
            evt1369 = (await service.GetAsync(actor, 1369))!;
        }
    }

    // Xóa các phân công cũ của đợt test trước nếu có
    var oldAssignments = await db.PhanCongDuAns.Where(p => p.MaDuAn == 1369).ToListAsync();
    if (oldAssignments.Count > 0)
    {
        db.PhanCongDuAns.RemoveRange(oldAssignments);
        await db.SaveChangesAsync();
        evt1369 = (await service.GetAsync(actor, 1369))!;
    }

    // Tìm các nhân viên bộ phận FO (phòng ban 2) có ca làm việc giao nhau với sự kiện #1369
    var candidates = await db.PhanCas
        .Include(p => p.NhanVien)
        .Where(p => p.NhanVien!.MaPhongBan == 2
            && p.NgayLamViec == new DateOnly(2026, 11, 7)
            && p.TrangThai != "DA_HUY"
            && p.DaCongBo
            && p.BatDauDuKien < evt1369.NgayKetThuc
            && p.KetThucDuKien > evt1369.NgayBatDau)
        .OrderBy(p => p.MaNhanVien)
        .ToListAsync();

    Console.WriteLine($"  [DEBUG] NhuCaus hiện tại của sự kiện #1369 ({evt1369.NhuCaus.Count} nhu cầu):");
    foreach (var nc in evt1369.NhuCaus)
    {
        Console.WriteLine($"    - Nhu cầu #{nc.MaNhuCau}: Dept='{nc.MaPhongBan}', VaiTro='{nc.VaiTro}', SoNguoi={nc.SoNguoi}, LaMau={nc.LaMau}");
    }

    Console.WriteLine($"  [DEBUG] Tìm thấy {candidates.Count} nhân viên FO có ca làm việc trùng giờ sự kiện:");
    foreach (var c in candidates)
    {
        var code = HotelEventRules.ResolveDepartmentCode(c.NhanVien);
        Console.WriteLine($"    - NV #{c.MaNhanVien} ({c.NhanVien?.Ho} {c.NhanVien?.Ten}) - DeptCode: '{code}' (MaPB={c.NhanVien?.MaPhongBan}) - Ca #{c.MaCa} ({c.BatDauDuKien:HH:mm} -> {c.KetThucDuKien:HH:mm} UTC)");
    }
    Assert(candidates.Count >= 2, "Có ít nhất 2 nhân sự FO có ca trực trùng giờ sự kiện để test định biên và nối ca");

    var ca1 = candidates[0];
    var ca2 = candidates[1];

    // TEST 4.2: Gán người thứ 1 (ca1) thành công vào vai trò testRole
    var overlap1 = HourlyEventRules.Intersect(evt1369.NgayBatDau, evt1369.NgayKetThuc, ca1.BatDauDuKien, ca1.KetThucDuKien);
    Assert(overlap1.HasValue, $"Ca của NV #{ca1.MaNhanVien} giao nhau với thời gian sự kiện #1369");

    var assignStart1 = HotelEventRules.Local(overlap1!.Value.Start);
    var duration = (overlap1.Value.End - overlap1.Value.Start) / 2;
    var assignEnd1 = HotelEventRules.Local(overlap1.Value.Start + duration);
    var input1 = new EventAssignmentInput
    {
        EmployeeId = ca1.MaNhanVien,
        Role = testRole,
        Start = assignStart1,
        End = assignEnd1,
        ShiftId = ca1.MaPhanCa,
        Version = evt1369.Version
    };
    await service.AssignAsync(actor, 1369, input1);
    evt1369 = (await service.GetAsync(actor, 1369))!;
    Assert(evt1369.PhanCongs.Any(p => p.MaNhanVien == ca1.MaNhanVien && p.TrangThaiThamGia != "HUY_PHAN_CONG"),
        $"Gán thành công người thứ 1 (NV #{ca1.MaNhanVien}) vào sự kiện trong khung giờ ca trực");

    // Chọn ca2 cùng khung giờ hoặc ca trực để kiểm thử định biên
    var caOverlap2 = candidates.FirstOrDefault(c => c.MaNhanVien != ca1.MaNhanVien && c.MaCa == ca1.MaCa) ?? ca2;

    // TEST 4.3: Gán người thứ 2 CÙNG VAI TRÒ & TRÙNG KHUNG GIỜ GIAO NHAU --> PHẢI BỊ CHẶN!
    bool roleQuotaBlocked = false;
    string quotaMsg = "";
    try
    {
        var input2Overlap = new EventAssignmentInput
        {
            EmployeeId = caOverlap2.MaNhanVien,
            Role = testRole,
            Start = assignStart1, // Trùng khung giờ với người 1
            End = assignEnd1,
            ShiftId = caOverlap2.MaPhanCa,
            Version = evt1369.Version
        };
        await service.AssignAsync(actor, 1369, input2Overlap);
        Console.WriteLine("  [DEBUG TEST 4.3] AssignAsync KHÔNG ném ngoại lệ! Đã gán được.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [DEBUG TEST 4.3] Caught: {ex.GetType().Name} | Field: {(ex as EventOperationException)?.Field} | Msg: {ex.Message}");
        if (ex is EventOperationException eop)
        {
            roleQuotaBlocked = eop.Field == "Assignment.Role" && eop.Message.Contains("đã gán đủ nhu cầu");
            quotaMsg = eop.Message;
        }
    }
    Assert(roleQuotaBlocked, $"Chặn vượt định biên nhu cầu vai trò {testRole} (1/1 người trùng giờ)", quotaMsg);

    // TEST 4.4: Gán người thứ 2 KHUNG GIỜ KẾ TIẾP (sau khi người 1 kết thúc) --> THÀNH CÔNG!
    var assignStart2 = assignEnd1;
    var assignEnd2 = HotelEventRules.Local(overlap1.Value.End);

    if (assignStart2 < assignEnd2)
    {
        var input2Sequential = new EventAssignmentInput
        {
            EmployeeId = caOverlap2.MaNhanVien,
            Role = testRole,
            Start = assignStart2,
            End = assignEnd2,
            ShiftId = caOverlap2.MaPhanCa,
            Version = evt1369.Version
        };
        await service.AssignAsync(actor, 1369, input2Sequential);
        evt1369 = (await service.GetAsync(actor, 1369))!;
        Assert(evt1369.PhanCongs.Count(p => p.TrangThaiThamGia != "HUY_PHAN_CONG") == 2,
            $"Gán người thứ 2 (NV #{caOverlap2.MaNhanVien}) khung giờ nối tiếp thành công, hỗ trợ đổi ca sự kiện");
    }
}


// -------------------------------------------------------------------------------------------------
// 5. KIỂM THỬ KẾT NỐI HTTP E2E ĐẾN CỔNG WEBADMIN (http://localhost:5065)
// -------------------------------------------------------------------------------------------------
Console.WriteLine("\n--- 5. Kiểm thử Kết nối HTTP E2E trực tiếp đến WebAdmin (Port 5065) ---");
using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5065") };
try
{
    var response = await httpClient.GetAsync("/");
    Assert(response.StatusCode == System.Net.HttpStatusCode.Redirect || response.IsSuccessStatusCode,
        "WebAdmin cổng 5065 phản hồi sẵn sàng (HTTP " + (int)response.StatusCode + ")");
}
catch (Exception ex)
{
    Assert(false, "WebAdmin cổng 5065 không phản hồi", ex.Message);
}

Console.WriteLine("\n======================================================================");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"  KẾT QUẢ KIỂM THỬ E2E: {passed}/{total} BÀI TEST ĐẠT (100% PASS)");
Console.ResetColor();
Console.WriteLine("======================================================================\n");
