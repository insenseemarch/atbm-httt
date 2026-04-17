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
            if (!string.IsNullOrEmpty(columnName))
            {
                _service.GrantObjectPrivilege(privilege, objectName, grantee, grantable, new List<string> { columnName });
                return;
            }

            if (string.IsNullOrWhiteSpace(objectName))
            {
                _service.GrantSystemPrivilege(privilege, grantee, grantable);
                return;
            }

            _service.GrantObjectPrivilege(privilege, objectName, grantee, grantable, null);
        }

        public void GrantRole(string roleName, string userName, bool adminOption)
        {
            _service.GrantRoleToUser(roleName, userName, adminOption);
        }

        public void RevokeRole(string roleName, string userName)
        {
            _service.RevokeRoleFromUser(roleName, userName);
        }

        public void RevokePrivilege(string privilegeOrRole, string principal, string objectNameOrNull)
        {
            _service.RevokePrivilege(privilegeOrRole, principal, objectNameOrNull);
        }

        // Lấy danh sách user/role cho UI
        public List<string> GetUserNames() => _service.GetUserNames();
        public List<string> GetRoleNames() => _service.GetRoleNames();
    }
}
