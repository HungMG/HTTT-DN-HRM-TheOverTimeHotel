using OvertimeHotel.HRM.Core.DTOs;
using OvertimeHotel.HRM.Core.Policies;

namespace OvertimeHotel.HRM.Core.Services;

/// <summary>
/// Tổng hợp công tháng theo từng nhân viên (PRD 4.4).
///
/// <para>
/// Hàm thuần: nhận danh sách lượt phân ca đã chiếu sẵn và mốc "bây giờ", trả về
/// bảng tổng hợp. Không truy vấn CSDL, không đọc đồng hồ hệ thống bên trong — nhờ
/// vậy cùng một dữ liệu luôn cho cùng một kết quả, và tháng cũ không bị đổi số
/// chỉ vì hôm nay đã trôi qua thêm vài ngày.
/// </para>
/// </summary>
public static class MonthlyAttendanceCalculator
{
    /// <summary>
    /// Một ca được coi là "đã kết thúc" khi giờ kết thúc dự kiến đã qua. Chỉ những
    /// ca này mới vào mẫu số của tỷ lệ chuyên cần.
    /// </summary>
    private static bool DaKetThuc(MonthlyAttendanceShiftInput s, DateTimeOffset now) =>
        s.KetThucDuKien <= now;

    /// <summary>
    /// Ca có dữ liệu quẹt thẻ vào hoặc ra. Ca vắng hoàn toàn không có cả hai.
    /// </summary>
    private static bool CoMat(MonthlyAttendanceShiftInput s) =>
        s.GioCheckIn.HasValue || s.GioCheckOut.HasValue;

    public static MonthlyAttendanceReportDto Calculate(
        DateOnly from,
        DateOnly to,
        IEnumerable<MonthlyAttendanceShiftInput> shifts,
        IReadOnlyDictionary<int, (string HoTen, string? PhongBan, string? ChucVu)> employees,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(employees);

        if (to < from)
        {
            throw new ArgumentException("Khoảng tổng hợp công không hợp lệ: ngày kết thúc trước ngày bắt đầu.");
        }

        var theoNhanVien = shifts
            .Where(s => s.NgayLamViec >= from && s.NgayLamViec <= to)
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Ca vắng: phân ca vẫn ở trạng thái DA_PHAN/VANG_MAT mà hết giờ vẫn không
        // có dấu vết quẹt thẻ. Đếm sau khi đã biết ca kết thúc để không gọi một
        // ca chiều nay (chưa tới giờ) là vắng.
        static bool LaVang(MonthlyAttendanceShiftInput s, DateTimeOffset now) =>
            !CoMat(s) &&
            DaKetThuc(s, now) &&
            !string.Equals(s.PhanCaTrangThai, "HOAN_THANH", StringComparison.Ordinal);

        var rows = new List<MonthlyAttendanceRowDto>(theoNhanVien.Count);

        foreach (var (employeeId, dsCa) in theoNhanVien.OrderBy(kv => kv.Key))
        {
            employees.TryGetValue(employeeId, out var thongTin);
            var ten = string.IsNullOrWhiteSpace(thongTin.HoTen) ? $"Nhân sự #{employeeId}" : thongTin.HoTen;

            var daKetThuc = dsCa.Where(s => DaKetThuc(s, now)).ToList();
            var coMat = dsCa.Where(CoMat).ToList();

            var dungGio = daKetThuc.Count(s => string.Equals(s.ChamCongTrangThai, AttendanceCalculator.STATUS_DUNG_GIO, StringComparison.Ordinal));
            var diTre = daKetThuc.Count(s => string.Equals(s.ChamCongTrangThai, AttendanceCalculator.STATUS_DI_TRE, StringComparison.Ordinal));
            var veSom = daKetThuc.Count(s => string.Equals(s.ChamCongTrangThai, AttendanceCalculator.STATUS_VE_SOM, StringComparison.Ordinal));
            var treVaSom = daKetThuc.Count(s => string.Equals(s.ChamCongTrangThai, AttendanceCalculator.STATUS_TRE_VA_SOM, StringComparison.Ordinal));

            var vang = daKetThuc.Count(s => LaVang(s, now));

            // "Chưa check-in" là ca đang trong khung giờ nhưng nhân viên chưa quẹt
            // vào và cũng chưa quẹt ra. Tách khỏi vắng để quản lý thấy tức thời
            // chứ không phải chờ hết ca mới biết.
            var chuaCheckIn = dsCa.Count(s => !CoMat(s) && s.BatDauDuKien <= now && s.KetThucDuKien > now);

            // Tổng phút lấy trên mọi ca có dữ liệu chấm công, kể cả ca chưa kết thúc:
            // số phút đi trễ đã ghi nhận lúc quẹt vào là sự thật, không phụ thuộc
            // việc ca đã xong hay chưa.
            var tongPhutTre = dsCa.Sum(s => Math.Max(0, s.PhutDiTre));
            var tongPhutVeSom = dsCa.Sum(s => Math.Max(0, s.PhutVeSom));

            // OT và giờ làm thực tế chỉ cộng trên ca đã có đủ cặp giờ vào/ra — ca
            // đang trực cộng dở dang sẽ thổi phồng số giờ công.
            decimal tongOt = 0m;
            decimal tongGio = 0m;
            foreach (var ca in dsCa)
            {
                if (ca.GioCheckIn.HasValue && ca.GioCheckOut.HasValue && ca.GioCheckOut.Value > ca.GioCheckIn.Value)
                {
                    tongGio += (decimal)Math.Round((ca.GioCheckOut.Value - ca.GioCheckIn.Value).TotalHours, 2);
                    tongOt += ca.GioTangCaOt;
                }
            }

            rows.Add(new MonthlyAttendanceRowDto(
                employeeId,
                ten,
                thongTin.PhongBan,
                thongTin.ChucVu,
                TongCaPhanCong: dsCa.Count,
                SoCaDaKetThuc: daKetThuc.Count,
                SoCaCoMat: coMat.Count,
                SoCaDungGio: dungGio,
                SoCaDiTre: diTre,
                SoCaVeSom: veSom,
                SoCaTreVaSom: treVaSom,
                SoCaVangMat: vang,
                SoCaChuaCheckIn: chuaCheckIn,
                TongPhutTre: tongPhutTre,
                TongPhutVeSom: tongPhutVeSom,
                TongGioOt: tongOt,
                TongGioLamThucTe: tongGio));
        }

        return new MonthlyAttendanceReportDto(from, to, rows);
    }
}
