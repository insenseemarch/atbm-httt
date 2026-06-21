-- ============================================================
-- 07.sql - Yêu cầu 4: Sao lưu và phục hồi bằng Oracle Data Pump
-- ============================================================
-- Lưu ý: Phần tạo directory object phải chạy bằng SYSDBA.
-- ============================================================

SET SERVEROUTPUT ON
SET LINESIZE 260
SET PAGESIZE 100


-- ============================================================
-- [07-SYSDBA-01] Kết nối: SYSDBA @ XEPDB1
-- Mục đích: Tạo Oracle Directory object cho Data Pump.
-- ============================================================

-- Tạo directory object trỏ đến thư mục vật lý lưu file .dmp và .log.
-- Ghi vào ổ C để Oracle có quyền ghi mặc định; tạo folder ngoài trước rồi điền PATH vào đây.
CREATE OR REPLACE DIRECTORY backup_dir AS 'C:\oracle_backup';

-- Cấp quyền READ/WRITE cho QLBV (export/import) và SYSTEM (import phục hồi).
GRANT READ, WRITE ON DIRECTORY backup_dir TO qlbv;
GRANT READ, WRITE ON DIRECTORY backup_dir TO system;

-- Cấp quyền JOB cho QLBV
GRANT CREATE JOB TO qlbv;
GRANT EXECUTE ON sys.dbms_scheduler TO qlbv;

-- Kiểm tra directory object và quyền vừa cấp.
SELECT directory_name, directory_path
FROM   dba_directories
WHERE  directory_name = 'BACKUP_DIR';

SELECT grantee, privilege
FROM   dba_tab_privs
WHERE  table_name = 'BACKUP_DIR'
ORDER  BY grantee, privilege;


-- ============================================================
-- [07-CMD] Lệnh expdp / impdp - chạy trong CMD hoặc PowerShell
-- Kết nối: qlbv/123@localhost:1521/xepdb1
-- ============================================================

/*
rem --- SAO LƯU CHỦ ĐỘNG ---

rem 1. Backup toàn bộ schema QLBV:
expdp qlbv/123@localhost:1521/xepdb1 ^
    schemas=qlbv ^
    directory=backup_dir ^
    dumpfile=qlbv_schema_%date:~-4%%date:~4,2%%date:~7,2%.dmp ^
    logfile=qlbv_schema_export.log

rem 2. Backup các bảng quan trọng:
expdp qlbv/123@localhost:1521/xepdb1 ^
    tables=qlbv.benhnhan,qlbv.hsba,qlbv.hsba_dv,qlbv.donthuoc ^
    directory=backup_dir ^
    dumpfile=qlbv_important_tables.dmp ^
    logfile=qlbv_important_tables_export.log

rem --- PHỤC HỒI DỮ LIỆU ---
rem Thay <ten_file_dump> bằng tên file .dmp tương ứng (theo ngày tháng nếu dùng %date%).

rem 3. Restore toàn schema QLBV (table_exists_action=replace sẽ ghi đè dữ liệu hiện tại):
impdp qlbv/123@localhost:1521/xepdb1 ^
    schemas=qlbv ^
    directory=backup_dir ^
    dumpfile=<ten_file_dump>.dmp ^
    logfile=qlbv_schema_import.log ^
    table_exists_action=replace

rem 4. Restore một bảng cụ thể (xác định từ audit log ở [07-AUDIT-01]):
impdp qlbv/123@localhost:1521/xepdb1 ^
    tables=qlbv.donthuoc ^
    directory=backup_dir ^
    dumpfile=<ten_file_dump>.dmp ^
    logfile=qlbv_table_import.log ^
    table_exists_action=replace
<<<<<<< Updated upstream
*/
=======
*/
>>>>>>> Stashed changes
