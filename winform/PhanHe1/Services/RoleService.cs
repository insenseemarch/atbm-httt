using System.Collections.Generic;
using System.Data;
using PhanHe1.Models;

namespace PhanHe1.Services
{
    public class RoleService
    {
        private readonly OracleAdminService _service;
        public RoleService(OracleAdminService service) => _service = service;

        public List<Role> GetAllRoles()
        {
            // Lấy từ view v_AppRoles
            var dt = _service.Query("SELECT * FROM v_AppRoles");
            var roles = new List<Role>();
            foreach (DataRow row in dt.Rows)
            {
                roles.Add(new Role
                {
                    RoleName = row["ROLE"].ToString(),
                    Description = row.Table.Columns.Contains("AUTHENTICATION_TYPE") ? row["AUTHENTICATION_TYPE"].ToString() : null,
                    Status = null // View không có STATUS, nếu cần thì bổ sung
                });
            }
            return roles;
        }

        public void CreateRole(string roleName, string description, string status)
        {
            // Gọi procedure thật sp_CreateRole
            _service.ExecuteProcedure("sp_CreateRole", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_rolename", roleName }
                // Nếu procedure có thêm params description/status thì bổ sung ở đây
            });
        }

        public void DeleteRole(string roleName)
        {
            _service.ExecuteProcedure("sp_DeleteRole", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_rolename", roleName }
            });
        }

        public void UpdateRole(string roleName, string description, string status)
        {
            // Gọi procedure thật sp_EditRole
            _service.ExecuteProcedure("sp_EditRole", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_rolename", roleName },
                { "p_password", description } // Nếu muốn sửa password cho role
                // Nếu procedure có thêm params status thì bổ sung ở đây
            });
        }
        // Thêm các hàm Create, Update, Delete nếu cần
    }
}
