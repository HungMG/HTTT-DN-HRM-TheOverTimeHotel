using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using OvertimeHotel.HRM.Core.Models;
using OvertimeHotel.HRM.Core.Policies;
using OvertimeHotel.HRM.Data.Context;
using OvertimeHotel.HRM.WebAdmin.Models;

namespace OvertimeHotel.HRM.WebAdmin.Services;

public sealed class PayrollBudgetService(AppDbContext db, IMemoryCache cache)
{
    public const string RevisionKey = "PayrollBudgetRevision";
    public static bool MissingTable(PostgresException e) => e.SqlState == PostgresErrorCodes.UndefinedTable
        && e.MessageText.Contains("ngan_sach_luong", StringComparison.Ordinal);

    public static async Task<NganSachLuong[]?> ReadAsync(AppDbContext db, int year)
    {
        try { return await db.NganSachLuongs.AsNoTracking().Where(b => b.Nam == year).ToArrayAsync(); }
        catch (PostgresException e) when (MissingTable(e) || e.SqlState == PostgresErrorCodes.UndefinedColumn) { return null; }
    }

    public async Task SaveAsync(PayrollBudgetInput input, int actor)
    {
        if (actor <= 0 || !await db.NhanViens.AnyAsync(n => n.MaNhanVien == actor))
            throw new InvalidOperationException("Phiên đăng nhập không có nhân viên hợp lệ.");
        if (!await db.PhongBans.AnyAsync(p => p.MaPhongBan == input.MaPhongBan))
            throw new InvalidOperationException("Phòng ban không tồn tại.");
        if (input.SoTien != decimal.Round(input.SoTien, 2)) throw new InvalidOperationException("Số tiền chỉ nhận tối đa hai chữ số thập phân.");
        if (input.Id.HasValue)
        {
            if (input.Version <= 0) throw new InvalidOperationException("Thiếu phiên bản ngân sách. Hãy tải lại.");
            var changed = await db.NganSachLuongs.Where(b => b.MaNganSach == input.Id && b.Nam == input.Nam
                && b.Thang == input.Thang && b.MaPhongBan == input.MaPhongBan
                && b.Version == input.Version
                && (b.TrangThai == "DRAFT" || b.TrangThai == "TU_CHOI"))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.SoTienNganSach, input.SoTien)
                    .SetProperty(b => b.GhiChu, input.GhiChu).SetProperty(b => b.NguoiTao, actor)
                    .SetProperty(b => b.TrangThai, "DRAFT").SetProperty(b => b.NguoiDuyet, (int?)null)
                    .SetProperty(b => b.NgayDuyet, (DateTime?)null).SetProperty(b => b.Version, b => b.Version + 1));
            if (changed != 1) throw new InvalidOperationException("Bản ghi đã đổi trạng thái, không tồn tại hoặc không còn cho phép sửa.");
        }
        else
        {
            db.NganSachLuongs.Add(new() { MaNganSach=Guid.NewGuid(), Nam=input.Nam, Thang=input.Thang,
                MaPhongBan=input.MaPhongBan, SoTienNganSach=input.SoTien, GhiChu=input.GhiChu, NguoiTao=actor });
            await db.SaveChangesAsync();
        }
        cache.Set(RevisionKey, Guid.NewGuid().ToString());
    }

    public async Task TransitionAsync(Guid id, string action, int actor, long expectedVersion)
    {
        if (actor <= 0 || !await db.NhanViens.AnyAsync(n => n.MaNhanVien == actor))
            throw new InvalidOperationException("Phiên đăng nhập không có nhân viên hợp lệ.");
        var row=await db.NganSachLuongs.AsNoTracking().Where(b => b.MaNganSach==id)
            .Select(b => new { b.NguoiTao, b.TrangThai, b.Version }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Ngân sách không tồn tại.");
        if (expectedVersion <= 0 || row.Version != expectedVersion)
            throw new InvalidOperationException("Ngân sách đã đổi so với bản đang xem. Hãy tải lại và kiểm tra số tiền.");
        if (!row.NguoiTao.HasValue || !PayrollBudgetPolicy.CanTransition(row.TrangThai, action, row.NguoiTao.Value, actor))
            throw new InvalidOperationException("Chuyển trạng thái không hợp lệ. Người lập không được tự duyệt/từ chối ngân sách của mình.");
        var target=action switch { "submit"=>"CHO_DUYET", "approve"=>"DA_DUYET", "reject"=>"TU_CHOI", "lock"=>"KHOA", _=>throw new InvalidOperationException() };
        var query=db.NganSachLuongs.Where(b => b.MaNganSach==id && b.TrangThai==row.TrangThai && b.Version==expectedVersion);
        int changed;
        if (action is "approve" or "reject")
            changed=await query.ExecuteUpdateAsync(s => s.SetProperty(b=>b.TrangThai,target)
                .SetProperty(b=>b.NguoiDuyet,actor).SetProperty(b=>b.NgayDuyet,DateTime.UtcNow)
                .SetProperty(b=>b.Version,b=>b.Version+1));
        else changed=await query.ExecuteUpdateAsync(s=>s.SetProperty(b=>b.TrangThai,target).SetProperty(b=>b.Version,b=>b.Version+1));
        if (changed!=1) throw new InvalidOperationException("Ngân sách vừa đổi trạng thái. Hãy tải lại.");
        cache.Set(RevisionKey,Guid.NewGuid().ToString());
    }
}
