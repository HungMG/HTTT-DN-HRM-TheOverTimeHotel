-- Approved fixed bonuses per employee/event. No excellent-rating multiplier.
-- Additive and rerunnable: preserve existing configuration and payroll history.
BEGIN;
SELECT pg_advisory_xact_lock(2026101602);
LOCK TABLE public.cau_hinh_khoan_luong IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE event_bonus_original ON COMMIT DROP AS
SELECT ma_khoan, ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta
FROM public.cau_hinh_khoan_luong;

ALTER TABLE public.cau_hinh_khoan_luong
    ADD COLUMN IF NOT EXISTS loai_du_an VARCHAR(50),
    ADD COLUMN IF NOT EXISTS ma_phong_ban INT;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.cau_hinh_khoan_luong'::regclass AND conname = 'fk_event_bonus_department') THEN
        ALTER TABLE public.cau_hinh_khoan_luong ADD CONSTRAINT fk_event_bonus_department
            FOREIGN KEY (ma_phong_ban) REFERENCES public.phong_ban(ma_phong_ban) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public.cau_hinh_khoan_luong'::regclass AND conname = 'chk_event_bonus_configuration') THEN
        ALTER TABLE public.cau_hinh_khoan_luong ADD CONSTRAINT chk_event_bonus_configuration CHECK (
            (loai_du_an IS NULL AND ma_phong_ban IS NULL)
            OR (loai_du_an IS NOT NULL AND ma_phong_ban IS NOT NULL AND loai_khoan = 'THUONG'
                AND loai_du_an IN ('TIEC_CUOI', 'HOI_NGHI_MICE', 'GALA_DINNER', 'FESTIVAL_AM_THUC', 'DOAN_VIP')
                AND gia_tri_mac_dinh >= 0)
        );
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_event_bonus_type_department
    ON public.cau_hinh_khoan_luong(loai_du_an, ma_phong_ban)
    WHERE loai_du_an IS NOT NULL;

CREATE TEMP TABLE event_bonus_seed (
    loai_du_an VARCHAR(50) NOT NULL,
    ma_phong_ban INT NOT NULL,
    ten_khoan VARCHAR(150) NOT NULL,
    gia_tri_mac_dinh NUMERIC(15,2) NOT NULL,
    PRIMARY KEY (loai_du_an, ma_phong_ban)
) ON COMMIT DROP;

INSERT INTO event_bonus_seed VALUES
('TIEC_CUOI', 2, 'Thưởng Tiệc cưới - Tiền sảnh', 400000),
('TIEC_CUOI', 4, 'Thưởng Tiệc cưới - F&B', 500000),
('TIEC_CUOI', 5, 'Thưởng Tiệc cưới - Bếp', 600000),
('TIEC_CUOI', 3, 'Thưởng Tiệc cưới - Buồng phòng', 300000),
('TIEC_CUOI', 6, 'Thưởng Tiệc cưới - Kỹ thuật', 500000),
('TIEC_CUOI', 7, 'Thưởng Tiệc cưới - An ninh', 400000),
('HOI_NGHI_MICE', 2, 'Thưởng MICE - Tiền sảnh', 400000),
('HOI_NGHI_MICE', 4, 'Thưởng MICE - F&B', 400000),
('HOI_NGHI_MICE', 5, 'Thưởng MICE - Bếp', 500000),
('HOI_NGHI_MICE', 6, 'Thưởng MICE - Kỹ thuật', 600000),
('HOI_NGHI_MICE', 7, 'Thưởng MICE - An ninh', 400000),
('HOI_NGHI_MICE', 9, 'Thưởng MICE - IT', 700000),
('GALA_DINNER', 2, 'Thưởng Gala Dinner - Tiền sảnh', 400000),
('GALA_DINNER', 4, 'Thưởng Gala Dinner - F&B', 600000),
('GALA_DINNER', 5, 'Thưởng Gala Dinner - Bếp', 700000),
('GALA_DINNER', 6, 'Thưởng Gala Dinner - Kỹ thuật', 600000),
('GALA_DINNER', 7, 'Thưởng Gala Dinner - An ninh', 400000),
('FESTIVAL_AM_THUC', 4, 'Thưởng Festival ẩm thực - F&B', 500000),
('FESTIVAL_AM_THUC', 5, 'Thưởng Festival ẩm thực - Bếp', 700000),
('FESTIVAL_AM_THUC', 3, 'Thưởng Festival ẩm thực - Buồng phòng', 400000),
('FESTIVAL_AM_THUC', 6, 'Thưởng Festival ẩm thực - Kỹ thuật', 500000),
('FESTIVAL_AM_THUC', 7, 'Thưởng Festival ẩm thực - An ninh', 400000),
('DOAN_VIP', 2, 'Thưởng Đoàn VIP - Tiền sảnh', 600000),
('DOAN_VIP', 4, 'Thưởng Đoàn VIP - F&B', 700000),
('DOAN_VIP', 5, 'Thưởng Đoàn VIP - Bếp', 800000),
('DOAN_VIP', 3, 'Thưởng Đoàn VIP - Buồng phòng', 600000),
('DOAN_VIP', 7, 'Thưởng Đoàn VIP - An ninh', 700000);

INSERT INTO public.cau_hinh_khoan_luong
    (ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta, loai_du_an, ma_phong_ban)
SELECT s.ten_khoan, 'THUONG', s.gia_tri_mac_dinh,
    'Thưởng/người/sự kiện; hoàn thành nhận đủ, thiếu thời lượng tính theo tỷ lệ, không tham gia nhận 0.',
    s.loai_du_an, s.ma_phong_ban
FROM event_bonus_seed s
WHERE NOT EXISTS (
    SELECT 1 FROM public.cau_hinh_khoan_luong c
    WHERE c.loai_du_an = s.loai_du_an AND c.ma_phong_ban = s.ma_phong_ban
)
ORDER BY s.loai_du_an, s.ma_phong_ban;

DO $$
BEGIN
    IF (SELECT count(*) FROM event_bonus_seed) <> 27 OR EXISTS (
        SELECT 1 FROM event_bonus_seed s
        LEFT JOIN public.cau_hinh_khoan_luong c
            ON c.loai_du_an = s.loai_du_an AND c.ma_phong_ban = s.ma_phong_ban
        WHERE c.ma_khoan IS NULL OR c.loai_khoan <> 'THUONG'
            OR c.ten_khoan <> s.ten_khoan OR c.gia_tri_mac_dinh <> s.gia_tri_mac_dinh
    ) THEN
        RAISE EXCEPTION 'Event bonus configuration does not match the 27 approved rates; transaction rolled back.';
    END IF;
    IF EXISTS (
        SELECT ma_khoan, ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta FROM event_bonus_original
        EXCEPT
        SELECT ma_khoan, ten_khoan, loai_khoan, gia_tri_mac_dinh, mo_ta FROM public.cau_hinh_khoan_luong
    ) THEN
        RAISE EXCEPTION 'Existing salary configuration changed; transaction rolled back.';
    END IF;
END $$;

COMMIT;
