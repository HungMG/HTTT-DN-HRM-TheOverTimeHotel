namespace OvertimeHotel.HRM.Core.Models;

public class PhanCa
{
    public int MaPhanCa { get; set; }
    public int MaNhanVien { get; set; }
    public int MaCa { get; set; }
    public DateOnly NgayLamViec { get; set; }
    public DateTimeOffset BatDauDuKien { get; set; }
    public DateTimeOffset KetThucDuKien { get; set; }
    public string TrangThai { get; set; } = "DA_PHAN";

    public virtual NhanVien? NhanVien { get; set; }
    public virtual CaLamViec? CaLamViec { get; set; }
    public virtual ChamCong? ChamCong { get; set; }
}
