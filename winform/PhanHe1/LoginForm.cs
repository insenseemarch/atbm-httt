using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Oracle.ManagedDataAccess.Client;

namespace PhanHe1
{
    public class LoginForm : Form
    {
        private TextBox txtHost;
        private TextBox txtPort;
        private TextBox txtService;
        private TextBox txtUser;
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblStatus;
        private PictureBox picLogo;
        private RadioButton rdoPh1;
        private RadioButton rdoPh2;

        public OracleAdminService AuthenticatedService { get; private set; }
        /// <summary>1 = Phân Hệ 1 (Quản Trị Oracle), 2 = Phân Hệ 2 (Hệ Thống Bệnh Viện)</summary>
        public int SelectedPhase { get; private set; } = 2;

        public LoginForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Đăng nhập Admin BỆNH VIỆN";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 510);
            BackColor = Color.White;
            Font = UiTheme.BodyFont;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                BackColor = Color.White,
                Padding = new Padding(24, 18, 24, 18)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            picLogo = new PictureBox
            {
                Dock = DockStyle.Fill,
                Height = 90,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = CreateDocCareLogoImage(250, 72)
            };
            panel.Controls.Add(picLogo, 0, 0);
            panel.SetColumnSpan(picLogo, 2);

            panel.Controls.Add(new Label { Text = "Host:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 1);
            txtHost = new TextBox { Dock = DockStyle.Fill, Text = "localhost" };
            panel.Controls.Add(txtHost, 1, 1);

            panel.Controls.Add(new Label { Text = "Port:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 2);
            txtPort = new TextBox { Dock = DockStyle.Fill, Text = "1521" };
            panel.Controls.Add(txtPort, 1, 2);

            panel.Controls.Add(new Label { Text = "Service Name:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 3);
            txtService = new TextBox { Dock = DockStyle.Fill, Text = "XEPDB1" };
            panel.Controls.Add(txtService, 1, 3);

            panel.Controls.Add(new Label { Text = "Tài khoản:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 4);
            txtUser = new TextBox { Dock = DockStyle.Fill, Text = "app_admin" };
            panel.Controls.Add(txtUser, 1, 4);

            panel.Controls.Add(new Label { Text = "Mật khẩu:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 5);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            panel.Controls.Add(txtPassword, 1, 5);

            // ── Lựa chọn phân hệ (row 6)
            rdoPh1 = new RadioButton
            {
                Text = "Phân Hệ 1 — Quản Trị Oracle",
                AutoSize = true, Checked = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = UiTheme.DeepBlue,
                Margin = new Padding(0, 6, 20, 4)
            };
            rdoPh2 = new RadioButton
            {
                Text = "Phân Hệ 2 — Hệ Thống Bệnh Viện",
                AutoSize = true, Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 140, 80),
                Margin = new Padding(0, 6, 0, 4)
            };
            rdoPh1.CheckedChanged += (s, e) => { if (rdoPh1.Checked) SelectedPhase = 1; };
            rdoPh2.CheckedChanged += (s, e) => { if (rdoPh2.Checked) SelectedPhase = 2; };

            var phasePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.FromArgb(240, 248, 255),
                Padding = new Padding(6, 4, 0, 4),
                Height = 36
            };
            phasePanel.Controls.Add(new Label { Text = "Vào phân hệ:", AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = Color.Gray, Margin = new Padding(0, 7, 10, 0) });
            phasePanel.Controls.Add(rdoPh1);
            phasePanel.Controls.Add(rdoPh2);

            panel.Controls.Add(phasePanel, 0, 6);
            panel.SetColumnSpan(phasePanel, 2);

            // Auto-detect phase theo username
            txtUser.TextChanged += (s, e) =>
            {
                string u = txtUser.Text.Trim().ToUpper();
                bool isAdmin = u == "APP_ADMIN" || u == "ADMIN" || u == "SYS" || u == "SYSTEM";
                rdoPh1.Checked = isAdmin;
                rdoPh2.Checked = !isAdmin;
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.DeepBlue,
                Text = "app_admin → Phân Hệ 1.  NV0002/BS0001/KTV001/BN000001 → Phân Hệ 2 (09.sql).",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panel.Controls.Add(lblStatus, 0, 7);
            panel.SetColumnSpan(lblStatus, 2);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnLogin = new Button { Text = "Đăng nhập", Width = 140, Height = 40 };
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.BackColor = UiTheme.PastelGreen;
            btnLogin.ForeColor = UiTheme.DeepBlue;
            btnLogin.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(102, 235, 188);
            btnLogin.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 211, 152);
            EnableRoundedButton(btnLogin, 12);
            EnhanceButtonDepth(btnLogin, Color.FromArgb(40, 180, 70));
            btnLogin.Click += btnLogin_Click;

            var btnTestAcc = new Button { Text = "Tài khoản test", Width = 140, Height = 40 };
            btnTestAcc.FlatStyle = FlatStyle.Flat;
            btnTestAcc.FlatAppearance.BorderSize = 0;
            btnTestAcc.BackColor = Color.FromArgb(255, 193, 7);
            btnTestAcc.ForeColor = Color.Black;
            btnTestAcc.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnTestAcc.Click += BtnTestAcc_Click;
            EnableRoundedButton(btnTestAcc, 12);

            var btnCancel = new Button { Text = "Thoát", Width = 140, Height = 40 };
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = UiTheme.BrandeisBlue;
            btnCancel.ForeColor = UiTheme.WhiteText;
            btnCancel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 147, 232);
            btnCancel.FlatAppearance.MouseDownBackColor = UiTheme.DeepBlue;
            EnableRoundedButton(btnCancel, 12);
            EnhanceButtonDepth(btnCancel, Color.FromArgb(40, 80, 200));
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(btnLogin);
            footer.Controls.Add(btnTestAcc);
            footer.Controls.Add(btnCancel);

            panel.Controls.Add(footer, 0, 8);
            panel.SetColumnSpan(footer, 2);

            Controls.Add(panel);
            AcceptButton = btnLogin;
        }

        private void BtnTestAcc_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("── Điều phối viên (09.sql) ──", null, null).Enabled = false;
            menu.Items.Add("NV0002 / nv123", null, (s2, args) => SetTestAccount("NV0002", "nv123"));
            menu.Items.Add("NV0001 / nv123 (Giám đốc nếu CAPBAC)", null, (s2, args) => SetTestAccount("NV0001", "nv123"));
            menu.Items.Add("── Giám đốc (Ban Giám đốc) ──", null, null).Enabled = false;
            menu.Items.Add("GD0001 / nv123 (HCM)", null, (s2, args) => SetTestAccount("GD0001", "nv123"));
            menu.Items.Add("GD0002 / nv123 (Hải Phòng)", null, (s2, args) => SetTestAccount("GD0002", "nv123"));
            menu.Items.Add("GD0003 / nv123 (Hà Nội)", null, (s2, args) => SetTestAccount("GD0003", "nv123"));
            menu.Items.Add("── Bác sĩ ──", null, null).Enabled = false;
            menu.Items.Add("BS0001 / nv123", null, (s2, args) => SetTestAccount("BS0001", "nv123"));
            menu.Items.Add("BS0002 / nv123", null, (s2, args) => SetTestAccount("BS0002", "nv123"));
            menu.Items.Add("── Kỹ thuật viên ──", null, null).Enabled = false;
            menu.Items.Add("KTV001 / nv123", null, (s2, args) => SetTestAccount("KTV001", "nv123"));
            menu.Items.Add("── Bệnh nhân ──", null, null).Enabled = false;
            menu.Items.Add("BN000001 / bn123", null, (s2, args) => SetTestAccount("BN000001", "bn123"));
            menu.Items.Add("── Quản trị audit ──", null, null).Enabled = false;
            menu.Items.Add("QLBV / 123", null, (s2, args) => SetTestAccount("QLBV", "123"));

            menu.Show((Control)sender, new Point(0, ((Button)sender).Height));
        }

        private void SetTestAccount(string user, string password)
        {
            txtUser.Text = user;
            txtPassword.Text = password;
            // Các test account đều là Phase 2 (trừ app_admin)
            bool isAdmin = user.ToUpper() == "APP_ADMIN" || user.ToUpper() == "ADMIN";
            rdoPh1.Checked = isAdmin;
            rdoPh2.Checked = !isAdmin;
        }

        private static string MapLoginError(Exception ex)
        {
            OracleException oracleEx = ex as OracleException;
            if (oracleEx != null)
            {
                switch (oracleEx.Number)
                {
                    case 1017:
                        return "Sai tài khoản hoặc mật khẩu Oracle.";
                    case 12514:
                        return "Sai Service Name hoặc service chưa đăng ký trên listener.";
                    case 12541:
                        return "Không kết nối được Listener. Kiểm tra Host/Port và trạng thái Oracle.";
                    case 12170:
                        return "Kết nối Oracle bị timeout. Kiểm tra mạng hoặc firewall.";
                    case 28000:
                        return "Tài khoản Oracle đang bị khóa.";
                    default:
                        return "Không thể đăng nhập Oracle. Mã lỗi ORA-" + oracleEx.Number + ".";
                }
            }

            if (ex is InvalidOperationException)
            {
                return ex.Message;
            }

            return "Không thể đăng nhập. Vui lòng kiểm tra lại thông tin kết nối.";
        }
        private void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;
            lblStatus.ForeColor = UiTheme.DeepBlue;
            lblStatus.Text = "Đang kết nối Oracle...";

            try
            {
                AuthenticatedService = OracleAdminService.Login(
                    txtHost.Text.Trim(),
                    txtPort.Text.Trim(),
                    txtService.Text.Trim(),
                    txtUser.Text.Trim(),
                    txtPassword.Text);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                string friendlyMessage = MapLoginError(ex);

                lblStatus.ForeColor = Color.Firebrick;
                lblStatus.Text = friendlyMessage;

                MessageBox.Show(
                    friendlyMessage,
                    "Đăng nhập thất bại",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }

        private static void EnableRoundedButton(Button button, int radius)
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

        private static void EnhanceButtonDepth(Button button, Color shadowColor)
        {
            // Keep this method as a no-op to preserve call sites while avoiding custom border artifacts.
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

        private static Image CreateDocCareLogoImage(int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.White);

                var rectH = new Rectangle(20, 24, 72, 24);
                var rectV = new Rectangle(44, 10, 24, 52);
                using (var deepBrush = new SolidBrush(UiTheme.DeepBlue))
                using (var aquaBrush = new SolidBrush(UiTheme.JordyBlue))
                using (var mintBrush = new SolidBrush(UiTheme.PastelGreen))
                {
                    g.FillRectangle(deepBrush, rectH);
                    g.FillRectangle(aquaBrush, rectV);
                    g.FillEllipse(mintBrush, 48, 0, 12, 12);
                }

                using (var font = new Font("Segoe UI", 26F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var textBrush = new SolidBrush(UiTheme.DeepBlue))
                {
                    g.DrawString("DocCare", font, textBrush, new PointF(102, 15));
                }
            }

            return bmp;
        }
    }
}
