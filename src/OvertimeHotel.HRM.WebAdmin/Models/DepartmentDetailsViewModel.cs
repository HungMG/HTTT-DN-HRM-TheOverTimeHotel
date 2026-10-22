using OvertimeHotel.HRM.Core.Models;

namespace OvertimeHotel.HRM.WebAdmin.Models;

public sealed class DepartmentDetailsViewModel
{
    public required PhongBan Department { get; init; }
    public required IReadOnlyList<ChucVu> Positions { get; init; }
}
