# PHÂN HỆ 1: ỨNG DỤNG QUẢN TRỊ CSDL ORACLE
## Hướng dẫn tổ chức và gộp code

---

## I. PHÂN TÍCH YÊU CẦU

### Chức năng chính (từ document yêu cầu):
1. **Quản lý User/Role**: Tạo, xóa, sửa (hiệu chính) user hoặc role
2. **Xem danh sách**: Tài khoản người dùng và role trên Oracle DB Server
3. **Cấp quyền**: 
   - Cấp quyền cho user, cấp quyền cho role, cấp role cho user
   - Hỗ trợ WITH GRANT OPTION
   - Cấp quyền trên table, view, procedure, function
   - Quyền SELECT/UPDATE có thể cấp ở mức cột (column-level)
4. **Thu hồi quyền**: Từ user hoặc role
5. **Xem quyền chi tiết**: Của một user/role trên các đối tượng dữ liệu

---

## II. KIẾN TRÚC HIỆN TẠI

```
atbm-httt/
├── script_schema.sql          # Schema định nghĩa (USERS, ROLES, COLUMNS, DB_OBJECTS, PRIVILEGES, ...)
├── mock_data.sql              # Dữ liệu mẫu test
├── SCRIPT/
│   ├── admin.sql              # Views (v_AppUsers, v_AppRoles, ...) + Procedures
│   └── sys.sql                # Setup user app_admin với các quyền cần thiết
├── procedure/                 # (Backup hoặc version khác)
│   ├── admin.sql
│   └── sys.sql
└── winform/
    └── PhanHe1/
        ├── Program.cs         # Main entry point
        ├── LoginForm.cs       # Form đăng nhập
        ├── Form1.cs           # Main UI
        ├── OracleAdminService.cs  # Service layer (kết nối DB)
        └── ...
```

---

## III. CHIẾN LƯỢC GỘP CODE

### 3.1 Tầng Database (SQL)
**Thứ tự chạy:**
1. **sys.sql** → Tạo user `app_admin` với đầy đủ quyền
2. **script_schema.sql** → Tạo các table hỗ trợ (USERS, ROLES, COLUMNS, DB_OBJECTS, ...)
3. **SCRIPT/admin.sql** → Tạo views + procedures

**Mục đích tách:**
- `script_schema.sql`: Định nghĩa schema quản lý quyền riêng (nếu cần)
- `SCRIPT/admin.sql`: Views/Procedures để WinForm gọi

**Tổ chức tối ưu:**
```
SQL/
├── 01_setup_app_admin.sql      # sys.sql (tạo user app_admin)
├── 02_schema_objects.sql        # script_schema.sql (bảng support)
├── 03_views_procedures.sql      # admin.sql (views + procedures)
└── 04_mock_data.sql             # mock_data.sql (dữ liệu test)
```

### 3.2 Tầng Ứng dụng (C# WinForm)
**Cấu trúc lớp:**
```
Models/
├── User.cs
├── Role.cs
└── PrivilegeGrant.cs

Services/
├── OracleAdminService.cs       # (Đã có) Kết nối DB + Query
├── UserService.cs              # Logic quản lý user
├── RoleService.cs              # Logic quản lý role
└── PrivilegeService.cs         # Logic cấp/thu hồi quyền

Forms/
├── LoginForm.cs                 # (Đã có)
├── Form1.cs                     # (Đã có) Main UI
├── UserManagementForm.cs        # Quản lý user
├── RoleManagementForm.cs        # Quản lý role
├── GrantPrivilegeForm.cs        # Cấp quyền
└── ViewPrivilegesForm.cs        # Xem quyền chi tiết
```

---

## IV. MAPPING GIỮA SQL VÀ C# CODE

| Chức năng | SQL View/Procedure | C# Method |
|-----------|-------------------|-----------|
| Xem danh sách User | `v_AppUsers` | `UserService.GetAllUsers()` |
| Xem danh sách Role | `v_AppRoles` | `RoleService.GetAllRoles()` |
| Tạo User | `sp_CreateUser()` | `UserService.CreateUser()` |
| Xóa User | `sp_DeleteUser()` | `UserService.DeleteUser()` |
| Sửa User | `sp_EditUser()` | `UserService.UpdateUser()` |
| Tạo Role | `sp_CreateRole()` | `RoleService.CreateRole()` |
| Cấp Role cho User | `sp_GrantRole()` | `PrivilegeService.GrantRoleToUser()` |
| Cấp quyền Hệ thống | `sp_GrantSysPrivs()` | `PrivilegeService.GrantSystemPrivilege()` |
| Cấp quyền trên Object | `sp_GrantObjPrivs()` | `PrivilegeService.GrantObjectPrivilege()` |
| Cấp quyền trên Cột | `sp_GrantColPrivs()` | `PrivilegeService.GrantColumnPrivilege()` |
| Xem quyền User | `v_UserPrivileges` | `PrivilegeService.GetUserPrivileges()` |
| Xem quyền Role | `v_RolePrivileges` | `PrivilegeService.GetRolePrivileges()` |
| Thu hồi quyền | `sp_RevokePrivilege()` | `PrivilegeService.RevokePrivilege()` |

---

## V. QUYẾT ĐỊNH THIẾT KẾ QUAN TRỌNG

### 5.1 Dữ liệu mẫu
- Giữ `mock_data.sql` riêng → Chỉ dùng lúc test/demo
- Không chạy trong production

### 5.2 Views vs Procedures
- **Views** (v_AppUsers, v_AppRoles, ...): Dùng cho SELECT_ONLY → WinForm binding DataGrid
- **Procedures** (sp_CreateUser, sp_GrantRole, ...): Cho DML operations (CREATE, ALTER, DROP, GRANT, REVOKE)

### 5.3 Security
- Luôn dùng `DBMS_ASSERT.SIMPLE_SQL_NAME()` để tránh SQL Injection
- Kết nối qua user `app_admin` (có quyền admin)
- C# `OracleAdminService` xử lý parameterized queries

### 5.4 Error Handling
- SQL: Try-catch blocks, EXCEPTION handlers
- C#: Try-catch, log errors, notify user via MessageBox

---

## VI. CÁC BƯỚC TRIỂN KHAI

### Phase 1: Setup Database
```bash
# Chạy trong SQL*Plus hoặc SQL Developer
sqlplus sys/password@db as sysdba
@SQL/01_setup_app_admin.sql
@SQL/02_schema_objects.sql
@SQL/03_views_procedures.sql
@SQL/04_mock_data.sql    # Nếu test
```

### Phase 2: Phát triển WinForm
1. **Models**: Định nghĩa User, Role, PrivilegeGrant classes
2. **Services**: OracleAdminService (mở rộng), UserService, RoleService, PrivilegeService
3. **Forms**: UI cho 5 chức năng chính
4. **Testing**: Unit tests + Integration tests với Oracle

### Phase 3: Integration
- WinForm → gọi C# Service → C# Service gọi SQL (View/Procedure)
- Xử lý Error, Transaction

---

## VII. TEMPLATE CODE C#

### OracleAdminService - Query View
```csharp
public DataTable GetAppUsers()
{
    try
    {
        using (var conn = factory.CreateConnection())
        {
            conn.ConnectionString = connectionString;
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM v_AppUsers";
            var adapter = factory.CreateDataAdapter();
            adapter.SelectCommand = cmd as DbCommand;
            var dt = new DataTable();
            adapter.Fill(dt);
            return dt;
        }
    }
    catch (Exception ex)
    {
        throw new Exception($"Lỗi khi lấy danh sách user: {ex.Message}");
    }
}
```

### OracleAdminService - Call Procedure
```csharp
public void CreateUser(string username, string password)
{
    try
    {
        using (var conn = factory.CreateConnection())
        {
            conn.ConnectionString = connectionString;
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_CreateUser";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(CreateParameter("p_username", username));
            cmd.Parameters.Add(CreateParameter("p_password", password));
            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception ex)
    {
        throw new Exception($"Lỗi khi tạo user: {ex.Message}");
    }
}

private DbParameter CreateParameter(string name, object value)
{
    var param = factory.CreateParameter();
    param.ParameterName = name;
    param.Value = value ?? DBNull.Value;
    return param;
}
```

### UserService Layer
```csharp
public class UserService
{
    private readonly OracleAdminService _service;

    public UserService(OracleAdminService service) => _service = service;

    public List<User> GetAllUsers()
    {
        var dt = _service.GetAppUsers();
        var users = new List<User>();
        foreach (DataRow row in dt.Rows)
        {
            users.Add(new User
            {
                Username = row["USERNAME"].ToString(),
                AccountStatus = row["ACCOUNT_STATUS"].ToString()
            });
        }
        return users;
    }

    public void CreateUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new Exception("Username và Password không được để trống");
        _service.CreateUser(username, password);
    }
}
```

---

## VIII. CHECKLIST GỘP CODE

- [ ] Organize SQL files trong folder SQL/
- [ ] Tạo Models (User, Role, PrivilegeGrant)
- [ ] Mở rộng OracleAdminService với các methods
- [ ] Tạo Service Layer (UserService, RoleService, PrivilegeService)
- [ ] Thiết kế UI Forms
- [ ] Implement Form event handlers
- [ ] Test integration với Oracle
- [ ] Error handling toàn bộ
- [ ] Documentation & Comments

---

## IX. FILE STRUCTURE FINAL

```
atbm-httt/
├── SQL/
│   ├── 01_setup_app_admin.sql
│   ├── 02_schema_objects.sql
│   ├── 03_views_procedures.sql
│   └── 04_mock_data.sql
├── winform/
│   └── PhanHe1/
│       ├── Models/
│       ├── Services/
│       ├── Forms/
│       ├── Program.cs
│       ├── App.config
│       ├── packages.config
│       └── PhanHe1.csproj
├── ARCHITECTURE.md
└── README.md (cập nhật)
```

