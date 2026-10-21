ALTER TABLE du_an
    ADD COLUMN IF NOT EXISTS tinh_phu_cap_khi_nghiem_thu boolean NOT NULL DEFAULT false;

ALTER TABLE phan_cong_du_an
    ADD COLUMN IF NOT EXISTS ma_phong_ban_su_kien varchar(30);
