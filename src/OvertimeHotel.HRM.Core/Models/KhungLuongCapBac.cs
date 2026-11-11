namespace OvertimeHotel.HRM.Core.Models;

public class KhungLuongCapBac
{
    public int MaKhungLuong { get; set; }
    public string NhomChucVu { get; set; } = string.Empty;
    public string? TrinhDo { get; set; }
    public int? ThamNienTuThang { get; set; }
    public int? ThamNienDenThang { get; set; }
    public decimal LuongToiThieu { get; set; }
    public decimal LuongToiDa { get; set; }
}
