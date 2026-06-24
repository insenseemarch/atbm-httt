-- Hướng dẫn khắc phục restore toàn schema (thực hiện bởi DBA / SYSDBA)
-- Mục đích: cấp các quyền hệ thống cần thiết và chạy lại import chỉ phần metadata nếu cần
-- Lưu ý: Thực hiện các lệnh dưới đây bằng tài khoản SYS (hoặc user có quyền tương đương)

/* Bước 1: Kết nối với Oracle dưới quyền SYSDBA */
-- Từ máy chủ hoặc client có sqlplus:
-- > sqlplus / as sysdba

/* Bước 2: Chạy các GRANT cần quyền cao (SYS/SYSDBA) */
GRANT EXECUTE ON SYS.DBMS_RLS TO QLBV;
GRANT EXECUTE ON SYS.DBMS_FGA TO QLBV;
GRANT EXECUTE ON SYS.DBMS_SCHEDULER TO QLBV;

-- (Tùy chọn) nếu cần cấp thêm:
-- GRANT UNLIMITED TABLESPACE TO QLBV;
-- GRANT SELECT ANY DICTIONARY TO QLBV; -- nếu chưa có

/* Bước 3: Kiểm tra file log import trước đó để biết lỗi chi tiết */
-- Mở file log trên Windows PowerShell:
-- > Get-Content -Path C:\oracle_backup\qlbv_schema_import.log -Tail 200

/* Bước 4: Chạy lại impdp để áp dụng chỉ phần metadata (policies, grants, job definitions, v.v.)
   CONTENT=METADATA_ONLY sẽ chỉ import metadata, không chạm tới dữ liệu. Nếu bạn muốn tái tạo
   hoàn toàn, dùng schemas=qlbv như bình thường (cẩn trọng). */
-- Ví dụ (chạy từ command prompt trên máy có Oracle client):
-- > impdp app_admin/your_password@localhost:1521/XEPDB1 schemas=qlbv \
--     directory=backup_dir dumpfile=qlbv_schema_20260624_2024.dmp \
--     logfile=qlbv_schema_import_fix.log content=METADATA_ONLY table_exists_action=replace

/* Bước 5: Kiểm tra logfile kết quả */
-- > Get-Content -Path C:\oracle_backup\qlbv_schema_import_fix.log -Tail 200

/* Ghi chú thêm:
 - Nếu bạn muốn impdp tạo user/schema từ dump thì KHÔNG nên tạo sẵn user QLBV trước khi impdp.
   Hoặc nếu app đã tạo user rồi, impdp sẽ báo USER already exists (ORA-31684) nhưng vẫn có
   thể import dữ liệu.  
 - Nếu import vẫn báo lỗi do thiếu privilege, cần lặp lại Bước 2 với SYS để đảm bảo các
   package/system object được cấp quyền.
 - Đây là thao tác DBA — nếu bạn không có SYS quyền, gửi file này cho DBA kèm đường dẫn dump.
*/
