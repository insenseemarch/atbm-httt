-- ============================================================
--  PHÂN HỆ 1: ỨNG DỤNG QUẢN TRỊ CSDL ORACLE
--  Oracle DDL Script
--  Tạo ngày: 2026-04-05
-- ============================================================

-- Xóa bảng cũ nếu tồn tại (thứ tự từ con -> cha)
BEGIN
    FOR t IN (
        SELECT table_name FROM user_tables
        WHERE table_name IN (
            'COLUMN_PRIVILEGE_GRANTS',
            'USER_PRIVILEGE_GRANTS',
            'ROLE_PRIVILEGE_GRANTS',
            'USER_ROLE_GRANTS',
            'COLUMNS',
            'PRIVILEGES',
            'DB_OBJECTS',
            'ROLES',
            'USERS'
        )
    ) LOOP
        EXECUTE IMMEDIATE 'DROP TABLE ' || t.table_name || ' CASCADE CONSTRAINTS';
    END LOOP;
END;
/

-- ============================================================
--  BẢNG 1: USERS
--  Lưu thông tin tài khoản người dùng Oracle
-- ============================================================
CREATE TABLE USERS (
    username            VARCHAR2(128)   NOT NULL,
    password_hash       VARCHAR2(256)   NOT NULL,
    status              VARCHAR2(20)    DEFAULT 'OPEN'
                            CONSTRAINT chk_user_status
                            CHECK (status IN ('OPEN', 'LOCKED', 'EXPIRED', 'EXPIRED_LOCKED')),
    created_at          DATE            DEFAULT SYSDATE NOT NULL,
    expired_date        DATE,
    default_tablespace  VARCHAR2(128)   DEFAULT 'USERS',
    profile             VARCHAR2(128)   DEFAULT 'DEFAULT',
    --
    CONSTRAINT pk_users PRIMARY KEY (username)
);

COMMENT ON TABLE  USERS                    IS 'Tài khoản người dùng trên Oracle DB Server';
COMMENT ON COLUMN USERS.username           IS 'Tên đăng nhập, PK, viết HOA theo chuẩn Oracle';
COMMENT ON COLUMN USERS.password_hash      IS 'Mật khẩu đã băm (hash)';
COMMENT ON COLUMN USERS.status             IS 'OPEN | LOCKED | EXPIRED | EXPIRED_LOCKED';
COMMENT ON COLUMN USERS.expired_date       IS 'Ngày hết hạn tài khoản, NULL = không hết hạn';
COMMENT ON COLUMN USERS.default_tablespace IS 'Tablespace mặc định của user';
COMMENT ON COLUMN USERS.profile            IS 'Profile áp dụng cho user';


-- ============================================================
--  BẢNG 2: ROLES
--  Lưu thông tin role trong hệ thống
-- ============================================================
CREATE TABLE ROLES (
    role_name   VARCHAR2(128)   NOT NULL,
    description VARCHAR2(500),
    status      VARCHAR2(20)    DEFAULT 'ACTIVE'
                    CONSTRAINT chk_role_status
                    CHECK (status IN ('ACTIVE', 'INACTIVE')),
    created_at  DATE            DEFAULT SYSDATE NOT NULL,
    --
    CONSTRAINT pk_roles PRIMARY KEY (role_name)
);

COMMENT ON TABLE  ROLES             IS 'Danh sách role trong Oracle DB Server';
COMMENT ON COLUMN ROLES.role_name   IS 'Tên role, PK';
COMMENT ON COLUMN ROLES.description IS 'Mô tả chức năng của role';
COMMENT ON COLUMN ROLES.status      IS 'ACTIVE | INACTIVE';


-- ============================================================
--  BẢNG 3: DB_OBJECTS
--  Lưu các đối tượng CSDL: TABLE, VIEW, PROCEDURE, FUNCTION
-- ============================================================
CREATE TABLE DB_OBJECTS (
    object_id   NUMBER          GENERATED ALWAYS AS IDENTITY,
    object_name VARCHAR2(128)   NOT NULL,
    object_type VARCHAR2(30)    NOT NULL
                    CONSTRAINT chk_obj_type
                    CHECK (object_type IN ('TABLE', 'VIEW', 'PROCEDURE', 'FUNCTION')),
    owner       VARCHAR2(128)   NOT NULL,
    status      VARCHAR2(20)    DEFAULT 'VALID'
                    CONSTRAINT chk_obj_status
                    CHECK (status IN ('VALID', 'INVALID')),
    --
    CONSTRAINT pk_db_objects PRIMARY KEY (object_id),
    CONSTRAINT uq_db_objects UNIQUE (object_name, object_type, owner)
);

COMMENT ON TABLE  DB_OBJECTS             IS 'Đối tượng CSDL: TABLE, VIEW, PROCEDURE, FUNCTION';
COMMENT ON COLUMN DB_OBJECTS.object_id   IS 'Khóa chính tự tăng';
COMMENT ON COLUMN DB_OBJECTS.object_name IS 'Tên đối tượng';
COMMENT ON COLUMN DB_OBJECTS.object_type IS 'TABLE | VIEW | PROCEDURE | FUNCTION';
COMMENT ON COLUMN DB_OBJECTS.owner       IS 'Schema sở hữu đối tượng';
COMMENT ON COLUMN DB_OBJECTS.status      IS 'VALID | INVALID';


-- ============================================================
--  BẢNG 4: PRIVILEGES
--  Danh mục các loại quyền hạn
-- ============================================================
CREATE TABLE PRIVILEGES (
    privilege_name  VARCHAR2(50)    NOT NULL,
    object_type     VARCHAR2(30)
                        CONSTRAINT chk_priv_obj_type
                        CHECK (object_type IN ('TABLE', 'VIEW', 'PROCEDURE', 'FUNCTION', 'ALL')),
    description     VARCHAR2(500),
    column_level    NUMBER(1)       DEFAULT 0
                        CONSTRAINT chk_priv_col_level
                        CHECK (column_level IN (0, 1)),
    --
    CONSTRAINT pk_privileges PRIMARY KEY (privilege_name)
);

-- ============================================================
--  DỮ LIỆU MẪU CHO BẢNG PRIVILEGES (XÓA DỮ LIỆU TRƯỚC KHI CHÈN)
-- ============================================================
DELETE FROM PRIVILEGES;
INSERT INTO PRIVILEGES (privilege_name, object_type, description, column_level) VALUES
    ('SELECT',    'TABLE',     'Quyền đọc dữ liệu từ bảng/view',            1);
INSERT INTO PRIVILEGES (privilege_name, object_type, description, column_level) VALUES
    ('INSERT',    'TABLE',     'Quyền thêm dữ liệu vào bảng',               0);
INSERT INTO PRIVILEGES (privilege_name, object_type, description, column_level) VALUES
    ('UPDATE',    'TABLE',     'Quyền cập nhật dữ liệu trong bảng/view',     1);
INSERT INTO PRIVILEGES (privilege_name, object_type, description, column_level) VALUES
    ('DELETE',    'TABLE',     'Quyền xóa dữ liệu khỏi bảng',              0);
INSERT INTO PRIVILEGES (privilege_name, object_type, description, column_level) VALUES
    ('EXECUTE',   'PROCEDURE', 'Quyền thực thi stored procedure',            0);
COMMENT ON TABLE  PRIVILEGES                  IS 'Danh mục các loại quyền hạn trong Oracle';
COMMENT ON COLUMN PRIVILEGES.privilege_name   IS 'Tên quyền: SELECT, INSERT, UPDATE, DELETE, EXECUTE';
COMMENT ON COLUMN PRIVILEGES.object_type      IS 'Loại đối tượng áp dụng quyền này';
COMMENT ON COLUMN PRIVILEGES.column_level     IS '1 = có thể phân quyền đến mức cột, 0 = không';

COMMENT ON TABLE  PRIVILEGES                  IS 'Danh mục các loại quyền hạn trong Oracle';
COMMENT ON COLUMN PRIVILEGES.privilege_name   IS 'Tên quyền: SELECT, INSERT, UPDATE, DELETE, EXECUTE';
COMMENT ON COLUMN PRIVILEGES.object_type      IS 'Loại đối tượng áp dụng quyền này';
COMMENT ON COLUMN PRIVILEGES.column_level     IS '1 = có thể phân quyền đến mức cột, 0 = không';


-- ============================================================
--  BẢNG 5: COLUMNS
--  Lưu danh sách cột của TABLE và VIEW
-- ============================================================
CREATE TABLE COLUMNS (
    column_id   NUMBER          GENERATED ALWAYS AS IDENTITY,
    object_id   NUMBER          NOT NULL,
    column_name VARCHAR2(128)   NOT NULL,
    data_type   VARCHAR2(50)    NOT NULL,
    position    NUMBER          NOT NULL,
    --
    CONSTRAINT pk_columns PRIMARY KEY (column_id),
    CONSTRAINT fk_columns_object FOREIGN KEY (object_id)
        REFERENCES DB_OBJECTS (object_id) ON DELETE CASCADE,
    CONSTRAINT uq_columns UNIQUE (object_id, column_name)
);

CREATE INDEX idx_columns_object ON COLUMNS (object_id);

COMMENT ON TABLE  COLUMNS             IS 'Danh sách cột của TABLE và VIEW (dùng cho phân quyền mức cột)';
COMMENT ON COLUMN COLUMNS.column_id   IS 'Khóa chính tự tăng';
COMMENT ON COLUMN COLUMNS.object_id   IS 'FK -> DB_OBJECTS';
COMMENT ON COLUMN COLUMNS.column_name IS 'Tên cột';
COMMENT ON COLUMN COLUMNS.data_type   IS 'Kiểu dữ liệu: VARCHAR2, NUMBER, DATE...';
COMMENT ON COLUMN COLUMNS.position    IS 'Vị trí thứ tự cột trong bảng';


-- ============================================================
--  BẢNG 6: USER_ROLE_GRANTS
--  Cấp role cho user (yêu cầu 3a)
-- ============================================================
CREATE TABLE USER_ROLE_GRANTS (
    grant_id        NUMBER          GENERATED ALWAYS AS IDENTITY,
    username        VARCHAR2(128)   NOT NULL,
    role_name       VARCHAR2(128)   NOT NULL,
    admin_option    NUMBER(1)       DEFAULT 0
                        CONSTRAINT chk_urg_admin
                        CHECK (admin_option IN (0, 1)),
    granted_at      DATE            DEFAULT SYSDATE NOT NULL,
    granted_by      VARCHAR2(128)   NOT NULL,
    --
    CONSTRAINT pk_user_role_grants PRIMARY KEY (grant_id),
    CONSTRAINT fk_urg_user FOREIGN KEY (username)
        REFERENCES USERS (username) ON DELETE CASCADE,
    CONSTRAINT fk_urg_role FOREIGN KEY (role_name)
        REFERENCES ROLES (role_name) ON DELETE CASCADE,
    CONSTRAINT uq_user_role UNIQUE (username, role_name)
);

CREATE INDEX idx_urg_username  ON USER_ROLE_GRANTS (username);
CREATE INDEX idx_urg_role_name ON USER_ROLE_GRANTS (role_name);

COMMENT ON TABLE  USER_ROLE_GRANTS              IS 'Cấp role cho user (tương đương GRANT role TO user)';
COMMENT ON COLUMN USER_ROLE_GRANTS.admin_option IS '1 = WITH ADMIN OPTION, cho phép user tiếp tục cấp role này';
COMMENT ON COLUMN USER_ROLE_GRANTS.granted_by   IS 'Tài khoản quản trị thực hiện lệnh cấp';


-- ============================================================
--  BẢNG 7: USER_PRIVILEGE_GRANTS
--  Cấp quyền trực tiếp cho user trên đối tượng (yêu cầu 3a, 3b, 3c)
-- ============================================================
CREATE TABLE USER_PRIVILEGE_GRANTS (
    grant_id        NUMBER          GENERATED ALWAYS AS IDENTITY,
    username        VARCHAR2(128)   NOT NULL,
    object_id       NUMBER          NOT NULL,
    privilege_name  VARCHAR2(50)    NOT NULL,
    grant_option    NUMBER(1)       DEFAULT 0
                        CONSTRAINT chk_upg_grant_opt
                        CHECK (grant_option IN (0, 1)),
    granted_at      DATE            DEFAULT SYSDATE NOT NULL,
    granted_by      VARCHAR2(128)   NOT NULL,
    --
    CONSTRAINT pk_user_priv_grants PRIMARY KEY (grant_id),
    CONSTRAINT fk_upg_user FOREIGN KEY (username)
        REFERENCES USERS (username) ON DELETE CASCADE,
    CONSTRAINT fk_upg_object FOREIGN KEY (object_id)
        REFERENCES DB_OBJECTS (object_id) ON DELETE CASCADE,
    CONSTRAINT fk_upg_privilege FOREIGN KEY (privilege_name)
        REFERENCES PRIVILEGES (privilege_name),
    CONSTRAINT uq_user_priv UNIQUE (username, object_id, privilege_name)
);

CREATE INDEX idx_upg_username   ON USER_PRIVILEGE_GRANTS (username);
CREATE INDEX idx_upg_object_id  ON USER_PRIVILEGE_GRANTS (object_id);
CREATE INDEX idx_upg_privilege  ON USER_PRIVILEGE_GRANTS (privilege_name);

COMMENT ON TABLE  USER_PRIVILEGE_GRANTS               IS 'Cấp quyền trực tiếp cho user trên đối tượng CSDL';
COMMENT ON COLUMN USER_PRIVILEGE_GRANTS.grant_option  IS '1 = WITH GRANT OPTION, user có thể tiếp tục cấp quyền này';
COMMENT ON COLUMN USER_PRIVILEGE_GRANTS.granted_by    IS 'Tài khoản quản trị thực hiện lệnh cấp';


-- ============================================================
--  BẢNG 8: ROLE_PRIVILEGE_GRANTS
--  Cấp quyền cho role trên đối tượng (yêu cầu 3a, 3b, 3c)
-- ============================================================
CREATE TABLE ROLE_PRIVILEGE_GRANTS (
    grant_id        NUMBER          GENERATED ALWAYS AS IDENTITY,
    role_name       VARCHAR2(128)   NOT NULL,
    object_id       NUMBER          NOT NULL,
    privilege_name  VARCHAR2(50)    NOT NULL,
    grant_option    NUMBER(1)       DEFAULT 0
                        CONSTRAINT chk_rpg_grant_opt
                        CHECK (grant_option IN (0, 1)),
    granted_at      DATE            DEFAULT SYSDATE NOT NULL,
    granted_by      VARCHAR2(128)   NOT NULL,
    --
    CONSTRAINT pk_role_priv_grants PRIMARY KEY (grant_id),
    CONSTRAINT fk_rpg_role FOREIGN KEY (role_name)
        REFERENCES ROLES (role_name) ON DELETE CASCADE,
    CONSTRAINT fk_rpg_object FOREIGN KEY (object_id)
        REFERENCES DB_OBJECTS (object_id) ON DELETE CASCADE,
    CONSTRAINT fk_rpg_privilege FOREIGN KEY (privilege_name)
        REFERENCES PRIVILEGES (privilege_name),
    CONSTRAINT uq_role_priv UNIQUE (role_name, object_id, privilege_name)
);

CREATE INDEX idx_rpg_role_name  ON ROLE_PRIVILEGE_GRANTS (role_name);
CREATE INDEX idx_rpg_object_id  ON ROLE_PRIVILEGE_GRANTS (object_id);
CREATE INDEX idx_rpg_privilege  ON ROLE_PRIVILEGE_GRANTS (privilege_name);

COMMENT ON TABLE  ROLE_PRIVILEGE_GRANTS               IS 'Cấp quyền cho role trên đối tượng CSDL';
COMMENT ON COLUMN ROLE_PRIVILEGE_GRANTS.grant_option  IS '1 = WITH GRANT OPTION';
COMMENT ON COLUMN ROLE_PRIVILEGE_GRANTS.granted_by    IS 'Tài khoản quản trị thực hiện lệnh cấp';


-- ============================================================
--  BẢNG 9: COLUMN_PRIVILEGE_GRANTS
--  Phân quyền SELECT / UPDATE đến mức cột (yêu cầu 3c)
-- ============================================================
CREATE TABLE COLUMN_PRIVILEGE_GRANTS (
    grant_id        NUMBER          GENERATED ALWAYS AS IDENTITY,
    grantee_type    VARCHAR2(10)    NOT NULL
                        CONSTRAINT chk_cpg_grantee_type
                        CHECK (grantee_type IN ('USER', 'ROLE')),
    grantee_name    VARCHAR2(128)   NOT NULL,
    column_id       NUMBER          NOT NULL,
    privilege_name  VARCHAR2(50)    NOT NULL,
    grant_option    NUMBER(1)       DEFAULT 0
                        CONSTRAINT chk_cpg_grant_opt
                        CHECK (grant_option IN (0, 1)),
    granted_at      DATE            DEFAULT SYSDATE NOT NULL,
    granted_by      VARCHAR2(128)   NOT NULL,
    --
    CONSTRAINT pk_col_priv_grants PRIMARY KEY (grant_id),
    CONSTRAINT fk_cpg_column FOREIGN KEY (column_id)
        REFERENCES COLUMNS (column_id) ON DELETE CASCADE,
    CONSTRAINT fk_cpg_privilege FOREIGN KEY (privilege_name)
        REFERENCES PRIVILEGES (privilege_name),
    CONSTRAINT uq_col_priv UNIQUE (grantee_type, grantee_name, column_id, privilege_name)
);

CREATE INDEX idx_cpg_column_id  ON COLUMN_PRIVILEGE_GRANTS (column_id);
CREATE INDEX idx_cpg_grantee    ON COLUMN_PRIVILEGE_GRANTS (grantee_type, grantee_name);

COMMENT ON TABLE  COLUMN_PRIVILEGE_GRANTS               IS 'Phân quyền SELECT/UPDATE đến mức cột cho user hoặc role';
COMMENT ON COLUMN COLUMN_PRIVILEGE_GRANTS.grantee_type  IS 'USER hoặc ROLE';
COMMENT ON COLUMN COLUMN_PRIVILEGE_GRANTS.grantee_name  IS 'Tên user hoặc tên role được cấp quyền';
COMMENT ON COLUMN COLUMN_PRIVILEGE_GRANTS.grant_option  IS '1 = WITH GRANT OPTION';


-- ============================================================
--  XÓA DỮ LIỆU CŨ (thứ tự từ con -> cha)
-- ============================================================
DELETE FROM COLUMN_PRIVILEGE_GRANTS;
DELETE FROM USER_PRIVILEGE_GRANTS;
DELETE FROM ROLE_PRIVILEGE_GRANTS;
DELETE FROM USER_ROLE_GRANTS;
DELETE FROM COLUMNS;
DELETE FROM DB_OBJECTS;
DELETE FROM ROLES;
DELETE FROM USERS;


-- ============================================================
--  COMMIT
-- ============================================================
COMMIT;


-- ============================================================
--  KIỂM TRA: Xem tất cả bảng vừa tạo
-- ============================================================
SELECT
    table_name,
    num_rows
FROM user_tables
WHERE table_name IN (
    'USERS', 'ROLES', 'DB_OBJECTS', 'PRIVILEGES',
    'COLUMNS', 'USER_ROLE_GRANTS', 'USER_PRIVILEGE_GRANTS',
    'ROLE_PRIVILEGE_GRANTS', 'COLUMN_PRIVILEGE_GRANTS'
)
ORDER BY table_name;