using System.Collections.Generic;
using System.Data;
using PhanHe1.Models;

namespace PhanHe1.Services
{
    public class UserService
    {
        private readonly OracleAdminService _service;
        public UserService(OracleAdminService service) => _service = service;

        public List<User> GetAllUsers()
        {
            // Lấy từ view v_AppUsers
            var dt = _service.Query("SELECT * FROM v_AppUsers");
            var users = new List<User>();
            foreach (DataRow row in dt.Rows)
            {
                users.Add(new User
                {
                    Username = row["USERNAME"].ToString(),
                    AccountStatus = row.Table.Columns.Contains("ACCOUNT_STATUS") ? row["ACCOUNT_STATUS"].ToString() : null,
                    DefaultTablespace = row.Table.Columns.Contains("DEFAULT_TABLESPACE") ? row["DEFAULT_TABLESPACE"].ToString() : null,
                    Profile = row.Table.Columns.Contains("PROFILE") ? row["PROFILE"].ToString() : null
                });
            }
            return users;
        }

        public void CreateUser(string username, string password)
        {
            _service.CreateUser(username, password);
        }

        public void CreateUser(string username, string password, string tablespace, string profile, string status)
        {
            // Gọi procedure thật sp_CreateUser
            _service.ExecuteProcedure("sp_CreateUser", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_username", username },
                { "p_password", password }
                // Nếu procedure có thêm params tablespace, profile, status thì bổ sung ở đây
            });
        }

        public void DeleteUser(string username)
        {
            _service.ExecuteProcedure("sp_DeleteUser", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_username", username }
            });
        }

        public void UpdateUser(string username, string password, string tablespace, string profile, string status)
        {
            // Gọi procedure đổi mật khẩu hoặc sửa user nếu có
            _service.ExecuteProcedure("sp_ChangePassword", new System.Collections.Generic.Dictionary<string, object>
            {
                { "p_username", username },
                { "p_newpassword", password }
            });
            // Nếu có procedure sửa tablespace/profile/status thì gọi thêm ở đây
        }
        // Thêm các hàm Update, Delete nếu cần
    }
}
