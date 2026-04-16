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
        private readonly Color colorPrimary = UiTheme.PastelGreen;
        private readonly Color colorPrimaryHover = Color.FromArgb(102, 235, 188);
        private readonly Color colorSecondary = UiTheme.BrandeisBlue;
        private readonly Color colorAccent = UiTheme.LightCyan;
        private readonly Color colorPanelBackground = UiTheme.BrandeisBlue;
        private readonly Color colorPageBackground = UiTheme.LightCyan;
        private readonly Color colorCardBackground = UiTheme.JordyBlue;

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
        private Label lblGrantToType;
        private Label lblGrantToName;
        private CheckBox chkGrantOption;

        private Panel pnlGrantObject;
        private ComboBox cmbObjectType;
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
            DialogResult = DialogResult.OK;
        }

        private void BuildUi()
        {
            Text = "PHÂN HỆ 1 - QUẢN TRỊ ORACLE BỆNH VIỆN";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1200, 720);
            Font = UiTheme.BodyFont;
            BackColor = colorPageBackground;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = colorPanelBackground,
                Padding = new Padding(12, 10, 12, 10)
            };

            var headerLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0),
                BackColor = Color.Transparent
            };

            var picHeaderLogo = new PictureBox
            {
                Width = 170,
                Height = 46,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = CreateHeaderLogoImage(170, 46),
                Margin = new Padding(0, 0, 10, 0)
            };

            lblHeader = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.WhiteText,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Text = "Xin chào: @" + service.CurrentUser,
                Margin = new Padding(0, 10, 0, 0)
            };

            headerLeft.Controls.Add(picHeaderLogo);
            headerLeft.Controls.Add(lblHeader);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 550,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            btnRefreshAll = new Button { Text = "Tải lại dữ liệu", Width = 130, Height = 40, Margin = new Padding(0, 0, 8, 0) };
            StylePrimaryButton(btnRefreshAll);
            btnRefreshAll.Click += delegate { LoadInitialData(); };

            btnCreateUser = new Button { Text = "Tạo User", Width = 110, Height = 40, Margin = new Padding(0, 0, 8, 0) };
            StylePrimaryButton(btnCreateUser);
            btnCreateUser.Click += btnCreateUser_Click;

            btnCreateRole = new Button { Text = "Tạo Role", Width = 110, Height = 40, Margin = new Padding(0, 0, 8, 0) };
            StylePrimaryButton(btnCreateRole);
            btnCreateRole.Click += btnCreateRole_Click;

            var btnLogout = new Button { Text = "Đăng xuất", Width = 110, Height = 40, Margin = new Padding(0, 0, 0, 0) };
            StylePrimaryButton(btnLogout);
            btnLogout.Click += BtnLogout_Click;

            buttonPanel.Controls.Add(btnRefreshAll);
            buttonPanel.Controls.Add(btnCreateUser);
            buttonPanel.Controls.Add(btnCreateRole);
            buttonPanel.Controls.Add(btnLogout);

            topPanel.Controls.Add(headerLeft);
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
            tabManage.Padding = new Padding(12);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 520
            };
            split.BackColor = Color.FromArgb(225, 245, 255);
            split.Panel1.BackColor = Color.FromArgb(245, 250, 248);
            split.Panel2.BackColor = Color.FromArgb(245, 250, 248);
            split.Panel1.Padding = new Padding(8, 8, 4, 8);
            split.Panel2.Padding = new Padding(4, 8, 8, 8);

            EventHandler keepRolePanelVisible = delegate
            {
                int totalWidth = split.ClientSize.Width;
                if (totalWidth <= 0)
                {
                    return;
                }

                int userMin = 320;
                int roleMin = 330;
                int desired = (int)(totalWidth * 0.64);
                int maxDistance = totalWidth - roleMin - split.SplitterWidth;

                if (maxDistance < userMin)
                {
                    split.SplitterDistance = Math.Max(1, totalWidth / 2);
                    return;
                }

                int safeDistance = Math.Max(userMin, Math.Min(desired, maxDistance));
                if (safeDistance > 0 && safeDistance < totalWidth)
                {
                    split.SplitterDistance = safeDistance;
                }
            };
            split.SizeChanged += delegate { keepRolePanelVisible(split, EventArgs.Empty); };
            split.HandleCreated += delegate
            {
                BeginInvoke((Action)delegate { keepRolePanelVisible(split, EventArgs.Empty); });
            };

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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));

            var grp = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Cấp quyền",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = colorCardBackground,
                Padding = new Padding(12)
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.FromArgb(236, 246, 250),
                Padding = new Padding(12)
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 218F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var grpRecipient = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Nhóm 1 - Đối tượng nhận quyền",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(236, 246, 250)
            };
            var recipientLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10, 8, 10, 8)
            };
            recipientLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            recipientLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            recipientLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            recipientLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            lblGrantToType = new Label { Text = "Cấp cho (User/Role):", AutoSize = true, Anchor = AnchorStyles.Left };
            recipientLayout.Controls.Add(lblGrantToType, 0, 0);
            cmbGrantToType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGrantToType.Items.AddRange(new object[] { "USER", "ROLE" });
            cmbGrantToType.SelectedIndex = 0;
            cmbGrantToType.SelectedIndexChanged += delegate { LoadPrincipalCombo(cmbGrantToType, cmbGrantToName); };
            recipientLayout.Controls.Add(cmbGrantToType, 1, 0);

            lblGrantToName = new Label { Text = "Đối tượng nhận quyền:", AutoSize = true, Anchor = AnchorStyles.Left };
            recipientLayout.Controls.Add(lblGrantToName, 2, 0);
            cmbGrantToName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            recipientLayout.Controls.Add(cmbGrantToName, 3, 0);
            grpRecipient.Controls.Add(recipientLayout);

            var grpDetail = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Nhóm 2 - Chi tiết quyền",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(236, 246, 250)
            };
            var detailLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(10, 8, 10, 10)
            };
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            detailLayout.Controls.Add(new Label { Text = "Kiểu cấp quyền:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cmbGrantType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGrantType.Items.AddRange(new object[]
            {
                "Cấp quyền hệ thống",
                "Cấp quyền đối tượng",
                "Cấp role cho user"
            });
            cmbGrantType.SelectedIndex = 0;
            cmbGrantType.SelectedIndexChanged += delegate { UpdateGrantPanels(); };
            detailLayout.Controls.Add(cmbGrantType, 1, 0);

            var detailHost = new Panel { Dock = DockStyle.Fill };

            pnlGrantObject = new Panel { Dock = DockStyle.Fill };
            var objLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3
            };
            objLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            objLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            objLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            objLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            objLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            objLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            objLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Row 0: Loại đối tượng + quyền
            objLayout.Controls.Add(new Label { Text = "Loại đối tượng:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cmbObjectType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbObjectType.Items.AddRange(new object[] { "TABLE", "VIEW", "PROCEDURE", "FUNCTION" });
            cmbObjectType.SelectedIndexChanged += cmbObjectType_SelectedIndexChanged;
            objLayout.Controls.Add(cmbObjectType, 1, 0);

            objLayout.Controls.Add(new Label { Text = "Quyền:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            cmbObjectPrivilege = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbObjectPrivilege.SelectedIndexChanged += CmbObjectPrivilege_SelectedIndexChanged;
            objLayout.Controls.Add(cmbObjectPrivilege, 3, 0);

            // Row 1: Tên object
            objLayout.Controls.Add(new Label { Text = "Tên đối tượng:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cmbObjectName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbObjectName.SelectedIndexChanged += cmbObjectName_SelectedIndexChanged;
            objLayout.Controls.Add(cmbObjectName, 1, 1);
            objLayout.SetColumnSpan(cmbObjectName, 3);

            // Row 2: Cột
            objLayout.Controls.Add(new Label { Text = "Tên cột (nếu có):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            clbColumns = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, Enabled = true };
            objLayout.Controls.Add(clbColumns, 1, 2);
            objLayout.SetColumnSpan(clbColumns, 3);

            pnlGrantObject.Controls.Add(objLayout);

            pnlGrantRole = new Panel { Dock = DockStyle.Fill };
            var roleLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 1,
                Height = 40
            };
            roleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            roleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            roleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            roleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            roleLayout.Controls.Add(new Label { Text = "Role cần cấp:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cmbGrantRoleName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            roleLayout.Controls.Add(cmbGrantRoleName, 1, 0);
            roleLayout.Controls.Add(new Label { Text = "User nhận role:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            cmbGrantRoleToUser = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            roleLayout.Controls.Add(cmbGrantRoleToUser, 3, 0);
            pnlGrantRole.Controls.Add(roleLayout);

            detailHost.Controls.Add(pnlGrantSystem);
            detailHost.Controls.Add(pnlGrantObject);
            detailHost.Controls.Add(pnlGrantRole);
            detailLayout.Controls.Add(detailHost, 0, 1);
            detailLayout.SetColumnSpan(detailHost, 2);
            grpDetail.Controls.Add(detailLayout);

            var grpOption = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Nhóm 3 - Tùy chọn thêm",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(236, 246, 250)
            };
            var optionLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10, 8, 10, 8),
                WrapContents = false
            };
            chkGrantOption = new CheckBox { Text = "WITH GRANT OPTION (Cho phép cấp tiếp quyền này)", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            optionLayout.Controls.Add(chkGrantOption);
            grpOption.Controls.Add(optionLayout);

            content.Controls.Add(grpRecipient, 0, 0);
            content.Controls.Add(grpDetail, 0, 1);
            content.Controls.Add(grpOption, 0, 2);
            grp.Controls.Add(content);
            root.Controls.Add(grp, 0, 0);

            var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = colorPageBackground };
            btnExecuteGrant = new Button { Text = "Thực hiện cấp quyền", Width = 260, Height = 44, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            btnExecuteGrant.Location = new Point(Math.Max(12, footer.Width - btnExecuteGrant.Width - 12), 12);
            footer.Resize += delegate
            {
                btnExecuteGrant.Location = new Point(Math.Max(12, footer.Width - btnExecuteGrant.Width - 12), Math.Max(12, footer.Height - btnExecuteGrant.Height - 12));
            };
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
                ColumnCount = 1,
                Padding = new Padding(8, 8, 8, 8)
            };
            root.BackColor = colorPageBackground;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var top = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = colorPanelBackground };
            top.Margin = new Padding(0, 0, 0, 10);
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
            dgvPrivileges.Margin = new Padding(0);
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
                Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(255, 232, 232),
                    ForeColor = Color.FromArgb(168, 28, 28),
                    SelectionBackColor = Color.FromArgb(250, 205, 205),
                    SelectionForeColor = Color.FromArgb(120, 0, 0),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 }
            };

            StyleGrid(grid);

            grid.Columns.Add("USERNAME", "User");
            grid.Columns.Add("ACCOUNT_STATUS", "Trạng thái");
            grid.Columns.Add(CreateButtonColumn("USER_ACTION", "Thao tác", "Thao tác"));

            grid.Columns["USERNAME"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["USERNAME"].MinimumWidth = 170;
            grid.Columns["ACCOUNT_STATUS"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["ACCOUNT_STATUS"].Width = 100;
            grid.Columns["USER_ACTION"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["USER_ACTION"].Width = 96;

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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 }
            };

            StyleGrid(grid);

            grid.Columns.Add("ROLE", "Role");
            grid.Columns.Add(CreateButtonColumn("ROLE_ACTION", "Thao tác", "Thao tác"));

            grid.Columns["ROLE"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns["ROLE"].MinimumWidth = 220;
            grid.Columns["ROLE_ACTION"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["ROLE_ACTION"].Width = 96;

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
                Width = 96,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Resizable = DataGridViewTriState.False,
                UseColumnTextForButtonValue = true,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = colorAccent,
                    ForeColor = UiTheme.DeepBlue,
                    SelectionBackColor = colorPanelBackground,
                    SelectionForeColor = UiTheme.WhiteText,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
        }

        private static Image CreateHeaderLogoImage(int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                var rectH = new Rectangle(8, 18, 42, 14);
                var rectV = new Rectangle(22, 8, 14, 30);
                using (var deepBrush = new SolidBrush(UiTheme.DeepBlue))
                using (var aquaBrush = new SolidBrush(UiTheme.JordyBlue))
                using (var mintBrush = new SolidBrush(UiTheme.PastelGreen))
                {
                    g.FillRectangle(deepBrush, rectH);
                    g.FillRectangle(aquaBrush, rectV);
                    g.FillEllipse(mintBrush, 24, 0, 10, 10);
                }

                using (var font = new Font("Segoe UI", 19F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var textBrush = new SolidBrush(UiTheme.WhiteText))
                {
                    g.DrawString("DocCare", font, textBrush, new PointF(58, 9));
                }
            }

            return bmp;
        }

        private void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = colorPrimary;
            button.ForeColor = UiTheme.DeepBlue;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = colorPrimaryHover;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 211, 152);
            EnableRoundedButton(button, 12);
            EnhanceButtonDepth(button, Color.FromArgb(40, 180, 70));
        }

        private void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = colorSecondary;
            button.ForeColor = UiTheme.WhiteText;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 147, 232);
            button.FlatAppearance.MouseDownBackColor = UiTheme.DeepBlue;
            EnableRoundedButton(button, 12);
            EnhanceButtonDepth(button, Color.FromArgb(40, 80, 200));
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
            UiTheme.StyleGrid(grid);
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
        private void cmbObjectType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadObjectsByType();
        }

        private void LoadObjectsByType()
        {
            cmbObjectName.Items.Clear();
            cmbObjectPrivilege.Items.Clear();
            clbColumns.Items.Clear();

            if (cmbObjectType.SelectedItem == null)
            {
                return;
            }

            string objectType = cmbObjectType.SelectedItem.ToString();
            var objects = service.GetObjectsForGrant(objectType);
            FillCombo(cmbObjectName, objects);
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

            FillCombo(cmbGrantRoleToUser, users);
            FillCombo(cmbGrantRoleName, roles);

            if (cmbObjectType != null && cmbObjectType.Items.Count > 0)
            {
                cmbObjectType.SelectedIndex = 0; // tự động gọi LoadObjectsByType qua event
            }
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
                lblGrantToType.Visible = false;
                cmbGrantToType.SelectedItem = "USER";
                cmbGrantToType.Enabled = false;
                cmbGrantToType.Visible = false;
                lblGrantToName.Visible = false;
                cmbGrantToName.Visible = false;
                LoadPrincipalCombo(cmbGrantToType, cmbGrantToName);
            }
            else
            {
                lblGrantToType.Visible = true;
                cmbGrantToType.Enabled = true;
                cmbGrantToType.Visible = true;
                lblGrantToName.Visible = true;
                cmbGrantToName.Visible = true;
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
            string accountStatus = dgvUsers.Rows[e.RowIndex].Cells["ACCOUNT_STATUS"].Value.ToString();
            string columnName = dgvUsers.Columns[e.ColumnIndex].Name;

            try
            {
                if (columnName == "USER_ACTION")
                {
                    var menu = new ContextMenuStrip();
                    menu.Items.Add("Thêm quyền", null, delegate { SelectPrincipalForGrant("USER", userName); });
                    menu.Items.Add("Xem quyền", null, delegate { SelectPrincipalAndLoadPrivileges("USER", userName); });
                    bool isLocked = IsUserLocked(accountStatus);
                    menu.Items.Add(isLocked ? "Mở khóa tài khoản" : "Khóa tài khoản", null, delegate { ToggleUserStatus(userName, accountStatus); });
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

        private void ToggleUserStatus(string userName, string currentStatus)
        {
            bool isLocked = IsUserLocked(currentStatus);
            bool lockStatus = !isLocked;
            string actionText = lockStatus ? "khóa" : "mở khóa";

            if (MessageBox.Show("Bạn có chắc muốn " + actionText + " tài khoản " + userName + "?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            service.LockUser(userName, lockStatus);
            LoadInitialData();
            MessageBox.Show("Đã " + actionText + " tài khoản " + userName + ".", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool IsUserLocked(string accountStatus)
        {
            return !string.IsNullOrWhiteSpace(accountStatus)
                && accountStatus.IndexOf("LOCKED", StringComparison.OrdinalIgnoreCase) >= 0;
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
            cmbObjectPrivilege.Items.Clear();
            
            if (cmbObjectName.SelectedItem == null)
            {
                return;
            }

            Tuple<string, string> objectInfo = ParseGrantObjectSelection();
            if (objectInfo == null)
            {
                return;
            }

            string objectType = objectInfo.Item1;
            string fullObjectName = objectInfo.Item2;
            
            // Lọc quyền dựa trên loại object
            if (objectType == "PROCEDURE" || objectType == "FUNCTION")
            {
                // PROCEDURE/FUNCTION chỉ có quyền EXECUTE
                cmbObjectPrivilege.Items.Add("EXECUTE");
                cmbObjectPrivilege.SelectedIndex = 0;
            }
            else
            {
                // TABLE/VIEW có tất cả quyền
                cmbObjectPrivilege.Items.AddRange(new object[] { "SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE" });
                cmbObjectPrivilege.SelectedIndex = 0;
            }
            
            // Lấy danh sách cột (nếu không phải PROCEDURE/FUNCTION)
            if (objectType == "TABLE" || objectType == "VIEW")
            {
                List<string> columns = service.GetColumns(fullObjectName);
                foreach (string col in columns)
                {
                    clbColumns.Items.Add(col);
                }
            }
        }

        private Tuple<string, string> ParseGrantObjectSelection()
        {
            if (cmbObjectName.SelectedItem == null)
            {
                return null;
            }

            string displayText = cmbObjectName.SelectedItem.ToString();
            string[] parts = displayText.Split(new[] { " | " }, 2, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                return Tuple.Create(parts[0], parts[1]);
            }

            return Tuple.Create(service.GetObjectType(displayText), displayText);
        }

        private void CmbObjectPrivilege_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Chỉ enable columns cho SELECT hoặc UPDATE
            clbColumns.Enabled = cmbObjectPrivilege.Text == "SELECT" || cmbObjectPrivilege.Text == "UPDATE";
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
                    Tuple<string, string> objectInfo = ParseGrantObjectSelection();
                    if (objectInfo == null)
                    {
                        throw new InvalidOperationException("Vui lòng chọn đối tượng cấp quyền.");
                    }

                    string fullObjectName = objectInfo.Item2;

                    var columns = new List<string>();
                    foreach (object selected in clbColumns.CheckedItems)
                    {
                        columns.Add(selected.ToString());
                    }

                    service.GrantObjectPrivilege(cmbObjectPrivilege.Text, fullObjectName, cmbGrantToName.Text, chkGrantOption.Checked, columns);
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
            string objectType = dgvPrivileges.Rows[e.RowIndex].Cells["COL_OBJECT_TYPE"].Value.ToString();
            string objectName = dgvPrivileges.Rows[e.RowIndex].Cells["COL_OBJECT_NAME"].Value.ToString();
            string principal = cmbViewName.SelectedItem.ToString();

            try
            {
                if (string.Equals(cmbViewType.Text, "USER", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(objectType, "ROLE", StringComparison.OrdinalIgnoreCase))
                {
                    service.RevokeRoleFromUser(objectName, principal);
                }
                else if (string.IsNullOrWhiteSpace(objectName) || objectName == "SYSTEM")
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

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
    }
}
