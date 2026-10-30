#!/usr/bin/env python3
"""Run migration/constraint checks on the dedicated local PostgreSQL socket only."""
import os
from pathlib import Path
import subprocess
import sys
import uuid

root = Path(__file__).resolve().parents[2]
cluster = Path.home() / '.local/share/hrm-isolated-check'
psql = cluster / 'pg/usr/lib/postgresql/18/bin/psql'
env = os.environ.copy()
env['LD_LIBRARY_PATH'] = str(cluster / 'pg/usr/lib/x86_64-linux-gnu')
db = 'hrm_requests_' + uuid.uuid4().hex[:12]
base = [str(psql), '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-h', str(cluster), '-p', '55448', '-U', os.environ.get('USER', 'bryan')]

def sql(source, database=db, success=True, expected=None):
    result = subprocess.run(base + ['-d', database], input='\\set VERBOSITY verbose\n' + source, text=True, capture_output=True, env=env)
    if (result.returncode == 0) != success or (expected and expected not in result.stderr):
        raise RuntimeError(f'Unexpected PostgreSQL result: {result.returncode}\n{result.stderr}')
    return result.stdout

def migration(name, settings=''):
    return settings + '\n' + (root / 'src/OvertimeHotel.HRM.Data/migrations' / name).read_text()

sql(f'CREATE DATABASE {db};', database='postgres')
print('Dedicated fixture database:', db, flush=True)
try:
    sql('''
CREATE TABLE don_tu (id int PRIMARY KEY, trang_thai text NOT NULL, CONSTRAINT old_leave_state CHECK (trang_thai IN ('CHO_DUYET','DA_DUYET','TU_CHOI')));
CREATE TABLE giai_trinh_cham_cong (id int PRIMARY KEY, trang_thai text NOT NULL, CONSTRAINT old_exp_state CHECK (trang_thai IN ('CHO_QUAN_LY','CHO_HR','DA_DUYET','TU_CHOI')), CONSTRAINT keep_exp_check CHECK (id > 0));
CREATE TABLE yeu_cau_doi_ca (id int PRIMARY KEY, trang_thai text NOT NULL DEFAULT 'CHO_DUYET', phan_hoi_duyet text, CONSTRAINT old_swap_state CHECK (trang_thai IN ('CHO_DUYET','DA_DUYET','TU_CHOI','DA_HUY')), CONSTRAINT old_reject_reason CHECK (trang_thai <> 'TU_CHOI' OR length(trim(phan_hoi_duyet)) > 0));
INSERT INTO yeu_cau_doi_ca VALUES (1,'TU_CHOI','lý do cũ');
''')
    sql(migration('20261031_don_tu_allow_da_huy.sql'), success=False, expected='hrm.status_constraint')
    print('PASS migration requires an inventoried constraint')
    sql(migration('20261031_don_tu_allow_da_huy.sql', "SET hrm.status_constraint='old_leave_state';"))
    sql(migration('20261101_giai_trinh_cham_cong_allow_da_huy.sql', "SET hrm.status_constraint='old_exp_state';"))
    sql("INSERT INTO don_tu VALUES (1,'DA_HUY'); INSERT INTO giai_trinh_cham_cong VALUES (1,'DA_HUY');")
    sql("INSERT INTO giai_trinh_cham_cong VALUES (-1,'DA_HUY');", success=False, expected='23514')
    print('PASS migrations 1 and 2 accept withdrawal and retain unrelated CHECK')
    sql(migration('20261031_don_tu_allow_da_huy.rollback.sql'), success=False, expected='23514')
    sql(migration('20261101_giai_trinh_cham_cong_allow_da_huy.rollback.sql'), success=False, expected='23514')
    sql('DELETE FROM don_tu; DELETE FROM giai_trinh_cham_cong;')
    sql(migration('20261031_don_tu_allow_da_huy.rollback.sql'))
    sql(migration('20261101_giai_trinh_cham_cong_allow_da_huy.rollback.sql'))
    sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.giai_trinh_cham_cong'::regclass AND conname='giai_trinh_cham_cong_trang_thai_check') THEN RAISE EXCEPTION 'old explanation CHECK name was not restored'; END IF; END $$;")
    print('PASS leave and explanation rollback reject incompatible data and restore inventoried constraints')
    sql(migration('20261031_don_tu_allow_da_huy.sql', "SET hrm.status_constraint='don_tu_trang_thai_check';"))
    sql(migration('20261101_giai_trinh_cham_cong_allow_da_huy.sql', "SET hrm.status_constraint='giai_trinh_cham_cong_trang_thai_check';"))

    settings = "SET hrm.status_constraint='old_swap_state'; SET hrm.rejection_constraint='old_reject_reason';"
    sql(migration('20261102_yeu_cau_doi_ca_receiver_flow.sql', settings), success=False, expected='23514')
    sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM yeu_cau_doi_ca WHERE id=1 AND phan_hoi_duyet='lý do cũ') OR EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='yeu_cau_doi_ca' AND column_name='ben_tu_choi') THEN RAISE EXCEPTION 'historical migration rollback failed'; END IF; END $$;")
    print('PASS TC-14 historical refusal stops migration and rolls back schema and row')
    sql('DELETE FROM yeu_cau_doi_ca WHERE id=1;')
    sql(migration('20261102_yeu_cau_doi_ca_receiver_flow.sql', settings))
    sql("INSERT INTO yeu_cau_doi_ca (id) VALUES (2);")
    sql("DO $$ BEGIN IF (SELECT trang_thai FROM yeu_cau_doi_ca WHERE id=2) <> 'CHO_NGUOI_NHAN' THEN RAISE EXCEPTION 'wrong default'; END IF; END $$;")
    bad = [
        "(3,'TU_CHOI','NGUOI_NHAN',NULL,NULL)",
        "(4,'TU_CHOI','NGUOI_NHAN','',NULL)",
        "(5,'TU_CHOI','NGUOI_NHAN','   ',NULL)",
        "(6,'TU_CHOI','QUAN_LY','B agreed',NULL)",
        "(7,'TU_CHOI',NULL,'B note','manager note')",
        "(8,'TU_CHOI','INVALID','reason','reason')",
    ]
    for row in bad:
        sql('INSERT INTO yeu_cau_doi_ca (id,trang_thai,ben_tu_choi,phan_hoi_nguoi_nhan,phan_hoi_duyet) VALUES ' + row + ';', success=False, expected='23514')
    sql("INSERT INTO yeu_cau_doi_ca (id,trang_thai,ben_tu_choi,phan_hoi_nguoi_nhan,phan_hoi_duyet) VALUES (9,'TU_CHOI','NGUOI_NHAN','Không đồng ý',NULL),(10,'TU_CHOI','QUAN_LY',NULL,'Không phù hợp');")
    print('PASS TC-12/13 rejection CHECK rejects NULL/blank/wrong party and accepts both valid parties')
    sql(migration('20261102_yeu_cau_doi_ca_receiver_flow.rollback.sql'), success=False, expected='Dừng rollback')
    sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM yeu_cau_doi_ca WHERE id=9 AND phan_hoi_nguoi_nhan='Không đồng ý') THEN RAISE EXCEPTION 'rollback lost history'; END IF; END $$;")
    print('PASS rollback guard preserves receiver history')
    sql('TRUNCATE yeu_cau_doi_ca;')
    sql(migration('20261102_yeu_cau_doi_ca_receiver_flow.rollback.sql'))
    sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.yeu_cau_doi_ca'::regclass AND conname='yeu_cau_doi_ca_trang_thai_check') OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public.yeu_cau_doi_ca'::regclass AND conname='chk_doi_ca_tu_choi_can_ly_do') THEN RAISE EXCEPTION 'original constraints not restored'; END IF; END $$;")
    print('PASS rollback restores inventoried old constraints when data is compatible')
    sql(migration('20261102_yeu_cau_doi_ca_receiver_flow.sql', "SET hrm.status_constraint='yeu_cau_doi_ca_trang_thai_check'; SET hrm.rejection_constraint='chk_doi_ca_tu_choi_can_ly_do';"))

    sql("""
CREATE ROLE anon NOLOGIN; CREATE ROLE authenticated NOLOGIN;
ALTER TABLE don_tu ADD COLUMN ma_nhan_vien int NOT NULL DEFAULT 1, ADD COLUMN ma_nguoi_duyet int, ADD COLUMN ngay_duyet timestamptz, ADD COLUMN phan_hoi_duyet text;
CREATE FUNCTION nhan_vien_hien_tai() RETURNS int LANGUAGE sql AS 'SELECT 1';
ALTER TABLE don_tu ENABLE ROW LEVEL SECURITY;
CREATE POLICY nop_don_tu_cua_toi ON don_tu FOR INSERT TO authenticated WITH CHECK (ma_nhan_vien = nhan_vien_hien_tai());
GRANT ALL ON don_tu, giai_trinh_cham_cong, yeu_cau_doi_ca TO anon, authenticated;
""")
    sql('GRANT UPDATE(trang_thai) ON public.yeu_cau_doi_ca TO authenticated;')
    sql("CREATE POLICY extra_insert ON public.don_tu FOR INSERT TO authenticated WITH CHECK (true);")
    sql(migration('20261103_requests_api_write_boundary.sql'), success=False, expected='additional permissive leave INSERT policy')
    sql('DROP POLICY extra_insert ON public.don_tu;')
    sql(migration('20261103_requests_api_write_boundary.sql'))
    sql("SET ROLE authenticated; INSERT INTO public.don_tu (id,trang_thai,ma_nhan_vien) VALUES (44,'DA_DUYET',1);", success=False, expected='42501')
    sql("SET ROLE authenticated; INSERT INTO public.don_tu (id,trang_thai,ma_nhan_vien) VALUES (45,'CHO_DUYET',1);")
    sql("SET ROLE authenticated; UPDATE public.yeu_cau_doi_ca SET trang_thai='DA_DUYET' WHERE id=2;", success=False, expected='42501')
    sql("SET ROLE authenticated; INSERT INTO public.giai_trinh_cham_cong VALUES (2,'CHO_QUAN_LY');", success=False, expected='42501')
    # Confirm both table and column privileges after the migration.
    sql("DO $$ BEGIN IF has_table_privilege('authenticated','public.yeu_cau_doi_ca','UPDATE') THEN RAISE EXCEPTION 'direct swap UPDATE still allowed'; END IF; END $$;")
    sql("DO $$ BEGIN IF has_table_privilege('anon','public.giai_trinh_cham_cong','TRUNCATE') OR has_any_column_privilege('authenticated','public.giai_trinh_cham_cong','INSERT,UPDATE') OR NOT has_table_privilege('authenticated','public.giai_trinh_cham_cong','SELECT') OR NOT has_table_privilege('authenticated','public.don_tu','INSERT,UPDATE') THEN RAISE EXCEPTION 'wrong privilege boundary'; END IF; END $$;")
    print('PASS direct workflow writes blocked; authenticated reads and leave writes retained')

finally:
    sql(f'DROP DATABASE {db} WITH (FORCE); DROP ROLE IF EXISTS anon; DROP ROLE IF EXISTS authenticated;', database='postgres')
    print('Removed dedicated fixture database')
