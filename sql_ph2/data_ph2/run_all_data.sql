-- ============================================================================
-- RUN TAT CA FILE DATA THEO DUNG THU TU KHOA NGOAI
-- ============================================================================

PROMPT === Start loading KHOA/NHANVIEN/THONGBAO ===
@insert_khoa.sql
@insert_nhanvien.sql
@insert_thongbao.sql

PROMPT === Start loading BENHNHAN (10 parts) ===
@insert_benhnhan_part_01.sql
@insert_benhnhan_part_02.sql
@insert_benhnhan_part_03.sql
@insert_benhnhan_part_04.sql
@insert_benhnhan_part_05.sql
@insert_benhnhan_part_06.sql
@insert_benhnhan_part_07.sql
@insert_benhnhan_part_08.sql
@insert_benhnhan_part_09.sql
@insert_benhnhan_part_10.sql

PROMPT === Start loading HSBA (10 parts) ===
@insert_hsba_part_01.sql
@insert_hsba_part_02.sql
@insert_hsba_part_03.sql
@insert_hsba_part_04.sql
@insert_hsba_part_05.sql
@insert_hsba_part_06.sql
@insert_hsba_part_07.sql
@insert_hsba_part_08.sql
@insert_hsba_part_09.sql
@insert_hsba_part_10.sql

PROMPT === Start loading HSBA_DV (10 parts) ===
@insert_hsba_dv_part_01.sql
@insert_hsba_dv_part_02.sql
@insert_hsba_dv_part_03.sql
@insert_hsba_dv_part_04.sql
@insert_hsba_dv_part_05.sql
@insert_hsba_dv_part_06.sql
@insert_hsba_dv_part_07.sql
@insert_hsba_dv_part_08.sql
@insert_hsba_dv_part_09.sql
@insert_hsba_dv_part_10.sql

PROMPT === Start loading DONTHUOC (10 parts) ===
@insert_donthuoc_part_01.sql
@insert_donthuoc_part_02.sql
@insert_donthuoc_part_03.sql
@insert_donthuoc_part_04.sql
@insert_donthuoc_part_05.sql
@insert_donthuoc_part_06.sql
@insert_donthuoc_part_07.sql
@insert_donthuoc_part_08.sql
@insert_donthuoc_part_09.sql
@insert_donthuoc_part_10.sql

PROMPT === Done ===
COMMIT;
