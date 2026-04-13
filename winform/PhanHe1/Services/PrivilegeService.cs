using System.Collections.Generic;
using System.Data;
using PhanHe1.Models;

namespace PhanHe1.Services
{
    public class PrivilegeService
    {
        private readonly OracleAdminService _service;
        public PrivilegeService(OracleAdminService service) => _service = service;

        // Ví dụ: Lấy danh sách quyền của user
        public List<PrivilegeGrant> GetUserPrivileges(string username)
        {
            // Lấy quyền object cho user từ view v_ObjectPrivs
            var dt = _service.Query($"SELECT * FROM v_ObjectPrivs WHERE GRANTEE = '{username}'");
            var privs = new List<PrivilegeGrant>();
            foreach (DataRow row in dt.Rows)
            {
                privs.Add(new PrivilegeGrant
                {
                    Grantee = row["GRANTEE"].ToString(),
                    Privilege = row["PRIVILEGE"].ToString(),
                    ObjectName = row["TABLE_NAME"].ToString(),
                    ObjectType = row.Table.Columns.Contains("OBJECT_TYPE") ? row["OBJECT_TYPE"].ToString() : null,
                    ColumnName = row.Table.Columns.Contains("COLUMN_NAME") ? row["COLUMN_NAME"].ToString() : null,
                    Grantable = row.Table.Columns.Contains("GRANTABLE") && row["GRANTABLE"].ToString() == "YES"
                });
            }
            return privs;
        }

        public List<PrivilegeGrant> GetRolePrivileges(string roleName)
        {
            // Lấy quyền role từ view v_PrivsRole
            var dt = _service.Query($"SELECT * FROM v_PrivsRole WHERE ROLE = '{roleName}'");
            var privs = new List<PrivilegeGrant>();
            foreach (System.Data.DataRow row in dt.Rows)
            {
                privs.Add(new PrivilegeGrant
                {
                    Grantee = roleName,
                    Privilege = row["PRIVILEGE"].ToString(),
                    ObjectName = row["TABLE_NAME"].ToString(),
                    ObjectType = null,
                    ColumnName = row.Table.Columns.Contains("COLUMN_NAME") ? row["COLUMN_NAME"].ToString() : null,
                    Grantable = false
                });
            }
            return privs;
        }

        public void GrantPrivilege(string grantee, string privilege, string objectName, string columnName, bool grantable)
        {
            // Gọi đúng procedure thật
            if (string.IsNullOrEmpty(columnName))
            {
                // Quyền hệ thống
                if (privilege == "CREATE SESSION" || privilege == "CREATE USER" || privilege == "CREATE ROLE")
                {
                    _service.ExecuteProcedure("sp_GrantSysPrivs", new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "p_privilege", privilege },
                        { "p_grantee", grantee },
                        { "p_admin_option", grantable ? "YES" : "NO" }
                    });
                }
                else
                {
                    // Quyền trên object
                    _service.ExecuteProcedure("sp_GrantObjPrivs", new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "p_privilege", privilege },
                        { "p_schema", "ADMIN" }, // hoặc schema phù hợp
                        { "p_object", objectName },
                        { "p_grantee", grantee },
                        { "p_grant_option", grantable ? "YES" : "NO" }
                    });
                }
            }
            else
            {
                // Quyền trên cột
                _service.ExecuteProcedure("sp_GrantColPrivs", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "p_privilege", privilege },
                    { "p_schema", "ADMIN" }, // hoặc schema phù hợp
                    { "p_object", objectName },
                    { "p_columns", columnName },
                    { "p_grantee", grantee },
                    { "p_grant_option", grantable ? "YES" : "NO" }
                });
            }
        }

        // Lấy danh sách user/role cho UI
        public List<string> GetUserNames() => _service.GetUserNames();
        public List<string> GetRoleNames() => _service.GetRoleNames();
    }
}
