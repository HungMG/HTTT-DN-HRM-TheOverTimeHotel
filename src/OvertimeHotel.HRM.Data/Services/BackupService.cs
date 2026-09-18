using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

public class BackupSnapshot
{
    public DateTime BackupTime { get; set; } = DateTime.UtcNow;
    public string SystemVersion { get; set; } = "1.0.0";
    public List<PhongBan> PhongBans { get; set; } = new();
    public List<ChucVu> ChucVus { get; set; } = new();
    public List<VaiTro> VaiTros { get; set; } = new();
    public List<Quyen> Quyens { get; set; } = new();
    public List<VaiTroQuyen> VaiTroQuyens { get; set; } = new();
    public List<CaLamViec> CaLamViecs { get; set; } = new();
    public List<LoaiDon> LoaiDons { get; set; } = new();
    public List<KyLuong> KyLuongs { get; set; } = new();
    public List<CauHinhKhoanLuong> CauHinhKhoanLuongs { get; set; } = new();
    public List<NhanVien> NhanViens { get; set; } = new();
    public List<HopDong> HopDongs { get; set; } = new();
    public List<TaiKhoan> TaiKhoans { get; set; } = new();
    public List<PhanCa> PhanCas { get; set; } = new();
    public List<ChamCong> ChamCongs { get; set; } = new();
    public List<DonTu> DonTus { get; set; } = new();
    public List<PhieuLuong> PhieuLuongs { get; set; } = new();
    public List<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = new();
    public List<NhatKyQuanTri> NhatKyQuanTris { get; set; } = new();
}

public class BackupService : IBackupService
{
    private readonly AppDbContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public BackupService(AppDbContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<string> ExportDatabaseToJsonAsync()
    {
        var snapshot = new BackupSnapshot
        {
            BackupTime = DateTime.UtcNow,
            SystemVersion = "1.0.0",
            PhongBans = await _context.PhongBans.AsNoTracking().ToListAsync(),
            ChucVus = await _context.ChucVus.AsNoTracking().ToListAsync(),
            VaiTros = await _context.VaiTros.AsNoTracking().ToListAsync(),
            Quyens = await _context.Quyens.AsNoTracking().ToListAsync(),
            VaiTroQuyens = await _context.VaiTroQuyens.AsNoTracking().ToListAsync(),
            CaLamViecs = await _context.CaLamViecs.AsNoTracking().ToListAsync(),
            LoaiDons = await _context.LoaiDons.AsNoTracking().ToListAsync(),
            KyLuongs = await _context.KyLuongs.AsNoTracking().ToListAsync(),
            CauHinhKhoanLuongs = await _context.CauHinhKhoanLuongs.AsNoTracking().ToListAsync(),
            NhanViens = await _context.NhanViens.AsNoTracking().ToListAsync(),
            HopDongs = await _context.HopDongs.AsNoTracking().ToListAsync(),
            TaiKhoans = await _context.TaiKhoans.AsNoTracking().ToListAsync(),
            PhanCas = await _context.PhanCas.AsNoTracking().ToListAsync(),
            ChamCongs = await _context.ChamCongs.AsNoTracking().ToListAsync(),
            DonTus = await _context.DonTus.AsNoTracking().ToListAsync(),
            PhieuLuongs = await _context.PhieuLuongs.AsNoTracking().ToListAsync(),
            ChiTietPhieuLuongs = await _context.ChiTietPhieuLuongs.AsNoTracking().ToListAsync(),
            NhatKyQuanTris = await _context.NhatKyQuanTris.AsNoTracking().ToListAsync()
        };

        return JsonSerializer.Serialize(snapshot, _jsonOptions);
    }

    public async Task<(bool Success, string Message, int RestoredRecords)> RestoreDatabaseFromJsonAsync(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return (false, "Dữ liệu bản sao lưu rỗng.", 0);
        }

        BackupSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<BackupSnapshot>(jsonContent, _jsonOptions);
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi định dạng file JSON: {ex.Message}", 0);
        }

        if (snapshot == null)
        {
            return (false, "Không thể đọc cấu trúc bản sao lưu.", 0);
        }

        var totalCount = 0;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Phục hồi các bảng danh mục độc lập
            if (snapshot.PhongBans.Count > 0)
            {
                foreach (var item in snapshot.PhongBans)
                {
                    if (!await _context.PhongBans.AnyAsync(x => x.MaPhongBan == item.MaPhongBan))
                        _context.PhongBans.Add(item);
                }
                totalCount += snapshot.PhongBans.Count;
            }

            if (snapshot.ChucVus.Count > 0)
            {
                foreach (var item in snapshot.ChucVus)
                {
                    if (!await _context.ChucVus.AnyAsync(x => x.MaChucVu == item.MaChucVu))
                        _context.ChucVus.Add(item);
                }
                totalCount += snapshot.ChucVus.Count;
            }

            if (snapshot.VaiTros.Count > 0)
            {
                foreach (var item in snapshot.VaiTros)
                {
                    if (!await _context.VaiTros.AnyAsync(x => x.MaVaiTro == item.MaVaiTro))
                        _context.VaiTros.Add(item);
                }
                totalCount += snapshot.VaiTros.Count;
            }

            if (snapshot.Quyens.Count > 0)
            {
                foreach (var item in snapshot.Quyens)
                {
                    if (!await _context.Quyens.AnyAsync(x => x.MaQuyen == item.MaQuyen))
                        _context.Quyens.Add(item);
                }
                totalCount += snapshot.Quyens.Count;
            }

            if (snapshot.CaLamViecs.Count > 0)
            {
                foreach (var item in snapshot.CaLamViecs)
                {
                    if (!await _context.CaLamViecs.AnyAsync(x => x.MaCa == item.MaCa))
                        _context.CaLamViecs.Add(item);
                }
                totalCount += snapshot.CaLamViecs.Count;
            }

            if (snapshot.LoaiDons.Count > 0)
            {
                foreach (var item in snapshot.LoaiDons)
                {
                    if (!await _context.LoaiDons.AnyAsync(x => x.MaLoaiDon == item.MaLoaiDon))
                        _context.LoaiDons.Add(item);
                }
                totalCount += snapshot.LoaiDons.Count;
            }

            if (snapshot.KyLuongs.Count > 0)
            {
                foreach (var item in snapshot.KyLuongs)
                {
                    if (!await _context.KyLuongs.AnyAsync(x => x.MaKyLuong == item.MaKyLuong))
                        _context.KyLuongs.Add(item);
                }
                totalCount += snapshot.KyLuongs.Count;
            }

            if (snapshot.CauHinhKhoanLuongs.Count > 0)
            {
                foreach (var item in snapshot.CauHinhKhoanLuongs)
                {
                    if (!await _context.CauHinhKhoanLuongs.AnyAsync(x => x.MaKhoan == item.MaKhoan))
                        _context.CauHinhKhoanLuongs.Add(item);
                }
                totalCount += snapshot.CauHinhKhoanLuongs.Count;
            }

            await _context.SaveChangesAsync();

            // 2. Phục hồi VaiTroQuyen
            if (snapshot.VaiTroQuyens.Count > 0)
            {
                foreach (var item in snapshot.VaiTroQuyens)
                {
                    if (!await _context.VaiTroQuyens.AnyAsync(x => x.MaVaiTro == item.MaVaiTro && x.MaQuyen == item.MaQuyen))
                        _context.VaiTroQuyens.Add(item);
                }
                totalCount += snapshot.VaiTroQuyens.Count;
                await _context.SaveChangesAsync();
            }

            // 3. Phục hồi NhanVien
            if (snapshot.NhanViens.Count > 0)
            {
                foreach (var item in snapshot.NhanViens)
                {
                    if (!await _context.NhanViens.AnyAsync(x => x.MaNhanVien == item.MaNhanVien))
                        _context.NhanViens.Add(item);
                }
                totalCount += snapshot.NhanViens.Count;
                await _context.SaveChangesAsync();
            }

            // 4. Phục hồi HopDong, TaiKhoan, PhanCa, DonTu, PhieuLuong
            if (snapshot.HopDongs.Count > 0)
            {
                foreach (var item in snapshot.HopDongs)
                {
                    if (!await _context.HopDongs.AnyAsync(x => x.MaHopDong == item.MaHopDong))
                        _context.HopDongs.Add(item);
                }
                totalCount += snapshot.HopDongs.Count;
            }

            if (snapshot.TaiKhoans.Count > 0)
            {
                foreach (var item in snapshot.TaiKhoans)
                {
                    if (!await _context.TaiKhoans.AnyAsync(x => x.MaTaiKhoan == item.MaTaiKhoan))
                        _context.TaiKhoans.Add(item);
                }
                totalCount += snapshot.TaiKhoans.Count;
            }

            if (snapshot.PhanCas.Count > 0)
            {
                foreach (var item in snapshot.PhanCas)
                {
                    if (!await _context.PhanCas.AnyAsync(x => x.MaPhanCa == item.MaPhanCa))
                        _context.PhanCas.Add(item);
                }
                totalCount += snapshot.PhanCas.Count;
            }

            if (snapshot.DonTus.Count > 0)
            {
                foreach (var item in snapshot.DonTus)
                {
                    if (!await _context.DonTus.AnyAsync(x => x.MaDon == item.MaDon))
                        _context.DonTus.Add(item);
                }
                totalCount += snapshot.DonTus.Count;
            }

            if (snapshot.PhieuLuongs.Count > 0)
            {
                foreach (var item in snapshot.PhieuLuongs)
                {
                    if (!await _context.PhieuLuongs.AnyAsync(x => x.MaPhieuLuong == item.MaPhieuLuong))
                        _context.PhieuLuongs.Add(item);
                }
                totalCount += snapshot.PhieuLuongs.Count;
            }

            await _context.SaveChangesAsync();

            // 5. Phục hồi ChamCong, ChiTietPhieuLuong
            if (snapshot.ChamCongs.Count > 0)
            {
                foreach (var item in snapshot.ChamCongs)
                {
                    if (!await _context.ChamCongs.AnyAsync(x => x.MaChamCong == item.MaChamCong))
                        _context.ChamCongs.Add(item);
                }
                totalCount += snapshot.ChamCongs.Count;
            }

            if (snapshot.ChiTietPhieuLuongs.Count > 0)
            {
                foreach (var item in snapshot.ChiTietPhieuLuongs)
                {
                    if (!await _context.ChiTietPhieuLuongs.AnyAsync(x => x.MaChiTiet == item.MaChiTiet))
                        _context.ChiTietPhieuLuongs.Add(item);
                }
                totalCount += snapshot.ChiTietPhieuLuongs.Count;
            }

            await _context.SaveChangesAsync();

            // 6. Phục hồi nhật ký quản trị sau khi các tài khoản đã tồn tại.
            if (snapshot.NhatKyQuanTris.Count > 0)
            {
                foreach (var item in snapshot.NhatKyQuanTris)
                {
                    if (!await _context.NhatKyQuanTris.AnyAsync(x => x.MaNhatKy == item.MaNhatKy))
                        _context.NhatKyQuanTris.Add(item);
                }
                totalCount += snapshot.NhatKyQuanTris.Count;
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return (true, $"Khôi phục thành công bản sao lưu vào CSDL!", totalCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi trong quá trình khôi phục: {ex.Message}", 0);
        }
    }

    public async Task<Dictionary<string, int>> GetTableStatisticsAsync()
    {
        var stats = new Dictionary<string, int>
        {
            ["phong_ban (Phòng ban)"] = await _context.PhongBans.CountAsync(),
            ["chuc_vu (Chức vụ)"] = await _context.ChucVus.CountAsync(),
            ["nhan_vien (Nhân viên)"] = await _context.NhanViens.CountAsync(),
            ["hop_dong (Hợp đồng LĐ)"] = await _context.HopDongs.CountAsync(),
            ["vai_tro (Vai trò RBAC)"] = await _context.VaiTros.CountAsync(),
            ["tai_khoan (Tài khoản)"] = await _context.TaiKhoans.CountAsync(),
            ["quyen (Quyền chức năng)"] = await _context.Quyens.CountAsync(),
            ["vai_tro_quyen (Phân quyền)"] = await _context.VaiTroQuyens.CountAsync(),
            ["ca_lam_viec (Ca làm việc 24/7)"] = await _context.CaLamViecs.CountAsync(),
            ["phan_ca (Lịch phân ca)"] = await _context.PhanCas.CountAsync(),
            ["cham_cong (Chấm công quẹt thẻ)"] = await _context.ChamCongs.CountAsync(),
            ["loai_don (Loại đơn từ)"] = await _context.LoaiDons.CountAsync(),
            ["don_tu (Đơn từ trực tuyến)"] = await _context.DonTus.CountAsync(),
            ["ky_luong (Kỳ tính lương)"] = await _context.KyLuongs.CountAsync(),
            ["cau_hinh_khoan_luong (Cấu hình thu chi)"] = await _context.CauHinhKhoanLuongs.CountAsync(),
            ["phieu_luong (Phiếu lương tháng)"] = await _context.PhieuLuongs.CountAsync(),
            ["chi_tiet_phieu_luong (Chi tiết phiếu lương)"] = await _context.ChiTietPhieuLuongs.CountAsync(),
            ["nhat_ky_quan_tri (Nhật ký quản trị)"] = await _context.NhatKyQuanTris.CountAsync()
        };

        return stats;
    }
}
