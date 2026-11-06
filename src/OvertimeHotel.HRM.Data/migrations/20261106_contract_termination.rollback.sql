ALTER TABLE hop_dong DROP CONSTRAINT IF EXISTS chk_hop_dong_cham_dut;
ALTER TABLE hop_dong DROP COLUMN IF EXISTS loai_cham_dut, DROP COLUMN IF EXISTS ly_do_cham_dut;
