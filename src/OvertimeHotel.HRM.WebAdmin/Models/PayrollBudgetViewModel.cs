using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public sealed class PayrollBudgetInput
{
    public Guid? Id { get; set; }
    [Range(0, long.MaxValue)] public long Version { get; set; }
    [Range(2000, 2200)] public int Nam { get; set; }
    [Range(1, 12)] public int Thang { get; set; }
    [Range(1, int.MaxValue)] public int MaPhongBan { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired]
    [Range(typeof(decimal), "0", "9999999999999.99")] public decimal SoTien { get; set; }
    [StringLength(2000)] public string? GhiChu { get; set; }
}

public sealed class PayrollBudgetViewModel
{
    public int Nam { get; set; }
    public bool Available { get; set; }
    public int ActorId { get; set; }
    public IReadOnlyList<NganSachLuong> Rows { get; set; } = [];
    public IReadOnlyList<SelectListItem> Departments { get; set; } = [];
}
