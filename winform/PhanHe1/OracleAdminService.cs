using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace PhanHe1
{
    public class OracleAdminService
    {
        private const string ProviderInvariantName = "Oracle.ManagedDataAccess.Client";
        private static readonly Regex IdentifierRegex = new Regex("^[A-Za-z][A-Za-z0-9_$#]*$", RegexOptions.Compiled);

        private readonly string connectionString;
        private readonly DbProviderFactory factory;

        public string CurrentUser { get; private set; }

        private OracleAdminService(string connectionString)
        {
            this.connectionString = connectionString;
            factory = DbProviderFactories.GetFactory(ProviderInvariantName);
        }

        public static OracleAdminService LoginAsAdmin(string host, string port, string serviceName, string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port) || string.IsNullOrWhiteSpace(serviceName)
                || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Bạn cần nhập đầy đủ thông tin kết nối.");
            }

            string connStr = string.Format(
                "User Id={0};Password={1};Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={2})(PORT={3}))(CONNECT_DATA=(SERVICE_NAME={4})));",
                userName,
                password,
                host,
                port,
                serviceName);

            var service = new OracleAdminService(connStr);
            service.ValidateAdminSession();
            return service;
        }

        public DataTable GetUsers()
        {
            try
            {
                return Query(
                    "SELECT USERNAME, ACCOUNT_STATUS FROM DBA_USERS WHERE ORACLE_MAINTAINED = 'N' ORDER BY USERNAME");
            }
            catch
            {
                return Query(
                    "SELECT USERNAME, ACCOUNT_STATUS FROM DBA_USERS " +
                    "WHERE USERNAME NOT IN ('SYS','SYSTEM','DBSNMP','SYSMAN','OUTLN','XDB','ANONYMOUS') " +
                    "ORDER BY USERNAME");
            }
        }

        public DataTable GetRoles()
        {
            try
            {
                return Query("SELECT ROLE FROM DBA_ROLES WHERE ORACLE_MAINTAINED = 'N' ORDER BY ROLE");
            }
            catch
            {
                return Query(
                    "SELECT ROLE FROM DBA_ROLES " +
                    "WHERE ROLE NOT IN ('CONNECT','RESOURCE','DBA','EXP_FULL_DATABASE','IMP_FULL_DATABASE') " +
                    "ORDER BY ROLE");
            }
        }

        public List<string> GetUserNames()
        {
            var list = new List<string>();
            DataTable dt = GetUsers();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(row["USERNAME"].ToString());
            }

            return list;
        }

        public List<string> GetRoleNames()
        {
            var list = new List<string>();
            DataTable dt = GetRoles();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(row["ROLE"].ToString());
            }

            return list;
        }

        public List<string> GetObjectsForGrant()
        {
            var list = new List<string>();
            DataTable dt = Query(
                "SELECT OBJECT_TYPE || ' | ' || OWNER || '.' || OBJECT_NAME AS DISPLAY_NAME " +
                "FROM DBA_OBJECTS " +
                "WHERE OBJECT_TYPE IN ('TABLE', 'VIEW', 'PROCEDURE', 'FUNCTION') " +
                "ORDER BY CASE OBJECT_TYPE WHEN 'TABLE' THEN 1 WHEN 'VIEW' THEN 2 WHEN 'PROCEDURE' THEN 3 WHEN 'FUNCTION' THEN 4 ELSE 5 END, OWNER, OBJECT_NAME");

            foreach (DataRow row in dt.Rows)
            {
                list.Add(row[0].ToString());
            }

            return list;
        }

        public List<string> GetColumns(string fullObjectName)
        {
            var parts = SplitOwnerAndObject(fullObjectName);
            string owner = parts.Item1;
            string objectName = parts.Item2;

            var list = new List<string>();
            
            // Thử query từ DBA_TAB_COLUMNS (cho TABLE và VIEW)
            try
            {
                DataTable dt = Query(
                    string.Format(
                        "SELECT COLUMN_NAME FROM DBA_TAB_COLUMNS WHERE OWNER = '{0}' AND TABLE_NAME = '{1}' ORDER BY COLUMN_ID",
                        owner,
                        objectName));

                foreach (DataRow row in dt.Rows)
                {
                    list.Add(row[0].ToString());
                }
            }
            catch
            {
                // Nếu không tìm thấy (PROCEDURE/FUNCTION), trả về list rỗng
            }

            return list;
        }

        public string GetObjectType(string fullObjectName)
        {
            var parts = SplitOwnerAndObject(fullObjectName);
            string owner = parts.Item1;
            string objectName = parts.Item2;

            // Kiểm tra loại object
            try
            {
                // Kiểm tra TABLE
                DataTable dtTable = Query(
                    $"SELECT OBJECT_TYPE FROM DBA_OBJECTS WHERE OWNER = '{owner}' AND OBJECT_NAME = '{objectName}' AND OBJECT_TYPE IN ('TABLE', 'VIEW')");
                if (dtTable.Rows.Count > 0)
                {
                    return dtTable.Rows[0][0].ToString();
                }

                // Kiểm tra PROCEDURE
                DataTable dtProc = Query(
                    $"SELECT OBJECT_TYPE FROM DBA_OBJECTS WHERE OWNER = '{owner}' AND OBJECT_NAME = '{objectName}' AND OBJECT_TYPE = 'PROCEDURE'");
                if (dtProc.Rows.Count > 0)
                {
                    return "PROCEDURE";
                }

                // Kiểm tra FUNCTION
                DataTable dtFunc = Query(
                    $"SELECT OBJECT_TYPE FROM DBA_OBJECTS WHERE OWNER = '{owner}' AND OBJECT_NAME = '{objectName}' AND OBJECT_TYPE = 'FUNCTION'");
                if (dtFunc.Rows.Count > 0)
                {
                    return "FUNCTION";
                }
            }
            catch { }

            return "TABLE"; // Mặc định là TABLE
        }

        public void CreateUser(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Mật khẩu không được rỗng.");
            }

            ExecuteProcedure("sp_CreateUser", new Dictionary<string, object>
            {
                { "p_username", userName },
                { "p_password", password }
            });
        }

        public void AlterUserPassword(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Mật khẩu không được rỗng.");
            }

            ExecuteProcedure("sp_ChangePassword", new Dictionary<string, object>
            {
                { "p_username", userName },
                { "p_newpassword", password }
            });
        }

        public void DropUser(string userName)
        {
            ExecuteProcedure("sp_DeleteUser", new Dictionary<string, object>
            {
                { "p_username", userName }
            });
        }

        public void LockUser(string userName, bool lockStatus)
        {
            string action = lockStatus ? "LOCK" : "UNLOCK";
            ExecuteProcedure("sp_LockUser", new Dictionary<string, object>
            {
                { "p_username", userName },
                { "p_action", action }
            });
        }

        public void CreateRole(string roleName)
        {
            ExecuteProcedure("sp_CreateRole", new Dictionary<string, object>
            {
                { "p_rolename", roleName }
            });
        }

        public void DropRole(string roleName)
        {
            ExecuteProcedure("sp_DeleteRole", new Dictionary<string, object>
            {
                { "p_rolename", roleName }
            });
        }

        public void GrantSystemPrivilege(string privilege, string principal, bool withAdminOption)
        {
            ExecuteProcedure("sp_GrantSysPrivs", new Dictionary<string, object>
            {
                { "p_privilege", privilege.Trim().ToUpperInvariant() },
                { "p_grantee", principal },
                { "p_admin_option", withAdminOption ? "YES" : "NO" }
            });
        }

        public void GrantObjectPrivilege(string privilege, string fullObjectName, string principal, bool withGrantOption, List<string> columns)
        {
            string safePrivilege = privilege.Trim().ToUpperInvariant();
            var parts = SplitOwnerAndObject(fullObjectName);
            string owner = parts.Item1;
            string objName = parts.Item2;

            // Nếu có danh sách cột và quyền là SELECT hoặc UPDATE
            if (columns != null && columns.Count > 0 && (safePrivilege == "SELECT" || safePrivilege == "UPDATE"))
            {
                // Kiểm tra principal là USER hay ROLE
                bool isRole = false;
                try
                {
                    DataTable dtRole = Query($"SELECT ROLE FROM DBA_ROLES WHERE ROLE = '{principal.ToUpperInvariant()}'");
                    if (dtRole.Rows.Count > 0) isRole = true;
                }
                catch { }

                // Nếu là ROLE, báo lỗi
                if (isRole)
                {
                    throw new InvalidOperationException("Không được chỉ định cột khi cấp quyền cho ROLE. Chỉ USER mới được cấp quyền trên cột.");
                }

                // Gọi sp_GrantColPrivs để cấp quyền trên cột
                string columnList = string.Join(",", columns);
                string grantOption = withGrantOption ? "YES" : "NO";

                ExecuteProcedure("sp_GrantColPrivs", new Dictionary<string, object>
                {
                    { "p_privilege", safePrivilege },
                    { "p_schema", owner },
                    { "p_object", objName },
                    { "p_columns", columnList },
                    { "p_grantee", principal },
                    { "p_grant_option", grantOption }
                });
            }
            else if (safePrivilege == "INSERT" || safePrivilege == "DELETE")
            {
                // INSERT, DELETE không được phép cấp mức cột
                if (columns != null && columns.Count > 0)
                {
                    throw new InvalidOperationException($"Quyền {safePrivilege} không hỗ trợ phân quyền mức cột. Chỉ cấp ở mức toàn bảng!");
                }

                // Gọi sp_GrantObjPrivs untuk cấp quyền toàn bảng
                string grantOption = withGrantOption ? "YES" : "NO";
                ExecuteProcedure("sp_GrantObjPrivs", new Dictionary<string, object>
                {
                    { "p_privilege", safePrivilege },
                    { "p_schema", owner },
                    { "p_object", objName },
                    { "p_grantee", principal },
                    { "p_grant_option", grantOption }
                });
            }
            else
            {
                // Các quyền khác cấp toàn bảng
                string grantOption = withGrantOption ? "YES" : "NO";
                ExecuteProcedure("sp_GrantObjPrivs", new Dictionary<string, object>
                {
                    { "p_privilege", safePrivilege },
                    { "p_schema", owner },
                    { "p_object", objName },
                    { "p_grantee", principal },
                    { "p_grant_option", grantOption }
                });
            }
        }

        public void GrantRoleToUser(string roleName, string userName, bool withAdminOption)
        {
            ExecuteProcedure("sp_GrantRole", new Dictionary<string, object>
            {
                { "p_role", roleName },
                { "p_grantee", userName },
                { "p_admin_option", withAdminOption ? "YES" : "NO" }
            });
        }

        public void RevokeRoleFromUser(string roleName, string userName)
        {
            ExecuteProcedure("sp_RevokeRoleUser", new Dictionary<string, object>
            {
                { "p_role", roleName },
                { "p_grantee", userName }
            });
        }

        public DataTable GetPrivileges(string principalType, string principal)
        {
            string safePrincipal = SafeIdentifier(principal);
            string type = (principalType ?? string.Empty).Trim().ToUpperInvariant();

            if (type == "USER")
            {
                return Query(
                    "SELECT OBJECT_TYPE, OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM (" +
                    "SELECT 'SYSTEM' AS OBJECT_TYPE, '' AS OBJECT_NAME, PRIVILEGE, '' AS COLUMN_NAME, ADMIN_OPTION AS GRANTABLE FROM DBA_SYS_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                    "UNION ALL " +
                    "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, " +
                    "CASE WHEN TABLE_NAME LIKE 'V$COL$%' THEN NVL((SELECT LISTAGG(c.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY c.COLUMN_ID) FROM DBA_TAB_COLUMNS c WHERE c.OWNER = DBA_TAB_PRIVS.OWNER AND c.TABLE_NAME = DBA_TAB_PRIVS.TABLE_NAME), '') ELSE '' END AS COLUMN_NAME, " +
                    "GRANTABLE FROM DBA_TAB_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                    "UNION ALL " +
                    "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM DBA_COL_PRIVS WHERE GRANTEE = '" + safePrincipal + "'" +
                    ") ORDER BY OBJECT_TYPE, OBJECT_NAME, PRIVILEGE");
            }

            return Query(
                "SELECT OBJECT_TYPE, OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM (" +
                "SELECT 'SYSTEM' AS OBJECT_TYPE, '' AS OBJECT_NAME, PRIVILEGE, '' AS COLUMN_NAME, ADMIN_OPTION AS GRANTABLE FROM DBA_SYS_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                "UNION ALL " +
                "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, " +
                "CASE WHEN TABLE_NAME LIKE 'V$COL$%' THEN NVL((SELECT LISTAGG(c.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY c.COLUMN_ID) FROM DBA_TAB_COLUMNS c WHERE c.OWNER = DBA_TAB_PRIVS.OWNER AND c.TABLE_NAME = DBA_TAB_PRIVS.TABLE_NAME), '') ELSE '' END AS COLUMN_NAME, " +
                "GRANTABLE FROM DBA_TAB_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                "UNION ALL " +
                "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM DBA_COL_PRIVS WHERE GRANTEE = '" + safePrincipal + "'" +
                ") ORDER BY OBJECT_TYPE, OBJECT_NAME, PRIVILEGE");
        }

        public void RevokePrivilege(string privilegeOrRole, string principal, string objectNameOrNull)
        {
            string safePrivilege = privilegeOrRole.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(objectNameOrNull))
            {
                // Thu hồi quyền hệ thống hoặc role
                ExecuteProcedure("sp_RevokeSysPrivs", new Dictionary<string, object>
                {
                    { "p_privilege", safePrivilege },
                    { "p_grantee", principal }
                });
            }
            else
            {
                // Thu hồi quyền trên đối tượng
                var parts = SplitOwnerAndObject(objectNameOrNull);
                string owner = parts.Item1;
                string objName = parts.Item2;

                ExecuteProcedure("sp_RevokeObjPrivs", new Dictionary<string, object>
                {
                    { "p_privilege", safePrivilege },
                    { "p_schema", owner },
                    { "p_object", objName },
                    { "p_grantee", principal }
                });
            }
        }

        private void ValidateAdminSession()
        {
            object isDba = ExecuteScalar("SELECT SYS_CONTEXT('USERENV','ISDBA') FROM DUAL");
            object sessionUser = ExecuteScalar("SELECT USER FROM DUAL");
            CurrentUser = sessionUser == null ? string.Empty : sessionUser.ToString();

            bool admin = string.Equals(Convert.ToString(isDba), "TRUE", StringComparison.OrdinalIgnoreCase);
            if (!admin)
            {
                object hasDbaRole = ExecuteScalar("SELECT COUNT(*) FROM SESSION_ROLES WHERE ROLE = 'DBA'");
                int count = 0;
                if (hasDbaRole != null)
                {
                    int.TryParse(hasDbaRole.ToString(), out count);
                }

                admin = count > 0;
            }

            if (!admin)
            {
                throw new InvalidOperationException("Tài khoản đăng nhập không phải admin/DBA trên Oracle.");
            }
        }

        private DataTable Query(string sql)
        {
            using (DbConnection conn = factory.CreateConnection())
            {
                conn.ConnectionString = connectionString;
                conn.Open();

                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (DbDataAdapter adapter = factory.CreateDataAdapter())
                    {
                        adapter.SelectCommand = cmd;
                        var table = new DataTable();
                        adapter.Fill(table);
                        return table;
                    }
                }
            }
        }

        private object ExecuteScalar(string sql)
        {
            using (DbConnection conn = factory.CreateConnection())
            {
                conn.ConnectionString = connectionString;
                conn.Open();

                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    return cmd.ExecuteScalar();
                }
            }
        }

        private void ExecuteNonQuery(string sql)
        {
            using (DbConnection conn = factory.CreateConnection())
            {
                conn.ConnectionString = connectionString;
                conn.Open();

                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ExecuteProcedure(string procName, Dictionary<string, object> parameters)
        {
            using (DbConnection conn = factory.CreateConnection())
            {
                conn.ConnectionString = connectionString;
                conn.Open();

                string resolvedProcedure = ResolveProcedureName(conn, procName);

                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = resolvedProcedure;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 30;

                    foreach (var kv in parameters)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = kv.Key;
                        param.Value = kv.Value ?? DBNull.Value;
                        cmd.Parameters.Add(param);
                    }

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Lỗi thực thi procedure {resolvedProcedure}: {ex.Message}", ex);
                    }
                }
            }
        }

        private string ResolveProcedureName(DbConnection conn, string procName)
        {
            string rawName = (procName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawName))
            {
                throw new InvalidOperationException("Tên procedure không hợp lệ.");
            }

            // If caller already provided OWNER.PROCEDURE, keep it and only validate each identifier.
            if (rawName.Contains("."))
            {
                var parts = rawName.Split('.');
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException("Tên procedure phải có dạng PROC hoặc OWNER.PROC");
                }

                return SafeIdentifier(parts[0]) + "." + SafeIdentifier(parts[1]);
            }

            string safeProcName = SafeIdentifier(rawName);

            using (DbCommand lookup = conn.CreateCommand())
            {
                lookup.CommandText =
                    "SELECT OWNER FROM (" +
                    "SELECT p.OWNER " +
                    "FROM DBA_PROCEDURES p " +
                    "LEFT JOIN DBA_USERS u ON u.USERNAME = p.OWNER " +
                    "WHERE p.OBJECT_NAME = :p_obj " +
                    "  AND p.PROCEDURE_NAME IS NULL " +
                    "  AND p.OBJECT_TYPE = 'PROCEDURE' " +
                    "  AND p.OWNER NOT IN ('SYS', 'SYSTEM') " +
                    "ORDER BY CASE " +
                    "  WHEN p.OWNER = USER THEN 0 " +
                    "  WHEN p.OWNER = 'APP_ADMIN' THEN 1 " +
                    "  WHEN NVL(u.ORACLE_MAINTAINED, 'N') = 'N' THEN 2 " +
                    "  ELSE 9 END, p.OWNER" +
                    ") WHERE ROWNUM = 1";

                var param = lookup.CreateParameter();
                param.ParameterName = "p_obj";
                param.Value = safeProcName;
                lookup.Parameters.Add(param);

                try
                {
                    object owner = lookup.ExecuteScalar();
                    if (owner != null && owner != DBNull.Value)
                    {
                        return owner.ToString().Trim().ToUpperInvariant() + "." + safeProcName;
                    }
                }
                catch
                {
                    // Fall through to ALL_PROCEDURES fallback when DBA views are restricted.
                }
            }

            using (DbCommand fallbackLookup = conn.CreateCommand())
            {
                fallbackLookup.CommandText =
                    "SELECT OWNER FROM (" +
                    "SELECT OWNER " +
                    "FROM ALL_PROCEDURES " +
                    "WHERE OBJECT_NAME = :p_obj " +
                    "  AND PROCEDURE_NAME IS NULL " +
                    "  AND OWNER NOT IN ('SYS', 'SYSTEM') " +
                    "ORDER BY CASE WHEN OWNER = USER THEN 0 WHEN OWNER = 'APP_ADMIN' THEN 1 ELSE 2 END, OWNER" +
                    ") WHERE ROWNUM = 1";

                var param = fallbackLookup.CreateParameter();
                param.ParameterName = "p_obj";
                param.Value = safeProcName;
                fallbackLookup.Parameters.Add(param);

                object owner = fallbackLookup.ExecuteScalar();
                if (owner != null && owner != DBNull.Value)
                {
                    return owner.ToString().Trim().ToUpperInvariant() + "." + safeProcName;
                }
            }

            // Fall back to current schema lookup behavior if procedure is not visible in ALL_PROCEDURES.
            return safeProcName;
        }

        private static string SafeIdentifier(string name)
        {
            string value = (name ?? string.Empty).Trim().ToUpperInvariant();
            if (!IdentifierRegex.IsMatch(value))
            {
                throw new InvalidOperationException("Tên đối tượng không hợp lệ: " + name);
            }

            return value;
        }

        private static string JoinSafeIdentifiers(List<string> values)
        {
            var safe = new List<string>();
            foreach (string value in values)
            {
                // Nếu tên cột có ký tự thường hoặc ký tự đặc biệt, thêm dấu ngoặc kép
                if (!IdentifierRegex.IsMatch(value) || value != value.ToUpperInvariant())
                {
                    safe.Add('"' + value + '"');
                }
                else
                {
                    safe.Add(SafeIdentifier(value));
                }
            }
            return string.Join(",", safe);
        }

        private static string SafeObjectName(string objectName)
        {
            var parts = SplitOwnerAndObject(objectName);
            return parts.Item1 + "." + parts.Item2;
        }

        private static Tuple<string, string> SplitOwnerAndObject(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName) || !fullName.Contains("."))
            {
                throw new InvalidOperationException("Tên đối tượng phải có dạng OWNER.OBJECT_NAME");
            }

            string[] parts = fullName.Split('.');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException("Tên đối tượng phải có dạng OWNER.OBJECT_NAME");
            }

            return Tuple.Create(SafeIdentifier(parts[0]), SafeIdentifier(parts[1]));
        }

        private static string EscapeDoubleQuotes(string value)
        {
            return value.Replace("\"", "\"\"");
        }
    }
}
