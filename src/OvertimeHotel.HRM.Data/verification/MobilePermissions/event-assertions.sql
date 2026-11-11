DO $$ BEGIN
 IF (SELECT count(*) FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen) JOIN public.vai_tro v USING(ma_vai_tro) WHERE q.ma_quyen_code='QL_SU_KIEN' AND v.ten_vai_tro='HR')<>1 THEN RAISE EXCEPTION 'Missing HR event grant'; END IF;
 IF EXISTS(SELECT 1 FROM public.vai_tro_quyen vq JOIN public.quyen q USING(ma_quyen) JOIN public.vai_tro v USING(ma_vai_tro) WHERE q.ma_quyen_code='QL_SU_KIEN' AND v.ten_vai_tro<>'HR') THEN RAISE EXCEPTION 'Unexpected event grant'; END IF;
END $$;
SELECT v.ten_vai_tro,exists(select 1 from public.vai_tro_quyen vq join public.quyen q using(ma_quyen) where vq.ma_vai_tro=v.ma_vai_tro and q.ma_quyen_code='QL_SU_KIEN') as event_granted FROM public.vai_tro v ORDER BY v.ma_vai_tro;
