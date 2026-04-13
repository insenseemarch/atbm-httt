-- ============================================================
-- SCRIPT 2: DATA POPULATION (FINAL - CLEAN)
-- ============================================================

-- ============================================================
-- 1. XÓA DỮ LIỆU CŨ
-- ============================================================
DELETE FROM COLUMN_PRIVILEGE_GRANTS;
DELETE FROM USER_PRIVILEGE_GRANTS;
DELETE FROM ROLE_PRIVILEGE_GRANTS;
DELETE FROM USER_ROLE_GRANTS;
DELETE FROM COLUMNS;
DELETE FROM DB_OBJECTS;
DELETE FROM ROLES;
DELETE FROM USERS;

COMMIT;

-- ============================================================
-- 2. USERS
-- ============================================================
INSERT INTO USERS VALUES 
('ADMIN', STANDARD_HASH('admin@123', 'SHA256'), 'OPEN', SYSDATE, NULL, 'USERS', 'DEFAULT');

INSERT INTO USERS VALUES 
('BACSI', STANDARD_HASH('bacsi@123', 'SHA256'), 'EXPIRED_LOCKED', SYSDATE, NULL, 'USERS', 'PROFILE_STRICT');

INSERT INTO USERS VALUES 
('KTV', STANDARD_HASH('ktv@123', 'SHA256'), 'EXPIRED', SYSDATE, NULL, 'USERS', 'PROFILE_LIMITED');

INSERT INTO USERS VALUES 
('BN', STANDARD_HASH('bn@123', 'SHA256'), 'LOCKED', SYSDATE, NULL, 'USERS', 'PROFILE_EXPIRE');

INSERT INTO USERS VALUES 
('DPV', STANDARD_HASH('dpv123', 'SHA256'), 'OPEN', SYSDATE, NULL, 'USERS', 'DEFAULT');

-- ============================================================
-- 3. ROLES
-- ============================================================
INSERT INTO ROLES VALUES ('ROLE_ADMIN', 'Toàn quyền hệ thống', 'ACTIVE', SYSDATE);
INSERT INTO ROLES VALUES ('ROLE_BACSI', 'Bác sĩ', 'ACTIVE', SYSDATE);
INSERT INTO ROLES VALUES ('ROLE_KTV', 'Kỹ thuật viên', 'INACTIVE', SYSDATE);
INSERT INTO ROLES VALUES ('ROLE_DPV', 'Điều phối viên', 'INACTIVE', SYSDATE);
INSERT INTO ROLES VALUES ('ROLE_BN', 'Bệnh nhân', 'ACTIVE', SYSDATE);

-- ============================================================
-- 4. DB_OBJECTS
-- ============================================================
INSERT INTO DB_OBJECTS (object_name, object_type, owner, status) VALUES ('BENHNHAN', 'TABLE', 'ADMIN', 'VALID');
INSERT INTO DB_OBJECTS (object_name, object_type, owner, status) VALUES ('NHANVIEN', 'TABLE', 'ADMIN', 'VALID');
INSERT INTO DB_OBJECTS (object_name, object_type, owner, status) VALUES ('HSBA', 'TABLE', 'ADMIN', 'INVALID');
INSERT INTO DB_OBJECTS (object_name, object_type, owner, status) VALUES ('HSBA_DV', 'TABLE', 'ADMIN', 'INVALID');
INSERT INTO DB_OBJECTS (object_name, object_type, owner, status) VALUES ('DONTHUOC', 'TABLE', 'ADMIN', 'VALID');

-- ============================================================
-- 5. COLUMNS
-- ============================================================
-- LUONG, PHUCAP thuộc NHANVIEN
INSERT INTO COLUMNS (object_id, column_name, data_type, position)
SELECT object_id, 'LUONG', 'NUMBER', 5
FROM DB_OBJECTS WHERE object_name = 'NHANVIEN';

INSERT INTO COLUMNS (object_id, column_name, data_type, position)
SELECT object_id, 'PHUCAP', 'NUMBER', 6
FROM DB_OBJECTS WHERE object_name = 'NHANVIEN';

-- ============================================================
-- 6. CẤP ROLE CHO USER
-- ============================================================
INSERT INTO USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
VALUES ('ADMIN', 'ROLE_ADMIN', 1, 'ADMIN');

INSERT INTO USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
VALUES ('BACSI', 'ROLE_BACSI', 0, 'ADMIN');

INSERT INTO USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
VALUES ('KTV', 'ROLE_KTV', 0, 'ADMIN');

INSERT INTO USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
VALUES ('DPV', 'ROLE_DPV', 0, 'ADMIN');

INSERT INTO USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
VALUES ('BN', 'ROLE_BN', 0, 'ADMIN');

-- ============================================================
-- 7. CẤP QUYỀN CHO ROLE
-- ============================================================

-- ADMIN full quyền
INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_ADMIN', object_id, 'SELECT', 1, 'ADMIN' FROM DB_OBJECTS;

INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_ADMIN', object_id, 'INSERT', 1, 'ADMIN' FROM DB_OBJECTS;

INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_ADMIN', object_id, 'UPDATE', 1, 'ADMIN' FROM DB_OBJECTS;

INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_ADMIN', object_id, 'DELETE', 1, 'ADMIN' FROM DB_OBJECTS;

-- BÁC SĨ
INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_BACSI', object_id, 'SELECT', 1, 'ADMIN'
FROM DB_OBJECTS WHERE object_name = 'HSBA';

INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_BACSI', object_id, 'UPDATE', 1, 'ADMIN'
FROM DB_OBJECTS WHERE object_name = 'HSBA';

-- KTV
INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_KTV', object_id, 'SELECT', 0, 'ADMIN'
FROM DB_OBJECTS WHERE object_name = 'HSBA_DV';

-- ĐIỀU PHỐI VIÊN
INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_DPV', object_id, 'SELECT', 1, 'ADMIN'
FROM DB_OBJECTS WHERE object_name IN ('BENHNHAN', 'HSBA');

INSERT INTO ROLE_PRIVILEGE_GRANTS
(role_name, object_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE_DPV', object_id, 'INSERT', 1, 'ADMIN'
FROM DB_OBJECTS WHERE object_name = 'HSBA';

-- ============================================================
-- 8. USER PRIVILEGE
-- ============================================================
-- BN chỉ được xem
INSERT INTO USER_PRIVILEGE_GRANTS
(username, object_id, privilege_name, grant_option, granted_by)
SELECT 'BN', object_id, 'SELECT', 0, 'ADMIN'
FROM DB_OBJECTS WHERE object_name = 'BENHNHAN';

-- ============================================================
-- 9. COLUMN LEVEL
-- ============================================================

-- KTV xem lương
INSERT INTO COLUMN_PRIVILEGE_GRANTS
(grantee_type, grantee_name, column_id, privilege_name, grant_option, granted_by)
SELECT 'USER', 'KTV', c.column_id, 'SELECT', 0, 'ADMIN'
FROM COLUMNS c
JOIN DB_OBJECTS o ON c.object_id = o.object_id
WHERE c.column_name = 'LUONG'
AND o.object_name = 'NHANVIEN';

-- Bác sĩ sửa phụ cấp
INSERT INTO COLUMN_PRIVILEGE_GRANTS
(grantee_type, grantee_name, column_id, privilege_name, grant_option, granted_by)
SELECT 'ROLE', 'ROLE_BACSI', c.column_id, 'UPDATE', 1, 'ADMIN'
FROM COLUMNS c
JOIN DB_OBJECTS o ON c.object_id = o.object_id
WHERE c.column_name = 'PHUCAP'
AND o.object_name = 'NHANVIEN';

-- ============================================================
-- 10. COMMIT
-- ============================================================
COMMIT;