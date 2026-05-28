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

        public OracleAdminService AuthenticatedService { get; private set; }

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
            ClientSize = new Size(620, 460);
            BackColor = Color.White;
            Font = UiTheme.BodyFont;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
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
            txtService = new TextBox { Dock = DockStyle.Fill, Text = "PDBQLBV" };
            panel.Controls.Add(txtService, 1, 3);

            panel.Controls.Add(new Label { Text = "Tài khoản:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 4);
            txtUser = new TextBox { Dock = DockStyle.Fill, Text = "app_admin" };
            panel.Controls.Add(txtUser, 1, 4);

            panel.Controls.Add(new Label { Text = "Mật khẩu:", AutoSize = true, ForeColor = UiTheme.DeepBlue, Font = UiTheme.HeaderFont }, 0, 5);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            panel.Controls.Add(txtPassword, 1, 5);

            lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.DeepBlue,
                Text = "Đăng nhập bằng tài khoản Oracle (app_admin để quản trị, DPV01/BACSI01/KTV01/BN001 để test phân hệ 2).",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true
            };
            panel.Controls.Add(lblStatus, 0, 6);
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

            panel.Controls.Add(footer, 0, 7);
            panel.SetColumnSpan(footer, 2);

            Controls.Add(panel);
            AcceptButton = btnLogin;
        }

        private void BtnTestAcc_Click(object sender, EventArgs e)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("DPV01 (Điều phối viên)", null, (s2, args) => SetTestAccount("DPV01", "DPV123"));
            menu.Items.Add("BACSI01 (Y sĩ/Bác sĩ)", null, (s2, args) => SetTestAccount("BACSI01", "BACSI123"));
            menu.Items.Add("KTV01 (Kỹ thuật viên)", null, (s2, args) => SetTestAccount("KTV01", "KTV123"));
            menu.Items.Add("BN001 (Bệnh nhân)", null, (s2, args) => SetTestAccount("BN001", "BN123"));
            menu.Items.Add("GD0001 (Giám đốc)", null, (s2, args) => SetTestAccount("GD0001", "123456"));

            menu.Show((Control)sender, new Point(0, ((Button)sender).Height));
        }

        private void SetTestAccount(string user, string password)
        {
            txtUser.Text = user;
            txtPassword.Text = password;
            MessageBox.Show($"Tài khoản test: {user}\nMật khẩu: {password}", "Tài khoản Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
