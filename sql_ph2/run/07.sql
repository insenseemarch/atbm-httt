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
*/

-- Sau khi chạy expdp thành công, ghi lịch sử backup thủ công (chạy bằng QLBV):
/*
INSERT INTO qlbv.backup_history (backup_name, backup_type, backup_path, object_scope, note)
VALUES (
    'qlbv_important_tables.dmp',
    'tables',
    'C:\oracle_backup',
    'benhnhan,hsba,hsba_dv,donthuoc',
    N'Backup các bảng quan trọng bằng expdp'
);
COMMIT;
*/


-- ============================================================
-- [07-AUTO-BACKUP] Kết nối: QLBV @ XEPDB1
-- Mục đích: Backup tự động hằng ngày bằng DBMS_SCHEDULER + DBMS_DATAPUMP.
--
-- Nếu QLBV chưa có quyền, SYSDBA chạy trước:
--   GRANT CREATE JOB                  TO qlbv;
--   GRANT EXECUTE ON sys.dbms_datapump  TO qlbv;
--   GRANT EXECUTE ON sys.dbms_scheduler TO qlbv;
-- ============================================================

CREATE OR REPLACE PROCEDURE qlbv.pr_auto_backup_schema
AUTHID DEFINER
AS
    v_handle    NUMBER;
    v_dump_name VARCHAR2(255);
    v_log_name  VARCHAR2(255);
BEGIN
    v_dump_name := 'qlbv_schema_auto_' || TO_CHAR(SYSDATE, 'yyyymmdd_hh24mi') || '.dmp';
    v_log_name  := 'qlbv_schema_auto_' || TO_CHAR(SYSDATE, 'yyyymmdd_hh24mi') || '.log';

    v_handle := DBMS_DATAPUMP.OPEN(
        operation => 'EXPORT',
        job_mode  => 'SCHEMA',
        job_name  => 'QLBV_AUTO_EXPORT_' || TO_CHAR(SYSDATE, 'yyyymmddhh24mi')
    );

    DBMS_DATAPUMP.ADD_FILE(
        handle    => v_handle,
        filename  => v_dump_name,
        directory => 'BACKUP_DIR',
        filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE
    );

    DBMS_DATAPUMP.ADD_FILE(
        handle    => v_handle,
        filename  => v_log_name,
        directory => 'BACKUP_DIR',
        filetype  => DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE
    );

    DBMS_DATAPUMP.METADATA_FILTER(
        handle => v_handle,
        name   => 'SCHEMA_EXPR',
        value  => '= ''QLBV'''
    );

    DBMS_DATAPUMP.START_JOB(v_handle);
    DBMS_DATAPUMP.DETACH(v_handle);

    INSERT INTO qlbv.backup_history (backup_name, backup_type, backup_path, object_scope, note)
    VALUES (
        v_dump_name,
        'auto',
        'C:\oracle_backup',
        'qlbv',
        N'Backup tự động schema QLBV bằng DBMS_SCHEDULER + DBMS_DATAPUMP'
    );
    COMMIT;

EXCEPTION
    WHEN OTHERS THEN
        BEGIN
            IF v_handle IS NOT NULL THEN
                DBMS_DATAPUMP.DETACH(v_handle);
            END IF;
        EXCEPTION
            WHEN OTHERS THEN NULL;
        END;
        RAISE;
END;
/

-- Xóa job cũ nếu tồn tại trước khi tạo lại.
BEGIN
    DBMS_SCHEDULER.DROP_JOB(job_name => 'QLBV.JOB_AUTO_BACKUP_SCHEMA', force => TRUE);
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -27475 THEN RAISE; END IF;
END;
/

-- Tạo job chạy hằng ngày lúc 23:30.
BEGIN
    DBMS_SCHEDULER.CREATE_JOB(
        job_name        => 'QLBV.JOB_AUTO_BACKUP_SCHEMA',
        job_type        => 'STORED_PROCEDURE',
        job_action      => 'QLBV.PR_AUTO_BACKUP_SCHEMA',
        start_date      => SYSTIMESTAMP,
        repeat_interval => 'FREQ=DAILY;BYHOUR=23;BYMINUTE=30;BYSECOND=0',
        enabled         => TRUE,
        comments        => 'Tự động backup schema QLBV hằng ngày lúc 23:30 vào C:\oracle_backup'
    );
END;
/

-- Kiểm tra job đã được tạo.
SELECT owner, job_name, enabled, repeat_interval, state
FROM   all_scheduler_jobs
WHERE  owner    = 'QLBV'
  AND  job_name = 'JOB_AUTO_BACKUP_SCHEMA';


-- ============================================================
-- [07-AUDIT-01] Kết nối: SYSDBA hoặc QLBV (cần SELECT ANY DICTIONARY)
-- Mục đích: Đọc audit log từ 06.sql để xác định sự cố trước khi restore.
-- return_code = 0: thao tác thành công; <> 0: thất bại.
-- ============================================================

-- Các thao tác trên bảng quan trọng của schema QLBV (100 bản ghi gần nhất).
SELECT
    event_timestamp,
    dbusername,
    action_name,
    object_schema,
    object_name,
    return_code,
    unified_audit_policies,
    fga_policy_name,
    sql_text
FROM   unified_audit_trail
WHERE  object_schema = 'QLBV'
  AND  object_name IN ('BENHNHAN','HSBA','HSBA_DV','DONTHUOC','NHANVIEN')
ORDER  BY event_timestamp DESC
FETCH FIRST 100 ROWS ONLY;

-- Log theo từng audit policy đã tạo trong 06.sql.
SELECT
    event_timestamp,
    dbusername,
    action_name,
    object_schema,
    object_name,
    return_code,
    unified_audit_policies,
    fga_policy_name,
    sql_text
FROM   unified_audit_trail
WHERE  unified_audit_policies IN (
           'AUDITSUCDPVUPDATEBN',   'AUDITSUCBSUPDATEDT',
           'AUDITSUCDPVEXECFUNC',
           'AUDITDONTHUOCINSERT',   'AUDITDONTHUOCUPDATE',
           'AUDITFAILBSUPDATENV',   'AUDITFAILBNDELETEHSBA',
           'AUDITFAILKTVDELETEDT',  'AUDITFAILDPVDELETEHSBA',
           'AUDITFAILBNUPDATEDV',   'AUDITFAILKTVSELECTBN',
           'AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV'
       )
   OR fga_policy_name IN (
           'AUDITSUADONTHUOC',
           'AUDITBSUPDATEHSBA',
           'AUDITKTVUPDATEKETQUA'
       )
ORDER  BY event_timestamp DESC
FETCH FIRST 100 ROWS ONLY;

-- FGA trail riêng (nếu môi trường ghi vào dba_fga_audit_trail).
SELECT
    timestamp,
    db_user,
    object_schema,
    object_name,
    policy_name,
    statement_type,
    sql_text
FROM   dba_fga_audit_trail
WHERE  object_schema = 'QLBV'
  AND  policy_name IN (
           'AUDITSUADONTHUOC',
           'AUDITBSUPDATEHSBA',
           'AUDITKTVUPDATEKETQUA'
       )
ORDER  BY timestamp DESC;


-- ============================================================
-- [07-RESTORE-HISTORY] Kết nối: QLBV @ XEPDB1
-- Mục đích: Ghi nhận kết quả restore sau khi impdp hoàn tất.
-- Sửa các giá trị mẫu theo audit log thực tế.
-- ============================================================

/*
INSERT INTO qlbv.restore_history (
    backup_name, restore_object,
    incident_time, incident_user, incident_action, audit_object_name, note
) VALUES (
    '<ten_file_dump>.dmp',
    'qlbv.donthuoc',
    TO_TIMESTAMP('2026-06-17 10:15:00', 'yyyy-mm-dd hh24:mi:ss'),
    'bs0001',
    'UPDATE',
    'DONTHUOC',
    N'Restore bảng DONTHUOC sau khi audit log ghi nhận cập nhật sai'
);
COMMIT;
*/

SELECT * FROM qlbv.restore_history ORDER BY restore_time DESC;


-- ============================================================
-- [07-FLASHBACK-DEMO] Kết nối: QLBV @ XEPDB1
-- Mục đích: Demo Flashback Table như phương án phục hồi bổ sung.
-- Chỉ bỏ comment phần UPDATE/FLASHBACK khi demo trên dữ liệu mẫu.
-- ============================================================

-- Ghi lại mốc thời gian trước thao tác lỗi.
SELECT TO_CHAR(SYSTIMESTAMP, 'yyyy-mm-dd hh24:mi:ss') AS before_incident_time FROM dual;

-- Xem một dòng DONTHUOC để demo.
SELECT * FROM qlbv.donthuoc WHERE ROWNUM = 1;

/*
ALTER TABLE qlbv.donthuoc ENABLE ROW MOVEMENT;

UPDATE qlbv.donthuoc
SET    lieudung = N'Dữ liệu bị sửa nhầm trong demo Flashback'
WHERE  ROWNUM = 1;
COMMIT;

FLASHBACK TABLE qlbv.donthuoc
TO TIMESTAMP TO_TIMESTAMP('<yyyy-mm-dd hh24:mi:ss>', 'yyyy-mm-dd hh24:mi:ss');

SELECT * FROM qlbv.donthuoc WHERE ROWNUM = 1;
*/


-- ============================================================
-- [07-DEMO-FLOW] Kịch bản demo từng bước
-- ============================================================
-- 1. SYSDBA chạy [07-SYSDBA-01]      : tạo BACKUP_DIR.
-- 2. QLBV   chạy [07-QLBV-01]        : tạo backup_history, restore_history.
-- 3. CMD/PowerShell [07-CMD] bước 1-2 : export schema và bảng quan trọng.
-- 4. Tạo sự cố demo (ví dụ: UPDATE sai DONTHUOC.LIEUDUNG).
-- 5. SYSDBA/QLBV đọc [07-AUDIT-01]   : xác định user, thời điểm, object, SQL text.
-- 6. Chọn file dump trước thời điểm sự cố.
-- 7. CMD/PowerShell [07-CMD] bước 3-4 : import/restore bảng bị ảnh hưởng.
-- 8. QLBV   chạy [07-RESTORE-HISTORY] : ghi kết quả vào restore_history.
-- ============================================================
