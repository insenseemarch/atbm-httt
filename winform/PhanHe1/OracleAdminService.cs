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
                "SELECT OWNER || '.' || OBJECT_NAME AS FULL_NAME " +
                "FROM DBA_OBJECTS " +
                "WHERE OBJECT_TYPE IN ('TABLE', 'VIEW', 'PROCEDURE', 'FUNCTION') " +
                "ORDER BY OWNER, OBJECT_NAME");

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
            DataTable dt = Query(
                string.Format(
                    "SELECT COLUMN_NAME FROM DBA_TAB_COLUMNS WHERE OWNER = '{0}' AND TABLE_NAME = '{1}' ORDER BY COLUMN_ID",
                    owner,
                    objectName));

            foreach (DataRow row in dt.Rows)
            {
                list.Add(row[0].ToString());
            }

            return list;
        }

        public void CreateUser(string userName, string password)
        {
            string safeUser = SafeIdentifier(userName);
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Mật khẩu không được rỗng.");
            }

            ExecuteNonQuery(string.Format("CREATE USER {0} IDENTIFIED BY \"{1}\"", safeUser, EscapeDoubleQuotes(password)));
        }

        public void AlterUserPassword(string userName, string password)
        {
            string safeUser = SafeIdentifier(userName);
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Mật khẩu không được rỗng.");
            }

            ExecuteNonQuery(string.Format("ALTER USER {0} IDENTIFIED BY \"{1}\"", safeUser, EscapeDoubleQuotes(password)));
        }

        public void DropUser(string userName)
        {
            ExecuteNonQuery(string.Format("DROP USER {0} CASCADE", SafeIdentifier(userName)));
        }

        public void CreateRole(string roleName)
        {
            ExecuteNonQuery(string.Format("CREATE ROLE {0}", SafeIdentifier(roleName)));
        }

        public void DropRole(string roleName)
        {
            ExecuteNonQuery(string.Format("DROP ROLE {0}", SafeIdentifier(roleName)));
        }

        public void GrantSystemPrivilege(string privilege, string principal, bool withAdminOption)
        {
            string sql = string.Format("GRANT {0} TO {1}", privilege.Trim().ToUpperInvariant(), SafeIdentifier(principal));
            if (withAdminOption)
            {
                sql += " WITH ADMIN OPTION";
            }

            ExecuteNonQuery(sql);
        }

        public void GrantObjectPrivilege(string privilege, string fullObjectName, string principal, bool withGrantOption, List<string> columns)
        {
            string safePrincipal = SafeIdentifier(principal);
            string safePrivilege = privilege.Trim().ToUpperInvariant();
            string safeObject = SafeObjectName(fullObjectName);

            string privilegeExpression = safePrivilege;
            if ((safePrivilege == "SELECT" || safePrivilege == "UPDATE") && columns != null && columns.Count > 0)
            {
                privilegeExpression = safePrivilege + " (" + JoinSafeIdentifiers(columns) + ")";
            }

            string sql = string.Format("GRANT {0} ON {1} TO {2}", privilegeExpression, safeObject, safePrincipal);
            if (withGrantOption)
            {
                sql += " WITH GRANT OPTION";
            }

            ExecuteNonQuery(sql);
        }

        public void GrantRoleToUser(string roleName, string userName, bool withAdminOption)
        {
            string sql = string.Format("GRANT {0} TO {1}", SafeIdentifier(roleName), SafeIdentifier(userName));
            if (withAdminOption)
            {
                sql += " WITH ADMIN OPTION";
            }

            ExecuteNonQuery(sql);
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
                    "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, '' AS COLUMN_NAME, GRANTABLE FROM DBA_TAB_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                    "UNION ALL " +
                    "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM DBA_COL_PRIVS WHERE GRANTEE = '" + safePrincipal + "'" +
                    ") ORDER BY OBJECT_TYPE, OBJECT_NAME, PRIVILEGE");
            }

            return Query(
                "SELECT OBJECT_TYPE, OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM (" +
                "SELECT 'SYSTEM' AS OBJECT_TYPE, '' AS OBJECT_NAME, PRIVILEGE, '' AS COLUMN_NAME, ADMIN_OPTION AS GRANTABLE FROM DBA_SYS_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                "UNION ALL " +
                "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, '' AS COLUMN_NAME, GRANTABLE FROM DBA_TAB_PRIVS WHERE GRANTEE = '" + safePrincipal + "' " +
                "UNION ALL " +
                "SELECT 'OBJECT' AS OBJECT_TYPE, OWNER || '.' || TABLE_NAME AS OBJECT_NAME, PRIVILEGE, COLUMN_NAME, GRANTABLE FROM DBA_COL_PRIVS WHERE GRANTEE = '" + safePrincipal + "'" +
                ") ORDER BY OBJECT_TYPE, OBJECT_NAME, PRIVILEGE");
        }

        public void RevokePrivilege(string privilegeOrRole, string principal, string objectNameOrNull)
        {
            string safePrincipal = SafeIdentifier(principal);
            string safePrivilege = privilegeOrRole.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(objectNameOrNull))
            {
                ExecuteNonQuery(string.Format("REVOKE {0} FROM {1}", safePrivilege, safePrincipal));
            }
            else
            {
                ExecuteNonQuery(string.Format("REVOKE {0} ON {1} FROM {2}", safePrivilege, SafeObjectName(objectNameOrNull), safePrincipal));
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

                using (DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = procName;
                    cmd.CommandType = CommandType.StoredProcedure;

                    foreach (var kv in parameters)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = kv.Key;
                        param.Value = kv.Value ?? DBNull.Value;
                        cmd.Parameters.Add(param);
                    }

                    cmd.ExecuteNonQuery();
                }
            }
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
                safe.Add(SafeIdentifier(value));
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
