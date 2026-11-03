-- Apply after receiver-flow migration and live schema/identity inventory.
-- Employee RPC only; approval remains in WebAdmin. No historical rows changed.
BEGIN;
DO $$ BEGIN
    IF has_schema_privilege('authenticated','public','CREATE') OR has_schema_privilege('anon','public','CREATE') THEN
        RAISE EXCEPTION 'Schema public cho phép client CREATE; cần kiểm kê và thu hồi quyền trước.';
    END IF;
END $$;
CREATE FUNCTION public.hrm_shift_swap(p_action text, p_payload jsonb DEFAULT '{}'::jsonb)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE
    me integer;
    dept integer;
    g public.phan_ca%ROWTYPE;
    n public.phan_ca%ROWTYPE;
    y public.yeu_cau_doi_ca%ROWTYPE;
    result jsonb;
    reason text := nullif(btrim(p_payload->>'reason'),'');
    accepted boolean := coalesce((p_payload->>'accept')::boolean,false);
    today date := (now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
BEGIN
    IF auth.uid() IS NULL THEN RAISE EXCEPTION 'Phiên đăng nhập không hợp lệ.' USING ERRCODE='42501'; END IF;
    me := public.nhan_vien_hien_tai();
    IF me IS NULL OR NOT EXISTS(SELECT 1 FROM public.tai_khoan t WHERE t.ma_nhan_vien=me AND t.trang_thai=true) THEN
        RAISE EXCEPTION 'Tài khoản không hoạt động.' USING ERRCODE='42501';
    END IF;
    SELECT ma_phong_ban INTO dept FROM public.nhan_vien WHERE ma_nhan_vien=me;
    IF p_action IN ('my-shifts','partner-shifts') THEN
        IF dept IS NULL THEN RAISE EXCEPTION 'Chưa xác định phòng ban nhân viên.'; END IF;
        SELECT coalesce(jsonb_agg(to_jsonb(q) ORDER BY q."NgayLamViec",q."BatDauDuKien"),'[]'::jsonb) INTO result
        FROM (
            SELECT p.ma_phan_ca AS "MaPhanCa",p.ma_nhan_vien AS "MaNhanVien",
                concat_ws(' ',v.ho,v.ten) AS "TenNhanVien",p.ngay_lam_viec AS "NgayLamViec",
                c.ten_ca AS "TenCa",to_char(c.gio_bat_dau,'HH24:MI') AS "GioBatDau",
                to_char(c.gio_ket_thuc,'HH24:MI') AS "GioKetThuc",
                p.bat_dau_du_kien AS "BatDauDuKien",p.ket_thuc_du_kien AS "KetThucDuKien"
            FROM public.phan_ca p JOIN public.nhan_vien v USING(ma_nhan_vien) JOIN public.ca_lam_viec c USING(ma_ca)
            WHERE ((p_action='my-shifts' AND p.ma_nhan_vien=me) OR
                   (p_action='partner-shifts' AND p.ma_nhan_vien<>me AND v.ma_phong_ban=dept))
                AND p.ngay_lam_viec BETWEEN today AND today+30 AND p.ket_thuc_du_kien>now()
                AND NOT EXISTS(SELECT 1 FROM public.cham_cong cc WHERE cc.ma_phan_ca=p.ma_phan_ca)
                AND NOT EXISTS(SELECT 1 FROM public.yeu_cau_doi_ca r WHERE r.trang_thai IN ('CHO_NGUOI_NHAN','CHO_DUYET')
                    AND p.ma_phan_ca IN(r.ma_phan_ca_gui,r.ma_phan_ca_nhan))
        ) q;
        RETURN result;
    ELSIF p_action IN ('list','detail') THEN
        SELECT coalesce(jsonb_agg(to_jsonb(q) ORDER BY q.priority,q."NgayGui" DESC),'[]'::jsonb) INTO result FROM (
            SELECT r.ma_yeu_cau AS "MaYeuCau",r.ma_phan_ca_gui AS "MaPhanCaGui",r.ma_phan_ca_nhan AS "MaPhanCaNhan",
                r.ma_nhan_vien_gui AS "MaNhanVienGui",r.ma_nhan_vien_nhan AS "MaNhanVienNhan",
                concat_ws(' ',vg.ho,vg.ten) AS "TenNguoiGui",concat_ws(' ',vn.ho,vn.ten) AS "TenNguoiNhan",
                cg.ten_ca AS "TenCaGui",cn.ten_ca AS "TenCaNhan",
                to_char(pg.ngay_lam_viec,'DD/MM/YYYY') AS "NgayCaGui",to_char(pn.ngay_lam_viec,'DD/MM/YYYY') AS "NgayCaNhan",
                to_char(cg.gio_bat_dau,'HH24:MI')||' - '||to_char(cg.gio_ket_thuc,'HH24:MI') AS "GioCaGui",
                to_char(cn.gio_bat_dau,'HH24:MI')||' - '||to_char(cn.gio_ket_thuc,'HH24:MI') AS "GioCaNhan",
                r.ly_do AS "LyDo",r.trang_thai AS "TrangThai",r.ben_tu_choi AS "BenTuChoi",
                r.phan_hoi_nguoi_nhan AS "PhanHoiNguoiNhan",r.ngay_nguoi_nhan_phan_hoi AS "NgayNguoiNhanPhanHoi",
                r.ma_nguoi_duyet AS "MaNguoiDuyet",nullif(concat_ws(' ',vd.ho,vd.ten),'') AS "TenNguoiDuyet",
                r.ngay_gui AS "NgayGui",r.ngay_duyet AS "NgayDuyet",r.phan_hoi_duyet AS "PhanHoiDuyet",
                r.ma_nhan_vien_gui=me AS "IsSender",r.ma_nhan_vien_nhan=me AS "IsReceiver",
                CASE WHEN r.ma_nhan_vien_nhan=me AND r.trang_thai='CHO_NGUOI_NHAN' THEN 0 WHEN r.trang_thai='CHO_DUYET' THEN 1 ELSE 2 END AS priority
            FROM public.yeu_cau_doi_ca r
            JOIN public.nhan_vien vg ON vg.ma_nhan_vien=r.ma_nhan_vien_gui
            JOIN public.nhan_vien vn ON vn.ma_nhan_vien=r.ma_nhan_vien_nhan
            JOIN public.phan_ca pg ON pg.ma_phan_ca=r.ma_phan_ca_gui JOIN public.phan_ca pn ON pn.ma_phan_ca=r.ma_phan_ca_nhan
            JOIN public.ca_lam_viec cg ON cg.ma_ca=pg.ma_ca JOIN public.ca_lam_viec cn ON cn.ma_ca=pn.ma_ca
            LEFT JOIN public.nhan_vien vd ON vd.ma_nhan_vien=r.ma_nguoi_duyet
            WHERE me IN(r.ma_nhan_vien_gui,r.ma_nhan_vien_nhan)
              AND (p_action='list' OR r.ma_yeu_cau=(p_payload->>'id')::integer)
        )q;
        IF p_action='detail' THEN RETURN result->0; END IF;
        RETURN result;
    END IF;

    -- Same lock as WebAdmin approval, sender withdrawal and API operations.
    PERFORM pg_advisory_xact_lock(2026101509);
    IF p_action IS NULL OR p_action NOT IN ('create','respond','withdraw') THEN RAISE EXCEPTION 'Thao tác không hợp lệ.'; END IF;
    IF p_action IN ('create','respond') AND (length(reason)>500 OR (p_action='create' OR NOT accepted) AND reason IS NULL) THEN
        RAISE EXCEPTION 'Vui lòng nhập lý do từ 1 đến 500 ký tự.';
    END IF;
    IF p_action IN ('respond','withdraw') THEN
        SELECT * INTO y FROM public.yeu_cau_doi_ca WHERE ma_yeu_cau=(p_payload->>'id')::integer FOR UPDATE;
        IF NOT FOUND OR (p_action='respond' AND y.ma_nhan_vien_nhan<>me) OR (p_action='withdraw' AND y.ma_nhan_vien_gui<>me) THEN
            RAISE EXCEPTION 'Bạn không có quyền xử lý yêu cầu này.' USING ERRCODE='42501';
        END IF;
        IF (p_action='respond' AND y.trang_thai<>'CHO_NGUOI_NHAN') OR
           (p_action='withdraw' AND y.trang_thai NOT IN ('CHO_NGUOI_NHAN','CHO_DUYET')) THEN
            RAISE EXCEPTION 'Yêu cầu đã được xử lý. Vui lòng tải lại.';
        END IF;
    END IF;
    IF p_action='create' OR (p_action='respond' AND accepted) THEN
        -- Row locks also serialize with FK-backed attendance writes.
        PERFORM 1 FROM public.phan_ca WHERE ma_phan_ca IN (
            CASE WHEN p_action='create' THEN (p_payload->>'senderShift')::integer ELSE y.ma_phan_ca_gui END,
            CASE WHEN p_action='create' THEN (p_payload->>'receiverShift')::integer ELSE y.ma_phan_ca_nhan END)
            ORDER BY ma_phan_ca FOR UPDATE;
        SELECT * INTO g FROM public.phan_ca WHERE ma_phan_ca=CASE WHEN p_action='create' THEN (p_payload->>'senderShift')::integer ELSE y.ma_phan_ca_gui END;
        SELECT * INTO n FROM public.phan_ca WHERE ma_phan_ca=CASE WHEN p_action='create' THEN (p_payload->>'receiverShift')::integer ELSE y.ma_phan_ca_nhan END;
        IF g.ma_phan_ca IS NULL OR n.ma_phan_ca IS NULL OR g.ma_phan_ca=n.ma_phan_ca THEN RAISE EXCEPTION 'Hai ca không hợp lệ.'; END IF;
        IF (p_action='create' AND (g.ma_nhan_vien<>me OR n.ma_nhan_vien=me)) OR
           (p_action='respond' AND (g.ma_nhan_vien<>y.ma_nhan_vien_gui OR n.ma_nhan_vien<>y.ma_nhan_vien_nhan)) THEN
            RAISE EXCEPTION 'Người sở hữu ca không hợp lệ hoặc lịch đã thay đổi.';
        END IF;
        IF dept IS NULL OR NOT EXISTS(SELECT 1 FROM public.nhan_vien WHERE ma_nhan_vien=g.ma_nhan_vien AND ma_phong_ban=dept)
           OR NOT EXISTS(SELECT 1 FROM public.nhan_vien WHERE ma_nhan_vien=n.ma_nhan_vien AND ma_phong_ban=dept) THEN
            RAISE EXCEPTION 'Chỉ được đổi ca trong cùng phòng ban.';
        END IF;
        IF g.ket_thuc_du_kien<=now() OR n.ket_thuc_du_kien<=now() OR
           g.ngay_lam_viec NOT BETWEEN today AND today+30 OR n.ngay_lam_viec NOT BETWEEN today AND today+30 OR
           g.ket_thuc_du_kien<=g.bat_dau_du_kien OR n.ket_thuc_du_kien<=n.bat_dau_du_kien THEN
            RAISE EXCEPTION 'Ca đã kết thúc hoặc ngoài khoảng 30 ngày được chọn.';
        END IF;
        IF EXISTS(SELECT 1 FROM public.cham_cong WHERE ma_phan_ca IN(g.ma_phan_ca,n.ma_phan_ca)) THEN RAISE EXCEPTION 'Ca đã có dữ liệu chấm công.'; END IF;
        IF EXISTS(SELECT 1 FROM public.yeu_cau_doi_ca r WHERE r.trang_thai IN ('CHO_NGUOI_NHAN','CHO_DUYET')
            AND (p_action='create' OR r.ma_yeu_cau<>y.ma_yeu_cau)
            AND (r.ma_phan_ca_gui IN(g.ma_phan_ca,n.ma_phan_ca) OR r.ma_phan_ca_nhan IN(g.ma_phan_ca,n.ma_phan_ca))) THEN
            RAISE EXCEPTION 'Ca đang có yêu cầu đổi ca chờ xử lý.';
        END IF;
        IF EXISTS(SELECT 1 FROM public.phan_ca p WHERE
            (p.ma_nhan_vien=g.ma_nhan_vien AND p.ma_phan_ca<>g.ma_phan_ca
                AND p.bat_dau_du_kien<n.ket_thuc_du_kien+interval '8 hours' AND p.ket_thuc_du_kien>n.bat_dau_du_kien-interval '8 hours') OR
            (p.ma_nhan_vien=n.ma_nhan_vien AND p.ma_phan_ca<>n.ma_phan_ca
                AND p.bat_dau_du_kien<g.ket_thuc_du_kien+interval '8 hours' AND p.ket_thuc_du_kien>g.bat_dau_du_kien-interval '8 hours')) THEN
            RAISE EXCEPTION 'Lịch sau đổi bị trùng ca hoặc nghỉ giữa ca dưới 8 giờ.';
        END IF;
    END IF;
    IF p_action='create' THEN
        INSERT INTO public.yeu_cau_doi_ca(ma_phan_ca_gui,ma_phan_ca_nhan,ma_nhan_vien_gui,ma_nhan_vien_nhan,ly_do,trang_thai,ngay_gui)
        VALUES(g.ma_phan_ca,n.ma_phan_ca,g.ma_nhan_vien,n.ma_nhan_vien,reason,'CHO_NGUOI_NHAN',now()) RETURNING * INTO y;
    ELSIF p_action='respond' THEN
        UPDATE public.yeu_cau_doi_ca SET trang_thai=CASE WHEN accepted THEN 'CHO_DUYET' ELSE 'TU_CHOI' END,
            ben_tu_choi=CASE WHEN accepted THEN NULL ELSE 'NGUOI_NHAN' END,phan_hoi_nguoi_nhan=reason,ngay_nguoi_nhan_phan_hoi=now()
        WHERE ma_yeu_cau=y.ma_yeu_cau;
    ELSE
        UPDATE public.yeu_cau_doi_ca SET trang_thai='DA_HUY' WHERE ma_yeu_cau=y.ma_yeu_cau;
    END IF;
    RETURN jsonb_build_object('Success',true,'Message',CASE p_action WHEN 'create' THEN 'Đã gửi yêu cầu. Chờ đồng nghiệp xác nhận.' WHEN 'respond' THEN 'Đã ghi nhận phản hồi.' ELSE 'Đã rút yêu cầu.' END,'MaYeuCau',y.ma_yeu_cau);
END;
$$;
REVOKE ALL ON FUNCTION public.hrm_shift_swap(text,jsonb) FROM PUBLIC,anon;
GRANT EXECUTE ON FUNCTION public.hrm_shift_swap(text,jsonb) TO authenticated;
-- No client may bypass the state machine with a direct table write.
REVOKE INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER ON public.yeu_cau_doi_ca FROM PUBLIC,anon,authenticated;
DO $$ BEGIN
    IF has_any_column_privilege('authenticated','public.yeu_cau_doi_ca','INSERT,UPDATE,REFERENCES')
       OR has_table_privilege('authenticated','public.yeu_cau_doi_ca','INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES')
       OR has_any_column_privilege('anon','public.yeu_cau_doi_ca','INSERT,UPDATE,REFERENCES')
       OR has_table_privilege('anon','public.yeu_cau_doi_ca','INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES') THEN
        RAISE EXCEPTION 'Quyền ghi kế thừa/cột còn tồn tại, cần kiểm kê trước khi triển khai.';
    END IF;
END $$;
NOTIFY pgrst,'reload schema';
COMMIT;
