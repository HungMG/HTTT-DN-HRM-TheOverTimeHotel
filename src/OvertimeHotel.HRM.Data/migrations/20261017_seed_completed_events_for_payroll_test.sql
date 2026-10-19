-- Manual test-data seed requested by the user: October 2026 payroll.
-- Only creates the two labelled events, assignments and their seed audit entries.
-- Reruns preserve assignments already transferred to payroll.
BEGIN;
SELECT pg_advisory_xact_lock(2026101509);

CREATE TEMP TABLE payroll_test_events (
    code INT PRIMARY KEY, name TEXT UNIQUE, type TEXT, venue TEXT,
    starts TIMESTAMPTZ, ends TIMESTAMPTZ, guests INT, budget NUMERIC(15,2)
) ON COMMIT DROP;
INSERT INTO payroll_test_events VALUES
(1, 'TEST LƯƠNG 10/2026 - Tiệc cưới', 'TIEC_CUOI', 'TEST - Sảnh Diamond',
 '2026-10-10 17:00:00+07', '2026-10-10 21:00:00+07', 40, 3800000),
(2, 'TEST LƯƠNG 10/2026 - Hội nghị MICE', 'HOI_NGHI_MICE', 'TEST - Phòng hội nghị Lotus',
 '2026-10-12 08:00:00+07', '2026-10-12 17:00:00+07', 60, 3900000);

CREATE TEMP TABLE payroll_test_members (
    event_code INT REFERENCES payroll_test_events(code), employee_id INT,
    department_id INT, amount NUMERIC(15,2), PRIMARY KEY(event_code, employee_id)
) ON COMMIT DROP;
INSERT INTO payroll_test_members VALUES
(1, 3, 2, 400000), (1, 5, 3, 300000), (1, 8, 4, 500000), (1, 9, 4, 500000),
(1, 11, 5, 600000), (1, 12, 5, 600000), (1, 14, 6, 500000), (1, 17, 7, 400000),
(2, 3, 2, 400000), (2, 8, 4, 400000), (2, 9, 4, 400000), (2, 11, 5, 500000),
(2, 12, 5, 500000), (2, 14, 6, 600000), (2, 17, 7, 400000), (2, 21, 9, 700000);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.ky_luong WHERE thang=10 AND nam=2026 AND trang_thai NOT IN ('MO','NHAP','TU_CHOI')) THEN
        RAISE EXCEPTION 'October 2026 payroll is already under approval or locked; no test data inserted.';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM public.nhan_vien n JOIN public.tai_khoan t ON t.ma_nhan_vien=n.ma_nhan_vien
        JOIN public.vai_tro v ON v.ma_vai_tro=t.ma_vai_tro
        WHERE n.ma_nhan_vien=3 AND n.trang_thai='DANG_LAM' AND t.trang_thai
            AND upper(v.ten_vai_tro) IN ('MANAGER','HR','ADMIN')
    ) THEN RAISE EXCEPTION 'The selected event manager is no longer eligible.'; END IF;
    IF EXISTS (
        SELECT 1 FROM payroll_test_members m JOIN payroll_test_events e ON e.code=m.event_code
        LEFT JOIN public.nhan_vien n ON n.ma_nhan_vien=m.employee_id
        WHERE n.ma_nhan_vien IS NULL OR n.ma_phong_ban IS DISTINCT FROM m.department_id
            OR n.trang_thai <> 'DANG_LAM' OR n.ngay_vao_lam > (e.starts AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
            OR n.ngay_thoi_viec <= (e.ends AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
            OR NOT EXISTS (
                SELECT 1 FROM public.hop_dong h WHERE h.ma_nhan_vien=m.employee_id AND h.trang_thai='HIEU_LUC'
                    AND h.ngay_bat_dau <= (e.starts AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
                    AND (h.ngay_ket_thuc IS NULL OR h.ngay_ket_thuc >= (e.ends AT TIME ZONE 'Asia/Ho_Chi_Minh')::date)
            ) OR NOT EXISTS (
                SELECT 1 FROM public.cau_hinh_khoan_luong c WHERE c.loai_khoan='THUONG'
                    AND c.loai_du_an=e.type AND c.ma_phong_ban=m.department_id AND c.gia_tri_mac_dinh=m.amount
            )
    ) THEN RAISE EXCEPTION 'An employee, contract or configured rate changed; no test data inserted.'; END IF;
    IF EXISTS (
        SELECT 1 FROM payroll_test_events e JOIN public.du_an d ON d.ten_du_an=e.name
        WHERE d.mo_ta IS DISTINCT FROM 'PAYROLL-EVENTS-20261017: Dữ liệu mẫu kiểm thử lương tháng 10/2026.'
            OR d.loai_du_an<>e.type OR d.ngay_bat_dau<>e.starts OR d.ngay_ket_thuc<>e.ends
            OR d.trang_thai<>'HOAN_THANH' OR d.ngan_sach_phu_cap<>e.budget
    ) THEN RAISE EXCEPTION 'A conflicting event already uses the fixture name; no existing data changed.'; END IF;
    IF EXISTS (
        SELECT 1 FROM payroll_test_members m JOIN payroll_test_events e ON e.code=m.event_code
        JOIN public.phan_cong_du_an p ON p.ma_nhan_vien=m.employee_id
        JOIN public.du_an d ON d.ma_du_an=p.ma_du_an
        WHERE d.ten_du_an<>e.name AND d.trang_thai<>'HUY' AND p.bat_dau<e.ends AND p.ket_thuc>e.starts
    ) THEN RAISE EXCEPTION 'An employee already has an overlapping event assignment.'; END IF;
END $$;

INSERT INTO public.du_an
    (ten_du_an, loai_du_an, dia_diem, ngay_bat_dau, ngay_ket_thuc, ngan_sach_phu_cap,
     ma_nguoi_quan_ly, mo_ta, trang_thai, so_khach_du_kien)
SELECT name, type, venue, starts, ends, budget, 3,
    'PAYROLL-EVENTS-20261017: Dữ liệu mẫu kiểm thử lương tháng 10/2026.', 'HOAN_THANH', guests
FROM payroll_test_events ORDER BY code ON CONFLICT (ten_du_an) DO NOTHING;

INSERT INTO public.phan_cong_du_an
    (ma_du_an, ma_nhan_vien, vai_tro_du_an, nhiem_vu_cu_the, phu_cap_du_kien, so_tien_phu_cap,
     bat_dau, ket_thuc, danh_gia, trang_thai_chi_tra, ngay_danh_gia, ngay_phan_cong)
SELECT d.ma_du_an, m.employee_id,
    CASE m.department_id WHEN 2 THEN 'Đón tiếp khách' WHEN 3 THEN 'Chuẩn bị và vệ sinh khu vực'
        WHEN 4 THEN 'Phục vụ tiệc' WHEN 5 THEN 'Chế biến món ăn' WHEN 6 THEN 'Âm thanh và ánh sáng'
        WHEN 7 THEN 'Bảo đảm an ninh' WHEN 9 THEN 'Hỗ trợ mạng và trình chiếu' END,
    'PAYROLL-EVENTS-20261017: Đã hoàn thành đầy đủ công việc; dùng kiểm tra phụ cấp sự kiện.',
    m.amount, m.amount, e.starts, e.ends, 'HOAN_THANH', 'CHUA_CHI_TRA', e.ends + INTERVAL '1 hour', e.starts - INTERVAL '2 days'
FROM payroll_test_members m JOIN payroll_test_events e ON e.code=m.event_code
JOIN public.du_an d ON d.ten_du_an=e.name
ORDER BY m.event_code, m.employee_id ON CONFLICT (ma_du_an, ma_nhan_vien) DO NOTHING;

INSERT INTO public.nhat_ky_quan_tri
    (ten_nguoi_thuc_hien, ten_tai_khoan_bi_tac_dong, hanh_dong, noi_dung, gia_tri_moi)
SELECT 'Dữ liệu thử tính lương', 'SK-' || d.ma_du_an, 'TAO_DU_LIEU_TEST',
    'Tạo dữ liệu mẫu theo yêu cầu: sự kiện hoàn thành, 8 thành viên, phụ cấp chờ kết chuyển lương.',
    'PAYROLL-EVENTS-20261017; HOAN_THANH; ngân sách=' || e.budget
FROM payroll_test_events e JOIN public.du_an d ON d.ten_du_an=e.name
WHERE NOT EXISTS (SELECT 1 FROM public.nhat_ky_quan_tri h WHERE h.ten_tai_khoan_bi_tac_dong='SK-' || d.ma_du_an
    AND h.hanh_dong='TAO_DU_LIEU_TEST' AND h.gia_tri_moi LIKE 'PAYROLL-EVENTS-20261017;%');

DO $$
BEGIN
    IF (SELECT count(*) FROM public.phan_cong_du_an p JOIN public.du_an d ON d.ma_du_an=p.ma_du_an
        JOIN payroll_test_events e ON e.name=d.ten_du_an) <> 16 OR EXISTS (
        SELECT 1 FROM payroll_test_members m JOIN payroll_test_events e ON e.code=m.event_code
        JOIN public.du_an d ON d.ten_du_an=e.name
        LEFT JOIN public.phan_cong_du_an p ON p.ma_du_an=d.ma_du_an AND p.ma_nhan_vien=m.employee_id
        WHERE p.ma_phan_cong IS NULL OR p.danh_gia<>'HOAN_THANH' OR p.ngay_danh_gia IS NULL
            OR p.phu_cap_du_kien<>m.amount OR p.so_tien_phu_cap<>m.amount OR p.bat_dau<>e.starts OR p.ket_thuc<>e.ends
            OR p.trang_thai_chi_tra NOT IN ('CHUA_CHI_TRA','DA_KET_CHUYEN_LUONG')
            OR (p.trang_thai_chi_tra='CHUA_CHI_TRA' AND p.ma_chi_tiet IS NOT NULL)
            OR (p.trang_thai_chi_tra='DA_KET_CHUYEN_LUONG' AND p.ma_chi_tiet IS NULL)
    ) OR EXISTS (
        SELECT 1 FROM payroll_test_events e JOIN payroll_test_members m ON m.event_code=e.code
        GROUP BY e.code,e.budget HAVING count(*)<>8 OR sum(m.amount)<>e.budget
    ) THEN RAISE EXCEPTION 'Test fixture did not match the expected 16 assignments and approved totals; rolling back.'; END IF;
END $$;
COMMIT;
