using System.Globalization;
using System.Text.Json;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.MobileApp.Services;

/// <summary>Maps Supabase snake_case fields to the shared attendance calculator's inputs.</summary>
public static class AttendanceSummaryPostgrstMapper
{
    public static JsonElement[] ReadRows(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Dữ liệu bảng công phải là danh sách.");
        return document.RootElement.EnumerateArray().Select(row => row.ValueKind == JsonValueKind.Object
            ? row.Clone() : throw new JsonException("Dòng bảng công không hợp lệ.")).ToArray();
    }

    public static PhanCa ToShift(JsonElement row)
    {
        var attendance = PostgrstJson.ReadEmbedded(row, "cham_cong");
        return new PhanCa
        {
            MaPhanCa = ReadId(row, "ma_phan_ca"),
            MaNhanVien = ReadId(row, "ma_nhan_vien"),
            MaCa = ReadId(row, "ma_ca"),
            NgayLamViec = ReadDate(row, "ngay_lam_viec"),
            BatDauDuKien = ReadTimestamp(row, "bat_dau_du_kien") ?? throw new JsonException("Thiếu giờ bắt đầu ca."),
            KetThucDuKien = ReadTimestamp(row, "ket_thuc_du_kien") ?? throw new JsonException("Thiếu giờ kết thúc ca."),
            TrangThai = row.GetProperty("trang_thai").GetString() ?? "",
            CaLamViec = new CaLamViec { TenCa = PostgrstJson.ReadEmbeddedString(row, "ca_lam_viec", "ten_ca") },
            ChamCong = attendance is { } value ? new ChamCong
            {
                MaChamCong = ReadId(value, "ma_cham_cong"),
                GioCheckIn = ReadTimestamp(value, "gio_check_in"),
                GioCheckOut = ReadTimestamp(value, "gio_check_out"),
                TrangThai = value.GetProperty("trang_thai").GetString() ?? ""
            } : null
        };
    }

    public static DonTu ToLeave(JsonElement row)
    {
        var type = PostgrstJson.ReadEmbedded(row, "loai_don")
            ?? throw new JsonException("Thiếu cấu hình loại đơn nghỉ.");
        if (!type.TryGetProperty("co_huong_luong", out _))
            throw new JsonException("Thiếu cấu hình phép hưởng lương.");
        return new DonTu
        {
            MaDon = ReadId(row, "ma_don"),
            MaNhanVien = ReadId(row, "ma_nhan_vien"),
            MaLoaiDon = ReadId(row, "ma_loai_don"),
            TuNgay = ReadDate(row, "tu_ngay"),
            DenNgay = ReadDate(row, "den_ngay"),
            TrangThai = row.GetProperty("trang_thai").GetString() ?? "",
            NgayDuyet = ReadTimestamp(row, "ngay_duyet"),
            LoaiDon = new LoaiDon
            {
                TenLoaiDon = type.GetProperty("ten_loai_don").GetString() ?? "",
                CoHuongLuong = PostgrstJson.ReadBool(type, "co_huong_luong")
            }
        };
    }

    private static int ReadId(JsonElement row, string column) =>
        PostgrstJson.ReadNullableInt(row, column) is int value && value > 0
            ? value : throw new JsonException($"Mã {column} không hợp lệ.");

    private static DateOnly ReadDate(JsonElement row, string column) =>
        DateOnly.TryParseExact(row.GetProperty(column).GetString(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value : throw new JsonException($"Ngày {column} không hợp lệ.");

    private static DateTimeOffset? ReadTimestamp(JsonElement row, string column)
    {
        var property = row.GetProperty(column);
        if (property.ValueKind == JsonValueKind.Null) return null;
        return DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var value)
            ? value : throw new JsonException($"Giờ {column} không hợp lệ.");
    }
}
