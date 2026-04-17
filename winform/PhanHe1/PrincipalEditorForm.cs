using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhanHe1
{
    public class PrincipalEditorForm : Form
    {
        private readonly bool isUserMode;
        private readonly string existingName;
        private readonly bool requireAdminConfirmation;
        private TextBox txtName;
        private TextBox txtPassword;
        private TextBox txtConfirmPassword;
        private TextBox txtAdminPassword;

        public string PrincipalName
        {
            get { return txtName.Text.Trim(); }
        }

        public string PasswordValue
        {
            get { return txtPassword.Text; }
        }

        public string ConfirmPasswordValue
        {
            get { return txtConfirmPassword == null ? string.Empty : txtConfirmPassword.Text; }
        }

        public string AdminPasswordValue
        {
            get { return txtAdminPassword == null ? string.Empty : txtAdminPassword.Text; }
        }

        public PrincipalEditorForm(string title, bool isUserMode, string existingName, bool requireAdminConfirmation = false)
        {
            this.isUserMode = isUserMode;
            this.existingName = existingName;
            this.requireAdminConfirmation = requireAdminConfirmation;
            BuildUi(title);
        }

        private void BuildUi(string title)
        {
            Text = title + " - BỆNH VIỆN DocCare";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = requireAdminConfirmation ? new Size(560, 320) : new Size(460, 220);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = requireAdminConfirmation ? 6 : 4,
                BackColor = UiTheme.JordyBlue,
                Padding = new Padding(14)
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            panel.Controls.Add(new Label { Text = isUserMode ? "Tên user:" : "Tên role:", AutoSize = true }, 0, 0);
            txtName = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(txtName, 1, 0);

            panel.Controls.Add(new Label { Text = "Mật khẩu:", AutoSize = true }, 0, 1);
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Enabled = isUserMode };
            panel.Controls.Add(txtPassword, 1, 1);

            if (requireAdminConfirmation)
            {
                panel.Controls.Add(new Label { Text = "Xác nhận mật khẩu mới:", AutoSize = true }, 0, 2);
                txtConfirmPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
                panel.Controls.Add(txtConfirmPassword, 1, 2);

                panel.Controls.Add(new Label { Text = "Mật khẩu app_admin hiện tại:", AutoSize = true }, 0, 3);
                txtAdminPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
                panel.Controls.Add(txtAdminPassword, 1, 3);
            }

            if (!string.IsNullOrWhiteSpace(existingName))
            {
                txtName.Text = existingName;
                txtName.ReadOnly = true;
            }

            var btnOk = new Button { Text = "Đồng ý", Width = 120, Height = 34 };
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.BackColor = UiTheme.PastelGreen;
            btnOk.ForeColor = UiTheme.WhiteText;
            btnOk.FlatAppearance.MouseOverBackColor = Color.FromArgb(102, 235, 188);
            btnOk.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 211, 152);
            EnableRoundedButton(btnOk, 12);
            EnhanceButtonDepth(btnOk, Color.FromArgb(40, 180, 70));
            btnOk.Click += btnOk_Click;
            var btnCancel = new Button { Text = "Hủy", Width = 120, Height = 34 };
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.BackColor = UiTheme.BrandeisBlue;
            btnCancel.ForeColor = UiTheme.WhiteText;
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 147, 232);
            btnCancel.FlatAppearance.MouseDownBackColor = UiTheme.DeepBlue;
            EnableRoundedButton(btnCancel, 12);
            EnhanceButtonDepth(btnCancel, Color.FromArgb(40, 80, 200));
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            footer.Controls.Add(btnOk);
            footer.Controls.Add(btnCancel);

            panel.Controls.Add(footer, 0, requireAdminConfirmation ? 5 : 3);
            panel.SetColumnSpan(footer, 2);

            Controls.Add(panel);
            AcceptButton = btnOk;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PrincipalName))
            {
                MessageBox.Show("Bạn cần nhập tên.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (isUserMode && string.IsNullOrWhiteSpace(PasswordValue))
            {
                MessageBox.Show("Bạn cần nhập mật khẩu cho user.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (requireAdminConfirmation)
            {
                if (string.IsNullOrWhiteSpace(ConfirmPasswordValue))
                {
                    MessageBox.Show("Bạn cần nhập xác nhận mật khẩu mới.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!string.Equals(PasswordValue, ConfirmPasswordValue, StringComparison.Ordinal))
                {
                    MessageBox.Show("Mật khẩu mới và xác nhận mật khẩu không khớp.", "Sai xác nhận mật khẩu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(AdminPasswordValue))
                {
                    MessageBox.Show("Bạn cần nhập mật khẩu app_admin hiện tại để xác nhận.", "Thiếu xác thực admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
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
