using System;
using System.Windows.Forms;
using PhanHe1.Services;

namespace PhanHe1.Forms
{
    public partial class ViewPrivilegesForm : Form
    {
        private readonly PrivilegeService _privService;
        private ComboBox cbType;
        private ComboBox cbName;
        private Button btnView;
        private DataGridView dgvPrivileges;

        public ViewPrivilegesForm(PrivilegeService privService)
        {
            InitializeComponent();
            _privService = privService;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            cbType = new ComboBox { Left = 20, Top = 20, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cbType.Items.AddRange(new object[] { "User", "Role" });
            cbType.SelectedIndex = 0;
            cbType.SelectedIndexChanged += (s, ev) => LoadNames();
            this.Controls.Add(cbType);

            cbName = new ComboBox { Left = 130, Top = 20, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            this.Controls.Add(cbName);

            btnView = new Button { Left = 300, Top = 20, Text = "Xem quyền", Width = 100 };
            btnView.Click += BtnView_Click;
            this.Controls.Add(btnView);

            dgvPrivileges = new DataGridView { Left = 20, Top = 60, Width = 500, Height = 250, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            this.Controls.Add(dgvPrivileges);

            LoadNames();
        }

        private void LoadNames()
        {
            cbName.Items.Clear();
            if (cbType.SelectedItem.ToString() == "User")
            {
                foreach (var user in _privService.GetUserNames())
                    cbName.Items.Add(user);
            }
            else
            {
                foreach (var role in _privService.GetRoleNames())
                    cbName.Items.Add(role);
            }
            if (cbName.Items.Count > 0) cbName.SelectedIndex = 0;
        }

        private void BtnView_Click(object sender, EventArgs e)
        {
            string name = cbName.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Chọn user hoặc role!");
                return;
            }
            if (cbType.SelectedItem.ToString() == "User")
            {
                dgvPrivileges.DataSource = _privService.GetUserPrivileges(name);
            }
            else
            {
                dgvPrivileges.DataSource = _privService.GetRolePrivileges(name);
            }
        }
    }
}
