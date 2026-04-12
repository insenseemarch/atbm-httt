using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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

        public OracleAdminService AuthenticatedService { get; private set; }

        public LoginForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Đăng nhập Oracle Admin";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 360);
            BackColor = Color.FromArgb(252, 248, 255);
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                BackColor = Color.FromArgb(249, 190, 221),
                Padding = new Padding(18)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            panel.Controls.Add(new Label { Text = "Host:", AutoSize = true }, 0, 0);
            txtHost = new TextBox { Dock = DockStyle.Fill, Text = "localhost" };
            panel.Controls.Add(txtHost, 1, 0);

            panel.Controls.Add(new Label { Text = "Port:", AutoSize = true }, 0, 1);
            txtPort = new TextBox { Dock = DockStyle.Fill, Text = "1521" };
            panel.Controls.Add(txtPort, 1, 1);

            panel.Controls.Add(new Label { Text = "Service Name:", AutoSize = true }, 0, 2);
            txtService = new TextBox { Dock = DockStyle.Fill, Text = "XE" };
            panel.Controls.Add(txtService, 1, 2);

            panel.Controls.Add(new Label { Text = "Tài khoản:", AutoSize = true }, 0, 3);
            txtUser = new TextBox { Dock = DockStyle.Fill, Text = "sys" };
            panel.Controls.Add(txtUser, 1, 3);

            panel.Controls.Add(new Label { Text = "Mật khẩu:", AutoSize = true }, 0, 4);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            panel.Controls.Add(txtPassword, 1, 4);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(115, 48, 88),
                Text = "Chỉ cho phép tài khoản admin/DBA đăng nhập.",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panel.Controls.Add(lblStatus, 0, 5);
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
            btnLogin.BackColor = Color.FromArgb(178, 152, 231);
            btnLogin.ForeColor = Color.FromArgb(46, 38, 70);
            btnLogin.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(166, 139, 221);
            btnLogin.FlatAppearance.MouseDownBackColor = Color.FromArgb(154, 128, 206);
            EnableRoundedButton(btnLogin, 12);
            EnhanceButtonDepth(btnLogin, Color.FromArgb(122, 99, 172));
            btnLogin.Click += btnLogin_Click;
            var btnCancel = new Button { Text = "Thoát", Width = 140, Height = 40 };
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = Color.FromArgb(184, 227, 233);
            btnCancel.ForeColor = Color.FromArgb(29, 44, 61);
            btnCancel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 219, 226);
            btnCancel.FlatAppearance.MouseDownBackColor = Color.FromArgb(158, 207, 214);
            EnableRoundedButton(btnCancel, 12);
            EnhanceButtonDepth(btnCancel, Color.FromArgb(110, 161, 168));
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(btnLogin);
            footer.Controls.Add(btnCancel);

            panel.Controls.Add(footer, 0, 6);
            panel.SetColumnSpan(footer, 2);

            Controls.Add(panel);
            AcceptButton = btnLogin;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;
            lblStatus.Text = "Đang kết nối Oracle...";

            try
            {
                AuthenticatedService = OracleAdminService.LoginAsAdmin(
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
                lblStatus.Text = "Đăng nhập thất bại: " + ex.Message;
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
    }
}
