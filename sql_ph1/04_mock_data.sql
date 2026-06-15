-- ============================================================
-- SCRIPT 2: DATA POPULATION (FINAL - CLEAN)
-- ============================================================

-- ============================================================
-- 1. XÓA DỮ LIỆU CŨ
-- ============================================================
ALTER SESSION SET CURRENT_SCHEMA = APP_ADMIN;

DELETE FROM APP_ADMIN.COLUMN_PRIVILEGE_GRANTS;
DELETE FROM APP_ADMIN.USER_PRIVILEGE_GRANTS;
DELETE FROM APP_ADMIN.ROLE_PRIVILEGE_GRANTS;
DELETE FROM APP_ADMIN.USER_ROLE_GRANTS;
DELETE FROM APP_ADMIN.COLUMNS;
DELETE FROM APP_ADMIN.DB_OBJECTS;
DELETE FROM APP_ADMIN.ROLES;
DELETE FROM APP_ADMIN.USERS;

COMMIT;

-- ============================================================
-- 2. USERS - TẠO USER THỰC TẾ TRONG ORACLE
-- ============================================================
DECLARE
    FUNCTION safe_name(p_name VARCHAR2) RETURN VARCHAR2 IS
    BEGIN
        RETURN DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(TRIM(p_name)));
    END;

    PROCEDURE ensure_user(p_username VARCHAR2, p_password VARCHAR2) IS
        l_user VARCHAR2(128) := safe_name(p_username);
        l_password VARCHAR2(4000) := REPLACE(p_password, '"', '""');
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'CREATE USER ' || l_user || ' IDENTIFIED BY "' || l_password || '"';
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE <> -1920 THEN
                    RAISE;
                END IF;

                EXECUTE IMMEDIATE 'ALTER USER ' || l_user || ' IDENTIFIED BY "' || l_password || '"';
        END;

        BEGIN
            EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO ' || l_user;
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE NOT IN (-1919, -1920, -1921, -1924, -1927) THEN
                    RAISE;
                END IF;
        END;
    END;

    PROCEDURE ensure_role(p_role VARCHAR2) IS
        l_role VARCHAR2(128) := safe_name(p_role);
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'CREATE ROLE ' || l_role;
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE <> -1921 THEN
                    RAISE;
                END IF;
        END;
    END;

    PROCEDURE ensure_role_grant(p_role VARCHAR2, p_grantee VARCHAR2) IS
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'GRANT ' || safe_name(p_role) || ' TO ' || safe_name(p_grantee);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE NOT IN (-1919, -1924, -1927) THEN
                    RAISE;
                END IF;
        END;
    END;

    PROCEDURE ensure_sys_priv(p_privilege VARCHAR2, p_grantee VARCHAR2) IS
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'GRANT ' || TRIM(p_privilege) || ' TO ' || safe_name(p_grantee);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE NOT IN (-1919, -1924, -1927) THEN
                    RAISE;
                END IF;
        END;
    END;

    PROCEDURE ensure_tab_priv(p_owner VARCHAR2, p_object VARCHAR2, p_privilege VARCHAR2, p_grantee VARCHAR2) IS
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'GRANT ' || TRIM(p_privilege) || ' ON ' || safe_name(p_owner) || '.' || safe_name(p_object) || ' TO ' || safe_name(p_grantee);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE NOT IN (-1919, -1924, -1927) THEN
                    RAISE;
                END IF;
        END;
    END;

    PROCEDURE ensure_col_priv(p_owner VARCHAR2, p_object VARCHAR2, p_column VARCHAR2, p_privilege VARCHAR2, p_grantee VARCHAR2) IS
    BEGIN
        BEGIN
            EXECUTE IMMEDIATE 'GRANT ' || TRIM(p_privilege) || ' (' || safe_name(p_column) || ') ON ' || safe_name(p_owner) || '.' || safe_name(p_object) || ' TO ' || safe_name(p_grantee);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE NOT IN (-1919, -1924, -1927) THEN
                    RAISE;
                END IF;
        END;
    END;

BEGIN
    IF USER <> 'APP_ADMIN' THEN
        RAISE_APPLICATION_ERROR(-20050, 'Hãy đăng nhập bằng APP_ADMIN để chạy script mock data.');
    END IF;

    -- ============================================================
    -- 2. USERS - TẠO USER THỰC TẾ TRONG ORACLE
    -- ============================================================
    ensure_user('ADMIN', 'admin@123');
    ensure_user('BACSI', 'bacsi@123');
    ensure_user('KTV', 'ktv@123');
    ensure_user('BN', 'bn@123');
    ensure_user('DPV', 'dpv123');

    -- Insert vào bảng tùy chỉnh USERS (nếu có)
    INSERT INTO APP_ADMIN.USERS VALUES
    ('ADMIN', STANDARD_HASH('admin@123', 'SHA256'), 'OPEN', SYSDATE, NULL, 'USERS', 'DEFAULT');

    INSERT INTO APP_ADMIN.USERS VALUES
    ('BACSI', STANDARD_HASH('bacsi@123', 'SHA256'), 'EXPIRED_LOCKED', SYSDATE, NULL, 'USERS', 'PROFILE_STRICT');

    INSERT INTO APP_ADMIN.USERS VALUES
    ('KTV', STANDARD_HASH('ktv@123', 'SHA256'), 'EXPIRED', SYSDATE, NULL, 'USERS', 'PROFILE_LIMITED');

    INSERT INTO APP_ADMIN.USERS VALUES
    ('BN', STANDARD_HASH('bn@123', 'SHA256'), 'LOCKED', SYSDATE, NULL, 'USERS', 'PROFILE_EXPIRE');

    INSERT INTO APP_ADMIN.USERS VALUES
    ('DPV', STANDARD_HASH('dpv123', 'SHA256'), 'OPEN', SYSDATE, NULL, 'USERS', 'DEFAULT');

    -- ============================================================
    -- 3. ROLES - TẠO ROLE THỰC TẾ TRONG ORACLE
    -- ============================================================
    ensure_role('ROLE_ADMIN');
    ensure_role('ROLE_BACSI');
    ensure_role('ROLE_KTV');
    ensure_role('ROLE_DPV');
    ensure_role('ROLE_BN');

    -- Insert vào bảng tùy chỉnh ROLES (nếu có)
    INSERT INTO APP_ADMIN.ROLES VALUES ('ROLE_ADMIN', 'Toàn quyền hệ thống', 'ACTIVE', SYSDATE);
    INSERT INTO APP_ADMIN.ROLES VALUES ('ROLE_BACSI', 'Bác sĩ', 'ACTIVE', SYSDATE);
    INSERT INTO APP_ADMIN.ROLES VALUES ('ROLE_KTV', 'Kỹ thuật viên', 'INACTIVE', SYSDATE);
    INSERT INTO APP_ADMIN.ROLES VALUES ('ROLE_DPV', 'Điều phối viên', 'INACTIVE', SYSDATE);
    INSERT INTO APP_ADMIN.ROLES VALUES ('ROLE_BN', 'Bệnh nhân', 'ACTIVE', SYSDATE);

    -- ============================================================
    -- 4. DB_OBJECTS
    -- ============================================================
    INSERT INTO APP_ADMIN.DB_OBJECTS (object_name, object_type, owner, status) VALUES ('BENHNHAN', 'TABLE', 'ADMIN', 'VALID');
    INSERT INTO APP_ADMIN.DB_OBJECTS (object_name, object_type, owner, status) VALUES ('NHANVIEN', 'TABLE', 'ADMIN', 'VALID');
    INSERT INTO APP_ADMIN.DB_OBJECTS (object_name, object_type, owner, status) VALUES ('HSBA', 'TABLE', 'ADMIN', 'INVALID');
    INSERT INTO APP_ADMIN.DB_OBJECTS (object_name, object_type, owner, status) VALUES ('HSBA_DV', 'TABLE', 'ADMIN', 'INVALID');
    INSERT INTO APP_ADMIN.DB_OBJECTS (object_name, object_type, owner, status) VALUES ('DONTHUOC', 'TABLE', 'ADMIN', 'VALID');

    -- ============================================================
    -- 5. COLUMNS
    -- ============================================================
    -- LUONG, PHUCAP thuộc NHANVIEN
    INSERT INTO COLUMNS (object_id, column_name, data_type, position)
    SELECT object_id, 'LUONG', 'NUMBER', 5
    FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'NHANVIEN';

    INSERT INTO COLUMNS (object_id, column_name, data_type, position)
    SELECT object_id, 'PHUCAP', 'NUMBER', 6
    FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'NHANVIEN';

    -- ============================================================
    -- 6. CẤP ROLE CHO USER - THỰC TẾ TRONG ORACLE
    -- ============================================================
    ensure_role_grant('ROLE_ADMIN', 'ADMIN');
    ensure_role_grant('ROLE_BACSI', 'BACSI');
    ensure_role_grant('ROLE_KTV', 'KTV');
    ensure_role_grant('ROLE_DPV', 'DPV');
    ensure_role_grant('ROLE_BN', 'BN');

    -- Insert vào bảng tùy chỉnh USER_ROLE_GRANTS (nếu có)
    INSERT INTO APP_ADMIN.USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
    VALUES ('ADMIN', 'ROLE_ADMIN', 1, 'ADMIN');

    INSERT INTO APP_ADMIN.USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
    VALUES ('BACSI', 'ROLE_BACSI', 0, 'ADMIN');

    INSERT INTO APP_ADMIN.USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
    VALUES ('KTV', 'ROLE_KTV', 0, 'ADMIN');

    INSERT INTO APP_ADMIN.USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
    VALUES ('DPV', 'ROLE_DPV', 0, 'ADMIN');

    INSERT INTO APP_ADMIN.USER_ROLE_GRANTS (username, role_name, admin_option, granted_by)
    VALUES ('BN', 'ROLE_BN', 0, 'ADMIN');

    -- ============================================================
    -- 7. CẤP QUYỀN CHO ROLE - THỰ TẾ TRONG ORACLE
    -- ============================================================
    ensure_sys_priv('CREATE TABLE', 'ROLE_ADMIN');
    ensure_sys_priv('CREATE VIEW', 'ROLE_ADMIN');
    ensure_sys_priv('CREATE PROCEDURE', 'ROLE_ADMIN');
    ensure_sys_priv('ALTER SYSTEM', 'ROLE_ADMIN');

    -- Insert vào bảng tùy chỉnh ROLE_PRIVILEGE_GRANTS (nếu có)
    BEGIN
        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_ADMIN', object_id, 'SELECT', 1, 'ADMIN' FROM APP_ADMIN.DB_OBJECTS;

        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_ADMIN', object_id, 'INSERT', 1, 'ADMIN' FROM APP_ADMIN.DB_OBJECTS;

        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_ADMIN', object_id, 'UPDATE', 1, 'ADMIN' FROM APP_ADMIN.DB_OBJECTS;

        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_ADMIN', object_id, 'DELETE', 1, 'ADMIN' FROM APP_ADMIN.DB_OBJECTS;
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- BÁC SĨ: Quyền SELECT, UPDATE
    BEGIN
        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_BACSI', object_id, 'SELECT', 1, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'HSBA';

        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_BACSI', object_id, 'UPDATE', 1, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'HSBA';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- KTV: Quyền SELECT
    BEGIN
        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_KTV', object_id, 'SELECT', 0, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'HSBA_DV';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- ĐIỀU PHỐI VIÊN: Quyền SELECT, INSERT
    BEGIN
        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_DPV', object_id, 'SELECT', 1, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name IN ('BENHNHAN', 'HSBA');

        INSERT INTO APP_ADMIN.ROLE_PRIVILEGE_GRANTS
        (role_name, object_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE_DPV', object_id, 'INSERT', 1, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'HSBA';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- ============================================================
    -- 8. USER PRIVILEGE - THỰC TẾ TRONG ORACLE
    -- ============================================================
    BEGIN
        ensure_tab_priv('ADMIN', 'BENHNHAN', 'SELECT', 'BN');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- Insert vào bảng tùy chỉnh USER_PRIVILEGE_GRANTS (nếu có)
    BEGIN
        INSERT INTO APP_ADMIN.USER_PRIVILEGE_GRANTS
        (username, object_id, privilege_name, grant_option, granted_by)
        SELECT 'BN', object_id, 'SELECT', 0, 'ADMIN'
        FROM APP_ADMIN.DB_OBJECTS WHERE object_name = 'BENHNHAN';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- ============================================================
    -- 9. COLUMN LEVEL PRIVILEGES - THỰC TẾ TRONG ORACLE
    -- ============================================================
    BEGIN
        EXECUTE IMMEDIATE 'CREATE OR REPLACE VIEW APP_ADMIN.V$COL$NHANVIEN$KTV AS SELECT LUONG FROM ADMIN.NHANVIEN';
        ensure_tab_priv('APP_ADMIN', 'V$COL$NHANVIEN$KTV', 'SELECT', 'KTV');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- Insert vào bảng tùy chỉ COLUMN_PRIVILEGE_GRANTS (nếu có)
    BEGIN
        INSERT INTO APP_ADMIN.COLUMN_PRIVILEGE_GRANTS
        (grantee_type, grantee_name, column_id, privilege_name, grant_option, granted_by)
        SELECT 'USER', 'KTV', c.column_id, 'SELECT', 0, 'ADMIN'
        FROM APP_ADMIN.COLUMNS c
        JOIN APP_ADMIN.DB_OBJECTS o ON c.object_id = o.object_id
        WHERE c.column_name = 'LUONG'
        AND o.object_name = 'NHANVIEN';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    -- Bác sĩ sửa phụ cấp (cấp quyền UPDATE trên cột)
    BEGIN
        ensure_col_priv('ADMIN', 'NHANVIEN', 'PHUCAP', 'UPDATE', 'ROLE_BACSI');
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;

    BEGIN
        INSERT INTO APP_ADMIN.COLUMN_PRIVILEGE_GRANTS
        (grantee_type, grantee_name, column_id, privilege_name, grant_option, granted_by)
        SELECT 'ROLE', 'ROLE_BACSI', c.column_id, 'UPDATE', 1, 'ADMIN'
        FROM APP_ADMIN.COLUMNS c
        JOIN APP_ADMIN.DB_OBJECTS o ON c.object_id = o.object_id
        WHERE c.column_name = 'PHUCAP'
        AND o.object_name = 'NHANVIEN';
    EXCEPTION
        WHEN OTHERS THEN
            IF SQLCODE <> -942 THEN
                RAISE;
            END IF;
    END;
END;
/

COMMIT;