-- Employee operations only. Manager verification and HR approval stay on WebAdmin.
BEGIN;
DO $$ BEGIN
 IF has_schema_privilege('authenticated','public','CREATE') OR has_schema_privilege('anon','public','CREATE') THEN
  RAISE EXCEPTION 'Cần kiểm kê quyền CREATE trong schema public trước triển khai.';
 END IF;
END $$;
CREATE FUNCTION public.hrm_attendance_explanation(p_action text,p_payload jsonb DEFAULT '{}'::jsonb)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, public
AS $$
DECLARE
 me integer; dept integer; actor_role text;
 shift public.phan_ca%ROWTYPE;
 explanation public.giai_trinh_cham_cong%ROWTYPE;
 result jsonb;
 proposed_in timestamptz := (p_payload->>'in')::timestamptz;
 proposed_out timestamptz := (p_payload->>'out')::timestamptz;
 reason text := nullif(btrim(p_payload->>'reason'),'');
 evidence text := nullif(btrim(p_payload->>'evidence'),'');
BEGIN
 IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ.' USING ERRCODE='42501'; END IF;
 me:=public.nhan_vien_hien_tai();
 SELECT nv.ma_phong_ban,v.ten_vai_tro INTO dept,actor_role FROM public.tai_khoan t
 JOIN public.nhan_vien nv USING(ma_nhan_vien) JOIN public.vai_tro v USING(ma_vai_tro)
 WHERE t.ma_nhan_vien=me AND t.trang_thai=true;
 IF NOT FOUND THEN RAISE EXCEPTION 'Tài khoản không hoạt động.' USING ERRCODE='42501'; END IF;
 IF p_action='eligible-shifts' THEN
  SELECT coalesce(jsonb_agg(to_jsonb(q) ORDER BY q."NgayLamViec" DESC,q."GioBatDau" DESC),'[]'::jsonb) INTO result FROM (
   SELECT p.ma_phan_ca AS "MaPhanCa",p.ngay_lam_viec AS "NgayLamViec",p.ma_ca AS "MaCa",c.ten_ca AS "TenCa",
    c.gio_bat_dau AS "GioBatDau",c.gio_ket_thuc AS "GioKetThuc",cc.gio_check_in AS "GioVaoThucTe",cc.gio_check_out AS "GioRaThucTe",cc.trang_thai AS "TrangThaiChamCong"
   FROM public.phan_ca p JOIN public.ca_lam_viec c USING(ma_ca) LEFT JOIN public.cham_cong cc USING(ma_phan_ca)
   WHERE p.ma_nhan_vien=me AND p.ket_thuc_du_kien BETWEEN now()-interval '45 days' AND now()
    AND NOT EXISTS(SELECT 1 FROM public.giai_trinh_cham_cong g WHERE g.ma_phan_ca=p.ma_phan_ca AND g.trang_thai IN ('CHO_QUAN_LY','CHO_HR'))
  )q;
  RETURN result;
 ELSIF p_action IN ('list','detail') THEN
  SELECT coalesce(jsonb_agg(to_jsonb(q) ORDER BY q."NgayGui" DESC),'[]'::jsonb) INTO result FROM (
   SELECT g.ma_giai_trinh AS "MaGiaiTrinh",g.ma_phan_ca AS "MaPhanCa",g.ma_nhan_vien AS "MaNhanVien",
    concat_ws(' ',nv.ho,nv.ten) AS "HoTenNhanVien",pb.ten_phong_ban AS "TenPhongBan",p.ngay_lam_viec AS "NgayLamViec",c.ten_ca AS "TenCa",
    g.gio_de_xuat_vao AS "GioDeXuatVao",g.gio_de_xuat_ra AS "GioDeXuatRa",cc.gio_check_in AS "GioVaoThucTe",cc.gio_check_out AS "GioRaThucTe",
    g.ly_do AS "LyDo",g.minh_chung AS "MinhChung",g.trang_thai AS "TrangThai",
    CASE g.trang_thai WHEN 'CHO_QUAN_LY' THEN 'Chờ Quản lý xác minh' WHEN 'CHO_HR' THEN 'Chờ HR xác nhận' WHEN 'DA_DUYET' THEN 'Đã duyệt' WHEN 'TU_CHOI' THEN 'Từ chối' WHEN 'DA_HUY' THEN 'Đã rút' ELSE g.trang_thai END AS "TrangThaiLabel",
    g.ngay_gui AS "NgayGui",g.ma_quan_ly_xac_minh AS "MaQuanLyXacMinh",nullif(concat_ws(' ',ql.ho,ql.ten),'') AS "HoTenQuanLy",
    g.ngay_quan_ly_xac_minh AS "NgayQuanLyXacMinh",g.y_kien_quan_ly AS "YKienQuanLy",g.ma_hr_xac_nhan AS "MaHrXacNhan",
    nullif(concat_ws(' ',hr.ho,hr.ten),'') AS "HoTenHr",g.ngay_hr_xac_nhan AS "NgayHrXacNhan",g.y_kien_hr AS "YKienHr",
    g.trang_thai='CHO_QUAN_LY' AND g.ma_nhan_vien=me AS "CanWithdraw"
   FROM public.giai_trinh_cham_cong g JOIN public.nhan_vien nv ON nv.ma_nhan_vien=g.ma_nhan_vien
   JOIN public.phan_ca p USING(ma_phan_ca) JOIN public.ca_lam_viec c USING(ma_ca)
   LEFT JOIN public.cham_cong cc USING(ma_phan_ca) LEFT JOIN public.phong_ban pb ON pb.ma_phong_ban=nv.ma_phong_ban
   LEFT JOIN public.nhan_vien ql ON ql.ma_nhan_vien=g.ma_quan_ly_xac_minh LEFT JOIN public.nhan_vien hr ON hr.ma_nhan_vien=g.ma_hr_xac_nhan
   WHERE (g.ma_nhan_vien=me OR (p_action='detail' AND (actor_role IN ('Admin','HR','IT') OR (actor_role IN ('Manager','QuanLy') AND dept=nv.ma_phong_ban))))
    AND (p_action='list' OR g.ma_giai_trinh=(p_payload->>'id')::integer)
  )q;
  IF p_action='detail' THEN RETURN result->0; END IF;
  RETURN result;
 ELSIF p_action='create' THEN
  IF reason IS NULL OR length(reason)>500 OR length(evidence)>500 THEN RAISE EXCEPTION 'Lý do bắt buộc; lý do và minh chứng tối đa 500 ký tự.'; END IF;
  IF proposed_in IS NULL AND proposed_out IS NULL THEN RAISE EXCEPTION 'Phải đề xuất ít nhất giờ vào hoặc giờ ra.'; END IF;
  IF proposed_in>now() OR proposed_out>now() THEN RAISE EXCEPTION 'Giờ đề xuất không được ở tương lai.'; END IF;
  IF proposed_out<=proposed_in THEN RAISE EXCEPTION 'Giờ ra đề xuất phải sau giờ vào.'; END IF;
  -- Serializes with shift swap; row lock serializes submissions for the same shift.
  PERFORM pg_advisory_xact_lock(2026101509);
  SELECT * INTO shift FROM public.phan_ca WHERE ma_phan_ca=(p_payload->>'shift')::integer FOR UPDATE;
  IF NOT FOUND OR shift.ma_nhan_vien<>me THEN RAISE EXCEPTION 'Ca không tồn tại hoặc không thuộc về bạn.' USING ERRCODE='42501'; END IF;
  IF shift.ket_thuc_du_kien>now() THEN RAISE EXCEPTION 'Ca trực chưa kết thúc.'; END IF;
  IF shift.ket_thuc_du_kien<now()-interval '45 days' THEN RAISE EXCEPTION 'Chỉ nhận giải trình trong vòng 45 ngày từ khi ca kết thúc.'; END IF;
  IF EXISTS(SELECT 1 FROM public.giai_trinh_cham_cong WHERE ma_phan_ca=shift.ma_phan_ca AND trang_thai IN ('CHO_QUAN_LY','CHO_HR')) THEN RAISE EXCEPTION 'Ca này đang có giải trình chờ xử lý.'; END IF;
  INSERT INTO public.giai_trinh_cham_cong(ma_phan_ca,ma_nhan_vien,gio_de_xuat_vao,gio_de_xuat_ra,ly_do,minh_chung,trang_thai,ngay_gui)
   VALUES(shift.ma_phan_ca,me,proposed_in,proposed_out,reason,evidence,'CHO_QUAN_LY',now()) RETURNING * INTO explanation;
  RETURN jsonb_build_object('Success',true,'Code','CREATED','Message','Đã gửi giải trình, chờ Quản lý xác minh.','MaGiaiTrinh',explanation.ma_giai_trinh);
 ELSIF p_action='withdraw' THEN
  SELECT * INTO explanation FROM public.giai_trinh_cham_cong WHERE ma_giai_trinh=(p_payload->>'id')::integer FOR UPDATE;
  IF NOT FOUND OR explanation.ma_nhan_vien<>me THEN RAISE EXCEPTION 'Bạn không có quyền rút giải trình này.' USING ERRCODE='42501'; END IF;
  IF explanation.trang_thai<>'CHO_QUAN_LY' THEN RAISE EXCEPTION 'Giải trình đã được xử lý hoặc chuyển HR; không thể rút.'; END IF;
  UPDATE public.giai_trinh_cham_cong SET trang_thai='DA_HUY' WHERE ma_giai_trinh=explanation.ma_giai_trinh;
  RETURN jsonb_build_object('Success',true,'Code','WITHDRAWN','Message','Đã rút giải trình.','MaGiaiTrinh',explanation.ma_giai_trinh);
 END IF;
 RAISE EXCEPTION 'Thao tác không hợp lệ.';
END;
$$;
REVOKE ALL ON FUNCTION public.hrm_attendance_explanation(text,jsonb) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_attendance_explanation(text,jsonb) TO authenticated;
REVOKE INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES ON public.giai_trinh_cham_cong FROM PUBLIC,anon,authenticated;
DO $$ DECLARE r text; BEGIN
 FOREACH r IN ARRAY ARRAY['anon','authenticated'] LOOP
  IF has_any_column_privilege(r,'public.giai_trinh_cham_cong','INSERT,UPDATE,REFERENCES') OR has_table_privilege(r,'public.giai_trinh_cham_cong','INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES') THEN
   RAISE EXCEPTION 'Quyền ghi kế thừa/cột còn tồn tại; cần kiểm kê trước triển khai.';
  END IF;
 END LOOP;
END $$;
NOTIFY pgrst,'reload schema';
COMMIT;
