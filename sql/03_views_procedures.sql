-- TẠO VIEW --

-- 1. View danh sách User
CREATE OR REPLACE VIEW v_AppUsers AS
SELECT USERNAME, ACCOUNT_STATUS, CREATED, DEFAULT_TABLESPACE
FROM DBA_USERS
WHERE ORACLE_MAINTAINED = 'N'
ORDER BY USERNAME;

-- 2. View danh sách Role
CREATE OR REPLACE VIEW v_AppRoles AS
SELECT ROLE, AUTHENTICATION_TYPE
FROM DBA_ROLES
WHERE ORACLE_MAINTAINED = 'N'
ORDER BY ROLE;


-- 3. View quyền trên Đối tượng
CREATE OR REPLACE VIEW v_ObjectPrivs AS
SELECT GRANTEE, OWNER, TABLE_NAME, PRIVILEGE, GRANTABLE
FROM DBA_TAB_PRIVS
    WHERE OWNER IN (SELECT owner FROM APP_ADMIN.DB_OBJECTS)
      AND TABLE_NAME IN (SELECT object_name FROM APP_ADMIN.DB_OBJECTS)
      AND TABLE_NAME NOT LIKE 'V$COL$%'
ORDER BY GRANTEE;

-- 4. View quyền trên Cột
    CREATE OR REPLACE VIEW v_ColumnPrivs AS
    SELECT GRANTEE, SUBSTR(TABLE_NAME, 7, INSTR(TABLE_NAME, '$', 1, 3) - 7) AS VIEW_NAME, PRIVILEGE, GRANTABLE, 'Dua qua View' AS COLUMN_NAME
    FROM DBA_TAB_PRIVS
    WHERE TABLE_NAME LIKE 'V$COL$%'
      AND OWNER = USER
UNION ALL
SELECT GRANTEE, TABLE_NAME AS VIEW_NAME, PRIVILEGE, GRANTABLE, COLUMN_NAME
FROM DBA_COL_PRIVS
WHERE OWNER IN (SELECT owner FROM APP_ADMIN.DB_OBJECTS)
  AND TABLE_NAME IN (SELECT object_name FROM APP_ADMIN.DB_OBJECTS)
ORDER BY GRANTEE;


-- 5. View danh sách Role được cấp cho User
CREATE OR REPLACE VIEW v_UserRole AS
SELECT GRANTEE, GRANTED_ROLE, ADMIN_OPTION
FROM DBA_ROLE_PRIVS
WHERE GRANTEE IN (SELECT USERNAME FROM DBA_USERS WHERE ORACLE_MAINTAINED = 'N')
  AND GRANTED_ROLE IN (SELECT ROLE FROM DBA_ROLES WHERE ORACLE_MAINTAINED = 'N')
ORDER BY GRANTEE;

-- 6. View chi tiết quyền của từng Role
CREATE OR REPLACE VIEW v_PrivsRole AS
SELECT ROLE, TABLE_NAME, PRIVILEGE, COLUMN_NAME
FROM ROLE_TAB_PRIVS
WHERE ROLE IN (SELECT ROLE FROM DBA_ROLES WHERE ORACLE_MAINTAINED = 'N')
  AND OWNER IN (SELECT owner FROM APP_ADMIN.DB_OBJECTS)
  AND TABLE_NAME IN (SELECT object_name FROM APP_ADMIN.DB_OBJECTS)
ORDER BY ROLE;

-- 7. View tổng hợp Đối tượng Hệ thống đồ án (Phân chia Category)
CREATE OR REPLACE VIEW v_ProjectObjects AS
SELECT 
    object_name, 
    object_type, 
    owner,
    'Đối tượng của hệ thống đồ án' AS category
FROM APP_ADMIN.DB_OBJECTS
UNION ALL
SELECT 
    view_name AS object_name,
    'VIEW' AS object_type,
    owner,
    'Đối tượng do người dùng tạo' AS category
FROM DBA_VIEWS
WHERE view_name LIKE 'V$COL$%'
  AND owner = 'APP_ADMIN'
ORDER BY category, object_type, object_name;

-- Tạo procedure--

-- Liên quan tới user:

-- 1. Tạo User
CREATE OR REPLACE PROCEDURE sp_CreateUser(
    p_username IN VARCHAR2,
    p_password IN VARCHAR2
) AS
    BEGIN
        EXECUTE IMMEDIATE 'CREATE USER ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_username)) || ' IDENTIFIED BY "' || REPLACE(p_password, '"', '""') || '"';
        EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_username));
    END;
/

-- 2. Xoá User
CREATE OR REPLACE PROCEDURE sp_DeleteUser(
    p_username IN VARCHAR2
) AS
    BEGIN 
         EXECUTE IMMEDIATE 'DROP USER ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_username)) || ' CASCADE';
    END;
/

-- 3. Đổi mật khẩu 
CREATE OR REPLACE PROCEDURE sp_ChangePassword(
    p_username IN VARCHAR2,
    p_newpassword IN VARCHAR2
) AS
    BEGIN
        EXECUTE IMMEDIATE 'ALTER USER ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_username)) || ' IDENTIFIED BY "' || REPLACE(p_newpassword, '"', '""') || '"';
    END;
/

-- 4. Khoá/ mở tài khoản 
CREATE OR REPLACE PROCEDURE sp_LockUser(
    p_username IN VARCHAR2,
    p_action IN VARCHAR2        -- lock hoặc unlock
) AS
    BEGIN 
        EXECUTE IMMEDIATE 'ALTER USER ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_username)) || ' ACCOUNT ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_action));
    END;
/


-- Liên quan tới Role

-- 5. Tạo role
CREATE OR REPLACE PROCEDURE sp_CreateRole(
    p_rolename IN VARCHAR2    
) AS
    BEGIN 
        EXECUTE IMMEDIATE 'CREATE ROLE ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_rolename));
    END;
    /
    
    -- 6. Xoá role
    CREATE OR REPLACE PROCEDURE sp_DeleteRole(
        p_rolename IN VARCHAR2
    ) AS
    BEGIN 
        EXECUTE IMMEDIATE 'DROP ROLE ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_rolename));
    END;
/

-- 7. Hiệu chỉnh Role (Thêm/Sửa mật khẩu role) 
CREATE OR REPLACE PROCEDURE sp_EditRole(
    p_rolename IN VARCHAR2,
    p_password IN VARCHAR2 DEFAULT NULL
) AS
    v_sql VARCHAR2(200);
    v_safe_role VARCHAR2(128);
BEGIN
    v_safe_role := DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_rolename));
    IF p_password IS NOT NULL THEN
        v_sql := 'ALTER ROLE ' || v_safe_role || ' IDENTIFIED BY "' || REPLACE(p_password, '"', '""') || '"';
    ELSE
        v_sql := 'ALTER ROLE ' || v_safe_role || ' NOT IDENTIFIED';
    END IF;
    EXECUTE IMMEDIATE v_sql;
EXCEPTION
    WHEN OTHERS THEN
        RAISE_APPLICATION_ERROR(-20007, 'Lỗi cập nhật Role: ' || SQLERRM);
END;
/

-- 8. Cấp role cho User
CREATE OR REPLACE PROCEDURE sp_GrantRole(
    p_role IN VARCHAR2,
    p_grantee IN VARCHAR2,
    p_admin_option IN VARCHAR2 DEFAULT 'NO'
) AS v_sql VARCHAR2(500);
    BEGIN 
        v_sql := 'GRANT ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_role)) || ' TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
    IF UPPER(p_admin_option) = 'YES' THEN
        v_sql := v_sql || ' WITH ADMIN OPTION';
    END IF;
    EXECUTE IMMEDIATE v_sql;
END;
/

-- 9. Thu hồi Role khỏi User
CREATE OR REPLACE PROCEDURE sp_RevokeRoleUser(
    p_role IN VARCHAR2,
    p_grantee IN VARCHAR2
) AS
    BEGIN 
        EXECUTE IMMEDIATE 'REVOKE ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_role)) || ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
    END;
/


-- Liên quan tới quyền:


-- 10. Cấp quyền trên Bảng/View/Proc/Func
CREATE OR REPLACE PROCEDURE sp_GrantObjPrivs(
    p_privilege IN VARCHAR2,
    p_schema IN VARCHAR2,
    p_object IN VARCHAR2,
    p_grantee IN VARCHAR2,
    p_grant_option IN VARCHAR2 DEFAULT 'NO'
) AS 
    v_sql VARCHAR2(500);
BEGIN 
    v_sql := 'GRANT ' || p_privilege || ' ON ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || ' TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
    IF UPPER(p_grant_option) = 'YES' THEN v_sql := v_sql || ' WITH GRANT OPTION'; END IF;
    EXECUTE IMMEDIATE v_sql;
EXCEPTION 
    WHEN OTHERS THEN RAISE_APPLICATION_ERROR(-20000, 'Lỗi cấp quyền: ' || SQLERRM);
END;
/

-- 11. Cấp quyền trên Cột
CREATE OR REPLACE PROCEDURE sp_GrantColPrivs(
    p_privilege  IN VARCHAR2,
    p_schema     IN VARCHAR2,
    p_object     IN VARCHAR2,
    p_columns    IN VARCHAR2,
    p_grantee    IN VARCHAR2,
    p_grant_option IN VARCHAR2 DEFAULT 'NO'
) AS
    v_view_name VARCHAR2(128);
    v_sql       VARCHAR2(1000);
    v_has_grantable NUMBER := 0;
BEGIN
    IF UPPER(p_privilege) IN ('INSERT', 'DELETE') THEN
        RAISE_APPLICATION_ERROR(-20001, 'INSERT/DELETE không hỗ trợ mức cột!');
    END IF;

    IF UPPER(p_privilege) = 'UPDATE' THEN
        v_sql := 'GRANT UPDATE (' || p_columns || ') ON ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || ' TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
        IF UPPER(p_grant_option) = 'YES' THEN
            v_sql := v_sql || ' WITH GRANT OPTION';
        END IF;
        EXECUTE IMMEDIATE v_sql;
    ELSIF UPPER(p_privilege) = 'SELECT' THEN
        -- Với object khác schema, APP_ADMIN phải có SELECT WITH GRANT OPTION
        -- trên bảng/view gốc thì mới GRANT SELECT qua view trung gian được.
        IF UPPER(p_schema) <> USER THEN
            SELECT COUNT(*)
            INTO v_has_grantable
            FROM DBA_TAB_PRIVS
            WHERE OWNER = DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema))
              AND TABLE_NAME = DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object))
              AND GRANTEE = USER
              AND PRIVILEGE = 'SELECT'
              AND GRANTABLE = 'YES';

            IF v_has_grantable = 0 THEN
                RAISE_APPLICATION_ERROR(
                    -20008,
                    'Thiếu SELECT WITH GRANT OPTION trên ' ||
                    DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) ||
                    '. Hãy cấp: GRANT SELECT ON ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || ' TO ' || USER || ' WITH GRANT OPTION'
                );
            END IF;
        END IF;

        v_view_name := SUBSTR('V$COL$' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || '$' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee)), 1, 128);
        v_sql := 'CREATE OR REPLACE VIEW ' || v_view_name ||
                 ' AS SELECT ' || p_columns ||
                 ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object));
        EXECUTE IMMEDIATE v_sql;

        v_sql := 'GRANT SELECT ON ' || v_view_name || ' TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
        IF UPPER(p_grant_option) = 'YES' THEN
            v_sql := v_sql || ' WITH GRANT OPTION';
        END IF;
        EXECUTE IMMEDIATE v_sql;
    ELSE 
        RAISE_APPLICATION_ERROR(-20003, 'Chỉ hỗ trợ phân quyền mức cột cho SELECT và UPDATE!');
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE IN (-20001, -20003, -20008) THEN RAISE;
        ELSIF SQLCODE = -1720 THEN
            RAISE_APPLICATION_ERROR(-20009, 'Không thể GRANT SELECT theo cột vì APP_ADMIN chưa có GRANT OPTION trên object nguồn: ' || SQLERRM);
        ELSE RAISE_APPLICATION_ERROR(-20002, 'Lỗi cấp quyền cột: ' || SQLERRM);
        END IF;
END;
/

-- 12. Thu hồi quyền trên Object 
CREATE OR REPLACE PROCEDURE sp_RevokeObjPrivs(
    p_privilege IN VARCHAR2,
    p_schema IN VARCHAR2,
    p_object IN VARCHAR2,
    p_grantee IN VARCHAR2
) AS
BEGIN 
    EXECUTE IMMEDIATE 'REVOKE ' || p_privilege || ' ON ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
EXCEPTION 
    WHEN OTHERS THEN RAISE_APPLICATION_ERROR(-20005, 'Lỗi thu hồi quyền: ' || SQLERRM);
END;
/

-- 13. Thu hồi quyền trên Cột
CREATE OR REPLACE PROCEDURE sp_RevokeColPrivs(
    p_privilege IN VARCHAR2,   -- SELECT hoặc UPDATE
    p_schema    IN VARCHAR2,   
    p_object    IN VARCHAR2,
    p_grantee   IN VARCHAR2
) AS
    v_view_name VARCHAR2(128);
BEGIN
    IF UPPER(p_privilege) = 'UPDATE' THEN
        EXECUTE IMMEDIATE 'REVOKE UPDATE ON ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_schema)) || '.' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
    ELSIF UPPER(p_privilege) = 'SELECT' THEN
        v_view_name := SUBSTR('V$COL$' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_object)) || '$' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee)), 1, 128);
        EXECUTE IMMEDIATE 'REVOKE SELECT ON ' || v_view_name || ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
        EXECUTE IMMEDIATE 'DROP VIEW ' || v_view_name;
    END IF;
EXCEPTION
    WHEN OTHERS THEN RAISE_APPLICATION_ERROR(-20006, 'Lỗi thu hồi quyền cột: ' || SQLERRM);
END;
/

-- 14. Cấp quyền Hệ thống (VD: CREATE TABLE, CREATE VIEW...)
CREATE OR REPLACE PROCEDURE sp_GrantSysPrivs(
    p_privilege IN VARCHAR2,
    p_grantee   IN VARCHAR2,
    p_admin_option IN VARCHAR2 DEFAULT 'NO'
) AS 
    v_sql VARCHAR2(500);
BEGIN
    v_sql := 'GRANT ' || p_privilege || ' TO ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
    IF UPPER(p_admin_option) = 'YES' THEN
        v_sql := v_sql || ' WITH ADMIN OPTION';
    END IF;
    EXECUTE IMMEDIATE v_sql;
EXCEPTION
    WHEN OTHERS THEN RAISE_APPLICATION_ERROR(-20010, 'Lỗi cấp quyền hệ thống: ' || SQLERRM);
END;
/

-- 15. Thu hồi quyền Hệ thống
CREATE OR REPLACE PROCEDURE sp_RevokeSysPrivs(
    p_privilege IN VARCHAR2,
    p_grantee   IN VARCHAR2
) AS
BEGIN
    EXECUTE IMMEDIATE 'REVOKE ' || p_privilege || ' FROM ' || DBMS_ASSERT.SIMPLE_SQL_NAME(UPPER(p_grantee));
EXCEPTION
    WHEN OTHERS THEN RAISE_APPLICATION_ERROR(-20011, 'Lỗi thu hồi quyền hệ thống: ' || SQLERRM);
END;
/
