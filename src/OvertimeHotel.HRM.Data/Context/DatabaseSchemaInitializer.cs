using Microsoft.EntityFrameworkCore;

namespace OvertimeHotel.HRM.Data.Context;

public static class DatabaseSchemaInitializer
{
    public static Task EnsureAdminAuditLogTableAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS nhat_ky_quan_tri (
                ma_nhat_ky BIGSERIAL PRIMARY KEY,
                ma_tai_khoan_thuc_hien INT NULL REFERENCES tai_khoan(ma_tai_khoan) ON DELETE SET NULL,
                ma_tai_khoan_bi_tac_dong INT NULL REFERENCES tai_khoan(ma_tai_khoan) ON DELETE SET NULL,
                ten_nguoi_thuc_hien VARCHAR(100) NOT NULL,
                ten_tai_khoan_bi_tac_dong VARCHAR(100) NOT NULL,
                hanh_dong VARCHAR(40) NOT NULL,
                noi_dung VARCHAR(500) NOT NULL,
                gia_tri_cu VARCHAR(1000),
                gia_tri_moi VARCHAR(1000),
                ly_do VARCHAR(500),
                dia_chi_ip VARCHAR(64),
                thoi_gian TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_thoi_gian
                ON nhat_ky_quan_tri(thoi_gian DESC);

            CREATE INDEX IF NOT EXISTS idx_nhat_ky_quan_tri_tai_khoan
                ON nhat_ky_quan_tri(ma_tai_khoan_bi_tac_dong);
            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public static Task EnsurePayrollWorkflowSchemaAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ALTER TABLE ky_luong 
                ADD COLUMN IF NOT EXISTS ngay_gui_duyet TIMESTAMPTZ NULL,
                ADD COLUMN IF NOT EXISTS nguoi_gui_duyet VARCHAR(100) NULL,
                ADD COLUMN IF NOT EXISTS ngay_duyet TIMESTAMPTZ NULL,
                ADD COLUMN IF NOT EXISTS nguoi_duyet VARCHAR(100) NULL,
                ADD COLUMN IF NOT EXISTS ly_do_tu_choi TEXT NULL,
                ADD COLUMN IF NOT EXISTS ngay_khoa_so TIMESTAMPTZ NULL,
                ADD COLUMN IF NOT EXISTS nguoi_khoa_so VARCHAR(100) NULL;

            ALTER TABLE ky_luong DROP CONSTRAINT IF EXISTS ky_luong_trang_thai_check;
            ALTER TABLE ky_luong DROP CONSTRAINT IF EXISTS uq_ky_luong_trang_thai;
            ALTER TABLE ky_luong ADD CONSTRAINT ky_luong_trang_thai_check 
                CHECK (trang_thai IN ('MO', 'NHAP', 'CHO_DUYET', 'DA_DUYET', 'TU_CHOI', 'KHOA_SO'));

            ALTER TABLE phieu_luong DROP CONSTRAINT IF EXISTS phieu_luong_trang_thai_check;
            ALTER TABLE phieu_luong ADD CONSTRAINT phieu_luong_trang_thai_check 
                CHECK (trang_thai IN ('NHAP', 'CHO_DUYET', 'DA_DUYET', 'DA_THANH_TOAN'));
            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
