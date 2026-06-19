-- ============================================================
-- 07.sql - Yêu cầu 4: Sao lưu và phục hồi bằng Oracle Data Pump
-- ============================================================
-- Lưu ý:
--   1. Phần tạo directory object phải chạy bằng SYSDBA.
--   2. Phần tạo bảng lịch sử và kiểm tra dữ liệu chạy bằng QLBV.
--   3. Lệnh expdp/impdp không chạy trong SQL Developer; sao chép sang CMD/PowerShell.
--   4. Thư mục C:\oracle_backup phải tồn tại trên máy chạy Oracle Database.
-- ============================================================

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

-- Kiểm tra directory object và quyền vừa cấp.
SELECT directory_name, directory_path
FROM   dba_directories
WHERE  directory_name = 'BACKUP_DIR';

SELECT grantee, privilege
FROM   dba_tab_privs
WHERE  table_name = 'BACKUP_DIR'
ORDER  BY grantee, privilege;