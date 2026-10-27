namespace OvertimeHotel.HRM.Core.Models;

public class NhuCauSuKien
{
    public int MaNhuCau { get; set; }
    public int MaDuAn { get; set; }
    public string MaPhongBan { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public int SoNguoi { get; set; }
    public bool LaMau { get; set; }
    public DuAn? DuAn { get; set; }
}

public class CongSuKien
{
    public int MaCong { get; set; }
    public int MaPhanCong { get; set; }
    public DateTimeOffset BatDau { get; set; }
    public DateTimeOffset KetThuc { get; set; }
    public int MaNguoiXacNhan { get; set; }
    public DateTimeOffset NgayXacNhan { get; set; }
    public PhanCongDuAn? PhanCong { get; set; }
}

public class QuyetToanSuKien
{
    public int MaQuyetToan { get; set; }
    public int MaDuAn { get; set; }
    public int MaNhanVien { get; set; }
    public DateOnly ThangPhatSinh { get; set; }
    public int MaKyChiTra { get; set; }
    public decimal SoPhut { get; set; }
    public decimal SoTien { get; set; }
    public string CanCuTinh { get; set; } = "[]";
    public int? MaChiTiet { get; set; }
    public int MaNguoiDuyet { get; set; }
    public DateTimeOffset NgayDuyet { get; set; }
    public DuAn? DuAn { get; set; }
    public NhanVien? NhanVien { get; set; }
    public KyLuong? KyChiTra { get; set; }
}

public class CongBoLichTuan
{
    public DateOnly DauTuan { get; set; }
    public long Version { get; set; } = 1;
    public int MaNguoiCongBo { get; set; }
    public DateTimeOffset NgayCongBo { get; set; }
    public string? LyDo { get; set; }
}

public class DiaDiemSuKien
{
    public int MaDiaDiem { get; set; }
    public string TenDiaDiem { get; set; } = "";
    public int SucChua { get; set; }
    public bool DangSuDung { get; set; } = true;
}
