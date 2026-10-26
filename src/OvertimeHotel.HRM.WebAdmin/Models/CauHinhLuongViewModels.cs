using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public enum CauHinhLuongCategory { TangCa, PhuCap, KhauTru, Thuong }

public static class CauHinhLuongLabels
{
    /// <summary>Loại hiển thị riêng cho dòng tham số hệ số thưởng sự kiện theo giờ.</summary>
    public const string EventRateType = "HE_SO_SU_KIEN";

    public static string Title(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.TangCa => "Tăng ca",
        CauHinhLuongCategory.PhuCap => "Phụ cấp",
        CauHinhLuongCategory.KhauTru => "Khấu trừ",
        CauHinhLuongCategory.Thuong => "Thưởng",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static string Description(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.TangCa =>
            "Hệ số nhân cho từng loại giờ làm thêm và giờ làm ca đêm. Đơn giá giờ = Lương cơ bản ÷ 26 ÷ 8.",
        CauHinhLuongCategory.PhuCap =>
            "Các khoản cộng vào Tổng thu nhập (Gross). Khoản theo ngày công nhân trực tiếp; khoản cố định tháng được phân bổ theo ngày công thực tế.",
        CauHinhLuongCategory.KhauTru =>
            "Các khoản trừ khỏi Tổng thu nhập (Gross): bảo hiểm bắt buộc, thuế TNCN, phạt vi phạm và khấu trừ khác.",
        CauHinhLuongCategory.Thuong =>
            "Các khoản thưởng cộng vào Gross và chịu thuế TNCN: chuyên cần, phí phục vụ, thưởng dự án / sự kiện khách sạn.",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static IReadOnlyDictionary<string, string> Types(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.PhuCap => new Dictionary<string, string>
        {
            ["THEO_NGAY_CONG"] = "Theo ngày công", ["CO_DINH_THANG"] = "Cố định theo tháng"
        },
        CauHinhLuongCategory.KhauTru => new Dictionary<string, string>
        {
            ["BAO_HIEM"] = "Bảo hiểm", ["THUE"] = "Thuế TNCN",
            ["VI_PHAM"] = "Vi phạm", ["TAM_UNG"] = "Khấu trừ khác"
        },
        _ => new Dictionary<string, string>()
    };

    public static string Unit(CauHinhLuongCategory category, string? type) => category switch
    {
        CauHinhLuongCategory.TangCa => "hệ số",
        CauHinhLuongCategory.PhuCap when type == "THEO_NGAY_CONG" => "VNĐ/ngày công",
        CauHinhLuongCategory.PhuCap => "VNĐ/tháng",
        CauHinhLuongCategory.KhauTru when type == "BAO_HIEM" => "%",
        CauHinhLuongCategory.KhauTru when type == "VI_PHAM" => "VNĐ/phút",
        CauHinhLuongCategory.Thuong when type == EventRateType => "hệ số",
        _ => "VNĐ"
    };

    /// <summary>Công thức áp dụng của từng dòng cấu hình, bám theo PRD 05.</summary>
    public static string AppliedFormula(CauHinhLuongCategory category, string? type) => category switch
    {
        CauHinhLuongCategory.TangCa => "Đơn giá giờ × Số giờ OT × Hệ số",
        CauHinhLuongCategory.PhuCap when type == "THEO_NGAY_CONG" => "Định mức × Ngày công thực tế",
        CauHinhLuongCategory.PhuCap => "Định mức × (Ngày công thực tế ÷ 26)",
        CauHinhLuongCategory.KhauTru when type == "BAO_HIEM" => "Lương cơ bản × Tỷ lệ %",
        CauHinhLuongCategory.KhauTru when type == "THUE" => "Biểu thuế lũy tiến từng phần",
        CauHinhLuongCategory.KhauTru when type == "VI_PHAM" => "Số phút vi phạm × Định mức",
        CauHinhLuongCategory.KhauTru => "Số tiền khấu trừ theo phát sinh",
        CauHinhLuongCategory.Thuong when type == EventRateType => "Lương tháng ÷ 26 ÷ 8 × Giờ sự kiện × Hệ số",
        CauHinhLuongCategory.Thuong => "Định mức cấu hình (snapshot khi chốt)",
        _ => "—"
    };

    /// <summary>Hiển thị giá trị kèm đơn vị đọc được, tránh nhập nhằng % và VNĐ.</summary>
    public static string ValueText(CauHinhLuongCategory category, string? type, decimal value) => category switch
    {
        CauHinhLuongCategory.TangCa => value.ToString("N2") + "×",
        CauHinhLuongCategory.PhuCap when type == "THEO_NGAY_CONG" => value.ToString("N0") + " đ/ngày",
        CauHinhLuongCategory.PhuCap => value.ToString("N0") + " đ/tháng",
        CauHinhLuongCategory.KhauTru when type == "BAO_HIEM" => value.ToString("N2") + "%",
        CauHinhLuongCategory.KhauTru when type == "THUE" => "Lũy tiến",
        CauHinhLuongCategory.KhauTru when type == "VI_PHAM" => value.ToString("N0") + " đ/phút",
        CauHinhLuongCategory.KhauTru => value.ToString("N0") + " đ",
        CauHinhLuongCategory.Thuong when type == EventRateType => value.ToString("N2") + "×",
        CauHinhLuongCategory.Thuong => value.ToString("N0") + " đ",
        _ => value.ToString("N2")
    };

    public static string TypeLabel(CauHinhLuongCategory category, string? type)
    {
        var types = Types(category);
        if (!string.IsNullOrEmpty(type) && types.TryGetValue(type, out var label)) return label;
        return category switch
        {
            CauHinhLuongCategory.TangCa => "Hệ số theo loại ngày",
            CauHinhLuongCategory.Thuong when type == EventRateType => "Tham số hệ số sự kiện",
            CauHinhLuongCategory.Thuong => "Thưởng chịu thuế",
            _ => Title(category)
        };
    }
}

public class CauHinhLuongRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public decimal Value { get; init; }
    public bool Taxable { get; init; }
    public string? Description { get; init; }
    public bool Used { get; init; }
    public int UsageCount { get; init; }
    /// <summary>Tham số hệ số (không phải khoản chi) — chỉ cho sửa giá trị, không cho xóa.</summary>
    public bool IsParameter { get; init; }
}

/// <summary>Một tham số đang có hiệu lực, dùng cho khối "Công thức &amp; tham số" đầu trang.</summary>
public class CauHinhLuongParameter
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string Unit { get; init; } = "";
    public string? Hint { get; init; }
}

/// <summary>Hướng dẫn tính lương của từng danh mục, lấy từ PRD 05 và giá trị cấu hình hiện hành.</summary>
public class CauHinhLuongGuide
{
    public string Formula { get; init; } = "";
    public string? FormulaNote { get; init; }
    public IReadOnlyList<CauHinhLuongParameter> Parameters { get; init; } = [];
    public IReadOnlyList<string> Rules { get; init; } = [];
}

public class CauHinhLuongListViewModel
{
    public CauHinhLuongCategory Category { get; set; }
    public string? Keyword { get; set; }
    public string? Type { get; set; }
    public List<CauHinhLuongRow> Items { get; set; } = [];
    public string? Error { get; set; }
    public CauHinhLuongGuide Guide { get; set; } = new();
    public int UsedCount => Items.Count(x => x.Used);
    public int TotalUsageCount => Items.Sum(x => x.UsageCount);
}

public class CauHinhLuongFormViewModel : IValidatableObject
{
    [EnumDataType(typeof(CauHinhLuongCategory))]
    public CauHinhLuongCategory Category { get; set; }
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên cấu hình.")]
    [StringLength(150, ErrorMessage = "Tên không vượt quá 150 ký tự.")]
    public string Name { get; set; } = string.Empty;
    [StringLength(30)]
    public string? Type { get; set; }
    [BindRequired]
    public decimal Value { get; set; }
    public bool Taxable { get; set; }
    [StringLength(255, ErrorMessage = "Mô tả không vượt quá 255 ký tự.")]
    public string? Description { get; set; }

    /// <summary>Ví dụ tính tiền hiển thị ngay trên form để HR kiểm tra trước khi lưu.</summary>
    public string FormulaHint => CauHinhLuongLabels.AppliedFormula(Category, Type);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Category == CauHinhLuongCategory.TangCa && Name.Length > 100)
            yield return new("Tên loại tăng ca không vượt quá 100 ký tự.", [nameof(Name)]);
        var types = CauHinhLuongLabels.Types(Category);
        if (types.Count > 0 && !types.ContainsKey(Type ?? ""))
            yield return new("Vui lòng chọn loại hợp lệ.", [nameof(Type)]);
        if (Value < 0 || Value > 9999999999999.99m ||
            (Category == CauHinhLuongCategory.TangCa && (Value <= 0 || Value > 99.99m)))
            yield return new("Giá trị nằm ngoài phạm vi cho phép.", [nameof(Value)]);
        if (decimal.Round(Value, 2) != Value)
            yield return new("Giá trị chỉ được có tối đa hai chữ số thập phân.", [nameof(Value)]);
        if (Category == CauHinhLuongCategory.KhauTru && Type == "BAO_HIEM" && Value > 100)
            yield return new("Tỷ lệ bảo hiểm không vượt quá 100%.", [nameof(Value)]);
    }
}
