BEGIN;

-- Cập nhật hàm kiểm tra quyền đọc phiếu lương:
-- 1. Phiếu lương chính thức CHỈ gửi và hiển thị khi kỳ lương đã KHÓA SỔ
--    (p.trang_thai = 'DA_THANH_TOAN' HOẶC k.trang_thai = 'KHOA_SO' HOẶC k.ngay_khoa_so IS NOT NULL).
-- 2. Trước khi khóa sổ, nhân viên CHỈ được đọc nếu HR đã phát hành bản nháp để đối soát
--    (k.ngay_phat_hanh_nhap IS NOT NULL VÀ k.trang_thai != 'KHOA_SO' VÀ k.ngay_khoa_so IS NULL).
-- 3. Khi Sếp duyệt (DA_DUYET) nhưng chưa khóa sổ, nếu không có phát hành nháp thì chưa gửi về app nhân viên.
CREATE OR REPLACE FUNCTION public.hrm_can_read_own_payslip(p_id integer) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT public.hrm_account_session_valid() AND EXISTS (
  SELECT 1 FROM public.phieu_luong p JOIN public.ky_luong k USING(ma_ky_luong)
  WHERE p.ma_phieu_luong=p_id AND p.ma_nhan_vien=public.nhan_vien_hien_tai() AND (
   -- Đã khóa sổ: gửi chính thức
   (p.trang_thai='DA_THANH_TOAN' OR k.trang_thai='KHOA_SO' OR k.ngay_khoa_so IS NOT NULL)
   -- Chưa khóa sổ: chỉ đọc khi HR đã phát hành bản nháp đối soát
   OR (k.ngay_phat_hanh_nhap IS NOT NULL AND k.trang_thai != 'KHOA_SO' AND k.ngay_khoa_so IS NULL
       AND coalesce(k.ly_do_tu_choi,'')='')
  )
 )
$$;

REVOKE ALL ON FUNCTION public.hrm_can_read_own_payslip(integer) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_can_read_own_payslip(integer) TO authenticated;

COMMIT;
