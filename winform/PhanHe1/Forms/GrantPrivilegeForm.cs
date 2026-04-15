using System;
using System.Windows.Forms;
using PhanHe1;
using PhanHe1.Services;

namespace PhanHe1.Forms
{
    public partial class GrantPrivilegeForm : Form
    {
        private readonly PrivilegeService _privService;
        private ComboBox cbGranteeType;
        private ComboBox cbGrantee;
        private ComboBox cbPrivilege;
        private TextBox txtObjectName;
        private TextBox txtColumnName;
        private CheckBox chkGrantable;
        private Button btnGrant;

        public GrantPrivilegeForm(PrivilegeService privService)
        {
            InitializeComponent();
            _privService = privService;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Text = "Cấp quyền - BỆNH VIỆN DocCare";
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            // Khởi tạo controls
            cbGranteeType = new ComboBox { Left = 20, Top = 20, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cbGranteeType.Items.AddRange(new object[] { "User", "Role" });
            cbGranteeType.SelectedIndex = 0;
            cbGranteeType.SelectedIndexChanged += (s, ev) => LoadGranteeList();
            this.Controls.Add(cbGranteeType);

            cbGrantee = new ComboBox { Left = 160, Top = 20, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            this.Controls.Add(cbGrantee);

            cbPrivilege = new ComboBox { Left = 20, Top = 60, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cbPrivilege.Items.AddRange(new object[] { "SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE" });
            cbPrivilege.SelectedIndex = 0;
            this.Controls.Add(cbPrivilege);

            txtObjectName = new TextBox { Left = 190, Top = 60, Width = 120, PlaceholderText = "Object Name" };
            this.Controls.Add(txtObjectName);

            txtColumnName = new TextBox { Left = 320, Top = 60, Width = 120, PlaceholderText = "Column Name (nếu có)" };
            this.Controls.Add(txtColumnName);

            chkGrantable = new CheckBox { Left = 20, Top = 100, Text = "WITH GRANT OPTION", ForeColor = UiTheme.ZucchiniGreen };
            this.Controls.Add(chkGrantable);

            btnGrant = new Button { Left = 160, Top = 100, Text = "Cấp quyền", Width = 100 };
            btnGrant.FlatStyle = FlatStyle.Flat;
            btnGrant.FlatAppearance.BorderSize = 0;
            btnGrant.BackColor = UiTheme.PastelGreen;
            btnGrant.ForeColor = UiTheme.WhiteText;
            btnGrant.Click += BtnGrant_Click;
            this.Controls.Add(btnGrant);

            LoadGranteeList();
        }

        private void LoadGranteeList()
        {
            cbGrantee.Items.Clear();
            if (cbGranteeType.SelectedItem.ToString() == "User")
            {
                foreach (var user in _privService.GetUserNames())
                    cbGrantee.Items.Add(user);
            }
            else
            {
                foreach (var role in _privService.GetRoleNames())
                    cbGrantee.Items.Add(role);
            }
            if (cbGrantee.Items.Count > 0) cbGrantee.SelectedIndex = 0;
        }

        private void BtnGrant_Click(object sender, EventArgs e)
        {
            string grantee = cbGrantee.SelectedItem?.ToString();
            string privilege = cbPrivilege.SelectedItem?.ToString();
            string objectName = txtObjectName.Text.Trim();
            string columnName = txtColumnName.Text.Trim();
            bool grantable = chkGrantable.Checked;
            if (string.IsNullOrEmpty(grantee) || string.IsNullOrEmpty(privilege) || string.IsNullOrEmpty(objectName))
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin.");
                return;
            }
            try
            {
                // Gọi service cấp quyền
                _privService.GrantPrivilege(grantee, privilege, objectName, columnName, grantable);
                MessageBox.Show("Cấp quyền thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
            }
        }
    }
}
