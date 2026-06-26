-- ============================================================================
-- 08.sql - Yêu cầu 4: Lập lịch Sao lưu Tự động hằng ngày & Phục hồi Flashback
-- ============================================================================
-- Lưu ý:
--   1. Phần cấp quyền hệ thống quản trị Scheduler phải chạy bằng SYSDBA.
--   2. Phần khởi tạo bảng lịch sử, procedure gạt log và Scheduler chạy bằng QLBV.
--   3. Tiến trình Flashback Table cần quyền quản trị hoặc Flashback trên bảng.
-- ============================================================================

SET SERVEROUTPUT ON
SET LINESIZE 260
SET PAGESIZE 100

-- ============================================================================
-- [08-QLBV-01] Kết nối: QLBV @ XEPDB1
-- Mục đích: Tạo bảng lịch sử sao lưu, phục hồi và bảng đóng gói cục bộ 
--           nhật ký kiểm toán phục vụ việc lưu trữ tự động lâu dài.
-- ============================================================================
-- === [QLBV] Initializing Audit Backup History Tables ===

BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE qlbv.restore_history CASCADE CONSTRAINTS';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE qlbv.backup_history CASCADE CONSTRAINTS';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

-- 1. Tạo bảng lưu lịch sử sao lưu hệ thống
CREATE TABLE qlbv.backup_history (
    backup_id        NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    backup_time      TIMESTAMP DEFAULT SYSTIMESTAMP,
    backup_type      VARCHAR2(50),  -- 'FULL', 'SCHEMA_QLBV', 'AUDIT_LOG_AUTO'
    file_name        VARCHAR2(255),
    status           VARCHAR2(50),  -- 'SUCCESS', 'FAILED'
    description      NVARCHAR2(500)
);

-- 2. Tạo bảng lưu lịch sử phục hồi hệ thống
CREATE TABLE qlbv.restore_history (
    restore_id       NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    restore_time     TIMESTAMP DEFAULT SYSTIMESTAMP,
    restore_type     VARCHAR2(50),
    file_src         VARCHAR2(255),
    executed_by      VARCHAR2(100) DEFAULT USER,
    status           VARCHAR2(50)
);

-- 3. Tạo bảng lưu trữ nhật ký kiểm toán đóng gói hằng ngày (Đồng bộ cấu trúc từ 06.sql)
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE qlbv.audit_archive_log CASCADE CONSTRAINTS';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

CREATE TABLE qlbv.audit_archive_log (
    archive_id       NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    archive_date     DATE DEFAULT SYSDATE,
    event_timestamp  TIMESTAMP,
    dbusername       VARCHAR2(128),
    action_name      VARCHAR2(64),
    object_schema    VARCHAR2(128),
    object_name      VARCHAR2(128),
    return_code      NUMBER,
    unified_audit_policies VARCHAR2(4000),
    fga_policy_name  VARCHAR2(256),
    sql_text         CLOB
);


-- ============================================================================
-- [08-QLBV-02] Kết nối: QLBV @ XEPDB1
-- Mục đích: Tạo thủ tục định kỳ trích xuất dữ liệu từ UNIFIED_AUDIT_TRAIL hệ thống
--           vào bảng lưu trữ cục bộ bảo trì an toàn.
-- ============================================================================
-- === [QLBV] Creating Stored Procedure for Automated Audit Archiving ===

CREATE OR REPLACE PROCEDURE qlbv.pr_auto_archive_audit_log
AS
    v_row_count NUMBER := 0;
    v_desc      NVARCHAR2(500); -- Khai báo biến trung gian để hứng chuỗi description
BEGIN
    -- 1. Trích xuất Standard Audit từ DBA_AUDIT_TRAIL
     INSERT INTO qlbv.audit_archive_log (
        event_timestamp, 
        dbusername, 
        action_name, 
        object_schema, 
        object_name, 
        return_code, 
        unified_audit_policies, 
        fga_policy_name, 
        sql_text
    )
    SELECT timestamp, 
           username, 
           action_name, 
           owner, 
           obj_name, 
           returncode, 
           'Standard Audit', 
           NULL, 
           TO_CLOB(comment_text)
    FROM   dba_audit_trail
    WHERE  owner = 'QLBV'
      AND  timestamp > (SELECT NVL(MAX(event_timestamp), TO_TIMESTAMP('2000-01-01', 'YYYY-MM-DD')) FROM qlbv.audit_archive_log WHERE unified_audit_policies = 'Standard Audit');

    v_row_count := SQL%ROWCOUNT;

    -- 2. Trích xuất FGA và Logon Failures từ UNIFIED_AUDIT_TRAIL
    INSERT INTO qlbv.audit_archive_log (
        event_timestamp, 
        dbusername, 
        action_name, 
        object_schema, 
        object_name, 
        return_code, 
        unified_audit_policies, 
        fga_policy_name, 
        sql_text
    )
    SELECT event_timestamp, 
           dbusername, 
           action_name, 
           object_schema, 
           object_name, 
           return_code, 
           unified_audit_policies, 
           fga_policy_name, 
           sql_text
    FROM   unified_audit_trail
    WHERE  ((fga_policy_name IN ('AUDITSUADONTHUOC', 'AUDITBSUPDATEHSBA_HOPPHAP'))
            OR (action_name = 'LOGON' AND return_code <> 0))
      AND  event_timestamp > (SELECT NVL(MAX(event_timestamp), TO_TIMESTAMP('2000-01-01', 'YYYY-MM-DD')) FROM qlbv.audit_archive_log WHERE unified_audit_policies <> 'Standard Audit');

    v_row_count := v_row_count + SQL%ROWCOUNT;
    COMMIT;

    -- Gán chuỗi thông báo thành công vào biến trước khi INSERT
    v_desc := N'Hệ thống tự động đóng gói dữ liệu và lưu trữ thành công ' || TO_NCHAR(v_row_count) || N' bản ghi.';

    -- Ghi nhận lịch sử sao lưu tự động thành công vào bảng
    INSERT INTO qlbv.backup_history (backup_type, file_name, status, description)
    VALUES ('AUDIT_LOG_AUTO', 'audit_archive_log', 'SUCCESS', v_desc);
    COMMIT;

EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        -- Gán chuỗi thông báo thất bại kèm mã lỗi hệ thống vào biến trước khi INSERT
        v_desc := SUBSTR(N'Lỗi tự động đóng gói dữ liệu kiểm toán: ' || TO_NCHAR(SQLERRM), 1, 500);
        
        INSERT INTO qlbv.backup_history (backup_type, file_name, status, description)
        VALUES ('AUDIT_LOG_AUTO', 'audit_archive_log', 'FAILED', v_desc);
        COMMIT;
END;
/


-- ============================================================================
-- [08-QLBV-03] Kết nối: QLBV @ XEPDB1
-- Mục đích: Tạo Job tự động gọi thủ tục gạt log chạy định kỳ hằng ngày lúc 23:00.
-- ============================================================================
-- === [QLBV] Creating Oracle Scheduler Job for Daily Archiving ===

-- Xóa Job lập lịch cũ nếu tồn tại trước khi khởi tạo lại để tránh xung đột
BEGIN
    DBMS_SCHEDULER.DROP_JOB(job_name => 'QLBV.JOB_DAILY_ARCHIVE_AUDIT', force => TRUE);
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

-- Cấu hình Job chạy tự động đều đặn hằng ngày vào lúc 23:00 đêm
BEGIN
    DBMS_SCHEDULER.CREATE_JOB (
        job_name        => 'QLBV.JOB_DAILY_ARCHIVE_AUDIT',
        job_type        => 'STORED_PROCEDURE',
        job_action      => 'QLBV.PR_AUTO_ARCHIVE_AUDIT_LOG',
        start_date      => SYSTIMESTAMP,
        repeat_interval => 'FREQ=DAILY;BYHOUR=23;BYMINUTE=0;BYSECOND=0',
        enabled         => TRUE,
        comments        => 'Tự động gạt dữ liệu nhật ký kiểm toán đồng bộ từ file 06.sql vào bảng archive hằng ngày lúc 23:00'
    );
END;
/

-- Kiểm tra xác nhận trạng thái kích hoạt của Scheduler Job trên hệ thống
SELECT owner, job_name, enabled, repeat_interval, state
FROM   all_scheduler_jobs
WHERE  owner    = 'QLBV'
  AND  job_name = 'JOB_DAILY_ARCHIVE_AUDIT';


-- ============================================================================
-- [08-FLASHBACK] PHỤC HỒI DỮ LIỆU NGHIỆP VỤ BẰNG CƠ CHẾ FLASHBACK TABLE
-- CONNECTION: QLBV
-- Mục đích: Vận dụng công nghệ Flashback để khôi phục dữ liệu về trạng thái trước 
--           khi xảy ra sai sót nghiệp vụ mà không cần khôi phục vật lý.
-- ============================================================================
-- === Preparing and Demonstrating Flashback Table Feature ===

-- Ghi lại mốc thời gian an toàn hiện tại trước khi xảy ra sự cố lỗi mẫu
SELECT TO_CHAR(SYSTIMESTAMP, 'yyyy-mm-dd hh24:mi:ss') AS BEFORE_INCIDENT_TIME FROM dual;

-- Xem trước một dòng dữ liệu của bảng ĐƠNTHUỐC để làm mẫu đối chứng
SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM qlbv.donthuoc WHERE ROWNUM = 1;

-- Kích hoạt tính năng chuyển dịch dòng dữ liệu (Bắt buộc phải có để chạy Flashback)
ALTER TABLE qlbv.donthuoc ENABLE ROW MOVEMENT;

/* -- KỊCH BẢN MÔ PHỎNG SỰ CỐ PHÁ HOẠI / SỬA NHẦM DỮ LIỆU BẰNG TAY:
UPDATE qlbv.donthuoc
SET    lieudung = N'Dữ liệu bị sửa nhầm/phá hoại trong kịch bản demo Flashback'
WHERE  ROWNUM = 1;
COMMIT;

-- Kiểm tra dữ liệu sau khi bị sửa lỗi (Dữ liệu đã bị sai lệch hoàn toàn)
SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM qlbv.donthuoc WHERE ROWNUM = 1;

-- LỆNH PHỤC HỒI: Đưa bảng dữ liệu trở ngược lại thời gian an toàn trước sự cố
-- Thay thế chuỗi <yyyy-mm-dd hh24:mi:ss> bằng giá trị trả về của BEFORE_INCIDENT_TIME ở trên
FLASHBACK TABLE qlbv.donthuoc
TO TIMESTAMP TO_TIMESTAMP('2026-06-21 02:20:19', 'yyyy-mm-dd hh24:mi:ss');

-- Xác minh dữ liệu sau khi Flashback thành công (Dữ liệu gốc đã quay trở lại hoàn hảo)
SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM qlbv.donthuoc WHERE ROWNUM = 1;
*/