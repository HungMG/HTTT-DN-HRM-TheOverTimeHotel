using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Data.Context;

namespace OvertimeHotel.HRM.Data.Services;

/// <summary>
/// Cấu trúc bản sao lưu toàn diện 30+ bảng CSDL chuẩn 3NF của The OverTime Hotel HRM.
/// </summary>
public class BackupSnapshot
{
    public DateTime BackupTime { get; set; } = DateTime.UtcNow;
    public string SystemVersion { get; set; } = "2.0.0";
    public string SystemName { get; set; } = "The OverTime Hotel HRM Enterprise Database";

    // ── 1. Danh mục & Cơ cấu tổ chức ───────────────────────────────────────
    public List<PhongBan> PhongBans { get; set; } = [];
    public List<ChucVu> ChucVus { get; set; } = [];
    public List<KhungLuongCapBac> KhungLuongCapBacs { get; set; } = [];
    public List<LoaiDon> LoaiDons { get; set; } = [];

    // ── 2. Tài khoản, Quyền hạn & Phân quyền RBAC ───────────────────────────
    public List<VaiTro> VaiTros { get; set; } = [];
    public List<Quyen> Quyens { get; set; } = [];
    public List<VaiTroQuyen> VaiTroQuyens { get; set; } = [];

    // ── 3. Quản trị Nhân sự & Hợp đồng lao động ────────────────────────────
    public List<NhanVien> NhanViens { get; set; } = [];
    public List<HopDong> HopDongs { get; set; } = [];
    public List<PhuLucLuong> PhuLucLuongs { get; set; } = [];
    public List<TaiKhoan> TaiKhoans { get; set; } = [];
    public List<YeuCauTaiKhoan> YeuCauTaiKhoans { get; set; } = [];

    // ── 4. Ca làm việc 24/7, Định biên & Lịch phân ca ───────────────────────
    public List<CaLamViec> CaLamViecs { get; set; } = [];
    public List<DinhBienCa> DinhBienCas { get; set; } = [];
    public List<DinhBienNgay> DinhBienNgays { get; set; } = [];
    public List<CongBoLichTuan> CongBoLichTuans { get; set; } = [];
    public List<PhanCa> PhanCas { get; set; } = [];

    // ── 5. Chấm công, Đổi ca, Giải trình & Đơn từ trực tuyến ────────────────
    public List<ChamCong> ChamCongs { get; set; } = [];
    public List<GiaiTrinhChamCong> GiaiTrinhChamCongs { get; set; } = [];
    public List<YeuCauDoiCa> YeuCauDoiCas { get; set; } = [];
    public List<DonTu> DonTus { get; set; } = [];

    // ── 6. Phân hệ Sự kiện khách sạn & Điều động nhân lực ──────────────────
    public List<DuAn> DuAns { get; set; } = [];
    public List<NhuCauSuKien> NhuCauSuKiens { get; set; } = [];
    public List<PhanCongDuAn> PhanCongDuAns { get; set; } = [];
    public List<CongSuKien> CongSuKiens { get; set; } = [];
    public List<QuyetToanSuKien> QuyetToanSuKiens { get; set; } = [];

    // ── 7. Cấu hình lương, Ngân sách, Phiếu lương & Khấu trừ ───────────────
    public List<KyLuong> KyLuongs { get; set; } = [];
    public List<NganSachLuong> NganSachLuongs { get; set; } = [];
    public List<CauHinhKhoanLuong> CauHinhKhoanLuongs { get; set; } = [];
    public List<CauHinhTangCa> CauHinhTangCas { get; set; } = [];
    public List<CauHinhPhuCap> CauHinhPhuCaps { get; set; } = [];
    public List<CauHinhKhauTru> CauHinhKhauTrus { get; set; } = [];
    public List<PhieuLuong> PhieuLuongs { get; set; } = [];
    public List<ChiTietPhieuLuong> ChiTietPhieuLuongs { get; set; } = [];
    public List<ChiTietTruBaoHiem> ChiTietTruBaoHiems { get; set; } = [];
    public List<ChiTietTruThueTncn> ChiTietTruThueTncns { get; set; } = [];
    public List<ChiTietTruViPhamTamUng> ChiTietTruViPhamTamUngs { get; set; } = [];

    // ── 8. Nhật ký kiểm toán an ninh quản trị ──────────────────────────────
    public List<NhatKyQuanTri> NhatKyQuanTris { get; set; } = [];
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
            SystemVersion = "2.0.0",

            // 1. Danh mục & Tổ chức
            PhongBans = await _context.PhongBans.AsNoTracking().ToListAsync(),
            ChucVus = await _context.ChucVus.AsNoTracking().ToListAsync(),
            KhungLuongCapBacs = await _context.KhungLuongCapBacs.AsNoTracking().ToListAsync(),
            LoaiDons = await _context.LoaiDons.AsNoTracking().ToListAsync(),

            // 2. Phân quyền RBAC
            VaiTros = await _context.VaiTros.AsNoTracking().ToListAsync(),
            Quyens = await _context.Quyens.AsNoTracking().ToListAsync(),
            VaiTroQuyens = await _context.VaiTroQuyens.AsNoTracking().ToListAsync(),

            // 3. Nhân sự & Hợp đồng
            NhanViens = await _context.NhanViens.AsNoTracking().ToListAsync(),
            HopDongs = await _context.HopDongs.AsNoTracking().ToListAsync(),
            PhuLucLuongs = await _context.PhuLucLuongs.AsNoTracking().ToListAsync(),
            TaiKhoans = await _context.TaiKhoans.AsNoTracking().ToListAsync(),
            YeuCauTaiKhoans = await _context.YeuCauTaiKhoans.AsNoTracking().ToListAsync(),

            // 4. Ca làm & Định biên
            CaLamViecs = await _context.CaLamViecs.AsNoTracking().ToListAsync(),
            DinhBienCas = await _context.DinhBienCas.AsNoTracking().ToListAsync(),
            DinhBienNgays = await _context.DinhBienNgays.AsNoTracking().ToListAsync(),
            CongBoLichTuans = await _context.CongBoLichTuans.AsNoTracking().ToListAsync(),
            PhanCas = await _context.PhanCas.AsNoTracking().ToListAsync(),

            // 5. Chấm công & Đơn từ
            ChamCongs = await _context.ChamCongs.AsNoTracking().ToListAsync(),
            GiaiTrinhChamCongs = await _context.GiaiTrinhChamCongs.AsNoTracking().ToListAsync(),
            YeuCauDoiCas = await _context.YeuCauDoiCas.AsNoTracking().ToListAsync(),
            DonTus = await _context.DonTus.AsNoTracking().ToListAsync(),

            // 6. Sự kiện khách sạn
            DuAns = await _context.DuAns.AsNoTracking().ToListAsync(),
            NhuCauSuKiens = await _context.NhuCauSuKiens.AsNoTracking().ToListAsync(),
            PhanCongDuAns = await _context.PhanCongDuAns.AsNoTracking().ToListAsync(),
            CongSuKiens = await _context.CongSuKiens.AsNoTracking().ToListAsync(),
            QuyetToanSuKiens = await _context.QuyetToanSuKiens.AsNoTracking().ToListAsync(),

            // 7. Lương bổng & Khấu trừ
            KyLuongs = await _context.KyLuongs.AsNoTracking().ToListAsync(),
            NganSachLuongs = await _context.NganSachLuongs.AsNoTracking().ToListAsync(),
            CauHinhKhoanLuongs = await _context.CauHinhKhoanLuongs.AsNoTracking().ToListAsync(),
            CauHinhTangCas = await _context.CauHinhTangCas.AsNoTracking().ToListAsync(),
            CauHinhPhuCaps = await _context.CauHinhPhuCaps.AsNoTracking().ToListAsync(),
            CauHinhKhauTrus = await _context.CauHinhKhauTrus.AsNoTracking().ToListAsync(),
            PhieuLuongs = await _context.PhieuLuongs.AsNoTracking().ToListAsync(),
            ChiTietPhieuLuongs = await _context.ChiTietPhieuLuongs.AsNoTracking().ToListAsync(),
            ChiTietTruBaoHiems = await _context.ChiTietTruBaoHiems.AsNoTracking().ToListAsync(),
            ChiTietTruThueTncns = await _context.ChiTietTruThueTncns.AsNoTracking().ToListAsync(),
            ChiTietTruViPhamTamUngs = await _context.ChiTietTruViPhamTamUngs.AsNoTracking().ToListAsync(),

            // 8. Nhật ký quản trị
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
            return (false, "Không thể giải mã cấu trúc bản sao lưu.", 0);
        }

        var totalCount = 0;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // ── BƯỚC 1: Phục hồi Danh mục độc lập (Không phụ thuộc FK) ───────────
            if (snapshot.PhongBans.Count > 0)
            {
                foreach (var item in snapshot.PhongBans)
                {
                    if (!await _context.PhongBans.AnyAsync(x => x.MaPhongBan == item.MaPhongBan))
                        _context.PhongBans.Add(item);
                }
                totalCount += snapshot.PhongBans.Count;
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

            if (snapshot.CauHinhTangCas.Count > 0)
            {
                foreach (var item in snapshot.CauHinhTangCas)
                {
                    if (!await _context.CauHinhTangCas.AnyAsync(x => x.MaLoaiOt == item.MaLoaiOt))
                        _context.CauHinhTangCas.Add(item);
                }
                totalCount += snapshot.CauHinhTangCas.Count;
            }

            if (snapshot.CauHinhPhuCaps.Count > 0)
            {
                foreach (var item in snapshot.CauHinhPhuCaps)
                {
                    if (!await _context.CauHinhPhuCaps.AnyAsync(x => x.MaPhuCap == item.MaPhuCap))
                        _context.CauHinhPhuCaps.Add(item);
                }
                totalCount += snapshot.CauHinhPhuCaps.Count;
            }

            if (snapshot.CauHinhKhauTrus.Count > 0)
            {
                foreach (var item in snapshot.CauHinhKhauTrus)
                {
                    if (!await _context.CauHinhKhauTrus.AnyAsync(x => x.MaKhoanTru == item.MaKhoanTru))
                        _context.CauHinhKhauTrus.Add(item);
                }
                totalCount += snapshot.CauHinhKhauTrus.Count;
            }

            await _context.SaveChangesAsync();

            // ── BƯỚC 2: Phục hồi Chức vụ & Khung lương (Phụ thuộc PhongBan) ──────
            if (snapshot.ChucVus.Count > 0)
            {
                foreach (var item in snapshot.ChucVus)
                {
                    if (!await _context.ChucVus.AnyAsync(x => x.MaChucVu == item.MaChucVu))
                        _context.ChucVus.Add(item);
                }
                totalCount += snapshot.ChucVus.Count;
                await _context.SaveChangesAsync();
            }

            if (snapshot.KhungLuongCapBacs.Count > 0)
            {
                foreach (var item in snapshot.KhungLuongCapBacs)
                {
                    if (!await _context.KhungLuongCapBacs.AnyAsync(x => x.MaKhungLuong == item.MaKhungLuong))
                        _context.KhungLuongCapBacs.Add(item);
                }
                totalCount += snapshot.KhungLuongCapBacs.Count;
            }

            if (snapshot.VaiTroQuyens.Count > 0)
            {
                foreach (var item in snapshot.VaiTroQuyens)
                {
                    if (!await _context.VaiTroQuyens.AnyAsync(x => x.MaVaiTro == item.MaVaiTro && x.MaQuyen == item.MaQuyen))
                        _context.VaiTroQuyens.Add(item);
                }
                totalCount += snapshot.VaiTroQuyens.Count;
            }

            await _context.SaveChangesAsync();

            // ── BƯỚC 3: Phục hồi Nhân viên & Ngân sách (Phụ thuộc ChucVu/PhongBan)
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

            if (snapshot.NganSachLuongs.Count > 0)
            {
                foreach (var item in snapshot.NganSachLuongs)
                {
                    if (!await _context.NganSachLuongs.AnyAsync(x => x.MaNganSach == item.MaNganSach))
                        _context.NganSachLuongs.Add(item);
                }
                totalCount += snapshot.NganSachLuongs.Count;
            }

            // ── BƯỚC 4: Hợp đồng, Tài khoản, Định biên & Sự kiện ─────────────────
            if (snapshot.HopDongs.Count > 0)
            {
                foreach (var item in snapshot.HopDongs)
                {
                    if (!await _context.HopDongs.AnyAsync(x => x.MaHopDong == item.MaHopDong))
                        _context.HopDongs.Add(item);
                }
                totalCount += snapshot.HopDongs.Count;
                await _context.SaveChangesAsync();
            }

            if (snapshot.PhuLucLuongs.Count > 0)
            {
                foreach (var item in snapshot.PhuLucLuongs)
                {
                    if (!await _context.PhuLucLuongs.AnyAsync(x => x.MaPhuLuc == item.MaPhuLuc))
                        _context.PhuLucLuongs.Add(item);
                }
                totalCount += snapshot.PhuLucLuongs.Count;
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

            if (snapshot.YeuCauTaiKhoans.Count > 0)
            {
                foreach (var item in snapshot.YeuCauTaiKhoans)
                {
                    if (!await _context.YeuCauTaiKhoans.AnyAsync(x => x.MaYeuCau == item.MaYeuCau))
                        _context.YeuCauTaiKhoans.Add(item);
                }
                totalCount += snapshot.YeuCauTaiKhoans.Count;
            }

            if (snapshot.DinhBienCas.Count > 0)
            {
                foreach (var item in snapshot.DinhBienCas)
                {
                    if (!await _context.DinhBienCas.AnyAsync(x => x.MaDinhBien == item.MaDinhBien))
                        _context.DinhBienCas.Add(item);
                }
                totalCount += snapshot.DinhBienCas.Count;
            }

            if (snapshot.DinhBienNgays.Count > 0)
            {
                foreach (var item in snapshot.DinhBienNgays)
                {
                    if (!await _context.DinhBienNgays.AnyAsync(x => x.MaDinhBienNgay == item.MaDinhBienNgay))
                        _context.DinhBienNgays.Add(item);
                }
                totalCount += snapshot.DinhBienNgays.Count;
            }

            if (snapshot.DuAns.Count > 0)
            {
                foreach (var item in snapshot.DuAns)
                {
                    if (!await _context.DuAns.AnyAsync(x => x.MaDuAn == item.MaDuAn))
                        _context.DuAns.Add(item);
                }
                totalCount += snapshot.DuAns.Count;
                await _context.SaveChangesAsync();
            }

            if (snapshot.NhuCauSuKiens.Count > 0)
            {
                foreach (var item in snapshot.NhuCauSuKiens)
                {
                    if (!await _context.NhuCauSuKiens.AnyAsync(x => x.MaNhuCau == item.MaNhuCau))
                        _context.NhuCauSuKiens.Add(item);
                }
                totalCount += snapshot.NhuCauSuKiens.Count;
            }

            await _context.SaveChangesAsync();

            // ── BƯỚC 5: Phân ca, Đơn từ & Phân công sự kiện ─────────────────────
            if (snapshot.CongBoLichTuans.Count > 0)
            {
                foreach (var item in snapshot.CongBoLichTuans)
                {
                    if (!await _context.CongBoLichTuans.AnyAsync(x => x.DauTuan == item.DauTuan))
                        _context.CongBoLichTuans.Add(item);
                }
                totalCount += snapshot.CongBoLichTuans.Count;
            }

            if (snapshot.PhanCas.Count > 0)
            {
                foreach (var item in snapshot.PhanCas)
                {
                    if (!await _context.PhanCas.AnyAsync(x => x.MaPhanCa == item.MaPhanCa))
                        _context.PhanCas.Add(item);
                }
                totalCount += snapshot.PhanCas.Count;
                await _context.SaveChangesAsync();
            }

            if (snapshot.PhanCongDuAns.Count > 0)
            {
                foreach (var item in snapshot.PhanCongDuAns)
                {
                    if (!await _context.PhanCongDuAns.AnyAsync(x => x.MaPhanCong == item.MaPhanCong))
                        _context.PhanCongDuAns.Add(item);
                }
                totalCount += snapshot.PhanCongDuAns.Count;
                await _context.SaveChangesAsync();
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

            if (snapshot.YeuCauDoiCas.Count > 0)
            {
                foreach (var item in snapshot.YeuCauDoiCas)
                {
                    if (!await _context.YeuCauDoiCas.AnyAsync(x => x.MaYeuCau == item.MaYeuCau))
                        _context.YeuCauDoiCas.Add(item);
                }
                totalCount += snapshot.YeuCauDoiCas.Count;
            }

            await _context.SaveChangesAsync();

            // ── BƯỚC 6: Chấm công, Giải trình, Công sự kiện & Quyết toán ────────
            if (snapshot.ChamCongs.Count > 0)
            {
                foreach (var item in snapshot.ChamCongs)
                {
                    if (!await _context.ChamCongs.AnyAsync(x => x.MaChamCong == item.MaChamCong))
                        _context.ChamCongs.Add(item);
                }
                totalCount += snapshot.ChamCongs.Count;
                await _context.SaveChangesAsync();
            }

            if (snapshot.GiaiTrinhChamCongs.Count > 0)
            {
                foreach (var item in snapshot.GiaiTrinhChamCongs)
                {
                    if (!await _context.GiaiTrinhChamCongs.AnyAsync(x => x.MaGiaiTrinh == item.MaGiaiTrinh))
                        _context.GiaiTrinhChamCongs.Add(item);
                }
                totalCount += snapshot.GiaiTrinhChamCongs.Count;
            }

            if (snapshot.CongSuKiens.Count > 0)
            {
                foreach (var item in snapshot.CongSuKiens)
                {
                    if (!await _context.CongSuKiens.AnyAsync(x => x.MaCong == item.MaCong))
                        _context.CongSuKiens.Add(item);
                }
                totalCount += snapshot.CongSuKiens.Count;
            }

            if (snapshot.QuyetToanSuKiens.Count > 0)
            {
                foreach (var item in snapshot.QuyetToanSuKiens)
                {
                    if (!await _context.QuyetToanSuKiens.AnyAsync(x => x.MaQuyetToan == item.MaQuyetToan))
                        _context.QuyetToanSuKiens.Add(item);
                }
                totalCount += snapshot.QuyetToanSuKiens.Count;
            }

            // ── BƯỚC 7: Phiếu lương, Chi tiết khấu trừ & Nhật ký kiểm toán ───────
            if (snapshot.PhieuLuongs.Count > 0)
            {
                foreach (var item in snapshot.PhieuLuongs)
                {
                    if (!await _context.PhieuLuongs.AnyAsync(x => x.MaPhieuLuong == item.MaPhieuLuong))
                        _context.PhieuLuongs.Add(item);
                }
                totalCount += snapshot.PhieuLuongs.Count;
                await _context.SaveChangesAsync();
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

            if (snapshot.ChiTietTruBaoHiems.Count > 0)
            {
                foreach (var item in snapshot.ChiTietTruBaoHiems)
                {
                    if (!await _context.ChiTietTruBaoHiems.AnyAsync(x => x.MaChiTietBh == item.MaChiTietBh))
                        _context.ChiTietTruBaoHiems.Add(item);
                }
                totalCount += snapshot.ChiTietTruBaoHiems.Count;
            }

            if (snapshot.ChiTietTruThueTncns.Count > 0)
            {
                foreach (var item in snapshot.ChiTietTruThueTncns)
                {
                    if (!await _context.ChiTietTruThueTncns.AnyAsync(x => x.MaChiTietThue == item.MaChiTietThue))
                        _context.ChiTietTruThueTncns.Add(item);
                }
                totalCount += snapshot.ChiTietTruThueTncns.Count;
            }

            if (snapshot.ChiTietTruViPhamTamUngs.Count > 0)
            {
                foreach (var item in snapshot.ChiTietTruViPhamTamUngs)
                {
                    if (!await _context.ChiTietTruViPhamTamUngs.AnyAsync(x => x.MaChiTietVp == item.MaChiTietVp))
                        _context.ChiTietTruViPhamTamUngs.Add(item);
                }
                totalCount += snapshot.ChiTietTruViPhamTamUngs.Count;
            }

            if (snapshot.NhatKyQuanTris.Count > 0)
            {
                foreach (var item in snapshot.NhatKyQuanTris)
                {
                    if (!await _context.NhatKyQuanTris.AnyAsync(x => x.MaNhatKy == item.MaNhatKy))
                        _context.NhatKyQuanTris.Add(item);
                }
                totalCount += snapshot.NhatKyQuanTris.Count;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return (true, "Khôi phục thành công toàn bộ dữ liệu vào CSDL!", totalCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi trong quá trình khôi phục: {ex.Message}", 0);
        }
    }

    public async Task<Dictionary<string, int>> GetTableStatisticsAsync()
    {
        // Tối ưu hoá tốc độ: Thay vì gửi 38 câu SELECT COUNT(*) tuần tự qua mạng Singapore mất 3-4 giây,
        // gom toàn bộ thành 1 câu SQL UNION ALL duy nhất trong 1 network roundtrip (< 100ms).
        try
        {
            const string batchCountSql = """
                SELECT 'phong_ban' AS tbl, count(*)::int AS cnt FROM public.phong_ban
                UNION ALL SELECT 'chuc_vu', count(*)::int FROM public.chuc_vu
                UNION ALL SELECT 'khung_luong_cap_bac', count(*)::int FROM public.khung_luong_cap_bac
                UNION ALL SELECT 'loai_don', count(*)::int FROM public.loai_don
                UNION ALL SELECT 'vai_tro', count(*)::int FROM public.vai_tro
                UNION ALL SELECT 'quyen', count(*)::int FROM public.quyen
                UNION ALL SELECT 'vai_tro_quyen', count(*)::int FROM public.vai_tro_quyen
                UNION ALL SELECT 'tai_khoan', count(*)::int FROM public.tai_khoan
                UNION ALL SELECT 'yeu_cau_tai_khoan', count(*)::int FROM public.yeu_cau_tai_khoan
                UNION ALL SELECT 'nhan_vien', count(*)::int FROM public.nhan_vien
                UNION ALL SELECT 'hop_dong', count(*)::int FROM public.hop_dong
                UNION ALL SELECT 'phu_luc_luong', count(*)::int FROM public.phu_luc_luong
                UNION ALL SELECT 'ca_lam_viec', count(*)::int FROM public.ca_lam_viec
                UNION ALL SELECT 'dinh_bien_ca', count(*)::int FROM public.dinh_bien_ca
                UNION ALL SELECT 'dinh_bien_ngay', count(*)::int FROM public.dinh_bien_ngay
                UNION ALL SELECT 'cong_bo_lich_tuan', count(*)::int FROM public.cong_bo_lich_tuan
                UNION ALL SELECT 'phan_ca', count(*)::int FROM public.phan_ca
                UNION ALL SELECT 'cham_cong', count(*)::int FROM public.cham_cong
                UNION ALL SELECT 'giai_trinh_cham_cong', count(*)::int FROM public.giai_trinh_cham_cong
                UNION ALL SELECT 'yeu_cau_doi_ca', count(*)::int FROM public.yeu_cau_doi_ca
                UNION ALL SELECT 'don_tu', count(*)::int FROM public.don_tu
                UNION ALL SELECT 'du_an', count(*)::int FROM public.du_an
                UNION ALL SELECT 'nhu_cau_su_kien', count(*)::int FROM public.nhu_cau_su_kien
                UNION ALL SELECT 'phan_cong_du_an', count(*)::int FROM public.phan_cong_du_an
                UNION ALL SELECT 'cong_su_kien', count(*)::int FROM public.cong_su_kien
                UNION ALL SELECT 'quyet_toan_su_kien', count(*)::int FROM public.quyet_toan_su_kien
                UNION ALL SELECT 'ky_luong', count(*)::int FROM public.ky_luong
                UNION ALL SELECT 'ngan_sach_luong', count(*)::int FROM public.ngan_sach_luong
                UNION ALL SELECT 'cau_hinh_khoan_luong', count(*)::int FROM public.cau_hinh_khoan_luong
                UNION ALL SELECT 'cau_hinh_tang_ca', count(*)::int FROM public.cau_hinh_tang_ca
                UNION ALL SELECT 'cau_hinh_phu_cap', count(*)::int FROM public.cau_hinh_phu_cap
                UNION ALL SELECT 'cau_hinh_khau_tru', count(*)::int FROM public.cau_hinh_khau_tru
                UNION ALL SELECT 'phieu_luong', count(*)::int FROM public.phieu_luong
                UNION ALL SELECT 'chi_tiet_phieu_luong', count(*)::int FROM public.chi_tiet_phieu_luong
                UNION ALL SELECT 'chi_tiet_tru_bao_hiem', count(*)::int FROM public.chi_tiet_tru_bao_hiem
                UNION ALL SELECT 'chi_tiet_tru_thue_tncn', count(*)::int FROM public.chi_tiet_tru_thue_tncn
                UNION ALL SELECT 'chi_tiet_tru_vi_pham_tam_ung', count(*)::int FROM public.chi_tiet_tru_vi_pham_tam_ung
                UNION ALL SELECT 'nhat_ky_quan_tri', count(*)::int FROM public.nhat_ky_quan_tri;
                """;

            var rawCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = batchCountSql;
                cmd.CommandTimeout = 20;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rawCounts[reader.GetString(0)] = reader.GetInt32(1);
                }
            }

            int GetCount(string tbl) => rawCounts.TryGetValue(tbl, out var c) ? c : 0;

            return new Dictionary<string, int>
            {
                ["1. phong_ban (Phòng ban khách sạn)"] = GetCount("phong_ban"),
                ["2. chuc_vu (Chức danh & Vị trí)"] = GetCount("chuc_vu"),
                ["3. khung_luong_cap_bac (Khung lương cấp bậc)"] = GetCount("khung_luong_cap_bac"),
                ["4. loai_don (Loại đơn từ trực tuyến)"] = GetCount("loai_don"),
                ["5. vai_tro (Vai trò RBAC)"] = GetCount("vai_tro"),
                ["6. quyen (Danh mục quyền chức năng)"] = GetCount("quyen"),
                ["7. vai_tro_quyen (Phân quyền vai trò)"] = GetCount("vai_tro_quyen"),
                ["8. tai_khoan (Tài khoản người dùng)"] = GetCount("tai_khoan"),
                ["9. yeu_cau_tai_khoan (Hàng đợi cấp tài khoản)"] = GetCount("yeu_cau_tai_khoan"),
                ["10. nhan_vien (Hồ sơ nhân viên)"] = GetCount("nhan_vien"),
                ["11. hop_dong (Hợp đồng lao động)"] = GetCount("hop_dong"),
                ["12. phu_luc_luong (Phụ lục lương)"] = GetCount("phu_luc_luong"),
                ["13. ca_lam_viec (Ca làm việc 24/7)"] = GetCount("ca_lam_viec"),
                ["14. dinh_bien_ca (Định biên chuẩn theo ca)"] = GetCount("dinh_bien_ca"),
                ["15. dinh_bien_ngay (Định biên riêng theo ngày)"] = GetCount("dinh_bien_ngay"),
                ["16. cong_bo_lich_tuan (Công bố lịch tuần)"] = GetCount("cong_bo_lich_tuan"),
                ["17. phan_ca (Lịch phân ca nhân viên)"] = GetCount("phan_ca"),
                ["18. cham_cong (Nhật ký chấm công)"] = GetCount("cham_cong"),
                ["19. giai_trinh_cham_cong (Giải trình chấm công)"] = GetCount("giai_trinh_cham_cong"),
                ["20. yeu_cau_doi_ca (Yêu cầu đổi ca trực)"] = GetCount("yeu_cau_doi_ca"),
                ["21. don_tu (Đơn từ nghỉ phép & xin nghỉ)"] = GetCount("don_tu"),
                ["22. du_an (Sự kiện & Dự án khách sạn)"] = GetCount("du_an"),
                ["23. nhu_cau_su_kien (Nhu cầu nhân lực sự kiện)"] = GetCount("nhu_cau_su_kien"),
                ["24. phan_cong_du_an (Điều động nhân sự sự kiện)"] = GetCount("phan_cong_du_an"),
                ["25. cong_su_kien (Chấm công giờ sự kiện)"] = GetCount("cong_su_kien"),
                ["26. quyet_toan_su_kien (Quyết toán phụ cấp sự kiện)"] = GetCount("quyet_toan_su_kien"),
                ["27. ky_luong (Kỳ tính lương)"] = GetCount("ky_luong"),
                ["28. ngan_sach_luong (Kế hoạch ngân sách lương)"] = GetCount("ngan_sach_luong"),
                ["29. cau_hinh_khoan_luong (Cấu hình khoản lương)"] = GetCount("cau_hinh_khoan_luong"),
                ["30. cau_hinh_tang_ca (Cấu hình hệ số OT)"] = GetCount("cau_hinh_tang_ca"),
                ["31. cau_hinh_phu_cap (Cấu hình phụ cấp)"] = GetCount("cau_hinh_phu_cap"),
                ["32. cau_hinh_khau_tru (Cấu hình khoản trừ)"] = GetCount("cau_hinh_khau_tru"),
                ["33. phieu_luong (Bảng phiếu lương nhân viên)"] = GetCount("phieu_luong"),
                ["34. chi_tiet_phieu_luong (Chi tiết phụ cấp phiếu lương)"] = GetCount("chi_tiet_phieu_luong"),
                ["35. chi_tiet_tru_bao_hiem (Chi tiết trừ BHXH/BHYT/BHTN)"] = GetCount("chi_tiet_tru_bao_hiem"),
                ["36. chi_tiet_tru_thue_tncn (Chi tiết trừ thuế TNCN)"] = GetCount("chi_tiet_tru_thue_tncn"),
                ["37. chi_tiet_tru_vi_pham_tam_ung (Khấu trừ phạt & tạm ứng)"] = GetCount("chi_tiet_tru_vi_pham_tam_ung"),
                ["38. nhat_ky_quan_tri (Nhật ký kiểm toán an ninh)"] = GetCount("nhat_ky_quan_tri")
            };
        }
        catch
        {
            // Dự phòng an toàn nếu lệnh raw SQL gặp sự cố kết nối
            return new Dictionary<string, int>
            {
                ["1. phong_ban (Phòng ban khách sạn)"] = await _context.PhongBans.CountAsync(),
                ["2. chuc_vu (Chức danh & Vị trí)"] = await _context.ChucVus.CountAsync(),
                ["3. khung_luong_cap_bac (Khung lương cấp bậc)"] = await _context.KhungLuongCapBacs.CountAsync(),
                ["4. loai_don (Loại đơn từ trực tuyến)"] = await _context.LoaiDons.CountAsync(),
                ["5. vai_tro (Vai trò RBAC)"] = await _context.VaiTros.CountAsync(),
                ["6. quyen (Danh mục quyền chức năng)"] = await _context.Quyens.CountAsync(),
                ["7. vai_tro_quyen (Phân quyền vai trò)"] = await _context.VaiTroQuyens.CountAsync(),
                ["8. tai_khoan (Tài khoản người dùng)"] = await _context.TaiKhoans.CountAsync(),
                ["9. yeu_cau_tai_khoan (Hàng đợi cấp tài khoản)"] = await _context.YeuCauTaiKhoans.CountAsync(),
                ["10. nhan_vien (Hồ sơ nhân viên)"] = await _context.NhanViens.CountAsync(),
                ["11. hop_dong (Hợp đồng lao động)"] = await _context.HopDongs.CountAsync(),
                ["12. phu_luc_luong (Phụ lục lương)"] = await _context.PhuLucLuongs.CountAsync(),
                ["13. ca_lam_viec (Ca làm việc 24/7)"] = await _context.CaLamViecs.CountAsync(),
                ["14. dinh_bien_ca (Định biên chuẩn theo ca)"] = await _context.DinhBienCas.CountAsync(),
                ["15. dinh_bien_ngay (Định biên riêng theo ngày)"] = await _context.DinhBienNgays.CountAsync(),
                ["16. cong_bo_lich_tuan (Công bố lịch tuần)"] = await _context.CongBoLichTuans.CountAsync(),
                ["17. phan_ca (Lịch phân ca nhân viên)"] = await _context.PhanCas.CountAsync(),
                ["18. cham_cong (Nhật ký chấm công)"] = await _context.ChamCongs.CountAsync(),
                ["19. giai_trinh_cham_cong (Giải trình chấm công)"] = await _context.GiaiTrinhChamCongs.CountAsync(),
                ["20. yeu_cau_doi_ca (Yêu cầu đổi ca trực)"] = await _context.YeuCauDoiCas.CountAsync(),
                ["21. don_tu (Đơn từ nghỉ phép & xin nghỉ)"] = await _context.DonTus.CountAsync(),
                ["22. du_an (Sự kiện & Dự án khách sạn)"] = await _context.DuAns.CountAsync(),
                ["23. nhu_cau_su_kien (Nhu cầu nhân lực sự kiện)"] = await _context.NhuCauSuKiens.CountAsync(),
                ["24. phan_cong_du_an (Điều động nhân sự sự kiện)"] = await _context.PhanCongDuAns.CountAsync(),
                ["25. cong_su_kien (Chấm công giờ sự kiện)"] = await _context.CongSuKiens.CountAsync(),
                ["26. quyet_toan_su_kien (Quyết toán phụ cấp sự kiện)"] = await _context.QuyetToanSuKiens.CountAsync(),
                ["27. ky_luong (Kỳ tính lương)"] = await _context.KyLuongs.CountAsync(),
                ["28. ngan_sach_luong (Kế hoạch ngân sách lương)"] = await _context.NganSachLuongs.CountAsync(),
                ["29. cau_hinh_khoan_luong (Cấu hình khoản lương)"] = await _context.CauHinhKhoanLuongs.CountAsync(),
                ["30. cau_hinh_tang_ca (Cấu hình hệ số OT)"] = await _context.CauHinhTangCas.CountAsync(),
                ["31. cau_hinh_phu_cap (Cấu hình phụ cấp)"] = await _context.CauHinhPhuCaps.CountAsync(),
                ["32. cau_hinh_khau_tru (Cấu hình khoản trừ)"] = await _context.CauHinhKhauTrus.CountAsync(),
                ["33. phieu_luong (Bảng phiếu lương nhân viên)"] = await _context.PhieuLuongs.CountAsync(),
                ["34. chi_tiet_phieu_luong (Chi tiết phụ cấp phiếu lương)"] = await _context.ChiTietPhieuLuongs.CountAsync(),
                ["35. chi_tiet_tru_bao_hiem (Chi tiết trừ BHXH/BHYT/BHTN)"] = await _context.ChiTietTruBaoHiems.CountAsync(),
                ["36. chi_tiet_tru_thue_tncn (Chi tiết trừ thuế TNCN)"] = await _context.ChiTietTruThueTncns.CountAsync(),
                ["37. chi_tiet_tru_vi_pham_tam_ung (Khấu trừ phạt & tạm ứng)"] = await _context.ChiTietTruViPhamTamUngs.CountAsync(),
                ["38. nhat_ky_quan_tri (Nhật ký kiểm toán an ninh)"] = await _context.NhatKyQuanTris.CountAsync()
            };
        }
    }
}
