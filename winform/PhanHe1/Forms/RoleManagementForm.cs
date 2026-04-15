using System;
using System.Windows.Forms;
using PhanHe1;
using PhanHe1.Services;

namespace PhanHe1.Forms
{
    public partial class RoleManagementForm : Form
    {
        private readonly RoleService _roleService;
        private DataGridView dgvRoles;
        private TextBox txtRoleName;
        private TextBox txtDescription;
        private ComboBox cbStatus;
        private Button btnCreate;
        private Button btnDelete;
        private Button btnUpdate;

        public RoleManagementForm(RoleService roleService)
        {
            InitializeComponent();
            _roleService = roleService;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Text = "Quản lý Role - BỆNH VIỆN DocCare";
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            dgvRoles = new DataGridView { Left = 20, Top = 20, Width = 400, Height = 200, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            dgvRoles.BackgroundColor = UiTheme.LightCyan;
            dgvRoles.EnableHeadersVisualStyles = false;
            dgvRoles.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.BrandeisBlue;
            dgvRoles.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.WhiteText;
            this.Controls.Add(dgvRoles);

            txtRoleName = new TextBox { Left = 20, Top = 240, Width = 120, PlaceholderText = "Role Name" };
            this.Controls.Add(txtRoleName);
            txtDescription = new TextBox { Left = 150, Top = 240, Width = 150, PlaceholderText = "Description" };
            this.Controls.Add(txtDescription);
            cbStatus = new ComboBox { Left = 310, Top = 240, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            cbStatus.Items.AddRange(new object[] { "ACTIVE", "INACTIVE" });
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

            dgvRoles.SelectionChanged += DgvRoles_SelectionChanged;
            LoadRoles();
        }

        private void LoadRoles()
        {
            dgvRoles.DataSource = null;
            dgvRoles.DataSource = _roleService.GetAllRoles();
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            string roleName = txtRoleName.Text.Trim();
            string desc = txtDescription.Text.Trim();
            string status = cbStatus.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(roleName))
            {
                MessageBox.Show("Role name không được để trống!");
                return;
            }
            try
            {
                _roleService.CreateRole(roleName, desc, status);
                LoadRoles();
                MessageBox.Show("Tạo role thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvRoles.SelectedRows.Count == 0)
            {
                MessageBox.Show("Chọn một role để xóa!");
                return;
            }
            string roleName = dgvRoles.SelectedRows[0].Cells["RoleName"].Value.ToString();
            try
            {
                _roleService.DeleteRole(roleName);
                LoadRoles();
                MessageBox.Show("Xóa role thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (dgvRoles.SelectedRows.Count == 0)
            {
                MessageBox.Show("Chọn một role để sửa!");
                return;
            }
            string roleName = dgvRoles.SelectedRows[0].Cells["RoleName"].Value.ToString();
            string desc = txtDescription.Text.Trim();
            string status = cbStatus.SelectedItem?.ToString();
            try
            {
                _roleService.UpdateRole(roleName, desc, status);
                LoadRoles();
                MessageBox.Show("Cập nhật role thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }

        private void DgvRoles_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvRoles.SelectedRows.Count > 0)
            {
                var row = dgvRoles.SelectedRows[0];
                txtRoleName.Text = row.Cells["RoleName"].Value.ToString();
                txtDescription.Text = row.Cells["Description"].Value?.ToString();
                cbStatus.SelectedItem = row.Cells["Status"].Value?.ToString();
            }
        }
    }
}
