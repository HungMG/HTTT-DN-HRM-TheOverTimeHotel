using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public enum CauHinhLuongCategory { TangCa, PhuCap, KhauTru, Thuong }

public static class CauHinhLuongLabels
{
    public static string Title(CauHinhLuongCategory category) => category switch
    {
        CauHinhLuongCategory.TangCa => "Tăng ca",
        CauHinhLuongCategory.PhuCap => "Phụ cấp",
        CauHinhLuongCategory.KhauTru => "Khấu trừ",
        CauHinhLuongCategory.Thuong => "Thưởng",
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
        _ => "VNĐ"
    };
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
}

public class CauHinhLuongListViewModel
{
    public CauHinhLuongCategory Category { get; set; }
    public string? Keyword { get; set; }
    public string? Type { get; set; }
    public List<CauHinhLuongRow> Items { get; set; } = [];
    public string? Error { get; set; }
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
