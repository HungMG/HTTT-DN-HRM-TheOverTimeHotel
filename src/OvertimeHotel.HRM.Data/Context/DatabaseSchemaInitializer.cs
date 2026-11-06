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

            ALTER TABLE nhan_vien
                ADD COLUMN IF NOT EXISTS so_nguoi_phu_thuoc INT NOT NULL DEFAULT 0;

            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public static Task EnsureShiftAutoPublishSchemaAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ALTER TABLE public.phan_ca
                ADD COLUMN IF NOT EXISTS gio_tang_ca_du_kien numeric(4,2) NOT NULL DEFAULT 0.00,
                ADD COLUMN IF NOT EXISTS ghi_chu_ot varchar(255);

            UPDATE public.phan_ca SET da_cong_bo = TRUE WHERE da_cong_bo = FALSE;

            ALTER TABLE public.phan_ca ALTER COLUMN da_cong_bo SET DEFAULT TRUE;
            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public static Task EnsureContractTerminationSchemaAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ALTER TABLE public.hop_dong
                ADD COLUMN IF NOT EXISTS loai_cham_dut varchar(30) NULL,
                ADD COLUMN IF NOT EXISTS ly_do_cham_dut varchar(500) NULL;

            UPDATE public.hop_dong
            SET loai_cham_dut = 'KHAC',
                ly_do_cham_dut = COALESCE(ly_do_cham_dut, 'Dữ liệu cũ')
            WHERE trang_thai IN ('DA_CHAM_DUT', 'CHAM_DUT') AND loai_cham_dut IS NULL;

            ALTER TABLE public.hop_dong DROP CONSTRAINT IF EXISTS chk_hop_dong_cham_dut;
            ALTER TABLE public.hop_dong ADD CONSTRAINT chk_hop_dong_cham_dut CHECK (
                (trang_thai NOT IN ('DA_CHAM_DUT', 'CHAM_DUT') OR loai_cham_dut IS NOT NULL)
                AND (loai_cham_dut IS NULL OR loai_cham_dut IN ('HET_HAN_KHONG_TAI_KY', 'NHAN_VIEN_XIN_NGHI', 'SA_THAI_VI_PHAM', 'KHAC'))
                AND (loai_cham_dut IS DISTINCT FROM 'KHAC' OR NULLIF(BTRIM(ly_do_cham_dut), '') IS NOT NULL)
            );
            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public static Task EnsureRemoveShiftSupervisorPositionAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        const string sql = """
            -- 1. Thêm chức vụ Nhân viên Bếp nếu chưa có
            INSERT INTO public.chuc_vu (ma_chuc_vu, ma_phong_ban, ten_chuc_vu, mo_ta)
            VALUES (13, 5, 'Nhân viên Bếp (Kitchen Staff)', 'Sơ chế nguyên liệu, hỗ trợ nấu nướng và vệ sinh khu vực bếp')
            ON CONFLICT (ma_chuc_vu) DO UPDATE
            SET ten_chuc_vu = EXCLUDED.ten_chuc_vu, ma_phong_ban = EXCLUDED.ma_phong_ban, mo_ta = EXCLUDED.mo_ta;

            SELECT setval('chuc_vu_ma_chuc_vu_seq', GREATEST((SELECT COALESCE(MAX(ma_chuc_vu), 1) FROM public.chuc_vu), 13));

            -- 2. Chuyển đổi nhân viên đang giữ chức vụ Giám Sát Ca (ID = 3) về chức vụ Nhân viên của bộ phận
            UPDATE public.nhan_vien SET ma_chuc_vu = 9 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 7; -- An ninh
            UPDATE public.nhan_vien SET ma_chuc_vu = 5 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 3; -- Buồng phòng
            UPDATE public.nhan_vien SET ma_chuc_vu = 7 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 4; -- Ẩm thực
            UPDATE public.nhan_vien SET ma_chuc_vu = 13 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 5; -- Bếp
            UPDATE public.nhan_vien SET ma_chuc_vu = 4 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 2; -- Tiền sảnh
            UPDATE public.nhan_vien SET ma_chuc_vu = 8 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 6; -- Kỹ thuật
            UPDATE public.nhan_vien SET ma_chuc_vu = 10 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 8; -- Nhân sự & Kế toán
            UPDATE public.nhan_vien SET ma_chuc_vu = 11 WHERE ma_chuc_vu = 3 AND ma_phong_ban = 9; -- CNTT
            UPDATE public.nhan_vien SET ma_chuc_vu = 4 WHERE ma_chuc_vu = 3; -- Fallback nếu còn

            -- 3. Cập nhật tài khoản của nhân sự từng giữ Giám Sát Ca: chuyển từ Manager (3) sang Employee (4)
            UPDATE public.tai_khoan SET ma_vai_tro = 4
            WHERE ma_vai_tro = 3 AND ma_nhan_vien IN (
                SELECT ma_nhan_vien FROM public.nhan_vien
                WHERE ma_chuc_vu IN (4, 5, 7, 8, 9, 10, 11, 13)
                  AND ma_nhan_vien IN (9, 12, 2281, 2289, 2303)
            );

            -- 4. Dọn dẹp các bảng phụ thuộc nếu có tham chiếu ma_chuc_vu = 3
            UPDATE public.dinh_bien_ngay SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;
            UPDATE public.dinh_bien_ca SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;
            UPDATE public.hop_dong SET ma_chuc_vu = 2 WHERE ma_chuc_vu = 3;

            -- 5. Xóa bỏ hoàn toàn chức vụ Giám Sát Ca (ID = 3)
            DELETE FROM public.chuc_vu WHERE ma_chuc_vu = 3;
            """;

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
