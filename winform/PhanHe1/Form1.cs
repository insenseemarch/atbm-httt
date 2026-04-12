using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhanHe1
{
    public partial class Form1 : Form
    {
        private readonly OracleAdminService service;
        private readonly Color colorPrimary = Color.FromArgb(178, 152, 231);   // #B298E7
        private readonly Color colorPrimaryHover = Color.FromArgb(166, 139, 221);
        private readonly Color colorSecondary = Color.FromArgb(184, 227, 233); // #B8E3E9
        private readonly Color colorAccent = Color.FromArgb(245, 184, 213);    // #F5B8D5
        private readonly Color colorPanelBackground = Color.FromArgb(245, 184, 213); // #F5B8D5
        private readonly Color colorPageBackground = Color.FromArgb(248, 242, 253);
        private readonly Color colorCardBackground = Color.FromArgb(243, 236, 251);

        private Label lblHeader;
        private Button btnRefreshAll;
        private Button btnCreateUser;
        private Button btnCreateRole;

        private TabControl tabMain;
        private TabPage tabManage;
        private TabPage tabGrant;
        private TabPage tabPrivileges;

        private DataGridView dgvUsers;
        private DataGridView dgvRoles;

        private ComboBox cmbGrantType;
        private ComboBox cmbGrantToType;
        private ComboBox cmbGrantToName;
        private CheckBox chkGrantOption;

        private Panel pnlGrantSystem;
        private ComboBox cmbSystemPrivilege;

        private Panel pnlGrantObject;
        private ComboBox cmbObjectName;
        private ComboBox cmbObjectPrivilege;
        private CheckedListBox clbColumns;

        private Panel pnlGrantRole;
        private ComboBox cmbGrantRoleName;
        private ComboBox cmbGrantRoleToUser;

        private Button btnExecuteGrant;

        private ComboBox cmbViewType;
        private ComboBox cmbViewName;
        private Button btnLoadPrivileges;
        private DataGridView dgvPrivileges;

        public Form1(OracleAdminService service)
        {
            this.service = service;
            InitializeComponent();
            BuildUi();
            LoadInitialData();
        }

        private void BuildUi()
        {
            Text = "PHÂN HỆ 1 - QUẢN TRỊ ORACLE";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1200, 720);
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            BackColor = colorPageBackground;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = colorPanelBackground,
                Padding = new Padding(12, 10, 12, 10)
            };

            lblHeader = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(64, 42, 85),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Text = "Đăng nhập bởi: " + service.CurrentUser + " | Chỉ cho phép DBA/ADMIN"
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 500,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            btnRefreshAll = new Button { Text = "Tải lại dữ liệu", Width = 160, Height = 40, Margin = new Padding(0, 0, 10, 0) };
            StyleSecondaryButton(btnRefreshAll);
            btnRefreshAll.Click += delegate { LoadInitialData(); };

            btnCreateUser = new Button { Text = "Tạo User", Width = 150, Height = 40, Margin = new Padding(0, 0, 10, 0) };
            StylePrimaryButton(btnCreateUser);
            btnCreateUser.Click += btnCreateUser_Click;

            btnCreateRole = new Button { Text = "Tạo Role", Width = 150, Height = 40 };
            StylePrimaryButton(btnCreateRole);
            btnCreateRole.Click += btnCreateRole_Click;

            buttonPanel.Controls.Add(btnRefreshAll);
            buttonPanel.Controls.Add(btnCreateUser);
            buttonPanel.Controls.Add(btnCreateRole);

            topPanel.Controls.Add(lblHeader);
            topPanel.Controls.Add(buttonPanel);

            tabMain = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            tabManage = new TabPage("Quản lý User và Role");
            tabGrant = new TabPage("Cấp quyền bằng nút bấm");
            tabPrivileges = new TabPage("Xem quyền và thu hồi");
            tabManage.BackColor = colorPageBackground;
            tabGrant.BackColor = colorPageBackground;
            tabPrivileges.BackColor = colorPageBackground;
            tabMain.TabPages.Add(tabManage);
            tabMain.TabPages.Add(tabGrant);
            tabMain.TabPages.Add(tabPrivileges);

            BuildManageTab();
            BuildGrantTab();
            BuildPrivilegesTab();

            Controls.Add(tabMain);
            Controls.Add(topPanel);
        }

        private void BuildManageTab()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 580
            };
            split.BackColor = Color.FromArgb(224, 212, 244);
            split.Panel1.BackColor = colorPageBackground;
            split.Panel2.BackColor = colorPageBackground;

            var grpUsers = new GroupBox { Dock = DockStyle.Fill, Text = "Danh sách User", Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = colorCardBackground };
            var grpRoles = new GroupBox { Dock = DockStyle.Fill, Text = "Danh sách Role", Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = colorCardBackground };

            dgvUsers = CreateUserGrid();
            dgvRoles = CreateRoleGrid();

            grpUsers.Controls.Add(dgvUsers);
            grpRoles.Controls.Add(dgvRoles);

            split.Panel1.Controls.Add(grpUsers);
            split.Panel2.Controls.Add(grpRoles);
            tabManage.Controls.Add(split);
        }

        private void BuildGrantTab()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            root.BackColor = colorPageBackground;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 82F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 18F));

            var grp = new GroupBox { Dock = DockStyle.Fill, Text = "Cấp quyền", Font = new Font("Segoe UI", 10F, FontStyle.Bold), BackColor = colorCardBackground };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 6,
                BackColor = Color.FromArgb(236, 246, 250),
                Padding = new Padding(10)
            };

            for (int i = 0; i < 4; i++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            layout.Controls.Add(new Label { Text = "Kiểu cấp quyền:", AutoSize = true }, 0, 0);
            cmbGrantType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGrantType.Items.AddRange(new object[]
            {
                "Cấp quyền hệ thống",
                "Cấp quyền đối tượng",
                "Cấp role cho user"
            });
            cmbGrantType.SelectedIndex = 0;
            cmbGrantType.SelectedIndexChanged += delegate { UpdateGrantPanels(); };
            layout.Controls.Add(cmbGrantType, 1, 0);

            layout.Controls.Add(new Label { Text = "Cấp cho (User/Role):", AutoSize = true }, 2, 0);
            cmbGrantToType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGrantToType.Items.AddRange(new object[] { "USER", "ROLE" });
            cmbGrantToType.SelectedIndex = 0;
            cmbGrantToType.SelectedIndexChanged += delegate { LoadPrincipalCombo(cmbGrantToType, cmbGrantToName); };
            layout.Controls.Add(cmbGrantToType, 3, 0);

            layout.Controls.Add(new Label { Text = "Đối tượng nhận quyền:", AutoSize = true }, 0, 1);
            cmbGrantToName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            layout.Controls.Add(cmbGrantToName, 1, 1);

            chkGrantOption = new CheckBox { Text = "Cho phép cấp tiếp quyền này", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            layout.Controls.Add(chkGrantOption, 2, 1);

            pnlGrantSystem = new Panel { Dock = DockStyle.Fill };
            pnlGrantSystem.Controls.Add(new Label { Text = "Quyền hệ thống:", AutoSize = true, Location = new Point(0, 8) });
            cmbSystemPrivilege = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Left = 130, Top = 4 };
            cmbSystemPrivilege.Items.AddRange(new object[]
            {
                "CREATE SESSION",
                "CREATE TABLE",
                "CREATE VIEW",
                "CREATE PROCEDURE",
                "UNLIMITED TABLESPACE"
            });
            cmbSystemPrivilege.SelectedIndex = 0;
            pnlGrantSystem.Controls.Add(cmbSystemPrivilege);

            pnlGrantObject = new Panel { Dock = DockStyle.Fill };
            pnlGrantObject.Controls.Add(new Label { Text = "Đối tượng:", AutoSize = true, Location = new Point(0, 8) });
            cmbObjectName = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 290, Left = 75, Top = 4 };
            cmbObjectName.SelectedIndexChanged += cmbObjectName_SelectedIndexChanged;
            pnlGrantObject.Controls.Add(cmbObjectName);

            pnlGrantObject.Controls.Add(new Label { Text = "Quyền:", AutoSize = true, Location = new Point(380, 8) });
            cmbObjectPrivilege = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Left = 435, Top = 4 };
            cmbObjectPrivilege.Items.AddRange(new object[] { "SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE" });
            cmbObjectPrivilege.SelectedIndex = 0;
            cmbObjectPrivilege.SelectedIndexChanged += delegate { clbColumns.Enabled = cmbObjectPrivilege.Text == "SELECT" || cmbObjectPrivilege.Text == "UPDATE"; };
            pnlGrantObject.Controls.Add(cmbObjectPrivilege);

            clbColumns = new CheckedListBox { Left = 580, Top = 4, Width = 320, Height = 95, CheckOnClick = true, Enabled = true };
            pnlGrantObject.Controls.Add(clbColumns);

            pnlGrantRole = new Panel { Dock = DockStyle.Fill };
            pnlGrantRole.Controls.Add(new Label { Text = "Role cần cấp:", AutoSize = true, Location = new Point(0, 8) });
            cmbGrantRoleName = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Left = 100, Top = 4 };
            pnlGrantRole.Controls.Add(cmbGrantRoleName);

            pnlGrantRole.Controls.Add(new Label { Text = "User nhận role:", AutoSize = true, Location = new Point(380, 8) });
            cmbGrantRoleToUser = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Left = 490, Top = 4 };
            pnlGrantRole.Controls.Add(cmbGrantRoleToUser);

            layout.SetColumnSpan(pnlGrantSystem, 4);
            layout.SetColumnSpan(pnlGrantObject, 4);
            layout.SetColumnSpan(pnlGrantRole, 4);
            layout.Controls.Add(pnlGrantSystem, 0, 2);
            layout.Controls.Add(pnlGrantObject, 0, 3);
            layout.Controls.Add(pnlGrantRole, 0, 4);

            grp.Controls.Add(layout);
            root.Controls.Add(grp, 0, 0);

            var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = colorPageBackground };
            btnExecuteGrant = new Button { Text = "Thực hiện cấp quyền", Width = 260, Height = 44, Dock = DockStyle.Right };
            StylePrimaryButton(btnExecuteGrant);
            btnExecuteGrant.Click += btnExecuteGrant_Click;
            footer.Controls.Add(btnExecuteGrant);
            root.Controls.Add(footer, 0, 1);

            tabGrant.Controls.Add(root);
        }

        private void BuildPrivilegesTab()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            root.BackColor = colorPageBackground;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var top = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = colorPanelBackground };
            top.Controls.Add(new Label { Text = "Loại:", AutoSize = true, Left = 6, Top = 16 });
            cmbViewType = new ComboBox { Left = 55, Top = 12, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbViewType.Items.AddRange(new object[] { "USER", "ROLE" });
            cmbViewType.SelectedIndex = 0;
            cmbViewType.SelectedIndexChanged += delegate { LoadPrincipalCombo(cmbViewType, cmbViewName); };
            top.Controls.Add(cmbViewType);

            top.Controls.Add(new Label { Text = "Tên:", AutoSize = true, Left = 205, Top = 16 });
            cmbViewName = new ComboBox { Left = 245, Top = 12, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            top.Controls.Add(cmbViewName);

            btnLoadPrivileges = new Button { Text = "Xem quyền", Left = 545, Top = 8, Width = 160, Height = 38 };
            StylePrimaryButton(btnLoadPrivileges);
            btnLoadPrivileges.Click += btnLoadPrivileges_Click;
            top.Controls.Add(btnLoadPrivileges);

            dgvPrivileges = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            StyleGrid(dgvPrivileges);

            dgvPrivileges.Columns.Add("COL_OBJECT_TYPE", "Loại đối tượng");
            dgvPrivileges.Columns.Add("COL_OBJECT_NAME", "Tên đối tượng");
            dgvPrivileges.Columns.Add("COL_PRIVILEGE", "Quyền");
            dgvPrivileges.Columns.Add("COL_COLUMN_NAME", "Cột");
            dgvPrivileges.Columns.Add("COL_GRANTABLE", "Có thể cấp tiếp");

            var colRevoke = new DataGridViewButtonColumn
            {
                Name = "COL_REVOKE",
                HeaderText = "Thu hồi",
                Text = "Thu hồi",
                UseColumnTextForButtonValue = true
            };
            dgvPrivileges.Columns.Add(colRevoke);
            dgvPrivileges.CellContentClick += dgvPrivileges_CellContentClick;

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(dgvPrivileges, 0, 1);
            tabPrivileges.Controls.Add(root);
        }

        private DataGridView CreateUserGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 }
            };

            StyleGrid(grid);

            grid.Columns.Add("USERNAME", "User");
            grid.Columns.Add("ACCOUNT_STATUS", "Trạng thái");
            grid.Columns.Add(CreateButtonColumn("USER_ACTION", "...", "Tùy chọn"));
            grid.CellContentClick += dgvUsers_CellContentClick;
            return grid;
        }

        private DataGridView CreateRoleGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 }
            };

            StyleGrid(grid);

            grid.Columns.Add("ROLE", "Role");
            grid.Columns.Add(CreateButtonColumn("ROLE_ACTION", "...", "Tùy chọn"));
            grid.CellContentClick += dgvRoles_CellContentClick;
            return grid;
        }

        private DataGridViewButtonColumn CreateButtonColumn(string name, string text, string headerText)
        {
            return new DataGridViewButtonColumn
            {
                Name = name,
                HeaderText = headerText,
                Text = text,
                Width = 46,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Resizable = DataGridViewTriState.False,
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = colorAccent,
                    ForeColor = Color.FromArgb(66, 36, 58),
                    SelectionBackColor = colorPanelBackground,
                    SelectionForeColor = Color.Black,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
        }

        private void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = colorPrimary;
            button.ForeColor = Color.FromArgb(46, 38, 70);
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = colorPrimaryHover;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(154, 128, 206);
            EnableRoundedButton(button, 12);
            EnhanceButtonDepth(button, Color.FromArgb(122, 99, 172));
        }

        private void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = colorSecondary;
            button.ForeColor = Color.FromArgb(29, 44, 61);
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 219, 226);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(158, 207, 214);
            EnableRoundedButton(button, 12);
            EnhanceButtonDepth(button, Color.FromArgb(110, 161, 168));
        }

        private static void EnhanceButtonDepth(Button button, Color shadowColor)
        {
            // Keep this method as a no-op to preserve call sites while avoiding custom border artifacts.
        }

        private void EnableRoundedButton(Button button, int radius)
        {
            ApplyRoundedRegion(button, radius);
            button.Resize += delegate { ApplyRoundedRegion(button, radius); };
            button.Paint += delegate (object sender, PaintEventArgs e)
            {
                if (button.Width <= 2 || button.Height <= 2)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                using (var path = CreateRoundedPath(rect, Math.Max(1, Math.Min(radius, Math.Min(button.Width, button.Height) / 2 - 1))))
                using (var edgePen = new Pen(button.BackColor, 1.4f))
                {
                    e.Graphics.DrawPath(edgePen, path);
                }
            };
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 1 || control.Height <= 1)
            {
                return;
            }

            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(control.Width, control.Height) / 2 - 1));
            if (safeRadius < 1)
            {
                return;
            }

            var rect = new Rectangle(0, 0, control.Width - 1, control.Height - 1);

            using (var path = CreateRoundedPath(rect, safeRadius))
            {
                if (control.Region != null)
                {
                    control.Region.Dispose();
                }

                control.Region = new Region(path);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Color.FromArgb(241, 233, 250);
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(178, 152, 231);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(33, 24, 56);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(252, 246, 255);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(249, 190, 221);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(236, 246, 250);
            grid.GridColor = Color.FromArgb(217, 204, 234);
        }

        private void LoadInitialData()
        {
            try
            {
                LoadUserGrid();
                LoadRoleGrid();
                FillAllCombos();
                UpdateGrantPanels();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được dữ liệu từ Oracle.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadUserGrid()
        {
            dgvUsers.Rows.Clear();
            DataTable users = service.GetUsers();
            foreach (DataRow row in users.Rows)
            {
                dgvUsers.Rows.Add(row["USERNAME"].ToString(), row["ACCOUNT_STATUS"].ToString());
            }
        }

        private void LoadRoleGrid()
        {
            dgvRoles.Rows.Clear();
            DataTable roles = service.GetRoles();
            foreach (DataRow row in roles.Rows)
            {
                dgvRoles.Rows.Add(row["ROLE"].ToString());
            }
        }

        private void FillAllCombos()
        {
            LoadPrincipalCombo(cmbGrantToType, cmbGrantToName);
            LoadPrincipalCombo(cmbViewType, cmbViewName);

            var users = service.GetUserNames();
            var roles = service.GetRoleNames();
            var objects = service.GetObjectsForGrant();

            FillCombo(cmbGrantRoleToUser, users);
            FillCombo(cmbGrantRoleName, roles);
            FillCombo(cmbObjectName, objects);
        }

        private void LoadPrincipalCombo(ComboBox typeCombo, ComboBox targetCombo)
        {
            if (typeCombo == null || targetCombo == null || typeCombo.SelectedItem == null)
            {
                return;
            }

            bool user = typeCombo.SelectedItem.ToString() == "USER";
            List<string> values = user ? service.GetUserNames() : service.GetRoleNames();
            FillCombo(targetCombo, values);
        }

        private void FillCombo(ComboBox combo, List<string> values)
        {
            combo.Items.Clear();
            foreach (string value in values)
            {
                combo.Items.Add(value);
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private void UpdateGrantPanels()
        {
            int mode = cmbGrantType.SelectedIndex;
            pnlGrantSystem.Visible = mode == 0;
            pnlGrantObject.Visible = mode == 1;
            pnlGrantRole.Visible = mode == 2;

            bool isGrantRoleToUserMode = mode == 2;
            if (isGrantRoleToUserMode)
            {
                cmbGrantToType.SelectedItem = "USER";
                cmbGrantToType.Enabled = false;
                LoadPrincipalCombo(cmbGrantToType, cmbGrantToName);
            }
            else
            {
                cmbGrantToType.Enabled = true;
            }
        }

        private void btnCreateUser_Click(object sender, EventArgs e)
        {
            using (var dlg = new PrincipalEditorForm("Tạo user", true, null))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                service.CreateUser(dlg.PrincipalName, dlg.PasswordValue);
                LoadInitialData();
                MessageBox.Show("Đã tạo user thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnCreateRole_Click(object sender, EventArgs e)
        {
            using (var dlg = new PrincipalEditorForm("Tạo role", false, null))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                service.CreateRole(dlg.PrincipalName);
                LoadInitialData();
                MessageBox.Show("Đã tạo role thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void dgvUsers_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            string userName = dgvUsers.Rows[e.RowIndex].Cells["USERNAME"].Value.ToString();
            string columnName = dgvUsers.Columns[e.ColumnIndex].Name;

            try
            {
                if (columnName == "USER_ACTION")
                {
                    var menu = new ContextMenuStrip();
                    menu.Items.Add("Thêm quyền", null, delegate { SelectPrincipalForGrant("USER", userName); });
                    menu.Items.Add("Xem quyền", null, delegate { SelectPrincipalAndLoadPrivileges("USER", userName); });
                    menu.Items.Add("Sửa (đổi mật khẩu)", null, delegate { EditUserPassword(userName); });
                    menu.Items.Add("Xóa user", null, delegate { DeleteUser(userName); });

                    Rectangle rect = dgvUsers.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    menu.Show(dgvUsers, new Point(rect.Left, rect.Bottom));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Thao tác thất bại.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dgvRoles_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            string roleName = dgvRoles.Rows[e.RowIndex].Cells["ROLE"].Value.ToString();
            string columnName = dgvRoles.Columns[e.ColumnIndex].Name;

            try
            {
                if (columnName == "ROLE_ACTION")
                {
                    var menu = new ContextMenuStrip();
                    menu.Items.Add("Thêm quyền", null, delegate { SelectPrincipalForGrant("ROLE", roleName); });
                    menu.Items.Add("Xem quyền", null, delegate { SelectPrincipalAndLoadPrivileges("ROLE", roleName); });
                    menu.Items.Add("Xóa role", null, delegate { DeleteRole(roleName); });

                    Rectangle rect = dgvRoles.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    menu.Show(dgvRoles, new Point(rect.Left, rect.Bottom));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Thao tác thất bại.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditUserPassword(string userName)
        {
            using (var dlg = new PrincipalEditorForm("Đổi mật khẩu user", true, userName))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    service.AlterUserPassword(userName, dlg.PasswordValue);
                    MessageBox.Show("Đã đổi mật khẩu user.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadInitialData();
                }
            }
        }

        private void DeleteUser(string userName)
        {
            if (MessageBox.Show("Xóa user " + userName + "?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                service.DropUser(userName);
                LoadInitialData();
            }
        }

        private void DeleteRole(string roleName)
        {
            if (MessageBox.Show("Xóa role " + roleName + "?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                service.DropRole(roleName);
                LoadInitialData();
            }
        }

        private void cmbObjectName_SelectedIndexChanged(object sender, EventArgs e)
        {
            clbColumns.Items.Clear();
            if (cmbObjectName.SelectedItem == null)
            {
                return;
            }

            List<string> columns = service.GetColumns(cmbObjectName.SelectedItem.ToString());
            foreach (string col in columns)
            {
                clbColumns.Items.Add(col);
            }
        }

        private void btnExecuteGrant_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbGrantType.SelectedIndex == 0)
                {
                    service.GrantSystemPrivilege(cmbSystemPrivilege.Text, cmbGrantToName.Text, chkGrantOption.Checked);
                }
                else if (cmbGrantType.SelectedIndex == 1)
                {
                    var columns = new List<string>();
                    foreach (object selected in clbColumns.CheckedItems)
                    {
                        columns.Add(selected.ToString());
                    }

                    service.GrantObjectPrivilege(cmbObjectPrivilege.Text, cmbObjectName.Text, cmbGrantToName.Text, chkGrantOption.Checked, columns);
                }
                else
                {
                    service.GrantRoleToUser(cmbGrantRoleName.Text, cmbGrantRoleToUser.Text, chkGrantOption.Checked);
                }

                MessageBox.Show("Cấp quyền thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadInitialData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cấp quyền thất bại.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLoadPrivileges_Click(object sender, EventArgs e)
        {
            try
            {
                LoadPrivilegesGrid(cmbViewType.Text, cmbViewName.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được quyền.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadPrivilegesGrid(string principalType, string principal)
        {
            dgvPrivileges.Rows.Clear();
            DataTable data = service.GetPrivileges(principalType, principal);
            foreach (DataRow row in data.Rows)
            {
                dgvPrivileges.Rows.Add(
                    row["OBJECT_TYPE"].ToString(),
                    row["OBJECT_NAME"].ToString(),
                    row["PRIVILEGE"].ToString(),
                    row["COLUMN_NAME"].ToString(),
                    row["GRANTABLE"].ToString());
            }
        }

        private void dgvPrivileges_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (dgvPrivileges.Columns[e.ColumnIndex].Name != "COL_REVOKE")
            {
                return;
            }

            if (cmbViewName.SelectedItem == null)
            {
                return;
            }

            string privilege = dgvPrivileges.Rows[e.RowIndex].Cells["COL_PRIVILEGE"].Value.ToString();
            string objectName = dgvPrivileges.Rows[e.RowIndex].Cells["COL_OBJECT_NAME"].Value.ToString();
            string principal = cmbViewName.SelectedItem.ToString();

            try
            {
                if (string.IsNullOrWhiteSpace(objectName) || objectName == "SYSTEM")
                {
                    service.RevokePrivilege(privilege, principal, null);
                }
                else
                {
                    service.RevokePrivilege(privilege, principal, objectName);
                }

                LoadPrivilegesGrid(cmbViewType.Text, principal);
                MessageBox.Show("Đã thu hồi quyền.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Thu hồi thất bại.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectPrincipalAndLoadPrivileges(string type, string name)
        {
            tabMain.SelectedTab = tabPrivileges;
            cmbViewType.SelectedItem = type;
            LoadPrincipalCombo(cmbViewType, cmbViewName);
            cmbViewName.SelectedItem = name;
            LoadPrivilegesGrid(type, name);
        }

        private void SelectPrincipalForGrant(string type, string name)
        {
            tabMain.SelectedTab = tabGrant;

            cmbGrantType.SelectedIndex = 0;
            cmbGrantToType.SelectedItem = type;
            LoadPrincipalCombo(cmbGrantToType, cmbGrantToName);

            if (cmbGrantToName.Items.Contains(name))
            {
                cmbGrantToName.SelectedItem = name;
            }
            else if (cmbGrantToName.Items.Count > 0)
            {
                cmbGrantToName.SelectedIndex = 0;
            }

            UpdateGrantPanels();
        }
    }
}
