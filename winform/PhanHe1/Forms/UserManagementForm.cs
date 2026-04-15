using System;
using System.Windows.Forms;
using PhanHe1;
using PhanHe1.Services;

namespace PhanHe1.Forms
{
    public partial class UserManagementForm : Form
    {
        private readonly UserService _userService;
        private DataGridView dgvUsers;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtDefaultTablespace;
        private TextBox txtProfile;
        private ComboBox cbStatus;
        private Button btnCreate;
        private Button btnDelete;
        private Button btnUpdate;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Text = "Quản lý User - BỆNH VIỆN DocCare";
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            _userService = new UserService();
            dgvUsers = new DataGridView { Left = 20, Top = 20, Width = 500, Height = 200, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            dgvUsers.BackgroundColor = UiTheme.LightCyan;
            dgvUsers.EnableHeadersVisualStyles = false;
            dgvUsers.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.BrandeisBlue;
            dgvUsers.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.WhiteText;
            this.Controls.Add(dgvUsers);

            txtUsername = new TextBox { Left = 20, Top = 240, Width = 100, PlaceholderText = "Username" };
            this.Controls.Add(txtUsername);
            txtPassword = new TextBox { Left = 130, Top = 240, Width = 100, PlaceholderText = "Password" };
            this.Controls.Add(txtPassword);
            txtDefaultTablespace = new TextBox { Left = 240, Top = 240, Width = 100, PlaceholderText = "Tablespace" };
            this.Controls.Add(txtDefaultTablespace);
            txtProfile = new TextBox { Left = 350, Top = 240, Width = 100, PlaceholderText = "Profile" };
            this.Controls.Add(txtProfile);
            cbStatus = new ComboBox { Left = 460, Top = 240, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cbStatus.Items.AddRange(new object[] { "OPEN", "LOCKED", "EXPIRED", "EXPIRED_LOCKED" });
            cbStatus.SelectedIndex = 0;
            this.Controls.Add(cbStatus);

            btnCreate = new Button { Left = 20, Top = 280, Text = "Tạo mới", Width = 80 };
            btnCreate.FlatStyle = FlatStyle.Flat;
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.BackColor = UiTheme.PastelGreen;
            btnCreate.ForeColor = UiTheme.WhiteText;
            btnCreate.Click += BtnCreate_Click;
            this.Controls.Add(btnCreate);
            btnDelete = new Button { Left = 110, Top = 280, Text = "Xóa", Width = 80 };
            btnDelete.FlatStyle = FlatStyle.Flat;
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.BackColor = UiTheme.BrandeisBlue;
            btnDelete.ForeColor = UiTheme.WhiteText;
            btnDelete.Click += BtnDelete_Click;
            this.Controls.Add(btnDelete);
            btnUpdate = new Button { Left = 200, Top = 280, Text = "Sửa", Width = 80 };
            btnUpdate.FlatStyle = FlatStyle.Flat;
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.BackColor = UiTheme.ZucchiniGreen;
            btnUpdate.ForeColor = UiTheme.WhiteText;
            btnUpdate.Click += BtnUpdate_Click;
            this.Controls.Add(btnUpdate);

            dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;
            LoadUsers();
        }

        private void LoadUsers()
        {
            dgvUsers.DataSource = null;
            dgvUsers.DataSource = _userService.GetAllUsers();
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text.Trim();
            string tablespace = txtDefaultTablespace.Text.Trim();
            string profile = txtProfile.Text.Trim();
            string status = cbStatus.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Username và Password không được để trống!");
                return;
            }
            try
            {
                _userService.CreateUser(username, password, tablespace, profile, status);
                LoadUsers();
                MessageBox.Show("Tạo user thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Chọn một user để xóa!");
                return;
            }
            string username = dgvUsers.SelectedRows[0].Cells["Username"].Value.ToString();
            try
            {
                _userService.DeleteUser(username);
                LoadUsers();
                MessageBox.Show("Xóa user thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (dgvUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Chọn một user để sửa!");
                return;
            }
            string username = dgvUsers.SelectedRows[0].Cells["Username"].Value.ToString();
            string password = txtPassword.Text.Trim();
            string tablespace = txtDefaultTablespace.Text.Trim();
            string profile = txtProfile.Text.Trim();
            string status = cbStatus.SelectedItem?.ToString();
            try
            {
                _userService.UpdateUser(username, password, tablespace, profile, status);
                LoadUsers();
                MessageBox.Show("Cập nhật user thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void DgvUsers_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvUsers.SelectedRows.Count > 0)
            {
                var row = dgvUsers.SelectedRows[0];
                txtUsername.Text = row.Cells["Username"].Value.ToString();
                txtPassword.Text = string.Empty; // Không hiển thị password
                txtDefaultTablespace.Text = row.Cells["DefaultTablespace"].Value?.ToString();
                txtProfile.Text = row.Cells["Profile"].Value?.ToString();
                cbStatus.SelectedItem = row.Cells["AccountStatus"].Value?.ToString();
            }
        }
    }
}
