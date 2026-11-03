"""Run after fixture.sql against an isolated DB named hrm_swap_test_* only.
Usage: python3 concurrency.py /path/to/psql -h /tmp -p 15439 -d hrm_swap_test_final
"""
import subprocess
import sys
import time

args = sys.argv[1:]

def query(sql):
    return subprocess.run(args + ['-X', '-v', 'ON_ERROR_STOP=1', '-At', '-c', sql], capture_output=True, text=True)

check = query('select current_database()')
assert check.returncode == 0 and check.stdout.strip().startswith('hrm_swap_test_'), 'Isolated fixture DB required'
sql = """
BEGIN;
SELECT set_config('test.uid','00000000-0000-0000-0000-000000000001',true),set_config('test.employee','1',true);
SET LOCAL ROLE authenticated;
SELECT public.hrm_shift_swap('create','{"senderShift":1,"receiverShift":2,"reason":"race"}');
SELECT pg_sleep(0.5);
COMMIT;
"""
first = subprocess.Popen(args + ['-X', '-v', 'ON_ERROR_STOP=1', '-c', sql], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
time.sleep(0.1)
second = query(sql)
first.communicate(timeout=15)
assert sorted([first.returncode, second.returncode]) == [0, 1], 'Exactly one concurrent create must succeed'
count = query("select count(*) from yeu_cau_doi_ca where trang_thai in ('CHO_NGUOI_NHAN','CHO_DUYET')")
assert count.returncode == 0 and count.stdout.strip() == '1', 'No duplicate open requests'
print('PASS: two concurrent clients produced exactly one open request')
