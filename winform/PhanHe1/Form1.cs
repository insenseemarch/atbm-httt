using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PhanHe1.Forms;

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
        private Button btnRestoreSchema;
        private Button btnSubsystem2;

        private TabControl tabMain;
        private TabPage tabManage;
        private TabPage tabGrant;
        private TabPage tabPrivileges;
        private TabPage tabAdvancedGrant;

        private DataGridView dgvUsers;
        private DataGridView dgvRoles;

        private ComboBox cmbGrantType;
        private ComboBox cmbGrantToType;
        private ComboBox cmbGrantToName;
        private Label lblGrantToType;
        private Label lblGrantToName;
        private GroupBox grpGrantOption;
        private CheckBox chkGrantOption;

        private ComboBox cmbObjectSource;

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

        private ComboBox cmbAdvancedPrivilege;
        private ComboBox cmbAdvancedTargetType;
        private ComboBox cmbAdvancedTargetName;
        private CheckBox chkAdvancedAdminOption;
        private Button btnExecuteAdvancedGrant;
        private bool advancedTabAccessVerified;
        private bool suppressAdvancedTabEvent;

        public Form1(OracleAdminService service)
        {
            this.service = service;
            InitializeComponent();
            BuildUi();
            // Only load full initial data when we built the full UI (not SYS-only minimal UI)
            if (!string.Equals(service.CurrentUser?.Trim(), "SYS", StringComparison.OrdinalIgnoreCase))
            {
                LoadInitialData();
            }
            DialogResult = DialogResult.OK;
        }

        private void BuildUi()
        {
            Text = "DocCare — Quản Trị Oracle";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1200, 720);
            Font = UiTheme.BodyFont;
            BackColor = colorPageBackground;

            // If logged in as SYS, present a minimal UI that only exposes Restore Schema
            if (string.Equals(service.CurrentUser?.Trim(), "SYS", StringComparison.OrdinalIgnoreCase))
            {
                BuildUiForSysOnly();
                return;
            }

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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
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

            btnRestoreSchema = new Button { Text = "Restore Schema", Width = 132, Height = 40, Margin = new Padding(0, 0, 8, 0) };
            StyleSecondaryButton(btnRestoreSchema);
            btnRestoreSchema.Click += BtnRestoreSchema_Click;

            btnSubsystem2 = new Button { Text = "Phân hệ 2", Width = 110, Height = 40, Margin = new Padding(0, 0, 8, 0) };
            StylePrimaryButton(btnSubsystem2);
            btnSubsystem2.Click += BtnSubsystem2_Click;


            var btnLogout = new Button { Text = "Đăng xuất", Width = 110, Height = 40, Margin = new Padding(0, 0, 0, 0) };
            StylePrimaryButton(btnLogout);
            btnLogout.Click += BtnLogout_Click;

            buttonPanel.Controls.Add(btnRefreshAll);
            buttonPanel.Controls.Add(btnCreateUser);
            buttonPanel.Controls.Add(btnCreateRole);
            // Hide top-level Restore Schema and "Phân hệ 2" for APP_ADMIN accounts
            if (!string.Equals(service.CurrentUser?.Trim(), "APP_ADMIN", StringComparison.OrdinalIgnoreCase))
                buttonPanel.Controls.Add(btnRestoreSchema);
            if (!string.Equals(service.CurrentUser?.Trim(), "APP_ADMIN", StringComparison.OrdinalIgnoreCase))
                buttonPanel.Controls.Add(btnSubsystem2);
            buttonPanel.Controls.Add(btnLogout);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.Controls.Add(headerLeft, 0, 0);
            headerLayout.Controls.Add(buttonPanel, 1, 0);

            topPanel.Controls.Add(headerLayout);

            tabMain = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            tabManage = new TabPage("Quản lý User và Role");
            tabGrant = new TabPage("Cấp quyền cơ bản");
            tabPrivileges = new TabPage("Xem quyền và thu hồi");
            tabAdvancedGrant = new TabPage("Cấp quyền nâng cao");
            tabManage.BackColor = colorPageBackground;
            tabGrant.BackColor = colorPageBackground;
            tabPrivileges.BackColor = colorPageBackground;
            tabAdvancedGrant.BackColor = colorPageBackground;
            tabMain.TabPages.Add(tabManage);
            tabMain.TabPages.Add(tabGrant);
            tabMain.TabPages.Add(tabAdvancedGrant);
            tabMain.TabPages.Add(tabPrivileges);
            tabMain.SelectedIndexChanged += TabMain_SelectedIndexChanged;

            BuildManageTab();
            BuildGrantTab();
            BuildPrivilegesTab();
            BuildAdvancedGrantTab();

            Controls.Add(tabMain);
            Controls.Add(topPanel);
        }

        private void BuildUiForSysOnly()
        {
            // Minimal header
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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            // Single big Restore button centered at bottom
            btnRestoreSchema = new Button { Text = "Restore Full Schema - Dùng khi cần khôi phục dữ liệu", Width = 360, Height = 64, AutoSize = false };
            StyleSecondaryButton(btnRestoreSchema);
            btnRestoreSchema.Click += BtnRestoreSchema_Click;

            var tip = new ToolTip();
            tip.SetToolTip(btnRestoreSchema, "Cảnh báo: thao tác này sẽ xóa và khôi phục toàn bộ schema QLBV. Chỉ dùng khi thực sự cần.");

            // Create a centered layout for the restore button
            var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            wrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var btnHolder = new Panel { AutoSize = true, Dock = DockStyle.Fill };
            btnHolder.Controls.Add(btnRestoreSchema);
            btnRestoreSchema.Anchor = AnchorStyles.None;
            wrapper.Controls.Add(btnHolder, 1, 1);

            topPanel.Controls.Add(headerLeft);
            topPanel.Controls.Add(buttonPanel);

            Controls.Add(wrapper);
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
                Text = "Cấp quyền cơ bản",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = colorCardBackground,
                Padding = new Padding(12)
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Color.FromArgb(236, 246, 250),
                Padding = new Padding(12)
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 360F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
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
            cmbGrantToType.SelectedIndexChanged += delegate { LoadPrincipalCombo(cmbGrantToType, cmbGrantToName); UpdateGrantPanels(); };
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
                RowCount = 3,
                Padding = new Padding(10, 8, 10, 10)
            };
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            detailLayout.Controls.Add(new Label { Text = "Kiểu cấp quyền:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            cmbGrantType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGrantType.Items.AddRange(new object[]
            {
                "Cấp quyền đối tượng",
                "Cấp role cho user"
            });
            cmbGrantType.SelectedIndex = 0;
            cmbGrantType.SelectedIndexChanged += delegate { UpdateGrantPanels(); };
            detailLayout.Controls.Add(cmbGrantType, 1, 0);

            detailLayout.Controls.Add(new Label { Text = "Nguồn đối tượng:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cmbObjectSource = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbObjectSource.Items.AddRange(new object[]
            {
                "Đối tượng do người dùng tạo",
                "Đối tượng của hệ thống"
            });
            cmbObjectSource.SelectedIndex = 0;
            cmbObjectSource.SelectedIndexChanged += cmbObjectSource_SelectedIndexChanged;
            detailLayout.Controls.Add(cmbObjectSource, 1, 1);

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

            detailHost.Controls.Add(pnlGrantObject);
            detailHost.Controls.Add(pnlGrantRole);
            detailLayout.Controls.Add(detailHost, 0, 2);
            detailLayout.SetColumnSpan(detailHost, 2);
            grpDetail.Controls.Add(detailLayout);

            grpGrantOption = new GroupBox
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
            grpGrantOption.Controls.Add(optionLayout);

            cmbGrantType.TabIndex = 0;
            cmbObjectSource.TabIndex = 1;
            cmbGrantToType.TabIndex = 2;
            cmbGrantToName.TabIndex = 3;
            cmbObjectType.TabIndex = 4;
            cmbObjectPrivilege.TabIndex = 5;
            cmbObjectName.TabIndex = 6;
            clbColumns.TabIndex = 7;
            chkGrantOption.TabIndex = 8;

            content.Controls.Add(grpRecipient, 0, 0);
            content.Controls.Add(grpDetail, 0, 1);
            content.Controls.Add(grpGrantOption, 0, 2);
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

        private void BuildAdvancedGrantTab()
        {
            tabAdvancedGrant.Padding = new Padding(12);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = colorPageBackground,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));

            var grp = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Cấp quyền hệ thống nâng cao",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = colorCardBackground,
                Padding = new Padding(12)
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.FromArgb(236, 246, 250),
                Padding = new Padding(12)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

            var lblWarning = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Cảnh báo: Cấp quyền nâng cao cho USER/ROLE có thể tác động trực tiếp đến CSDL, gây rủi ro bảo mật và toàn vẹn dữ liệu. Hệ thống yêu cầu xác thực lại mật khẩu trước khi sử dụng tab này và trước khi thực thi cấp quyền.",
                ForeColor = Color.FromArgb(120, 0, 0),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = false,
                Padding = new Padding(0, 0, 0, 8)
            };
            content.Controls.Add(lblWarning, 0, 0);
            content.SetColumnSpan(lblWarning, 2);

            content.Controls.Add(new Label { Text = "System Privilege:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            cmbAdvancedPrivilege = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAdvancedPrivilege.Items.AddRange(new object[]
            {
                "CREATE SESSION",
                "CREATE TABLE",
                "CREATE VIEW",
                "CREATE PROCEDURE",
                "CREATE USER",
                "CREATE ROLE"
            });
            cmbAdvancedPrivilege.SelectedIndex = 0;
            content.Controls.Add(cmbAdvancedPrivilege, 1, 1);

            var recipientPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            recipientPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            recipientPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cmbAdvancedTargetType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAdvancedTargetType.Items.AddRange(new object[] { "USER", "ROLE" });
            cmbAdvancedTargetType.SelectedIndex = 0;
            cmbAdvancedTargetType.SelectedIndexChanged += delegate { LoadAdvancedPrincipalTargets(); };

            cmbAdvancedTargetName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            recipientPanel.Controls.Add(cmbAdvancedTargetType, 0, 0);
            recipientPanel.Controls.Add(cmbAdvancedTargetName, 1, 0);

            content.Controls.Add(new Label { Text = "Đối tượng nhận quyền:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            content.Controls.Add(recipientPanel, 1, 2);

            chkAdvancedAdminOption = new CheckBox
            {
                Text = "Cho phép cấp tiếp quyền hệ thống.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Left
            };
            content.Controls.Add(new Label { Text = "Tùy chọn:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            content.Controls.Add(chkAdvancedAdminOption, 1, 3);

            grp.Controls.Add(content);
            root.Controls.Add(grp, 0, 0);

            var footer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = colorPageBackground };
            btnExecuteAdvancedGrant = new Button
            {
                Text = "Xác nhận cấp quyền nâng cao",
                Width = 300,
                Height = 44,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            btnExecuteAdvancedGrant.Location = new Point(Math.Max(12, footer.Width - btnExecuteAdvancedGrant.Width - 12), 12);
            footer.Resize += delegate
            {
                btnExecuteAdvancedGrant.Location = new Point(
                    Math.Max(12, footer.Width - btnExecuteAdvancedGrant.Width - 12),
                    Math.Max(12, footer.Height - btnExecuteAdvancedGrant.Height - 12));
            };
            StyleSecondaryButton(btnExecuteAdvancedGrant);
            btnExecuteAdvancedGrant.Click += btnExecuteAdvancedGrant_Click;
            footer.Controls.Add(btnExecuteAdvancedGrant);
            root.Controls.Add(footer, 0, 1);

            tabAdvancedGrant.Controls.Add(root);
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
            // Ensure dgvUsers exists (guard against initialization order issues)
            if (dgvUsers == null)
            {
                dgvUsers = CreateUserGrid();
                try
                {
                    // try to find existing group box inside tabManage -> SplitContainer.Panel1
                    var split = tabManage?.Controls.OfType<SplitContainer>().FirstOrDefault();
                    if (split != null)
                    {
                        var grp = split.Panel1.Controls.OfType<GroupBox>().FirstOrDefault();
                        if (grp != null)
                        {
                            grp.Controls.Add(dgvUsers);
                        }
                        else
                        {
                            split.Panel1.Controls.Add(dgvUsers);
                        }
                    }
                    else
                    {
                        tabManage?.Controls.Add(dgvUsers);
                    }
                }
                catch { /* best-effort attach */ }
            }

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

        private void cmbObjectSource_SelectedIndexChanged(object sender, EventArgs e)
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
            string objectSource = GetSelectedGrantObjectSource();
            var objects = service.GetObjectsForGrant(objectSource, objectType);
            FillCombo(cmbObjectName, objects);
        }
        private void LoadRoleGrid()
        {
            // Ensure dgvRoles exists (guard against initialization order issues)
            if (dgvRoles == null)
            {
                dgvRoles = CreateRoleGrid();
                try
                {
                    var split = tabManage?.Controls.OfType<SplitContainer>().FirstOrDefault();
                    if (split != null)
                    {
                        var grp = split.Panel2.Controls.OfType<GroupBox>().FirstOrDefault();
                        if (grp != null)
                        {
                            grp.Controls.Add(dgvRoles);
                        }
                        else
                        {
                            split.Panel2.Controls.Add(dgvRoles);
                        }
                    }
                    else
                    {
                        tabManage?.Controls.Add(dgvRoles);
                    }
                }
                catch { }
            }

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
            LoadAdvancedPrincipalTargets();

            var users = service.GetUserNames();
            var roles = service.GetRoleNames();

            FillCombo(cmbGrantRoleToUser, users);
            FillCombo(cmbGrantRoleName, roles);

            if (cmbObjectSource != null && cmbObjectSource.Items.Count > 0)
            {
                cmbObjectSource.SelectedIndex = 0;
            }

            if (cmbObjectType != null && cmbObjectType.Items.Count > 0)
            {
                cmbObjectType.SelectedIndex = 0; // tự động gọi LoadObjectsByType qua event
            }
        }

        private string GetSelectedGrantObjectSource()
        {
            if (cmbObjectSource == null || cmbObjectSource.SelectedItem == null)
            {
                return null;
            }

            return cmbObjectSource.SelectedIndex == 0 ? "USER" : "SYSTEM";
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
            if (combo == null) return;
            combo.Items.Clear();
            if (values == null) return;
            foreach (string value in values)
            {
                combo.Items.Add(value);
            }

            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private void LoadAdvancedPrincipalTargets()
        {
            if (cmbAdvancedTargetType == null || cmbAdvancedTargetName == null || cmbAdvancedTargetType.SelectedItem == null)
            {
                return;
            }

            bool targetUser = string.Equals(cmbAdvancedTargetType.SelectedItem.ToString(), "USER", StringComparison.OrdinalIgnoreCase);
            var values = targetUser ? service.GetUserNames() : service.GetRoleNames();
            FillCombo(cmbAdvancedTargetName, values);

            if (targetUser && !string.IsNullOrWhiteSpace(service.CurrentUser) && cmbAdvancedTargetName.Items.Contains(service.CurrentUser))
            {
                cmbAdvancedTargetName.SelectedItem = service.CurrentUser;
            }
        }

        private void TabMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressAdvancedTabEvent || tabMain.SelectedTab != tabAdvancedGrant)
            {
                return;
            }

            string warning = "Bạn đang vào tab Cấp quyền nâng cao. Khi cấp quyền này cho USER hoặc ROLE, đối tượng được cấp có thể tác động trực tiếp đến DB và gây rủi ro bảo mật. Hệ thống yêu cầu xác thực lại mật khẩu để tiếp tục.";
            MessageBox.Show(warning, "Cảnh báo bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (advancedTabAccessVerified)
            {
                return;
            }

            if (!PromptAndVerifyCurrentPassword("Xác thực tab nâng cao", "Nhập lại mật khẩu tài khoản admin đang đăng nhập để mở tab Cấp quyền nâng cao:"))
            {
                suppressAdvancedTabEvent = true;
                tabMain.SelectedTab = tabManage;
                suppressAdvancedTabEvent = false;
                return;
            }

            advancedTabAccessVerified = true;
        }

        private bool PromptAndVerifyCurrentPassword(string title, string instruction)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(500, 170);
                dialog.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                dialog.ShowInTaskbar = false;

                var lbl = new Label
                {
                    Text = instruction,
                    Left = 16,
                    Top = 16,
                    Width = 468,
                    Height = 48
                };

                var txt = new TextBox
                {
                    Left = 16,
                    Top = 72,
                    Width = 468,
                    UseSystemPasswordChar = true
                };

                var btnOk = new Button
                {
                    Text = "Xác nhận",
                    DialogResult = DialogResult.OK,
                    Left = 286,
                    Top = 112,
                    Width = 96
                };

                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Left = 388,
                    Top = 112,
                    Width = 96
                };

                dialog.Controls.Add(lbl);
                dialog.Controls.Add(txt);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancel);
                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("Mật khẩu không được để trống.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (!service.VerifyCurrentPassword(txt.Text))
                {
                    MessageBox.Show("Mật khẩu xác thực không đúng.", "Xác thực thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                return true;
            }
        }

        private void UpdateGrantPanels()
        {
            int mode = cmbGrantType.SelectedIndex;
            pnlGrantObject.Visible = mode == 0;
            pnlGrantRole.Visible = mode == 1;

            bool isGrantRoleToUserMode = mode == 1;
            if (isGrantRoleToUserMode)
            {
                lblGrantToType.Visible = false;
                cmbGrantToType.SelectedItem = "USER";
                cmbGrantToType.Enabled = false;
                cmbGrantToType.Visible = false;
                lblGrantToName.Visible = false;
                cmbGrantToName.Visible = false;
                LoadPrincipalCombo(cmbGrantToType, cmbGrantToName);

                // Cấp role được chọn WITH ADMIN OPTION trong nhóm 3.
                chkGrantOption.Enabled = true;
                if (grpGrantOption != null)
                {
                    grpGrantOption.Enabled = true;
                }
            }
            else
            {
                lblGrantToType.Visible = true;
                cmbGrantToType.Enabled = true;
                cmbGrantToType.Visible = true;
                lblGrantToName.Visible = true;
                cmbGrantToName.Visible = true;

                // Khi cấp quyền đối tượng: nếu cấp cho ROLE thì khóa WITH GRANT OPTION, nếu cấp cho USER thì không khóa
                bool isGrantingToRole = cmbGrantToType.SelectedItem != null && cmbGrantToType.SelectedItem.ToString() == "ROLE";
                chkGrantOption.Enabled = !isGrantingToRole;
                if (grpGrantOption != null)
                {
                    grpGrantOption.Enabled = !isGrantingToRole;
                }
                if (isGrantingToRole)
                {
                    chkGrantOption.Checked = false;
                }
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

        private void BtnSubsystem2_Click(object sender, EventArgs e)
        {
            using (var subsystem2 = new SubSystem2Form(service))
            {
                subsystem2.ShowDialog(this);
            }
        }

        private void BtnRestoreSchema_Click(object sender, EventArgs e)
        {
            if (!service.IsAdminSession)
            {
                MessageBox.Show("Restore schema chỉ dành cho tài khoản admin/DBA.", "Không đủ quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var subsystem2 = new SubSystem2Form(service, true))
            {
                subsystem2.ShowDialog(this);
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
            using (var dlg = new PrincipalEditorForm("Đổi mật khẩu user", true, userName, true))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (!service.VerifyCurrentPassword(dlg.AdminPasswordValue))
                    {
                        MessageBox.Show("Mật khẩu app_admin hiện tại không đúng.", "Xác thực thất bại", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

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
            string previousPrivilege = cmbObjectPrivilege.SelectedItem == null
                ? string.Empty
                : cmbObjectPrivilege.SelectedItem.ToString();
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
                // PROCEDURE/FUNCTION cho phép EXECUTE và DEBUG trên từng object cụ thể
                cmbObjectPrivilege.Items.AddRange(new object[] { "EXECUTE", "DEBUG" });
            }
            else
            {
                // TABLE/VIEW chỉ có SELECT/INSERT/UPDATE/DELETE
                cmbObjectPrivilege.Items.AddRange(new object[] { "SELECT", "INSERT", "UPDATE", "DELETE" });
            }

            if (!string.IsNullOrWhiteSpace(previousPrivilege) && cmbObjectPrivilege.Items.Contains(previousPrivilege))
            {
                cmbObjectPrivilege.SelectedItem = previousPrivilege;
            }
            else if (cmbObjectPrivilege.Items.Count > 0)
            {
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
            bool allowColumnGrant = cmbObjectPrivilege.Text == "SELECT" || cmbObjectPrivilege.Text == "UPDATE";
            clbColumns.Enabled = allowColumnGrant;

            // INSERT/DELETE (và các quyền khác) không dùng phân quyền cột => tự bỏ toàn bộ cột đã check.
            if (!allowColumnGrant)
            {
                for (int i = 0; i < clbColumns.Items.Count; i++)
                {
                    clbColumns.SetItemChecked(i, false);
                }
            }
        }

        private void btnExecuteGrant_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbGrantType.SelectedIndex == 0)
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

        private void btnExecuteAdvancedGrant_Click(object sender, EventArgs e)
        {
            try
            {
                if (cmbAdvancedPrivilege.SelectedItem == null)
                {
                    throw new InvalidOperationException("Vui lòng chọn system privilege cần cấp.");
                }

                if (cmbAdvancedTargetName.SelectedItem == null)
                {
                    throw new InvalidOperationException("Vui lòng chọn đối tượng nhận quyền.");
                }

                if (!PromptAndVerifyCurrentPassword("Xác thực cấp quyền nâng cao", "Nhập lại mật khẩu admin để xác nhận thao tác cấp quyền nâng cao:"))
                {
                    return;
                }

                service.GrantSystemPrivilege(
                    cmbAdvancedPrivilege.SelectedItem.ToString(),
                    cmbAdvancedTargetName.SelectedItem.ToString(),
                    chkAdvancedAdminOption.Checked);

                MessageBox.Show("Cấp quyền hệ thống nâng cao thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadInitialData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cấp quyền nâng cao thất bại.\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
