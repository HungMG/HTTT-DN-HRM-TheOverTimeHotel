-- Add the event workflow permission. Only HR is selected initially.
BEGIN;
INSERT INTO public.quyen(ma_quyen_code,ten_quyen,mo_ta)
VALUES ('QL_SU_KIEN','Quản lý sự kiện khách sạn','Truy cập luồng sự kiện: lập kế hoạch, phân công, theo dõi và nghiệm thu theo phạm vi vai trò.')
ON CONFLICT(ma_quyen_code) DO UPDATE SET ten_quyen=excluded.ten_quyen,mo_ta=excluded.mo_ta;
-- Role grants are managed in the permission screen.

-- Keep mobile effective permissions consistent with WebAdmin's explicit event grant.
-- Current permission checks are installed by the mobile alignment migration.
NOTIFY pgrst,'reload schema';
COMMIT;
