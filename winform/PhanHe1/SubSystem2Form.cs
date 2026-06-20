using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PhanHe1.Forms
{
    // ════════════════════════════════════════════════════════════════════════
    // [GỘPCODE-TỔNG] Gộp từ admin_ph2.sql + sys_PH2.sql vào WinForms
    //   ✅ [1] Schema prefix toàn bộ đổi APP_ADMIN.* → QLBV.* (107 vị trí)
    //          Nguồn: schema_phanhe2.sql — tất cả bảng thuộc schema QLBV.
    //          Ý nghĩa: VPD (fn_vpd*) và OLS (OLS_QLBV_POLICY) chỉ hoạt
    //          động trên bảng QLBV — dùng sai schema là hoàn toàn mất bảo mật.
    //   ✅ [2] KTV: tên cột LOAIDV/NGAYDV đúng theo DDL (xem BuildKTV_DV)
    //   ✅ [3] BS delete HSBA_DV: WHERE đủ PK (MAHSBA, LOAIDV, NGAYDV) (xem Bs_XoaDV)
    //   ✅ [4] DONTHUOC: chỉ có MAHSBA, NGAYDT, TENTHUOC, LIEUDUNG (không có TRANGTHAI)
    //   ✅ [5] Audit SQL: filter OBJECT_SCHEMA='QLBV' + FGA (xem LoadAuditData)
    // ════════════════════════════════════════════════════════════════════════
    public class SubSystem2Form : Form
    {
        private enum UserRole { Unknown, DPV, BACSI, KTV, BN, ADMIN, GIAMDOC }
        private readonly OracleAdminService service;
        private readonly string currentUser;
        private readonly List<string> currentRoles;
        private readonly UserRole userRole;

        // BN/NV form fields
        private TextBox txtBnFullName, txtBnCccd, txtBnGender, txtBnDob;
        private TextBox txtBnSonha, txtBnDuong, txtBnQuan, txtBnTinh;
        private TextBox txtBnMedicalHistory, txtBnFamilyHistory, txtBnDrugAllergies;
        private TextBox txtNvFullName, txtNvGender, txtNvDob, txtNvCmnd, txtNvRole, txtNvDept, txtNvCoSo, txtNvAddress, txtNvPhone;

        public SubSystem2Form(OracleAdminService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            currentUser = service.CurrentUser ?? string.Empty;
            currentRoles = service.GetCurrentRoles() ?? new List<string>();
            userRole = DetermineUserRole();

            Text = $"PHÂN HỆ 2 - {GetRoleTitle()}";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.LightCyan;
            BuildUi();
        }

        // ── role detection ───────────────────────────────────────────────
        // ✅ [GỘPCODE-1] ROLE_DPV, ROLE_BACSI, ROLE_KTV, ROLE_BENHNHAN đã đúng
        //    Nguồn: admin_ph2.sql — các role được tạo và grant trên bảng QLBV.*.
        // ✅ [GỘPCODE-A] GIAMDOC: detect via CAPBAC='Ban Giám đốc' từ QLBV.NHANVIEN
        //    Nguồn: admin_ph2.sql — u1: NV0001 có CAPBAC='Ban Giám đốc', OLS label BGD.
        //    SQL không tạo ROLE_GIAMDOC — Giám đốc là nhân viên có CAPBAC cao nhất.
        private UserRole DetermineUserRole()
        {
            if (currentRoles.Contains("DBA") || string.Equals(currentUser, "APP_ADMIN", StringComparison.OrdinalIgnoreCase))
                return UserRole.ADMIN;

            // Giám đốc trước DPV — NV0001 có CAPBAC 'Ban Giám đốc' (09.sql / admin_ph2)
            try
            {
                var dtCap = service.Query($"SELECT CAPBAC FROM QLBV.NHANVIEN WHERE MANV = '{Esc(currentUser)}'");
                if (dtCap.Rows.Count > 0 && dtCap.Rows[0]["CAPBAC"]?.ToString() == "Ban Giám đốc")
                    return UserRole.GIAMDOC;
            }
            catch { }
            if (currentUser.StartsWith("GD", StringComparison.OrdinalIgnoreCase))
                return UserRole.GIAMDOC;

            if (currentRoles.Contains("ROLE_DPV") || currentUser.StartsWith("DPV", StringComparison.OrdinalIgnoreCase)
                || currentUser.StartsWith("NV", StringComparison.OrdinalIgnoreCase))
                return UserRole.DPV;
            if (currentRoles.Contains("ROLE_BACSI") || currentUser.StartsWith("BACSI", StringComparison.OrdinalIgnoreCase)
                || (currentUser.StartsWith("BS", StringComparison.OrdinalIgnoreCase) && currentUser.Length >= 5))
                return UserRole.BACSI;
            if (currentRoles.Contains("ROLE_KTV") || currentUser.StartsWith("KTV", StringComparison.OrdinalIgnoreCase))
                return UserRole.KTV;
            if (currentRoles.Contains("ROLE_BENHNHAN") || currentUser.StartsWith("BN", StringComparison.OrdinalIgnoreCase))
                return UserRole.BN;
            return UserRole.Unknown;
        }

        private string GetRoleTitle()
        {
            switch (userRole)
            {
                case UserRole.DPV: return "Điều Phối Viên";
                case UserRole.BACSI: return "Bác Sĩ / Y Sĩ";
                case UserRole.KTV: return "Kỹ Thuật Viên";
                case UserRole.BN: return "Bệnh Nhân";
                case UserRole.ADMIN: return "Quản Trị Viên";
                case UserRole.GIAMDOC: return "Ban Giám Đốc";
                default: return "Người Dùng";
            }
        }

        // ── build shell ──────────────────────────────────────────────────

        private void BuildUi()
        {
            Controls.Clear();
            Controls.Add(BuildMainPanel());
            Controls.Add(BuildInfoBar());
            Controls.Add(BuildHeader());
        }

        private Panel BuildMainPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill };
            switch (userRole)
            {
                case UserRole.DPV: BuildDPVInterface(p); break;
                case UserRole.BACSI: BuildBacsiInterface(p); break;
                case UserRole.KTV: BuildKTVInterface(p); break;
                case UserRole.BN: BuildBNInterface(p); break;
                case UserRole.ADMIN: BuildAdminInterface(p); break;
                case UserRole.GIAMDOC: BuildGiamDocInterface(p); break;
                default: p.Controls.Add(new Label { Text = "Không xác định được vai trò.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 12) }); break;
            }
            return p;
        }

        // ── header / infobar ─────────────────────────────────────────────

        private Panel BuildHeader()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = UiTheme.BrandeisBlue, Padding = new Padding(18, 14, 18, 14) };
            top.Controls.Add(new Label { AutoSize = true, Text = $"DocCare  ·  {GetRoleTitle()}", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = UiTheme.WhiteText, Dock = DockStyle.Left });

            // XỬ LÝ CHỮ TRÊN NÚT DỰA VÀO ROLE
            string btnText = (userRole == UserRole.ADMIN) ? "Đóng" : "Đăng xuất";
            var btnLogout = QuickBtn(btnText, Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 120, 40);

            // THÊM HỘP THOẠI XÁC NHẬN TRƯỚC KHI THOÁT
            btnLogout.Click += (s, e) => {
                string actionText = (userRole == UserRole.ADMIN) ? "đóng phiên quản trị" : "đăng xuất khỏi hệ thống";
                if (MessageBox.Show($"Bạn có chắc chắn muốn {actionText}?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            var flow = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
            flow.Controls.Add(btnLogout);
            top.Controls.Add(flow);
            return top;
        }

        private Panel BuildInfoBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(235, 248, 255), Padding = new Padding(16, 6, 16, 6) };
            bar.Controls.Add(new Label { AutoSize = true, Font = UiTheme.BodyFont, ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Left, Text = $"Đăng nhập: {currentUser}   |   Vai trò: {GetRoleTitle()}   |   Oracle DB [Trực tuyến]" });
            return bar;
        }

        // ── reusable helpers ─────────────────────────────────────────────

        /// Create a styled tab control with large, readable tabs
        private static TabControl MakeTabs()
        {
            var tc = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ItemSize = new Size(200, 38),  // wider, taller tabs
                SizeMode = TabSizeMode.Fixed,
                Padding = new Point(16, 6)
            };
            return tc;
        }

        private static TabPage MakeTab(string text)
            => new TabPage(text) { BackColor = UiTheme.LightCyan, UseVisualStyleBackColor = false };

        private static Button QuickBtn(string text, Color bg, Color fg, int w = 130, int h = 36)
        {
            var b = new Button { Text = text, Width = w, Height = h, AutoSize = false, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private static TextBox SearchBox(string hint, int w = 230)
        {
            var tb = new TextBox { Width = w, Height = 32, Font = new Font("Segoe UI", 10F), ForeColor = Color.Gray, Text = hint, Margin = new Padding(0, 0, 6, 0) };
            tb.Enter += (s, e) => { if (tb.Text == hint) { tb.Text = ""; tb.ForeColor = Color.Black; } };
            tb.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(tb.Text)) { tb.Text = hint; tb.ForeColor = Color.Gray; } };
            return tb;
        }

        private static string Val(TextBox tb, string hint) => tb.ForeColor == Color.Gray ? "" : tb.Text.Trim();

        private DataGridView MakeGrid(bool readOnly = true)
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = readOnly,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidth = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40
            };
            g.RowTemplate.Height = 30;
            UiTheme.StyleGrid(g);
            return g;
        }

        private static Panel Toolbar(params Control[] ctrls)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, AutoSize = false, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(8, 9, 8, 9), BackColor = Color.FromArgb(238, 248, 255) };
            foreach (var c in ctrls) bar.Controls.Add(c);
            return bar;
        }

        private static Label Note(string text) => new Label { Text = text, AutoSize = true, ForeColor = Color.FromArgb(100, 100, 120), Margin = new Padding(4, 10, 0, 0), Font = new Font("Segoe UI", 8.5F) };

        private void LoadGrid(DataGridView grid, string sql)
        {
            try { grid.DataSource = service.Query(sql); UiTheme.StyleGrid(grid); }
            catch (Exception ex) { Err("Không thể tải dữ liệu:\n" + ex.Message); }
        }

        private static string Esc(string s) => (s ?? "").Replace("'", "''");

        private string GetBackupDirPath()
        {
            try
            {
                var dt = service.Query("SELECT DIRECTORY_PATH FROM DBA_DIRECTORIES WHERE DIRECTORY_NAME = 'BACKUP_DIR'");
                if (dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return dt.Rows[0][0].ToString();
            }
            catch { }
            return @"C:\oracle_backup";
        }

        private static string ResolveDataPumpExe(string tool)
        {
            string file = tool.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? tool : tool + ".exe";
            string oracleHome = Environment.GetEnvironmentVariable("ORACLE_HOME");
            if (!string.IsNullOrWhiteSpace(oracleHome))
            {
                string path = Path.Combine(oracleHome, "bin", file);
                if (File.Exists(path)) return path;
            }
            foreach (string segment in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;
                string path = Path.Combine(segment.Trim(), file);
                if (File.Exists(path)) return path;
            }
            return file;
        }

        private static string QuoteCliArg(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.IndexOfAny(new[] { ' ', '"', '\t' }) >= 0)
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            return value;
        }

        private async System.Threading.Tasks.Task<int> RunDataPumpCliAsync(string tool, string args, TextBox log)
        {
            string exe = ResolveDataPumpExe(tool);
            string displayArgs = Regex.Replace(args, @"/[^/@\s""]+@", "/***@");
            log.AppendText($"\r\nC:\\> {tool} {displayArgs}\r\n");
            log.AppendText($"[{DateTime.Now:HH:mm:ss}] Đang chạy {tool} (có thể mất vài phút)...\r\n");

            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var proc = new Process())
                    {
                        proc.StartInfo.FileName = exe;
                        proc.StartInfo.Arguments = args;
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.RedirectStandardOutput = true;
                        proc.StartInfo.RedirectStandardError = true;
                        proc.StartInfo.CreateNoWindow = true;

                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                BeginInvoke(new Action(() => { log.AppendText(e.Data + "\r\n"); log.ScrollToCaret(); }));
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                BeginInvoke(new Action(() => { log.AppendText(e.Data + "\r\n"); log.ScrollToCaret(); }));
                        };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                        return proc.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() =>
                        log.AppendText($"[LỖI]: {ex.Message}\r\n   Kiểm tra: Oracle Client đã cài? {tool}.exe có trong PATH hoặc ORACLE_HOME\\bin?\r\n")));
                    return -1;
                }
            });
        }

        private void SetDataPumpButtonsEnabled(FlowLayoutPanel bar, bool enabled)
        {
            foreach (Control c in bar.Controls)
                if (c is Button b) b.Enabled = enabled;
        }

        private string PromptText(string title, string prompt, string defaultValue)
        {
            using (var dlg = new Form())
            {
                dlg.Text = title;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ClientSize = new Size(420, 118);
                var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true, MaximumSize = new Size(390, 0) };
                var txt = new TextBox { Text = defaultValue, Location = new Point(12, 36), Width = 380 };
                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(232, 72), Width = 75 };
                var btnCancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Location = new Point(317, 72), Width = 75 };
                dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                if (dlg.ShowDialog(this) != DialogResult.OK) return null;
                string v = txt.Text.Trim();
                return string.IsNullOrEmpty(v) ? defaultValue : v;
            }
        }
        private static void Err(string m) => MessageBox.Show(m, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        private static void Ok(string m) => MessageBox.Show(m, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

        private static Panel Wrap(DataGridView grid, Panel toolbar)
        {
            var p = new Panel { Dock = DockStyle.Fill };
            p.Controls.Add(grid);
            p.Controls.Add(toolbar);
            return p;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ĐIỀU PHỐI VIÊN  (TC#2)
        // ═══════════════════════════════════════════════════════════════════

        private DataGridView dgvDpvBN, dgvDpvHSBA, dgvDpvDV;

        private void BuildDPVInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân"); 
            var tBN = MakeTab("Bệnh Nhân");
            var tHSBA = MakeTab("Hồ Sơ Bệnh Án");
            var tDV = MakeTab("Điều Phối Dịch Vụ");
            var tTB = MakeTab("Thông Báo Khẩn (OLS)");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo); 
            BuildDPV_BN(tBN); BuildDPV_HSBA(tHSBA); BuildDPV_DV(tDV); BuildThongBaoTab(tTB, false);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tBN, tHSBA, tDV, tTB, tDemo });
            parent.Controls.Add(tabs);
        }

        // DPV – Bệnh Nhân
        private void BuildDPV_BN(TabPage tab)
        {
            dgvDpvBN = MakeGrid(true);

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Thêm Bệnh Nhân", UiTheme.PastelGreen, UiTheme.DeepBlue, 150);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvDpvBN, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.BENHNHAN" : $"SELECT * FROM QLBV.BENHNHAN WHERE MABN LIKE '%{Esc(kw)}%'"); };
            btnAdd.Click += (s, e) => DPV_ThemBN();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvBN, "SELECT * FROM QLBV.BENHNHAN");

            dgvDpvBN.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvBN.Rows[e.RowIndex];
                    string ma = r.Cells["MABN"].Value?.ToString();

                    var fields = new Dictionary<string, string> {
                        { "TENBN", r.Cells["TENBN"].Value?.ToString() },
                        { "PHAI", r.Cells["PHAI"].Value?.ToString() },
                        { "NGAYSINH", r.Cells["NGAYSINH"].Value != null ? Convert.ToDateTime(r.Cells["NGAYSINH"].Value).ToString("dd/MM/yyyy") : "" },
                        { "CCCD", r.Cells["CCCD"].Value?.ToString() },
                        { "SONHA", r.Cells["SONHA"].Value?.ToString() },
                        { "TENDUONG", r.Cells["TENDUONG"].Value?.ToString() },
                        { "QUANHUYEN", r.Cells["QUANHUYEN"].Value?.ToString() },
                        { "TINHTP", r.Cells["TINHTP"].Value?.ToString() },
                        { "TIENSUBENH", r.Cells["TIENSUBENH"].Value?.ToString() },
                        { "TIENSUBENHGD", r.Cells["TIENSUBENHGD"].Value?.ToString() },
                        { "DIUNGTHUOC", r.Cells["DIUNGTHUOC"].Value?.ToString() }
                    };

                    using (var f = new EditRowForm($"Cập nhật Bệnh Nhân: {ma}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                string ngaySinhSql = string.IsNullOrEmpty(f.NewValues["NGAYSINH"]) ? "NULL" : $"TO_DATE('{f.NewValues["NGAYSINH"]}','DD/MM/YYYY')";

                                string sqlUpdate = $"UPDATE QLBV.BENHNHAN SET " +
                                                   $"TENBN=N'{Esc(f.NewValues["TENBN"])}', " +
                                                   $"PHAI=N'{Esc(f.NewValues["PHAI"])}', " +
                                                   $"NGAYSINH={ngaySinhSql}, " +
                                                   $"CCCD='{Esc(f.NewValues["CCCD"])}', " +
                                                   $"SONHA=N'{Esc(f.NewValues["SONHA"])}', " +
                                                   $"TENDUONG=N'{Esc(f.NewValues["TENDUONG"])}', " +
                                                   $"QUANHUYEN=N'{Esc(f.NewValues["QUANHUYEN"])}', " +
                                                   $"TINHTP=N'{Esc(f.NewValues["TINHTP"])}', " +
                                                   $"TIENSUBENH=N'{Esc(f.NewValues["TIENSUBENH"])}', " +
                                                   $"TIENSUBENHGD=N'{Esc(f.NewValues["TIENSUBENHGD"])}', " +
                                                   $"DIUNGTHUOC=N'{Esc(f.NewValues["DIUNGTHUOC"])}' " +
                                                   $"WHERE MABN='{Esc(ma)}'";

                                service.ExecuteNonQuery(sqlUpdate);
                                Ok("Cập nhật toàn bộ thông tin bệnh nhân thành công!");
                                LoadGrid(dgvDpvBN, "SELECT * FROM QLBV.BENHNHAN");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvBN, Toolbar(txtS, btnS, btnAdd, btnRe, Note("Nhấn đúp (Double-click) vào dòng để chỉnh sửa toàn bộ thông tin bệnh nhân."))));
            LoadGrid(dgvDpvBN, "SELECT * FROM QLBV.BENHNHAN");
        }

        private void DPV_ThemBN()
        {
            using (var f = new BenhNhanAddForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.BENHNHAN(MABN,TENBN,PHAI,NGAYSINH,CCCD,SONHA,TENDUONG,QUANHUYEN,TINHTP,TIENSUBENH,TIENSUBENHGD,DIUNGTHUOC) VALUES('{Esc(f.MaBN)}',N'{Esc(f.TenBN)}',N'{Esc(f.Phai)}',TO_DATE('{f.NgaySinh:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.CCCD)}',N'{Esc(f.SoNha)}',N'{Esc(f.TenDuong)}',N'{Esc(f.QuanHuyen)}',N'{Esc(f.TinhTP)}',N'{Esc(f.TienSuBenh)}',N'{Esc(f.TienSuBenhGD)}',N'{Esc(f.DiUngThuoc)}')"); Ok("Đã thêm bệnh nhân!"); LoadGrid(dgvDpvBN, "SELECT * FROM QLBV.BENHNHAN"); } catch (Exception ex) { Err(ex.Message); }
        }

        private void DPV_LuuBN()
        {
            var dt = dgvDpvBN.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET SONHA=N'{Esc(r["SONHA"].ToString())}',TENDUONG=N'{Esc(r["TENDUONG"].ToString())}',QUANHUYEN=N'{Esc(r["QUANHUYEN"].ToString())}',TINHTP=N'{Esc(r["TINHTP"].ToString())}',TIENSUBENH=N'{Esc(r["TIENSUBENH"].ToString())}',TIENSUBENHGD=N'{Esc(r["TIENSUBENHGD"].ToString())}',DIUNGTHUOC=N'{Esc(r["DIUNGTHUOC"].ToString())}' WHERE MABN='{Esc(r["MABN"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu {n} thay đổi."); LoadGrid(dgvDpvBN, "SELECT * FROM QLBV.BENHNHAN"); } else Ok("Không có thay đổi nào.");
        }

        // DPV – HSBA
        private void BuildDPV_HSBA(TabPage tab)
        {
            dgvDpvHSBA = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Tạo HSBA", UiTheme.PastelGreen, UiTheme.DeepBlue, 130);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            // ✅ [GỘPCODE-E] Nút Tính Chi Phí gọi fn_TinhTongChiPhiDieuTri
            //    Nguồn: sys_PH2.sql §3.2 — AuditNVCalcFee ghi vết khi DPV/NV gọi hàm này
            var btnCalc = QuickBtn("Tính Chi Phí", Color.FromArgb(255, 200, 0), UiTheme.DeepBlue, 120);
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvDpvHSBA, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.HSBA" : $"SELECT * FROM QLBV.HSBA WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };
            btnAdd.Click += (s, e) => DPV_ThemHSBA();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvHSBA, "SELECT * FROM QLBV.HSBA");
            btnCalc.Click += (s, e) => {
                if (dgvDpvHSBA.CurrentRow == null) { Err("Chọn một HSBA để tính chi phí."); return; }
                string maHsba = dgvDpvHSBA.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                try
                {
                    var dt = service.Query($"SELECT QLBV.fn_TinhTongChiPhiDieuTri('{Esc(maHsba)}') AS TONG_DV FROM DUAL");
                    string tongDv = dt.Rows[0]["TONG_DV"]?.ToString() ?? "0";
                    MessageBox.Show($"HSBA: {maHsba}\nTổng số dịch vụ điều trị: {tongDv} dịch vụ\n\n(AuditSucDPVExecFunc đã ghi vết lần truy vấn này)",
                        "Chi Phí Điều Trị", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { Err($"fn_TinhTongChiPhiDieuTri: {ex.Message}"); }
            };

            dgvDpvHSBA.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvHSBA.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();

                    var fields = new Dictionary<string, string> {
                        { "MABS", r.Cells["MABS"].Value?.ToString() },
                        { "MAKHOA", r.Cells["MAKHOA"].Value?.ToString() }
                    };

                    using (var f = new EditRowForm($"Điều phối Bác sĩ cho HSBA: {ma}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET MABS='{Esc(f.NewValues["MABS"])}', MAKHOA='{Esc(f.NewValues["MAKHOA"])}' WHERE MAHSBA='{Esc(ma)}'");
                                Ok("Điều phối y bác sĩ thành công!");
                                LoadGrid(dgvDpvHSBA, "SELECT * FROM QLBV.HSBA");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvHSBA, Toolbar(txtS, btnS, btnAdd, btnRe, btnCalc, Note("Nhấn đúp để phân công BS/Khoa (AuditDPVUpdateHSBA). Chọn dòng → 'Tính Chi Phí' (AuditSucDPVExecFunc)."))));
            LoadGrid(dgvDpvHSBA, "SELECT * FROM QLBV.HSBA");
        }

        private void DPV_ThemHSBA()
        {
            using (var f = new HsbaAddForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA(MAHSBA,MABN,NGAY,MABS,MAKHOA) VALUES('{Esc(f.MaHSBA)}','{Esc(f.MaBN)}',TO_DATE('{f.Ngay:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaBS)}','{Esc(f.MaKhoa)}')"); Ok("Đã tạo HSBA! Y bác sĩ sẽ điền chẩn đoán/kết luận sau."); LoadGrid(dgvDpvHSBA, "SELECT * FROM QLBV.HSBA"); } catch (Exception ex) { Err(ex.Message); }
        }

        private void DPV_LuuHSBA()
        {
            var dt = dgvDpvHSBA.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET MABS='{Esc(r["MABS"].ToString())}',MAKHOA='{Esc(r["MAKHOA"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu điều phối {n} HSBA."); LoadGrid(dgvDpvHSBA, "SELECT * FROM QLBV.HSBA"); } else Ok("Không có thay đổi nào.");
        }

        // DPV – Điều phối KTV (không nhập kết quả)
        private void BuildDPV_DV(TabPage tab)
        {
            dgvDpvDV = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Thêm Dịch Vụ", UiTheme.PastelGreen, UiTheme.DeepBlue, 140);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvDpvDV, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.HSBA_DV" : $"SELECT * FROM QLBV.HSBA_DV WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };
            btnAdd.Click += (s, e) => DPV_ThemDV();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvDV, "SELECT * FROM QLBV.HSBA_DV");

            dgvDpvDV.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvDV.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string loaiDv = r.Cells["LOAIDV"].Value?.ToString();
                    string ngayDv = Convert.ToDateTime(r.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy");


                    var fields = new Dictionary<string, string> {
                        { "MAKTV", r.Cells["MAKTV"].Value?.ToString() }
                    };

                    using (var f = new EditRowForm($"Phân công KTV cho: {loaiDv}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.HSBA_DV SET MAKTV='{Esc(f.NewValues["MAKTV"])}' WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(loaiDv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')");
                                Ok("Điều phối KTV thành công!");
                                LoadGrid(dgvDpvDV, "SELECT * FROM QLBV.HSBA_DV");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvDV, Toolbar(txtS, btnS, btnAdd, btnRe, Note("Nhấn đúp (Double-click) để phân công / thay đổi Mã KTV phụ trách."))));
            LoadGrid(dgvDpvDV, "SELECT * FROM QLBV.HSBA_DV");
        }

        private void DPV_ThemDV()
        {
            using (var f = new HsbaDvAddForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA_DV(MAHSBA,LOAIDV,NGAYDV,MAKTV,KETQUA) VALUES('{Esc(f.MaHSBA)}',N'{Esc(f.LoaiDV)}',TO_DATE('{f.NgayDV:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaKTV)}',NULL)"); Ok("Đã điều phối KTV!"); LoadGrid(dgvDpvDV, "SELECT * FROM QLBV.HSBA_DV"); } catch (Exception ex) { Err(ex.Message); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BÁC SĨ / Y SĨ  (TC#3)
        //  - KHÔNG có nút thêm HSBA (điều phối viên mới tạo)
        //  - Có cập nhật TENTHUOC/LIEUDUNG trên đơn thuốc (ghi vết qua trigger)
        // ═══════════════════════════════════════════════════════════════════

        private DataGridView dgvBsHSBA, dgvBsBN, dgvBsDV, dgvBsDT;

        private void BuildBacsiInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân"); 
            var tHSBA = MakeTab("Hồ Sơ Bệnh Án");
            var tBN = MakeTab("Bệnh Nhân");
            var tDV = MakeTab("Dịch Vụ (HSBA_DV)");
            var tDT = MakeTab("Đơn Thuốc");
            var tTB = MakeTab("Thông Báo Khẩn (OLS)");
            // ✅ [GỘPCODE-D] Tab Báo Cáo Điều Trị — VW_BaoCaoDieuTri + sp_KhoiTaoHSBAKhancap
            var tBaoCao = MakeTab("Báo Cáo Điều Trị");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo);
            BuildBs_HSBA(tHSBA); BuildBs_BN(tBN); BuildBs_DV(tDV); BuildBs_DT(tDT);
            BuildBs_BaoCao(tBaoCao); BuildThongBaoTab(tTB, false);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tHSBA, tBN, tDV, tDT, tBaoCao, tTB, tDemo });
            parent.Controls.Add(tabs);
        }

        // Bác sĩ – HSBA (không thêm, chỉ xóa + lưu CHANDOAN/DIEUTRI/KETLUAN)
        private void BuildBs_HSBA(TabPage tab)
        {
            dgvBsHSBA = MakeGrid(true); // Cấm gõ trực tiếp lên lưới

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvBsHSBA, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.HSBA" : $"SELECT * FROM QLBV.HSBA WHERE MABN LIKE '%{Esc(kw)}%'"); };
            btnRe.Click += (s, e) => LoadGrid(dgvBsHSBA, "SELECT * FROM QLBV.HSBA");

            // Xử lý Double-click mở Form Cập nhật
            dgvBsHSBA.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvBsHSBA.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    var fields = new Dictionary<string, string> {
                    { "CHANDOAN", r.Cells["CHANDOAN"].Value?.ToString() },
                    { "DIEUTRI", r.Cells["DIEUTRI"].Value?.ToString() },
                    { "KETLUAN", r.Cells["KETLUAN"].Value?.ToString() }
                };
                    using (var f = new EditRowForm($"Cập nhật HSBA: {ma}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET CHANDOAN=N'{Esc(f.NewValues["CHANDOAN"])}', DIEUTRI=N'{Esc(f.NewValues["DIEUTRI"])}', KETLUAN=N'{Esc(f.NewValues["KETLUAN"])}' WHERE MAHSBA='{Esc(ma)}'");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvBsHSBA, "SELECT * FROM QLBV.HSBA");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsHSBA, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng hồ sơ để cập nhật thông tin."))));
            LoadGrid(dgvBsHSBA, "SELECT * FROM QLBV.HSBA");
        }


        private void Bs_LuuHSBA()
        {
            var dt = dgvBsHSBA.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET CHANDOAN=N'{Esc(r["CHANDOAN"].ToString())}',DIEUTRI=N'{Esc(r["DIEUTRI"].ToString())}',KETLUAN=N'{Esc(r["KETLUAN"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu {n} thay đổi (ghi vết audit)."); LoadGrid(dgvBsHSBA, "SELECT * FROM QLBV.HSBA"); } else Ok("Không có thay đổi nào.");
        }

        // Bác sĩ – Bệnh Nhân
        private void BuildBs_BN(TabPage tab)
        {
            dgvBsBN = MakeGrid(true); // Read-only

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvBsBN, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.BENHNHAN" : $"SELECT * FROM QLBV.BENHNHAN WHERE MABN LIKE '%{Esc(kw)}%'"); };
            btnRe.Click += (s, e) => LoadGrid(dgvBsBN, "SELECT * FROM QLBV.BENHNHAN");

            dgvBsBN.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvBsBN.Rows[e.RowIndex];
                    string ma = r.Cells["MABN"].Value?.ToString();
                    var fields = new Dictionary<string, string> {
                    { "TIENSUBENH", r.Cells["TIENSUBENH"].Value?.ToString() },
                    { "TIENSUBENHGD", r.Cells["TIENSUBENHGD"].Value?.ToString() },
                    { "DIUNGTHUOC", r.Cells["DIUNGTHUOC"].Value?.ToString() }
                };
                    using (var f = new EditRowForm($"Cập nhật Bệnh Nhân: {ma}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET TIENSUBENH=N'{Esc(f.NewValues["TIENSUBENH"])}', TIENSUBENHGD=N'{Esc(f.NewValues["TIENSUBENHGD"])}', DIUNGTHUOC=N'{Esc(f.NewValues["DIUNGTHUOC"])}' WHERE MABN='{Esc(ma)}'");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvBsBN, "SELECT * FROM QLBV.BENHNHAN");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsBN, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng bệnh nhân để cập nhật tiền sử/dị ứng."))));
            LoadGrid(dgvBsBN, "SELECT * FROM QLBV.BENHNHAN");
        }

        private void Bs_LuuBN()
        {
            var dt = dgvBsBN.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET TIENSUBENH=N'{Esc(r["TIENSUBENH"].ToString())}',TIENSUBENHGD=N'{Esc(r["TIENSUBENHGD"].ToString())}',DIUNGTHUOC=N'{Esc(r["DIUNGTHUOC"].ToString())}' WHERE MABN='{Esc(r["MABN"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã cập nhật {n} bệnh nhân."); LoadGrid(dgvBsBN, "SELECT * FROM QLBV.BENHNHAN"); } else Ok("Không có thay đổi nào.");
        }

        // Bác sĩ – HSBA_DV
        private void BuildBs_DV(TabPage tab)
        {
            dgvBsDV = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvBsDV, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.HSBA_DV" : $"SELECT * FROM QLBV.HSBA_DV WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };

            var btnAdd = QuickBtn("+ Thêm Dịch Vụ", UiTheme.PastelGreen, UiTheme.DeepBlue);
            var btnDel = QuickBtn("Xóa Dòng", Color.FromArgb(206, 17, 38), UiTheme.WhiteText);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            btnAdd.Click += (s, e) => Bs_ThemDV(); btnDel.Click += (s, e) => Bs_XoaDV(); btnRe.Click += (s, e) => LoadGrid(dgvBsDV, "SELECT * FROM QLBV.HSBA_DV");

            tab.Controls.Add(Wrap(dgvBsDV, Toolbar(txtS, btnS, btnAdd, btnDel, btnRe)));
            LoadGrid(dgvBsDV, "SELECT * FROM QLBV.HSBA_DV");
        }

        // ✅ [GỘPCODE-B] BS chỉ định dịch vụ — INSERT bao gồm MAKTV (NOT NULL theo schema)
        //    Nguồn: schema_phanhe2.sql — MAKTV VARCHAR2(20) NOT NULL + FK→NHANVIEN(MANV)
        //    Nghiệp vụ: BS đề xuất KTV thực hiện, DPV có thể cập nhật MAKTV sau (grant UPDATE(MAKTV)).
        private void Bs_ThemDV()
        {
            using (var f = new HsbaDvAddForm(true))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA_DV(MAHSBA,LOAIDV,NGAYDV,MAKTV) VALUES('{Esc(f.MaHSBA)}',N'{Esc(f.LoaiDV)}',TO_DATE('{f.NgayDV:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaKTV)}')");
                        Ok("Đã chỉ định dịch vụ! DPV có thể cập nhật lại Kỹ thuật viên nếu cần.");
                        LoadGrid(dgvBsDV, "SELECT * FROM QLBV.HSBA_DV");
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }
        // ✅ [GỘPCODE-3] DELETE HSBA_DV dùng đúng PK composite (MAHSBA, LOAIDV, NGAYDV)
        //    Nguồn: schema_phanhe2.sql — CONSTRAINT PK_HSBA_DV PRIMARY KEY (MAHSBA, LOAIDV, NGAYDV)
        //    Trước: WHERE chỉ có MAHSBA + LOAIDV → xóa nhầm nhiều dòng cùng dịch vụ.
        //    Sau: thêm AND NGAYDV=TO_DATE(...) → xóa đúng 1 dòng theo PK đầy đủ.
        private void Bs_XoaDV() { if (dgvBsDV.CurrentRow == null) { Err("Chọn dòng."); return; } string ma = dgvBsDV.CurrentRow.Cells["MAHSBA"].Value?.ToString(); string dv = dgvBsDV.CurrentRow.Cells["LOAIDV"].Value?.ToString(); string ngayDv = dgvBsDV.CurrentRow.Cells["NGAYDV"].Value != null ? Convert.ToDateTime(dgvBsDV.CurrentRow.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy") : ""; if (MessageBox.Show($"Xóa DV '{dv}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) try { service.ExecuteNonQuery($"DELETE FROM QLBV.HSBA_DV WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(dv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')"); Ok("Đã xóa."); LoadGrid(dgvBsDV, "SELECT * FROM QLBV.HSBA_DV"); } catch (Exception ex) { Err(ex.Message); } }

        // Bác sĩ – Đơn Thuốc  (thêm + xóa + sửa TENTHUOC/LIEUDUNG, ghi vết qua trigger)
        private void BuildBs_DT(TabPage tab)
        {
            dgvBsDT = MakeGrid(true); // Read-only

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnAdd = QuickBtn("+ Thêm Đơn Thuốc", UiTheme.PastelGreen, UiTheme.DeepBlue, 155);
            var btnDel = QuickBtn("Xóa Dòng", Color.FromArgb(206, 17, 38), UiTheme.WhiteText);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvBsDT, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.DONTHUOC" : $"SELECT * FROM QLBV.DONTHUOC WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };
            btnAdd.Click += (s, e) => Bs_ThemDT();
            btnDel.Click += (s, e) => Bs_XoaDT();
            btnRe.Click += (s, e) => LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");

            dgvBsDT.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvBsDT.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string tenThuocCu = r.Cells["TENTHUOC"].Value?.ToString();
                    string ngayDt = Convert.ToDateTime(r.Cells["NGAYDT"].Value).ToString("dd/MM/yyyy");

                    var fields = new Dictionary<string, string> {
                    { "TENTHUOC", tenThuocCu },
                    { "LIEUDUNG", r.Cells["LIEUDUNG"].Value?.ToString() }
                };
                    using (var f = new EditDonThuocForm($"Cập nhật Thuốc: {tenThuocCu}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.DONTHUOC SET TENTHUOC=N'{Esc(f.NewValues["TENTHUOC"])}', LIEUDUNG=N'{Esc(f.NewValues["LIEUDUNG"])}' WHERE MAHSBA='{Esc(ma)}' AND NGAYDT=TO_DATE('{ngayDt}','DD/MM/YYYY') AND TENTHUOC=N'{Esc(tenThuocCu)}'");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsDT, Toolbar(txtS, btnS, btnAdd, btnDel, btnRe, Note("Nhấn đúp (Double-click) vào dòng để sửa TÊN THUỐC / LIỀU DÙNG."))));
            LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");
        }

        private void Bs_ThemDT()
        {
            using (var f = new DonThuocAddForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        service.ExecuteNonQuery($"INSERT INTO QLBV.DONTHUOC(MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG) VALUES('{Esc(f.MaHSBA)}',TO_DATE('{f.NgayDT:dd/MM/yyyy}','DD/MM/YYYY'),N'{Esc(f.TenThuoc)}',N'{Esc(f.LieuDung)}')");
                        Ok("Đã thêm đơn thuốc!"); LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }

        private void Bs_XoaDT()
        {
            if (dgvBsDT.CurrentRow == null) { Err("Chọn dòng."); return; }
            string ma = dgvBsDT.CurrentRow.Cells["MAHSBA"].Value?.ToString();
            string tn = dgvBsDT.CurrentRow.Cells["TENTHUOC"].Value?.ToString();
            if (MessageBox.Show($"Xóa thuốc '{tn}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                try { service.ExecuteNonQuery($"DELETE FROM QLBV.DONTHUOC WHERE MAHSBA='{Esc(ma)}' AND TENTHUOC=N'{Esc(tn)}'"); Ok("Đã xóa."); LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC"); } catch (Exception ex) { Err(ex.Message); }
        }

        private void Bs_LuuDT()
        {
            var dt = dgvBsDT.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows)
                if (r.RowState == DataRowState.Modified)
                    try
                    {
                        service.ExecuteNonQuery($"UPDATE QLBV.DONTHUOC SET TENTHUOC=N'{Esc(r["TENTHUOC"].ToString())}',LIEUDUNG=N'{Esc(r["LIEUDUNG"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}' AND NGAYDT=TO_DATE('{Convert.ToDateTime(r["NGAYDT"]):dd/MM/yyyy}','DD/MM/YYYY') AND TENTHUOC=N'{Esc(r["TENTHUOC", DataRowVersion.Original].ToString())}'");
                        n++;
                    }
                    catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu {n} thay đổi (ghi vết audit)."); LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC"); } else Ok("Không có thay đổi nào.");
        }

        // ✅ [GỘPCODE-D] Báo Cáo Điều Trị: VW_BaoCaoDieuTri + sp_KhoiTaoHSBAKhancap
        //    Nguồn: sys_PH2.sql §3.2 — AuditBSSelectBaoCao ghi vết SELECT view
        //           sys_PH2.sql §3.2 — AuditBSExecCapCuu ghi vết EXECUTE procedure
        private void BuildBs_BaoCao(TabPage tab)
        {
            var grid = MakeGrid(true);
            var btnRe     = QuickBtn("Tải Lại Báo Cáo", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 155);
            var btnCapCuu = QuickBtn("🚨 Tạo HSBA Cấp Cứu", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 180);

            btnRe.Click += (s, e) => {
                try { grid.DataSource = service.Query("SELECT * FROM QLBV.VW_BaoCaoDieuTri"); UiTheme.StyleGrid(grid); }
                catch (Exception ex) { Err($"VW_BaoCaoDieuTri: {ex.Message}"); }
            };

            btnCapCuu.Click += (s, e) => {
                using (var f = new CapCuuForm())
                    if (f.ShowDialog(this) == DialogResult.OK)
                        try
                        {
                            service.ExecuteNonQuery($"BEGIN QLBV.sp_KhoiTaoHSBAKhancap('{Esc(f.MaHSBA)}','{Esc(f.MaBN)}','{Esc(currentUser)}','{Esc(f.MaKhoa)}'); END;");
                            Ok($"Đã tạo HSBA cấp cứu '{f.MaHSBA}' — AuditBSExecCapCuu đã ghi vết.");
                            try { grid.DataSource = service.Query("SELECT * FROM QLBV.VW_BaoCaoDieuTri"); UiTheme.StyleGrid(grid); } catch { }
                        }
                        catch (Exception ex) { Err(ex.Message); }
            };

            tab.Controls.Add(Wrap(grid, Toolbar(btnRe, btnCapCuu,
                Note("VW_BaoCaoDieuTri → AuditSucBSSelectView  |  sp_KhoiTaoHSBAKhancap → AuditSucBSExecProc. Thao tác bị chặn → tab Chức Năng Mở Rộng."))));

            try { grid.DataSource = service.Query("SELECT * FROM QLBV.VW_BaoCaoDieuTri"); UiTheme.StyleGrid(grid); }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CHỨC NĂNG MỞ RỘNG — UI nghiệp vụ bình thường, thao tác bị chặn → audit (09.sql)
        // ═══════════════════════════════════════════════════════════════════

        private void HandleDemoAction(string code, string policy, string category, Action action)
        {
            try
            {
                action();
                MessageBox.Show(
                    $"Thao tác đã gửi nhưng có thể không có hiệu lực (0 dòng / VPD).\nKiểm tra tab Kiểm Toán — policy {policy}.",
                    $"[{code}] Cần xác nhận audit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                string friendly = MapAuditDemoError(ex, category);
                MessageBox.Show(
                    $"{friendly}\n\n[{code}] Policy audit: {policy}\n\nĐăng nhập QLBV → Kiểm Toán để xem log.",
                    "Không thể thực hiện", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string MapAuditDemoError(Exception ex, string category)
        {
            string msg = ex?.Message ?? "";
            if (msg.IndexOf("ORA-01031", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bạn không có quyền thực hiện thao tác này (insufficient privileges). Hệ thống đã ghi nhận audit.";
            if (msg.IndexOf("ORA-00942", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Không thể truy cập dữ liệu — bảng/view không khả dụng với tài khoản của bạn.";
            if (msg.IndexOf("ORA-28115", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ORA-28113", StringComparison.OrdinalIgnoreCase) >= 0)
                return "VPD đã chặn: dữ liệu ngoài phạm vi bạn được phép thao tác.";
            if (msg.IndexOf("ORA-01403", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Không tìm thấy dữ liệu phù hợp — có thể do VPD ẩn bản ghi.";
            if (msg.IndexOf("ORA-06564", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ORA-06550", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ORA-06553", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bạn không có quyền chạy chức năng / procedure này.";
            if (msg.IndexOf("ORA-02291", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("ORA-02292", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Thao tác bị chặn bởi ràng buộc dữ liệu.";
            if (msg.IndexOf("VPD", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("0 dòng", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("-20001", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Không thể cập nhật — hồ sơ ngoài phạm vi điều trị của bạn (VPD).";
            return $"{category}: {msg}";
        }

        private void BuildAuditDemoTab(TabPage tab)
        {
            if (userRole == UserRole.ADMIN || userRole == UserRole.GIAMDOC || userRole == UserRole.Unknown)
            {
                tab.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Tab Chức Năng Mở Rộng dành cho tài khoản nghiệp vụ.\n\n" +
                           "Đăng nhập: NV0002, BS0001, KTV001 hoặc BN000001 (xem menu Tài khoản test).\n" +
                           "Sau thao tác → QLBV → Kiểm Toán.",
                    Font = new Font("Segoe UI", 10F), ForeColor = UiTheme.DeepBlue
                });
                return;
            }

            var subTabs = new TabControl { Dock = DockStyle.Fill };
            switch (userRole)
            {
                case UserRole.DPV:   BuildDemo_DpvHsba(subTabs); break;
                case UserRole.BACSI: BuildDemo_BsChiPhi(subTabs); BuildDemo_BsNhanVien(subTabs); BuildDemo_BsHsbaKhac(subTabs); break;
                case UserRole.KTV:   BuildDemo_KtvDonThuoc(subTabs); BuildDemo_KtvBenhNhan(subTabs); BuildDemo_KtvDichVu(subTabs); break;
                case UserRole.BN:    BuildDemo_BnHsba(subTabs); BuildDemo_BnDichVu(subTabs); BuildDemo_BnCapCuu(subTabs); break;
            }
            tab.Controls.Add(subTabs);
        }

        // [TB4] DPV — quản lý HSBA có nút xóa (không được phép DELETE)
        private void BuildDemo_DpvHsba(TabControl parent)
        {
            var page = MakeTab("Hồ Sơ Bệnh Án");
            var grid = MakeGrid(true);
            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var btnDel = QuickBtn("Xóa HSBA", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 110);

            void Reload(string kw = "")
            {
                string sql = string.IsNullOrEmpty(kw)
                    ? "SELECT * FROM QLBV.HSBA"
                    : $"SELECT * FROM QLBV.HSBA WHERE MAHSBA LIKE '%{Esc(kw)}%'";
                LoadGrid(grid, sql);
            }

            btnS.Click += (s, e) => Reload(Val(txtS, "Tìm mã HSBA..."));
            btnRe.Click += (s, e) => Reload();
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn một hồ sơ bệnh án cần xóa."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                if (MessageBox.Show($"Xóa hồ sơ '{ma}'?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB4", "AuditFailDPVDeleteHSBA", "Vượt quyền DELETE",
                    () => service.ExecuteNonQuery($"DELETE FROM QLBV.HSBA WHERE MAHSBA='{Esc(ma)}'"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(txtS, btnS, btnRe, btnDel,
                Note("Quản lý hồ sơ bệnh án — chọn dòng trên lưới rồi Xóa HSBA."))));
            parent.TabPages.Add(page);
            Reload();
        }

        // [TBx] BS — tính chi phí (chỉ DPV được EXECUTE function)
        private void BuildDemo_BsChiPhi(TabControl parent)
        {
            var page = MakeTab("Chi Phí Điều Trị");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 100);
            var btnCalc = QuickBtn("Tính Tổng Chi Phí", Color.FromArgb(255, 200, 0), UiTheme.DeepBlue, 155);

            btnRe.Click += (s, e) => LoadGrid(grid, "SELECT * FROM QLBV.HSBA");
            btnCalc.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn một HSBA trên lưới."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                HandleDemoAction("TBx", "AuditFailBSExecFunc", "Vượt quyền EXECUTE",
                    () => service.Query($"SELECT QLBV.fn_TinhTongChiPhiDieuTri('{Esc(ma)}') AS TONG_DV FROM DUAL"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnCalc,
                Note("Chọn HSBA → Tính Tổng Chi Phí điều trị."))));
            parent.TabPages.Add(page);
            try { LoadGrid(grid, "SELECT * FROM QLBV.HSBA"); } catch { }
        }

        // [TB1] BS — quản lý nhân viên (UPDATE/DELETE bị chặn)
        private void BuildDemo_BsNhanVien(TabControl parent)
        {
            var page = MakeTab("Nhân Viên");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var btnDel = QuickBtn("Xóa Nhân Viên", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 130);

            btnRe.Click += (s, e) =>
            {
                try { LoadGrid(grid, "SELECT MANV, HOTEN, CAPBAC, MAKHOA, COSO FROM QLBV.NHANVIEN WHERE ROWNUM <= 50"); }
                catch (Exception ex) { Err("Không thể tải danh sách nhân viên: " + ex.Message); }
            };

            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var r = grid.Rows[e.RowIndex];
                string manv = r.Cells["MANV"].Value?.ToString();
                var fields = new Dictionary<string, string> { { "COSO", r.Cells["COSO"].Value?.ToString() } };
                using (var f = new EditRowForm($"Cập nhật nhân viên: {manv}", fields))
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    HandleDemoAction("TB1", "AuditFailBSUpdateNV", "Vượt quyền UPDATE",
                        () => service.ExecuteNonQuery($"UPDATE QLBV.NHANVIEN SET COSO=N'{Esc(f.NewValues["COSO"])}' WHERE MANV='{Esc(manv)}'"));
                }
            };

            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn nhân viên cần xóa."); return; }
                string manv = grid.CurrentRow.Cells["MANV"].Value?.ToString();
                if (MessageBox.Show($"Xóa nhân viên '{manv}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB1b", "AuditFailBSUpdateNV", "Vượt quyền DELETE",
                    () => service.ExecuteNonQuery($"DELETE FROM QLBV.NHANVIEN WHERE MANV='{Esc(manv)}'"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnDel,
                Note("Nhấp đúp để sửa thông tin, hoặc chọn dòng → Xóa Nhân Viên."))));
            parent.TabPages.Add(page);
        }

        // [3.3.c] BS — sửa HSBA của bác sĩ khác (VPD)
        private void BuildDemo_BsHsbaKhac(TabControl parent)
        {
            var page = MakeTab("Hồ Sơ Bác Sĩ Khác");
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), BackColor = Color.White };

            var lbl = new Label
            {
                Text = "Nhập mã HSBA của bác sĩ khác để cập nhật chẩn đoán (thao tác ngoài phạm vi VPD).",
                AutoSize = true, MaximumSize = new Size(700, 0), Font = new Font("Segoe UI", 10F), ForeColor = UiTheme.DeepBlue
            };
            var tbl = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Location = new Point(0, 40), Padding = new Padding(0, 12, 0, 0) };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));

            var txtMa = new TextBox { Width = 280, Text = "HS010001" };
            var txtCd = new TextBox { Width = 400, Multiline = true, Height = 72 };
            tbl.Controls.Add(new Label { Text = "Mã HSBA:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            tbl.Controls.Add(txtMa, 1, 0);
            tbl.Controls.Add(new Label { Text = "Chẩn đoán:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            tbl.Controls.Add(txtCd, 1, 1);

            var btnSave = QuickBtn("Lưu Chẩn Đoán", UiTheme.PastelGreen, UiTheme.DeepBlue, 150, 40);
            btnSave.Location = new Point(140, 200);
            btnSave.Click += (s, e) =>
            {
                string ma = txtMa.Text.Trim();
                if (string.IsNullOrEmpty(ma)) { Err("Nhập mã HSBA."); return; }
                HandleDemoAction("3.3.c", "AuditIllegalUpdateHSBA", "VPD chặn",
                    () => service.ExecuteNonQuery(
                        $"BEGIN UPDATE QLBV.HSBA SET CHANDOAN=N'{Esc(txtCd.Text)}' WHERE MAHSBA='{Esc(ma)}' AND MABS != USER; " +
                        "IF SQL%ROWCOUNT = 0 THEN RAISE_APPLICATION_ERROR(-20001, 'VPD: Cap nhat 0 dong'); END IF; END;"));
            };

            pnl.Controls.Add(btnSave);
            pnl.Controls.Add(tbl);
            pnl.Controls.Add(lbl);
            page.Controls.Add(pnl);
            parent.TabPages.Add(page);
        }

        // [TB3] KTV — đơn thuốc
        private void BuildDemo_KtvDonThuoc(TabControl parent)
        {
            var page = MakeTab("Đơn Thuốc");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var btnDel = QuickBtn("Xóa Đơn Thuốc", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 140);

            btnRe.Click += (s, e) =>
            {
                try { LoadGrid(grid, "SELECT * FROM QLBV.DONTHUOC WHERE ROWNUM <= 30"); }
                catch (Exception ex) { HandleDemoAction("TB3-load", "AuditFailKTVDeleteDT", "Vượt quyền", () => { throw ex; }); }
            };
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn đơn thuốc cần xóa."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                string tn = grid.CurrentRow.Cells["TENTHUOC"].Value?.ToString();
                if (MessageBox.Show($"Xóa thuốc '{tn}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB3", "AuditFailKTVDeleteDT", "Vượt quyền DELETE",
                    () => service.ExecuteNonQuery($"DELETE FROM QLBV.DONTHUOC WHERE MAHSBA='{Esc(ma)}' AND TENTHUOC=N'{Esc(tn)}'"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnDel,
                Note("Chọn đơn thuốc trên lưới → Xóa Đơn Thuốc."))));
            parent.TabPages.Add(page);
        }

        // [TB6] KTV — tra cứu bệnh nhân trực tiếp
        private void BuildDemo_KtvBenhNhan(TabControl parent)
        {
            var page = MakeTab("Tra Cứu Bệnh Nhân");
            var grid = MakeGrid(true);
            var txtS = SearchBox("Tìm mã BN...", 180);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Danh Sách", UiTheme.BrandeisBlue, UiTheme.WhiteText, 130);

            void Search(string kw)
            {
                string sql = string.IsNullOrEmpty(kw)
                    ? "SELECT * FROM QLBV.BENHNHAN WHERE ROWNUM <= 30"
                    : $"SELECT * FROM QLBV.BENHNHAN WHERE MABN LIKE '%{Esc(kw)}%'";
                HandleDemoAction("TB6", "AuditFailKTVSelectBN", "Vượt quyền SELECT",
                    () => { grid.DataSource = service.Query(sql); UiTheme.StyleGrid(grid); });
            }

            btnRe.Click += (s, e) => Search("");
            btnS.Click += (s, e) => Search(Val(txtS, "Tìm mã BN..."));

            page.Controls.Add(Wrap(grid, Toolbar(txtS, btnS, btnRe,
                Note("Tra cứu danh sách bệnh nhân toàn viện."))));
            parent.TabPages.Add(page);
        }

        // [3.3.d] KTV — thêm/xóa DV ngoài phạm vi
        private void BuildDemo_KtvDichVu(TabControl parent)
        {
            var page = MakeTab("Dịch Vụ Mở Rộng");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var btnAdd = QuickBtn("+ Thêm Dịch Vụ", UiTheme.PastelGreen, UiTheme.DeepBlue, 140);
            var btnDel = QuickBtn("Xóa Dòng", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 100);

            btnRe.Click += (s, e) => LoadGrid(grid, "SELECT * FROM QLBV.VW_KTV_XemHSBADV");
            btnAdd.Click += (s, e) =>
            {
                using (var f = new HsbaDvAddForm())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    HandleDemoAction("3.3.d+", "AuditIllegalHSBADV", "VPD chặn",
                        () => service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.VW_KTV_XemHSBADV(MAHSBA,LOAIDV,NGAYDV,MAKTV,KETQUA) " +
                            $"VALUES('{Esc(f.MaHSBA)}',N'{Esc(f.LoaiDV)}',TO_DATE('{f.NgayDV:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaKTV)}',N'Demo')"));
                }
            };
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn dòng dịch vụ."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                string dv = grid.CurrentRow.Cells["LOAIDV"].Value?.ToString();
                string ngay = Convert.ToDateTime(grid.CurrentRow.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy");
                if (MessageBox.Show($"Xóa dịch vụ '{dv}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("3.3.d", "AuditIllegalHSBADV", "VPD chặn",
                    () => service.ExecuteNonQuery(
                        $"DELETE FROM QLBV.VW_KTV_XemHSBADV WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(dv)}' AND NGAYDV=TO_DATE('{ngay}','DD/MM/YYYY')"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnAdd, btnDel,
                Note("Thêm hoặc xóa dịch vụ được phân công."))));
            parent.TabPages.Add(page);
            try { LoadGrid(grid, "SELECT * FROM QLBV.VW_KTV_XemHSBADV"); } catch { }
        }

        // [TB2] BN — xóa HSBA
        private void BuildDemo_BnHsba(TabControl parent)
        {
            var page = MakeTab("Hồ Sơ Bệnh Án");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var btnDel = QuickBtn("Xóa Hồ Sơ", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 110);

            btnRe.Click += (s, e) =>
            {
                try { LoadGrid(grid, "SELECT * FROM QLBV.HSBA WHERE ROWNUM <= 20"); }
                catch (Exception ex) { HandleDemoAction("TB2-load", "AuditFailBNDeleteHSBA", "Vượt quyền", () => { throw ex; }); }
            };
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn hồ sơ cần xóa."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                if (MessageBox.Show($"Xóa hồ sơ '{ma}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB2", "AuditFailBNDeleteHSBA", "Vượt quyền DELETE",
                    () => service.ExecuteNonQuery($"DELETE FROM QLBV.HSBA WHERE MAHSBA='{Esc(ma)}'"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnDel,
                Note("Chọn hồ sơ → Xóa Hồ Sơ."))));
            parent.TabPages.Add(page);
        }

        // [TB5] BN — sửa kết quả dịch vụ
        private void BuildDemo_BnDichVu(TabControl parent)
        {
            var page = MakeTab("Kết Quả Dịch Vụ");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnRe.Click += (s, e) =>
            {
                try { LoadGrid(grid, "SELECT * FROM QLBV.HSBA_DV WHERE ROWNUM <= 30"); }
                catch (Exception ex) { HandleDemoAction("TB5-load", "AuditFailBNUpdateDV", "Vượt quyền", () => { throw ex; }); }
            };

            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var r = grid.Rows[e.RowIndex];
                string ma = r.Cells["MAHSBA"].Value?.ToString();
                string dv = r.Cells["LOAIDV"].Value?.ToString();
                string ngay = Convert.ToDateTime(r.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy");
                var fields = new Dictionary<string, string> { { "KETQUA", r.Cells["KETQUA"].Value?.ToString() } };
                using (var f = new EditRowForm($"Cập nhật kết quả: {dv}", fields))
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    HandleDemoAction("TB5", "AuditFailBNUpdateDV", "Vượt quyền UPDATE",
                        () => service.ExecuteNonQuery(
                            $"UPDATE QLBV.HSBA_DV SET KETQUA=N'{Esc(f.NewValues["KETQUA"])}' " +
                            $"WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(dv)}' AND NGAYDV=TO_DATE('{ngay}','DD/MM/YYYY')"));
                }
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe,
                Note("Nhấp đúp vào dòng để cập nhật kết quả dịch vụ."))));
            parent.TabPages.Add(page);
        }

        // [TB7] BN — tạo HSBA cấp cứu (proc của BS)
        private void BuildDemo_BnCapCuu(TabControl parent)
        {
            var page = MakeTab("Cấp Cứu");
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32, 28, 32, 28), BackColor = Color.FromArgb(255, 248, 248) };

            pnl.Controls.Add(new Label
            {
                Text = "Yêu cầu khởi tạo hồ sơ bệnh án cấp cứu",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 40, 40),
                AutoSize = true, Location = new Point(0, 0)
            });
            pnl.Controls.Add(new Label
            {
                Text = "Chức năng này thường dành cho Bác sĩ. Bạn có thể gửi yêu cầu — hệ thống sẽ kiểm tra quyền.",
                Location = new Point(0, 36), Size = new Size(600, 40), Font = new Font("Segoe UI", 9.5F), ForeColor = Color.Gray
            });

            var btn = QuickBtn("🚨 Tạo HSBA Cấp Cứu", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 200, 44);
            btn.Location = new Point(0, 88);
            btn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btn.Click += (s, e) =>
            {
                using (var f = new CapCuuForm())
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    HandleDemoAction("TB7", "AuditBonusBNExecProc", "Vượt quyền EXECUTE",
                        () => service.ExecuteNonQuery(
                            $"BEGIN QLBV.sp_KhoiTaoHSBAKhancap('{Esc(f.MaHSBA)}','{Esc(f.MaBN)}','BS0001','{Esc(f.MaKhoa)}'); END;"));
                }
            };

            pnl.Controls.Add(btn);
            page.Controls.Add(pnl);
            parent.TabPages.Add(page);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  KỸ THUẬT VIÊN  (TC#4)
        // ═══════════════════════════════════════════════════════════════════

        private DataGridView dgvKtvDV;

        private void BuildKTVInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân"); 
            var tDV = MakeTab("Dịch Vụ Được Giao");
            var tTB = MakeTab("Thông Báo Khẩn (OLS)");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo);
            BuildKTV_DV(tDV); BuildThongBaoTab(tTB, false);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tDV, tTB, tDemo });
            parent.Controls.Add(tabs);
        }

        // ✅ [GỘPCODE-2] Tên cột HSBA_DV đã sửa theo schema_phanhe2.sql
        //    Nguồn: schema_phanhe2.sql — PRIMARY KEY (MAHSBA, LOAIDV, NGAYDV)
        //    Trước: MADV, NGAY → Sau: LOAIDV, NGAYDV (đúng tên cột DDL).
        //    UPDATE KETQUA dùng đúng PK composite (MAHSBA, LOAIDV, NGAYDV).
        private void BuildKTV_DV(TabPage tab)
        {
            dgvKtvDV = MakeGrid(true); // Read-only

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvKtvDV, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.VW_KTV_XemHSBADV" : $"SELECT * FROM QLBV.VW_KTV_XemHSBADV WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };
            btnRe.Click += (s, e) => LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.VW_KTV_XemHSBADV");

            dgvKtvDV.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvKtvDV.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string loaiDv = r.Cells["LOAIDV"].Value?.ToString();
                    string ngayDv = r.Cells["NGAYDV"].Value != null ? Convert.ToDateTime(r.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy") : "";

                    var fields = new Dictionary<string, string> {
                    { "KETQUA", r.Cells["KETQUA"].Value?.ToString() }
                };
                    using (var f = new EditRowForm($"Cập nhật Kết quả Dịch vụ: {loaiDv}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.VW_KTV_XemHSBADV SET KETQUA=N'{Esc(f.NewValues["KETQUA"])}' WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(loaiDv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.VW_KTV_XemHSBADV");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvKtvDV, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng dịch vụ để điền KẾT QUẢ."))));
            LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.VW_KTV_XemHSBADV");
        }

        private void KTV_LuuKetQua()
        {
            var dt = dgvKtvDV.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.VW_KTV_XemHSBADV SET KETQUA=N'{Esc(r["KETQUA"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}' AND LOAIDV=N'{Esc(r["LOAIDV"].ToString())}' AND NGAYDV=TO_DATE('{Convert.ToDateTime(r["NGAYDV"]):dd/MM/yyyy}','DD/MM/YYYY')"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu kết quả {n} dịch vụ (ghi vết audit)."); LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.VW_KTV_XemHSBADV"); } else Ok("Không có thay đổi nào.");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BỆNH NHÂN  (TC#5)  — redesigned BG
        // ═══════════════════════════════════════════════════════════════════

        private void BuildBNInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildBN_Info(tInfo);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tDemo });
            parent.Controls.Add(tabs);
        }

        private void BuildBN_Info(TabPage tab)
        {
            // Màu nền dịu mắt cho BN
            tab.BackColor = Color.FromArgb(240, 249, 255);

            var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(240, 249, 255) };

            // Card trắng nổi lên
            var card = new Panel
            {
                Width = 740,
                AutoSize = true,
                BackColor = Color.White,
                Padding = new Padding(32, 24, 32, 24),
                Left = 30,
                Top = 24
            };
            card.Paint += (s, e) =>
            {
                // Nhẹ shadow bằng border
                var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                e.Graphics.DrawRectangle(new System.Drawing.Pen(Color.FromArgb(190, 220, 240), 1), rect);
            };

            // Header màu gradient nhẹ bên trong card
            var cardHeader = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.BrandeisBlue };
            cardHeader.Controls.Add(new Label
            {
                Text = "Thông tin cá nhân của bạn",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = UiTheme.WhiteText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });

            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(0, 16, 0, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460F));

            int row = 0;
            BNField(layout, row++, "Họ và tên", out txtBnFullName, readOnly: true);
            BNField(layout, row++, "Số CCCD", out txtBnCccd, readOnly: true);
            BNField(layout, row++, "Giới tính", out txtBnGender, readOnly: true);
            BNField(layout, row++, "Ngày sinh", out txtBnDob, readOnly: true);
            BNField(layout, row++, "Số nhà", out txtBnSonha);
            BNField(layout, row++, "Tên đường", out txtBnDuong);
            BNField(layout, row++, "Quận / Huyện", out txtBnQuan);
            BNField(layout, row++, "Tỉnh / Thành phố", out txtBnTinh);
            BNField(layout, row++, "Tiền sử bệnh", out txtBnMedicalHistory, multiline: true);
            BNField(layout, row++, "Tiền sử bệnh GĐ", out txtBnFamilyHistory, multiline: true);
            BNField(layout, row++, "Dị ứng thuốc", out txtBnDrugAllergies, multiline: true);

            // Divider
            var divider = new Panel { Height = 2, BackColor = Color.FromArgb(200, 230, 250), Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 8) };

            var btnSave = QuickBtn("Cập Nhật Thông Tin", UiTheme.PastelGreen, UiTheme.DeepBlue, 200, 42);
            btnSave.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnSave.Click += BN_Luu;

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            btnRow.Controls.Add(btnSave);

            card.Controls.Add(btnRow);
            card.Controls.Add(divider);
            card.Controls.Add(layout);
            card.Controls.Add(cardHeader);

            // Tự động căn giữa card khi resize cửa sổ
            outer.Resize += (s, e) => { card.Left = Math.Max(0, (outer.Width - card.Width) / 2); };

            outer.Controls.Add(card);
            tab.Controls.Add(outer);
            LoadBNInfo();
        }

        private static void BNField(TableLayoutPanel layout, int row, string label, out TextBox box, bool readOnly = false, bool multiline = false)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lbl = new Label { Text = label + ":", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 10, 10, 0) };
            box = new TextBox
            {
                Width = 440,
                Font = UiTheme.BodyFont,
                ReadOnly = readOnly,
                BackColor = readOnly ? Color.FromArgb(232, 244, 255) : Color.White,
                Multiline = multiline,
                Height = multiline ? 62 : 30,
                Margin = new Padding(0, 6, 0, 4),
                BorderStyle = BorderStyle.FixedSingle
            };
            layout.Controls.Add(lbl, 0, row);
            layout.Controls.Add(box, 1, row);
        }

        private void LoadBNInfo()
        {
            try
            {
                DataTable dt = service.Query("SELECT * FROM QLBV.VW_BenhNhan_Xemthongtin");
                if (dt.Rows.Count == 0) { Ok("Không tìm thấy thông tin bệnh nhân."); return; }
                DataRow r = dt.Rows[0];
                txtBnFullName.Text = r["TENBN"]?.ToString() ?? "";
                txtBnCccd.Text = r["CCCD"]?.ToString() ?? "";
                txtBnGender.Text = r["PHAI"]?.ToString() ?? "";
                if (DateTime.TryParse(r["NGAYSINH"]?.ToString(), out DateTime dob)) txtBnDob.Text = dob.ToString("dd/MM/yyyy");
                txtBnSonha.Text = r["SONHA"]?.ToString() ?? "";
                txtBnDuong.Text = r["TENDUONG"]?.ToString() ?? "";
                txtBnQuan.Text = r["QUANHUYEN"]?.ToString() ?? "";
                txtBnTinh.Text = r["TINHTP"]?.ToString() ?? "";
                txtBnMedicalHistory.Text = r["TIENSUBENH"]?.ToString() ?? "";
                txtBnFamilyHistory.Text = r["TIENSUBENHGD"]?.ToString() ?? "";
                txtBnDrugAllergies.Text = r["DIUNGTHUOC"]?.ToString() ?? "";
            }
            catch (Exception ex) { Err("Không thể tải dữ liệu: " + ex.Message); }
        }

        private void BN_Luu(object sender, EventArgs e)
        {
            try
            {
                service.ExecuteNonQuery($"UPDATE QLBV.VW_BenhNhan_Xemthongtin SET SONHA=N'{Esc(txtBnSonha.Text)}',TENDUONG=N'{Esc(txtBnDuong.Text)}',QUANHUYEN=N'{Esc(txtBnQuan.Text)}',TINHTP=N'{Esc(txtBnTinh.Text)}',TIENSUBENH=N'{Esc(txtBnMedicalHistory.Text)}',TIENSUBENHGD=N'{Esc(txtBnFamilyHistory.Text)}',DIUNGTHUOC=N'{Esc(txtBnDrugAllergies.Text)}'");
                Ok("Đã cập nhật thông tin thành công!");
            }
            catch (Exception ex) { Err("Lỗi: " + ex.Message); }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ADMIN
        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        //  BAN GIÁM ĐỐC
        // ═══════════════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════════════
        //  BAN GIÁM ĐỐC (Xem Full Bảng)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildGiamDocInterface(Panel parent)
        {
            var tabs = MakeTabs();

            // Tạo các tab
            var tNV = MakeTab("Báo Cáo: Nhân Viên");
            var tBN = MakeTab("Báo Cáo: Bệnh Nhân");
            var tHSBA = MakeTab("Báo Cáo: HSBA");
            var tDV = MakeTab("Báo Cáo: Dịch Vụ");
            var tDT = MakeTab("Báo Cáo: Đơn Thuốc");
            var tTB = MakeTab("Thông Báo (OLS)");

            // Đổ giao diện vào từng tab
            BuildAdmin_NV(tNV);  // Có bộ lọc chi nhánh
            BuildAdmin_BN(tBN);  // Có bộ lọc chi nhánh

            // Chức năng tìm kiếm đã truyền đúng cột MAHSBA
            BuildGiamDoc_ReportTab(tHSBA, "SELECT * FROM QLBV.HSBA", "MAHSBA", "Tìm theo mã HSBA...");
            BuildGiamDoc_ReportTab(tDV, "SELECT * FROM QLBV.HSBA_DV", "MAHSBA", "Tìm theo mã HSBA...");
            BuildGiamDoc_ReportTab(tDT, "SELECT * FROM QLBV.DONTHUOC", "MAHSBA", "Tìm theo mã HSBA...");
            BuildThongBaoTab(tTB, false); // Xem thông báo

            // Nhét tất cả vào màn hình chính
            tabs.TabPages.AddRange(new[] {tNV, tBN, tHSBA, tDV, tDT, tTB });
            parent.Controls.Add(tabs);
        }
        private void BuildAdminInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tTB     = MakeTab("Thông Báo Khẩn (OLS)");
            var tAudit  = MakeTab("Kiểm Toán (Audit)");
            var tBackup = MakeTab("Sao Lưu (Backup)");
            var tTK     = MakeTab("Quản Lý Tài Khoản");

            var tOLS = MakeTab("OLS Bảo Mật");

            BuildThongBaoTab(tTB, true);
            BuildAdmin_Audit(tAudit);
            BuildAdmin_Backup(tBackup);
            BuildAdmin_QuanLyTK(tTK);
            BuildAdmin_OLS(tOLS);

            tabs.TabPages.AddRange(new[] { tTB, tAudit, tBackup, tTK, tOLS });
            parent.Controls.Add(tabs);
        }

        // ---------------------------------------------------------------------
        //  TÍNH NĂNG KIỂM TOÁN (AUDIT) - YÊU CẦU 3 
        // ---------------------------------------------------------------------
        // ✅ [GỘPCODE-F] Audit UI: 2 chế độ xem — Nghiệp vụ QLBV + Đăng Nhập Thất Bại
        //    Nguồn: sys_PH2.sql §3.1 — AuditSession (LOGON whenever not successful)
        //           sys_PH2.sql §3.4 — Query đọc nhật ký
        private void BuildAdmin_Audit(TabPage tab)
        {
            var grid    = MakeGrid(true);

            // ── §3.4.5: Tất cả QLBV (tổng hợp Standard + FGA)
            var btnAll  = QuickBtn("Tất Cả QLBV",           Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,                 175);
            // ── §3.4.1: Đăng nhập thất bại (AuditSession)
            var btnLog  = QuickBtn("Đăng Nhập Thất Bại",    Color.FromArgb(206, 17,  38),  UiTheme.WhiteText,                175);
            // ── §3.4.2: Standard Audit policies (16 policies run/06.sql §3.2)
            var btnStd  = QuickBtn("Standard Audit (§3.4.2)", Color.FromArgb(0,  120, 180), UiTheme.WhiteText,                185);
            // ── §3.4.3: FGA policies (AuditSuaDonThuoc, AuditBSUpdateHSBA, AuditKTVUpdateKetQua)
            var btnFga  = QuickBtn("FGA Policies (§3.4.3)",  Color.FromArgb(120, 80, 180),  UiTheme.WhiteText,                170);
            // ── §3.4.4: Đơn thuốc INSERT/UPDATE (AuditDonThuocInsert, AuditDonThuocUpdate)
            var btnDT   = QuickBtn("Đơn Thuốc (§3.4.4)",    Color.FromArgb(30,  160, 100),  UiTheme.WhiteText,                155);
            // ── §3.3.c/d: Thao tác bất hợp pháp (AuditIllegalUpdateHSBA, AuditIllegalHSBADV)
            var btnIll  = QuickBtn("Bất Hợp Pháp (§3.3)",   Color.FromArgb(180,  60,  60),  UiTheme.WhiteText,                165);

            btnAll.Click  += (s, e) => LoadAuditData(grid);
            btnLog.Click  += (s, e) => LoadLoginFailures(grid);
            btnStd.Click  += (s, e) => LoadStandardAudit(grid);
            btnFga.Click  += (s, e) => LoadFgaAudit(grid);
            btnDT.Click   += (s, e) => LoadDonThuocAudit(grid);
            btnIll.Click  += (s, e) => LoadIllegalAudit(grid);

            grid.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0) return;
                string colName = grid.Columns.Contains("CHI TIẾT MÔ TẢ") ? "CHI TIẾT MÔ TẢ"
                               : grid.Columns.Contains("CÂU SQL")         ? "CÂU SQL" : null;
                if (colName == null) return;
                var cell = grid.Rows[e.RowIndex].Cells[colName].Value;
                if (cell != null)
                    MessageBox.Show(cell.ToString(), "Chi Tiết Nhật Ký", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Toolbar 2 hàng để chứa 6 nút
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 98,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                Padding = new Padding(8, 6, 8, 4), BackColor = Color.FromArgb(238, 248, 255)
            };
            bar.Controls.Add(btnAll);
            bar.Controls.Add(btnLog);
            bar.Controls.Add(btnStd);
            bar.Controls.Add(btnFga);
            bar.Controls.Add(btnDT);
            bar.Controls.Add(btnIll);
            bar.Controls.Add(Note("§3.4.5 Tất cả  |  §3.4.1 Đăng nhập  |  §3.4.2 Standard (18 policies)  |  §3.4.3 FGA  |  §3.4.4 Đơn thuốc  |  §3.3 Bất hợp pháp"));

            var wrapper = new Panel { Dock = DockStyle.Fill };
            wrapper.Controls.Add(grid);
            wrapper.Controls.Add(bar);
            tab.Controls.Add(wrapper);
            // Lazy load: chỉ query khi user mở tab lần đầu
            bool _auditLoaded = false;
            tab.Enter += (s, e) => { if (_auditLoaded) return; _auditLoaded = true; LoadAuditData(grid); };
        }

        private void LoadLoginFailures(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                                         AS ""NGƯỜI DÙNG"",
                        ACTION_NAME                                        AS ""HÀNH ĐỘNG"",
                        RETURN_CODE                                        AS ""MÃ LỖI"",
                        OS_USERNAME                                        AS ""OS USER"",
                        UNIFIED_AUDIT_POLICIES                             AS ""POLICY""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE ACTION_NAME = 'LOGON'
                      AND RETURN_CODE <> 0
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 100 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký đăng nhập thất bại:\n" + ex.Message);
            }
        }

        // ── §3.4.2: Standard Audit — khớp run/06.sql §3.4.2 (18 policies)
        private void LoadStandardAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_SCHEMA             AS ""SCHEMA"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        RETURN_CODE               AS ""MÃ KQ"",
                        UNIFIED_AUDIT_POLICIES    AS ""POLICY"",
                        SQL_TEXT                  AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN (
                        'AUDITSUCDPVUPDATEBN','AUDITSUCBSSELECTVIEW','AUDITSUCBSEXECPROC',
                        'AUDITFAILBSEXECFUNC','AUDITSUCDPVEXECFUNC','AUDITDPVUPDATEHSBA',
                        'AUDITSUCKTVUPDATEDV','AUDITSUCBSUPDATEDT',
                        'AUDITDONTHUOCINSERT','AUDITDONTHUOCUPDATE',
                        'AUDITBONUSBNSELECTINFO','AUDITBONUSBNEXECPROC',
                        'AUDITFAILBSUPDATENV','AUDITFAILBNDELETEHSBA','AUDITFAILKTVDELETEDT',
                        'AUDITFAILDPVDELETEHSBA','AUDITFAILBNUPDATEDV','AUDITFAILKTVSELECTBN'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải Standard Audit:\n" + ex.Message);
            }
        }

        // ── §3.3.c/d: AuditIllegalUpdateHSBA + AuditIllegalHSBADV
        private void LoadIllegalAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        RETURN_CODE               AS ""MÃ KQ"",
                        UNIFIED_AUDIT_POLICIES    AS ""POLICY"",
                        SQL_TEXT                  AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN (
                        'AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký bất hợp pháp:\n" + ex.Message);
            }
        }

        // ── §3.4.3: FGA policies (run/06.sql §3.3)
        private void LoadFgaAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME          AS ""NGƯỜI DÙNG"",
                        FGA_POLICY_NAME     AS ""FGA POLICY"",
                        OBJECT_NAME         AS ""ĐỐI TƯỢNG"",
                        SQL_TEXT            AS ""CÂU SQL""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(FGA_POLICY_NAME) IN (
                        'AUDITSUADONTHUOC',
                        'AUDITBSUPDATEHSBA',
                        'AUDITKTVUPDATEKETQUA'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải FGA Audit:\n" + ex.Message);
            }
        }

        // ── §3.4.4: Đơn thuốc INSERT/UPDATE — theo bảng DONTHUOC + các policy liên quan (06.sql §3.3.a+)
        private void LoadDonThuocAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_SCHEMA             AS ""SCHEMA"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        RETURN_CODE               AS ""MÃ KQ"",
                        NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES) AS ""POLICY"",
                        SQL_TEXT                  AS ""CÂU SQL""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE (
                        (OBJECT_SCHEMA = 'QLBV' AND OBJECT_NAME = 'DONTHUOC'
                         AND ACTION_NAME IN ('INSERT','UPDATE'))
                        OR UPPER(UNIFIED_AUDIT_POLICIES) IN (
                            'AUDITDONTHUOCINSERT','AUDITDONTHUOCUPDATE','AUDITSUCBSUPDATEDT'
                        )
                        OR UPPER(FGA_POLICY_NAME) = 'AUDITSUADONTHUOC'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký đơn thuốc:\n" + ex.Message);
            }
        }

        // ✅ [GỘPCODE-5] Audit SQL gộp từ sys_PH2.sql §3.4
        private void LoadAuditData(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME AS ""NGƯỜI DÙNG"",
                        ACTION_NAME AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME AS ""ĐỐI TƯỢNG"",
                        NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES) AS ""POLICY"",
                        SQL_TEXT AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE OBJECT_SCHEMA = 'QLBV'
                      AND DBUSERNAME NOT IN ('SYS', 'SYSTEM')
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";

                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký audit:\n" + ex.Message);
            }
        }

        // ---------------------------------------------------------------------
        //  TÍNH NĂNG SAO LƯU & PHỤC HỒI (BACKUP/RESTORE) - YÊU CẦU 4
        // ---------------------------------------------------------------------
        // 07.sql + 08.sql: 6 sub-tab Sao Lưu & Phục Hồi
        private void BuildAdmin_Backup(TabPage tab)
        {
            var subTabs = new TabControl { Dock = DockStyle.Fill };
            var tPump   = new TabPage("Data Pump");
            var tCmd    = new TabPage("CMD / PowerShell");
            var tHist   = new TabPage("Lịch Sử & Phục Hồi");
            var tFlashQ = new TabPage("Flashback Query");
            var tFlashT = new TabPage("Flashback Table");
            var tSched  = new TabPage("Scheduler");
            BuildBackup_DataPump(tPump);
            BuildBackup_CmdPanel(tCmd);
            BuildBackup_HistoryPanel(tHist);
            BuildBackup_Flashback(tFlashQ);
            BuildBackup_FlashbackTable(tFlashT);
            BuildBackup_Scheduler(tSched);
            subTabs.TabPages.AddRange(new[] { tPump, tCmd, tHist, tFlashQ, tFlashT, tSched });
            tab.Controls.Add(subTabs);
        }

        // ── 08.sql [07-QLBV-01, 07-QLBV-02]: Data Pump + BACKUP_HISTORY tracking
        private void BuildBackup_DataPump(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Oracle Data Pump — Sao Lưu & Phục Hồi  (08.sql [07-QLBV-01, 07-CMD])", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };

            var btnBackupSchema  = QuickBtn("Backup Schema QLBV",     UiTheme.BrandeisBlue,       UiTheme.WhiteText, 192, 38);
            var btnBackupTables  = QuickBtn("Backup Bảng Quan Trọng", Color.FromArgb(0, 140, 200), UiTheme.WhiteText, 200, 38);
            var btnRestoreSchema = QuickBtn("Restore Schema (impdp)", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 188, 38);
            var btnCheckDir      = QuickBtn("Kiểm Tra BACKUP_DIR",    Color.FromArgb(80, 80, 120), UiTheme.WhiteText, 161, 38);
            var btnTableList     = QuickBtn("Liệt Kê Bảng NV",      Color.FromArgb(100, 120, 180), UiTheme.WhiteText, 155, 38);
            var btnRowCount      = QuickBtn("Đếm Số Dòng Bảng",      Color.FromArgb(50, 150, 80), UiTheme.WhiteText, 154, 38);

            var flowBtn = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White };
            foreach (var b in new Control[] { btnBackupSchema, btnBackupTables, btnRestoreSchema, btnCheckDir, btnTableList, btnRowCount })
                flowBtn.Controls.Add(b);

            var lblHist  = new Label { Text = "Lịch Sử Sao Lưu — QLBV.BACKUP_HISTORY  (08.sql [07-QLBV-01])", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 };
            var btnReH   = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 78, 26);
            var barHist  = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barHist.Controls.Add(btnReH);
            var gridHist = MakeGrid(true);
            var txtLog   = new TextBox { Multiline = true, Dock = DockStyle.Bottom, Height = 210, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10F), ScrollBars = ScrollBars.Vertical, Text = "C:\\> Sẵn sàng. Bấm nút để thực hiện.\r\n" };

            void RefreshHist()
            {
                try
                {
                    gridHist.DataSource = service.Query(
                        "SELECT BACKUP_ID, BACKUP_NAME, BACKUP_TYPE, TO_CHAR(BACKUP_TIME,'DD/MM/YYYY HH24:MI') AS BACKUP_TIME, BACKUP_PATH, OBJECT_SCOPE, NOTE " +
                        "FROM QLBV.BACKUP_HISTORY ORDER BY BACKUP_TIME DESC FETCH FIRST 30 ROWS ONLY");
                    UiTheme.StyleGrid(gridHist);
                }
                catch { }
            }

            btnReH.Click += (s, e) => RefreshHist();

            // 07.sql [07-SYSDBA-01]: kiểm tra BACKUP_DIR và quyền
            btnCheckDir.Click += (s, e) =>
            {
                try
                {
                    txtLog.AppendText("\r\nSQL> SELECT directory_name, directory_path FROM dba_directories WHERE directory_name = 'BACKUP_DIR';\r\n");
                    var dt = service.Query("SELECT DIRECTORY_NAME, DIRECTORY_PATH FROM DBA_DIRECTORIES WHERE DIRECTORY_NAME = 'BACKUP_DIR'");
                    if (dt.Rows.Count > 0)
                        foreach (DataRow r in dt.Rows) txtLog.AppendText($"  {r[0],-16} {r[1]}\r\n");
                    else
                        txtLog.AppendText("  (Chưa có BACKUP_DIR — cần chạy 07.sql [07-SYSDBA-01] bằng SYSDBA)\r\n");
                    txtLog.AppendText("SQL> SELECT grantee, privilege FROM dba_tab_privs WHERE table_name = 'BACKUP_DIR';\r\n");
                    var dtG = service.Query("SELECT GRANTEE, PRIVILEGE FROM DBA_TAB_PRIVS WHERE TABLE_NAME = 'BACKUP_DIR' ORDER BY GRANTEE");
                    foreach (DataRow r in dtG.Rows) txtLog.AppendText($"  {r[0],-16} {r[1]}\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            // 08.sql [07-QLBV-02]: liệt kê bảng nghiệp vụ
            btnTableList.Click += (s, e) =>
            {
                try
                {
                    var dt = service.Query(
                        "SELECT TABLE_NAME FROM USER_TABLES " +
                        "WHERE TABLE_NAME IN ('KHOA','BENHNHAN','NHANVIEN','HSBA','HSBA_DV','DONTHUOC','THONGBAO') " +
                        "ORDER BY TABLE_NAME");
                    txtLog.AppendText("\r\nSQL> Danh sách bảng nghiệp vụ (08.sql [07-QLBV-02]):\r\n  TABLE_NAME\r\n  ----------\r\n");
                    foreach (DataRow r in dt.Rows) txtLog.AppendText($"  {r["TABLE_NAME"]}\r\n");
                    if (dt.Rows.Count == 0) txtLog.AppendText("  (Không tìm thấy bảng — kiểm tra schema QLBV)\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            // 08.sql [07-QLBV-02]: đếm số dòng 4 bảng ưu tiên backup
            btnRowCount.Click += (s, e) =>
            {
                try
                {
                    var dt = service.Query(
                        "SELECT 'BENHNHAN' AS TABLE_NAME, COUNT(*) AS ROW_COUNT FROM QLBV.BENHNHAN " +
                        "UNION ALL SELECT 'HSBA',     COUNT(*) FROM QLBV.HSBA " +
                        "UNION ALL SELECT 'HSBA_DV',  COUNT(*) FROM QLBV.HSBA_DV " +
                        "UNION ALL SELECT 'DONTHUOC', COUNT(*) FROM QLBV.DONTHUOC");
                    txtLog.AppendText("\r\nSQL> Số dòng bảng ưu tiên backup (08.sql [07-QLBV-02]):\r\n  TABLE_NAME    ROW_COUNT\r\n  ------------- ----------\r\n");
                    foreach (DataRow r in dt.Rows) txtLog.AppendText($"  {r["TABLE_NAME"],-14}{r["ROW_COUNT"]}\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            btnBackupSchema.Click += async (s, e) =>
            {
                string df = $"qlbv_schema_{DateTime.Now:yyyyMMdd_HHmm}.dmp";
                SetDataPumpButtonsEnabled(flowBtn, false);
                try
                {
                    string conn = QuoteCliArg(service.GetDataPumpConnectString());
                    string args = $"{conn} schemas=qlbv directory=backup_dir dumpfile={df} logfile=qlbv_schema_export.log";
                    int code = await RunDataPumpCliAsync("expdp", args, txtLog);
                    if (code == 0)
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thành công (exit 0). File: {df}\r\n");
                        string backupPath = GetBackupDirPath();
                        service.ExecuteNonQuery($"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_NAME, BACKUP_TYPE, BACKUP_PATH, OBJECT_SCOPE, NOTE) VALUES ('{Esc(df)}', 'schema', '{Esc(backupPath)}', 'qlbv', N'Backup schema QLBV qua expdp (WinForms)')");
                        service.ExecuteNonQuery("COMMIT");
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đã ghi vào QLBV.BACKUP_HISTORY.\r\n");
                        RefreshHist();
                    }
                    else
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thất bại (exit {code}). Không ghi BACKUP_HISTORY.\r\n");
                        Err($"expdp thất bại (mã thoát {code}). Xem log phía trên.");
                    }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI]: {ex.Message}\r\n");
                    Err(ex.Message);
                }
                finally { SetDataPumpButtonsEnabled(flowBtn, true); }
            };

            btnBackupTables.Click += async (s, e) =>
            {
                string df = $"qlbv_important_tables_{DateTime.Now:yyyyMMdd_HHmm}.dmp";
                SetDataPumpButtonsEnabled(flowBtn, false);
                try
                {
                    string conn = QuoteCliArg(service.GetDataPumpConnectString());
                    string args = $"{conn} tables=qlbv.benhnhan,qlbv.hsba,qlbv.hsba_dv,qlbv.donthuoc directory=backup_dir dumpfile={df} logfile=qlbv_important_tables_export.log";
                    int code = await RunDataPumpCliAsync("expdp", args, txtLog);
                    if (code == 0)
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thành công (exit 0). File: {df}\r\n");
                        string backupPath = GetBackupDirPath();
                        service.ExecuteNonQuery($"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_NAME, BACKUP_TYPE, BACKUP_PATH, OBJECT_SCOPE, NOTE) VALUES ('{Esc(df)}', 'tables', '{Esc(backupPath)}', 'benhnhan,hsba,hsba_dv,donthuoc', N'Backup bảng quan trọng qua expdp (WinForms)')");
                        service.ExecuteNonQuery("COMMIT");
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đã ghi vào QLBV.BACKUP_HISTORY.\r\n");
                        RefreshHist();
                    }
                    else
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thất bại (exit {code}). Không ghi BACKUP_HISTORY.\r\n");
                        Err($"expdp thất bại (mã thoát {code}). Xem log phía trên.");
                    }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI]: {ex.Message}\r\n");
                    Err(ex.Message);
                }
                finally { SetDataPumpButtonsEnabled(flowBtn, true); }
            };

            btnRestoreSchema.Click += async (s, e) =>
            {
                if (MessageBox.Show("CẢNH BÁO: Sẽ ghi đè toàn bộ schema QLBV.\nBạn có chắc chắn?", "Phục Hồi Schema", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                string defaultDump = "qlbv_schema_latest.dmp";
                try
                {
                    var dtDump = service.Query("SELECT BACKUP_NAME FROM QLBV.BACKUP_HISTORY WHERE BACKUP_TYPE IN ('schema','manual') ORDER BY BACKUP_TIME DESC FETCH FIRST 1 ROW ONLY");
                    if (dtDump.Rows.Count > 0 && dtDump.Rows[0][0] != DBNull.Value)
                        defaultDump = dtDump.Rows[0][0].ToString();
                }
                catch { }

                string df = PromptText("Phục Hồi Schema", "Tên file .dmp trong backup_dir:", defaultDump);
                if (df == null) return;

                SetDataPumpButtonsEnabled(flowBtn, false);
                try
                {
                    string conn = QuoteCliArg(service.GetDataPumpConnectString());
                    string args = $"{conn} schemas=qlbv directory=backup_dir dumpfile={df} logfile=qlbv_schema_import.log table_exists_action=replace";
                    int code = await RunDataPumpCliAsync("impdp", args, txtLog);
                    if (code == 0)
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Import thành công (exit 0). File: {df}\r\n");
                    else
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Import thất bại (exit {code}).\r\n");
                        Err($"impdp thất bại (mã thoát {code}). Xem log phía trên.");
                    }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI]: {ex.Message}\r\n");
                    Err(ex.Message);
                }
                finally { SetDataPumpButtonsEnabled(flowBtn, true); }
            };

            pnl.Controls.Add(txtLog);
            pnl.Controls.Add(gridHist);
            pnl.Controls.Add(barHist);
            pnl.Controls.Add(lblHist);
            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
            // Lazy load: chỉ query khi user mở tab lần đầu
            bool _dpLoaded = false;
            tab.Enter += (s, e) => { if (_dpLoaded) return; _dpLoaded = true; RefreshHist(); };
        }

        // ── 08.sql [07-CMD]: Tạo và chạy lệnh expdp/impdp trong CMD hoặc PowerShell
        private void BuildBackup_CmdPanel(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Lệnh expdp / impdp — Chạy trong CMD hoặc PowerShell  (08.sql [07-CMD])", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };
            var lblDesc  = new Label { Text = "Chọn loại lệnh, điền tham số → Sao Chép hoặc Chạy Trực Tiếp trong CMD (cần Oracle Client cài trên máy).", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(90, 90, 110), Dock = DockStyle.Top, Height = 22, AutoSize = false };

            var flowCfg = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White, WrapContents = false };
            var cmbCmd  = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 5, 12, 0) };
            cmbCmd.Items.AddRange(new object[] {
                "1. Backup toàn Schema QLBV (expdp schemas)",
                "2. Backup Bảng Quan Trọng (expdp tables)",
                "3. Restore toàn Schema QLBV (impdp schemas)",
                "4. Restore Một Bảng cụ thể (impdp tables)"
            });
            cmbCmd.SelectedIndex = 0;
            var txtHost = new TextBox { Text = "localhost:1521/xepdb1", Width = 190, Margin = new Padding(0, 6, 10, 0) };
            var txtDump = new TextBox { Text = "qlbv_schema_latest.dmp", Width = 210, Margin = new Padding(0, 6, 10, 0) };
            var txtTbl  = new TextBox { Text = "qlbv.donthuoc", Width = 165, Margin = new Padding(0, 6, 0,  0) };
            foreach (var c in new Control[] {
                new Label { Text = "Loại:",       AutoSize = true, Margin = new Padding(0, 10, 4, 0), Font = new Font("Segoe UI", 9F) }, cmbCmd,
                new Label { Text = "Host:",       AutoSize = true, Margin = new Padding(0, 10, 4, 0), Font = new Font("Segoe UI", 9F) }, txtHost,
                new Label { Text = "File dump:",  AutoSize = true, Margin = new Padding(0, 10, 4, 0), Font = new Font("Segoe UI", 9F) }, txtDump,
                new Label { Text = "Bảng (#4):", AutoSize = true, Margin = new Padding(0, 10, 4, 0), Font = new Font("Segoe UI", 9F) }, txtTbl
            }) flowCfg.Controls.Add(c);

            var txtCmd = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, BackColor = Color.FromArgb(20, 26, 32), ForeColor = Color.FromArgb(190, 235, 150), Font = new Font("Consolas", 11F), WordWrap = false, Dock = DockStyle.Fill };

            var btnCopy = QuickBtn("Sao Chép Lệnh",     UiTheme.BrandeisBlue,       UiTheme.WhiteText, 152, 40);
            var btnRun  = QuickBtn("▶ Chạy trong CMD",  Color.FromArgb(30, 160, 60), UiTheme.WhiteText, 162, 40);
            var btnManual = QuickBtn("Ghi BACKUP_HISTORY (manual)", Color.FromArgb(0, 120, 180), UiTheme.WhiteText, 220, 40);
            var flowAct = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            flowAct.Controls.Add(btnCopy);
            flowAct.Controls.Add(btnRun);
            flowAct.Controls.Add(btnManual);
            flowAct.Controls.Add(Note("Sau expdp thành công qua CMD → bấm 'Ghi BACKUP_HISTORY' (08.sql [07-CMD], backup_type=manual)."));

            string GetCmd()
            {
                string host = txtHost.Text.Trim(), dump = txtDump.Text.Trim(), tbl = txtTbl.Text.Trim();
                switch (cmbCmd.SelectedIndex)
                {
                    case 0: return $"expdp qlbv/123@{host} ^\r\n    schemas=qlbv ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_schema_export.log";
                    case 1: return $"expdp qlbv/123@{host} ^\r\n    tables=qlbv.benhnhan,qlbv.hsba,qlbv.hsba_dv,qlbv.donthuoc ^\r\n    directory=backup_dir ^\r\n    dumpfile=qlbv_important_tables.dmp ^\r\n    logfile=qlbv_important_tables_export.log";
                    case 2: return $"impdp qlbv/123@{host} ^\r\n    schemas=qlbv ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_schema_import.log ^\r\n    table_exists_action=replace";
                    case 3: return $"impdp qlbv/123@{host} ^\r\n    tables={tbl} ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_table_import.log ^\r\n    table_exists_action=replace";
                    default: return "";
                }
            }

            EventHandler updatePreview = (s, e) => txtCmd.Text = GetCmd();
            cmbCmd.SelectedIndexChanged += updatePreview;
            txtHost.TextChanged += updatePreview;
            txtDump.TextChanged += updatePreview;
            txtTbl.TextChanged  += updatePreview;
            txtCmd.Text = GetCmd();

            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(GetCmd()); Ok("Đã sao chép lệnh vào clipboard!"); }
                catch (Exception ex) { Err(ex.Message); }
            };

            btnRun.Click += (s, e) =>
            {
                string cmd = GetCmd().Replace(" ^\r\n    ", " ");
                if (MessageBox.Show($"Sẽ mở cửa sổ CMD và chạy lệnh:\r\n\r\n{cmd}\r\n\r\nXác nhận?", "Chạy lệnh", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/K \"{cmd}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { Err($"Không thể mở CMD: {ex.Message}\r\nHãy sao chép lệnh và chạy thủ công."); }
            };

            // 08.sql [07-CMD]: ghi lịch sử backup sau expdp thủ công (backup_type = 'manual')
            btnManual.Click += (s, e) =>
            {
                string dump = txtDump.Text.Trim();
                if (string.IsNullOrEmpty(dump)) { Err("Nhập tên file dump trước."); return; }
                string btype = cmbCmd.SelectedIndex <= 1 ? (cmbCmd.SelectedIndex == 0 ? "schema" : "tables") : "manual";
                string scope = cmbCmd.SelectedIndex == 0 ? "qlbv"
                             : cmbCmd.SelectedIndex == 1 ? "benhnhan,hsba,hsba_dv,donthuoc"
                             : cmbCmd.SelectedIndex == 3 ? txtTbl.Text.Trim() : "qlbv";
                if (cmbCmd.SelectedIndex >= 2) btype = "manual";
                try
                {
                    service.ExecuteNonQuery(
                        $"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_NAME, BACKUP_TYPE, BACKUP_PATH, OBJECT_SCOPE, NOTE) " +
                        $"VALUES ('{Esc(dump)}', '{btype}', 'C:\\oracle_backup', '{Esc(scope)}', N'Backup thủ công qua expdp CMD (08.sql [07-CMD])')");
                    service.ExecuteNonQuery("COMMIT");
                    Ok($"Đã ghi vào QLBV.BACKUP_HISTORY (backup_type={btype}).");
                }
                catch (Exception ex) { Err(ex.Message); }
            };

            pnl.Controls.Add(flowAct);
            pnl.Controls.Add(txtCmd);
            pnl.Controls.Add(flowCfg);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
        }

        // ── 08.sql [07-RESTORE-HISTORY, 07-AUDIT-01]: Lịch sử sao lưu + phục hồi + phân tích sự cố
        private void BuildBackup_HistoryPanel(TabPage tab)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 210, BackColor = Color.White };

            // PANEL TOP: BACKUP_HISTORY
            var gridB = MakeGrid(true);
            var btnRB = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 78, 26);
            var barB  = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barB.Controls.Add(btnRB);
            split.Panel1.Controls.Add(gridB);
            split.Panel1.Controls.Add(barB);
            split.Panel1.Controls.Add(new Label { Text = "QLBV.BACKUP_HISTORY — Lịch Sử Sao Lưu", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 26 });

            // PANEL BOTTOM: RESTORE_HISTORY + controls
            var gridR    = MakeGrid(true);
            var btnRR    = QuickBtn("Tải Lại",               Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,  78, 26);
            var btnAdd   = QuickBtn("+ Ghi Nhận Phục Hồi",  UiTheme.BrandeisBlue,          UiTheme.WhiteText, 188, 26);
            var btnAudit = QuickBtn("Phân Tích Sự Cố (07-AUDIT-01)", Color.FromArgb(80, 80, 120), UiTheme.WhiteText, 232, 26);
            var barR     = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barR.Controls.AddRange(new Control[] { btnRR, btnAdd, btnAudit });
            split.Panel2.Controls.Add(gridR);
            split.Panel2.Controls.Add(barR);
            split.Panel2.Controls.Add(new Label { Text = "QLBV.RESTORE_HISTORY — Lịch Sử Phục Hồi", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 26 });

            void RefreshB() { try { gridB.DataSource = service.Query("SELECT * FROM QLBV.BACKUP_HISTORY ORDER BY BACKUP_TIME DESC FETCH FIRST 30 ROWS ONLY"); UiTheme.StyleGrid(gridB); } catch { } }
            void RefreshR() { try { gridR.DataSource = service.Query("SELECT * FROM QLBV.RESTORE_HISTORY ORDER BY RESTORE_TIME DESC FETCH FIRST 30 ROWS ONLY"); UiTheme.StyleGrid(gridR); } catch { } }

            btnRB.Click += (s, e) => RefreshB();
            btnRR.Click += (s, e) => RefreshR();

            // 08.sql [07-AUDIT-01]: query audit trail xác định sự cố trước khi restore
            btnAudit.Click += (s, e) =>
            {
                var dlg    = new Form { Text = "Phân Tích Sự Cố — UNIFIED_AUDIT_TRAIL  (08.sql [07-AUDIT-01])", Width = 1100, Height = 600, StartPosition = FormStartPosition.CenterParent };
                var gAudit = MakeGrid(true);
                var cmb    = new ComboBox { Width = 420, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 4, 8, 0) };
                cmb.Items.AddRange(new object[] {
                    "Theo tên bảng (BENHNHAN/HSBA/HSBA_DV/DONTHUOC/NHANVIEN)",
                    "Theo audit policy (08.sql [07-AUDIT-01] query 2)",
                    "FGA trail riêng (DBA_FGA_AUDIT_TRAIL)"
                });
                cmb.SelectedIndex = 0;
                var btnQ = QuickBtn("Truy Vấn", UiTheme.BrandeisBlue, UiTheme.WhiteText, 100, 30);
                var barA = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4, 5, 4, 0) };
                barA.Controls.Add(new Label { Text = "Lọc theo:", AutoSize = true, Margin = new Padding(0, 7, 4, 0), Font = new Font("Segoe UI", 9F) });
                barA.Controls.Add(cmb);
                barA.Controls.Add(btnQ);
                btnQ.Click += (ss, ee) =>
                {
                    try
                    {
                        string sql;
                        if (cmb.SelectedIndex == 0)
                        {
                            // 08.sql [07-AUDIT-01] query 1
                            sql = "SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT FROM UNIFIED_AUDIT_TRAIL WHERE OBJECT_SCHEMA = 'QLBV' AND OBJECT_NAME IN ('BENHNHAN','HSBA','HSBA_DV','DONTHUOC','NHANVIEN') ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        else if (cmb.SelectedIndex == 1)
                        {
                            // 08.sql [07-AUDIT-01] query 2 — verbatim
                            sql = @"SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT
FROM UNIFIED_AUDIT_TRAIL
WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN (
    'AUDITSUCDPVUPDATEBN','AUDITSUCBSUPDATEDT','AUDITSUCDPVEXECFUNC',
    'AUDITDONTHUOCINSERT','AUDITDONTHUOCUPDATE',
    'AUDITFAILBSUPDATENV','AUDITFAILBNDELETEHSBA','AUDITFAILKTVDELETEDT',
    'AUDITFAILDPVDELETEHSBA','AUDITFAILBNUPDATEDV','AUDITFAILKTVSELECTBN',
    'AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV'
) OR UPPER(FGA_POLICY_NAME) IN (
    'AUDITSUADONTHUOC','AUDITBSUPDATEHSBA','AUDITKTVUPDATEKETQUA'
)
ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        else
                        {
                            // 08.sql [07-AUDIT-01] query 3 — DBA_FGA_AUDIT_TRAIL
                            sql = @"SELECT TIMESTAMP AS EVENT_TIMESTAMP, DB_USER AS DBUSERNAME, OBJECT_SCHEMA, OBJECT_NAME,
    POLICY_NAME AS FGA_POLICY_NAME, STATEMENT_TYPE AS ACTION_NAME, SQL_TEXT
FROM DBA_FGA_AUDIT_TRAIL
WHERE OBJECT_SCHEMA = 'QLBV'
  AND UPPER(POLICY_NAME) IN ('AUDITSUADONTHUOC','AUDITBSUPDATEHSBA','AUDITKTVUPDATEKETQUA')
ORDER BY TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        gAudit.DataSource = service.Query(sql);
                        UiTheme.StyleGrid(gAudit);
                    }
                    catch (Exception ex) { Err(ex.Message); }
                };
                var pA = new Panel { Dock = DockStyle.Fill };
                pA.Controls.Add(gAudit);
                pA.Controls.Add(barA);
                dlg.Controls.Add(pA);
                dlg.ShowDialog();
            };

            // 08.sql [07-RESTORE-HISTORY]: INSERT vào restore_history sau khi impdp hoàn tất
            btnAdd.Click += (s, e) =>
            {
                var dlg    = new Form { Text = "Ghi Nhận Kết Quả Phục Hồi — QLBV.RESTORE_HISTORY  (08.sql [07-RESTORE-HISTORY])", Width = 560, Height = 470, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
                var layout = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155F));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 345F));
                int rowIdx = 0;

                TextBox MkField(string lbl, string def = "", bool multi = false)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.Controls.Add(new Label { Text = lbl + ":", AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 10, 8, 0), Anchor = AnchorStyles.Right }, 0, rowIdx);
                    var tb = new TextBox { Text = def, Width = 335, Multiline = multi, Height = multi ? 52 : 26, Font = new Font("Segoe UI", 9.5F), Margin = new Padding(0, 6, 0, 0) };
                    layout.Controls.Add(tb, 1, rowIdx);
                    rowIdx++;
                    return tb;
                }

                var tBakName  = MkField("Tên file dump");
                var tObject   = MkField("Object phục hồi", "qlbv.donthuoc");
                var tIncident = MkField("Thời điểm sự cố", DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"));
                var tUser     = MkField("User gây sự cố");
                var tAction   = MkField("Hành động (UPDATE/DELETE...)");
                var tAuditObj = MkField("Object audit");
                var tNote     = MkField("Ghi chú", "", multi: true);

                var btnSave   = QuickBtn("Lưu",  UiTheme.BrandeisBlue,         UiTheme.WhiteText, 100, 36);
                var btnCancel = QuickBtn("Hủy",  Color.FromArgb(200, 200, 200), UiTheme.DeepBlue,   80, 36);
                var flowF = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(16, 8, 0, 0) };
                flowF.Controls.AddRange(new Control[] { btnSave, btnCancel });

                btnSave.Click += (ss, ee) =>
                {
                    try
                    {
                        string ts = string.IsNullOrWhiteSpace(tIncident.Text) ? "NULL" : $"TO_TIMESTAMP('{Esc(tIncident.Text)}','YYYY-MM-DD HH24:MI:SS')";
                        service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.RESTORE_HISTORY (BACKUP_NAME, RESTORE_OBJECT, INCIDENT_TIME, INCIDENT_USER, INCIDENT_ACTION, AUDIT_OBJECT_NAME, NOTE) " +
                            $"VALUES (N'{Esc(tBakName.Text)}', N'{Esc(tObject.Text)}', {ts}, N'{Esc(tUser.Text)}', N'{Esc(tAction.Text)}', N'{Esc(tAuditObj.Text)}', N'{Esc(tNote.Text)}')");
                        service.ExecuteNonQuery("COMMIT");
                        Ok("Đã ghi nhận vào QLBV.RESTORE_HISTORY!");
                        dlg.DialogResult = DialogResult.OK;
                        RefreshR();
                    }
                    catch (Exception ex) { Err(ex.Message); }
                };
                btnCancel.Click += (ss, ee) => dlg.Close();

                var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                scroll.Controls.Add(layout);
                dlg.Controls.Add(flowF);
                dlg.Controls.Add(scroll);
                dlg.ShowDialog();
            };

            tab.Controls.Add(split);
            // Lazy load: chỉ query khi user mở tab lần đầu
            bool _histLoaded = false;
            tab.Enter += (s, e) => { if (_histLoaded) return; _histLoaded = true; RefreshB(); RefreshR(); };
        }

        // Flashback Query — run/05.sql §Câu 4: Khôi phục dữ liệu theo thời điểm
        private void BuildBackup_Flashback(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 0), BackColor = Color.White };

            var lblTitle = new Label
            {
                Text = "Khôi Phục Dữ Liệu Qua Flashback Query  (run/05.sql §Câu 4)",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Dock = DockStyle.Top, Height = 38
            };
            var lblDesc = new Label
            {
                Text = "Thực hiện theo 3 bước:  Bước 1 → ghi nhận timestamp & dữ liệu gốc  |  Bước 2 → giả lập làm hỏng dữ liệu  |  Bước 3 → khôi phục bằng AS OF TIMESTAMP",
                Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(90, 90, 110),
                Dock = DockStyle.Top, Height = 26, AutoSize = false
            };

            // Input row: MABN
            var flowInput = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 8, 0, 0), BackColor = Color.White
            };
            var lblMabn = new Label { Text = "Mã bệnh nhân (MABN):", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 6, 8, 0) };
            var txtMabn = new TextBox { Text = "BN000001", Width = 150, Font = UiTheme.BodyFont, Margin = new Padding(0, 4, 0, 0) };
            flowInput.Controls.Add(lblMabn);
            flowInput.Controls.Add(txtMabn);

            // Step buttons + DateTimePicker
            var flowSteps = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 54,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 6, 0, 4), BackColor = Color.White
            };
            var btnStep1 = QuickBtn("Bước 1: Ghi Nhận Thời Điểm Gốc",    UiTheme.BrandeisBlue,              UiTheme.WhiteText,  245, 40);
            var btnStep2 = QuickBtn("Bước 2: Giả Lập Làm Hỏng Dữ Liệu",  Color.FromArgb(206, 17, 38),       UiTheme.WhiteText,  248, 40);
            var dtpFlash = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss",
                ShowUpDown = true, Width = 210,
                Margin = new Padding(10, 5, 6, 0), Value = DateTime.Now
            };
            var btnStep3 = QuickBtn("Bước 3: Khôi Phục Flashback",        Color.FromArgb(30, 160, 100),       UiTheme.WhiteText,  225, 40);
            flowSteps.Controls.Add(btnStep1);
            flowSteps.Controls.Add(btnStep2);
            flowSteps.Controls.Add(dtpFlash);
            flowSteps.Controls.Add(btnStep3);

            var lblDtp = new Label
            {
                Text = "← Điều chỉnh mốc thời gian (tự điền sau Bước 1) rồi bấm Bước 3",
                Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(100, 100, 120),
                Dock = DockStyle.Top, Height = 22, AutoSize = false
            };

            var txtLog = new TextBox
            {
                Multiline = true, Dock = DockStyle.Bottom, Height = 360, ReadOnly = true,
                BackColor = Color.Black, ForeColor = Color.Lime,
                Font = new Font("Consolas", 10.5F), ScrollBars = ScrollBars.Vertical,
                Text = "SQL> -- Hệ thống sẵn sàng. Thực hiện theo thứ tự Bước 1 → Bước 2 → Bước 3.\r\n"
            };

            // Closure state
            string savedTimestamp = null;

            btnStep1.Click += (s, e) =>
            {
                string mabn = Esc(txtMabn.Text.Trim());
                if (string.IsNullOrEmpty(mabn)) { Err("Vui lòng nhập MABN."); return; }
                try
                {
                    var tsRow  = service.Query("SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS TS FROM DUAL");
                    savedTimestamp = tsRow.Rows[0]["TS"].ToString();

                    var dtRow  = service.Query($"SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    string cccd = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["CCCD"]?.ToString() ?? "(null)") : "(không tìm thấy)";

                    if (DateTime.TryParseExact(savedTimestamp, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime ts))
                        dtpFlash.Value = ts;

                    txtLog.AppendText($"\r\nSQL> SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS TS FROM DUAL;\r\n");
                    txtLog.AppendText($"TS\r\n-------------------\r\n{savedTimestamp}\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    txtLog.AppendText($"CCCD\r\n-------------------\r\n{cccd}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 1 HOÀN THÀNH]\r\n");
                    txtLog.AppendText($"   Snapshot timestamp : {savedTimestamp}\r\n");
                    txtLog.AppendText($"   CCCD gốc           : {cccd}\r\n");
                    txtLog.AppendText($"   => Tiếp theo: Bấm Bước 2 để giả lập dữ liệu bị hỏng.\r\n");
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"\r\n[LỖI Bước 1]: {ex.Message}\r\n");
                }
            };

            btnStep2.Click += (s, e) =>
            {
                string mabn = Esc(txtMabn.Text.Trim());
                if (savedTimestamp == null) { Err("Vui lòng thực hiện Bước 1 trước để ghi nhận mốc thời gian."); return; }
                if (MessageBox.Show(
                    $"CẢNH BÁO: Sẽ cập nhật CCCD của [{mabn}] thành '999999999999' để giả lập dữ liệu bị hỏng.\n\nBạn có chắc chắn?",
                    "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    txtLog.AppendText($"\r\nSQL> UPDATE QLBV.BENHNHAN SET CCCD = '999999999999' WHERE MABN = '{mabn}';\r\n");
                    service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET CCCD = '999999999999' WHERE MABN = '{mabn}'");
                    service.ExecuteNonQuery("COMMIT");

                    var dtRow   = service.Query($"SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    string now  = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["CCCD"]?.ToString() ?? "(null)") : "(null)";
                    txtLog.AppendText($"1 row updated.\r\nSQL> COMMIT;\r\nCommit complete.\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    txtLog.AppendText($"CCCD\r\n-------------------\r\n{now}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 2 HOÀN THÀNH] Dữ liệu đã bị hỏng!\r\n");
                    txtLog.AppendText($"   CCCD hiện tại: {now}\r\n");
                    txtLog.AppendText($"   => Tiếp theo: Kiểm tra mốc thời gian trong DateTimePicker rồi bấm Bước 3.\r\n");
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"\r\n[LỖI Bước 2]: {ex.Message}\r\n");
                }
            };

            btnStep3.Click += (s, e) =>
            {
                string mabn   = Esc(txtMabn.Text.Trim());
                string flashTs = dtpFlash.Value.ToString("yyyy-MM-dd HH:mm:ss");
                if (savedTimestamp == null) { Err("Vui lòng thực hiện Bước 1 trước."); return; }
                if (MessageBox.Show(
                    $"Sẽ khôi phục CCCD của [{mabn}] về trạng thái tại thời điểm:\n{flashTs}\n\nXác nhận?",
                    "Xác nhận Flashback", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    // AS OF TIMESTAMP — run/05.sql §Câu 4
                    string restoreSql =
                        $"UPDATE QLBV.BENHNHAN SET CCCD = (" +
                        $"  SELECT CCCD FROM QLBV.BENHNHAN" +
                        $"  AS OF TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS')" +
                        $"  WHERE MABN = '{mabn}'" +
                        $") WHERE MABN = '{mabn}'";

                    txtLog.AppendText($"\r\nSQL> UPDATE QLBV.BENHNHAN\r\n");
                    txtLog.AppendText($"  2  SET CCCD = (\r\n");
                    txtLog.AppendText($"  3    SELECT CCCD FROM QLBV.BENHNHAN\r\n");
                    txtLog.AppendText($"  4    AS OF TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS')\r\n");
                    txtLog.AppendText($"  5    WHERE MABN = '{mabn}'\r\n");
                    txtLog.AppendText($"  6  ) WHERE MABN = '{mabn}';\r\n");

                    service.ExecuteNonQuery(restoreSql);
                    service.ExecuteNonQuery("COMMIT");

                    var dtRow     = service.Query($"SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    string restored = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["CCCD"]?.ToString() ?? "(null)") : "(null)";
                    txtLog.AppendText($"1 row updated.\r\nSQL> COMMIT;\r\nCommit complete.\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    txtLog.AppendText($"CCCD\r\n-------------------\r\n{restored}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 3 HOÀN THÀNH] ✓ DỮ LIỆU ĐÃ ĐƯỢC KHÔI PHỤC!\r\n");
                    txtLog.AppendText($"   CCCD sau khôi phục: {restored}\r\n");
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"\r\n[LỖI Bước 3 - Flashback]: {ex.Message}\r\n");
                    txtLog.AppendText($"   Gợi ý: Cần đảm bảo UNDO_RETENTION đủ lớn và dữ liệu chưa vượt quá retention window.\r\n");
                    txtLog.AppendText($"   Lệnh kiểm tra: SHOW PARAMETER UNDO_RETENTION;\r\n");
                }
            };

            pnl.Controls.Add(flowInput);
            pnl.Controls.Add(flowSteps);
            pnl.Controls.Add(lblDtp);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(txtLog);
            tab.Controls.Add(pnl);
        }

        // ── 08.sql [07-FLASHBACK-DEMO]: FLASHBACK TABLE qlbv.donthuoc TO TIMESTAMP
        private void BuildBackup_FlashbackTable(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Flashback Table — Khôi Phục Toàn Bộ Bảng  (08.sql [07-FLASHBACK-DEMO])", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };
            var lblDesc  = new Label { Text = "Khác Flashback Query (row-level): FLASHBACK TABLE phục hồi toàn bộ trạng thái bảng. Yêu cầu ROW MOVEMENT enabled.", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(90, 90, 110), Dock = DockStyle.Top, Height = 22, AutoSize = false };

            var flowInput = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            var lblTbl    = new Label { Text = "Bảng demo:", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 6, 8, 0) };
            var cmbTbl    = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 0, 0) };
            cmbTbl.Items.AddRange(new object[] { "QLBV.DONTHUOC", "QLBV.BENHNHAN", "QLBV.HSBA" });
            cmbTbl.SelectedIndex = 0;
            flowInput.Controls.Add(lblTbl);
            flowInput.Controls.Add(cmbTbl);

            var flowSteps = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White };
            var btnS1  = QuickBtn("Bước 1: Ghi Nhận Thời Điểm",  UiTheme.BrandeisBlue,        UiTheme.WhiteText, 238, 40);
            var btnS2  = QuickBtn("Bước 2: Giả Lập Sửa Nhầm",    Color.FromArgb(206, 17, 38),  UiTheme.WhiteText, 222, 40);
            var dtpFT  = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", ShowUpDown = true, Width = 210, Margin = new Padding(10, 5, 6, 0), Value = DateTime.Now };
            var btnS3  = QuickBtn("Bước 3: FLASHBACK TABLE",      Color.FromArgb(30, 160, 100),  UiTheme.WhiteText, 210, 40);
            flowSteps.Controls.AddRange(new Control[] { btnS1, btnS2, dtpFT, btnS3 });

            var lblDtp = new Label { Text = "← Mốc thời gian (tự điền sau Bước 1) rồi bấm Bước 3", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(100, 100, 120), Dock = DockStyle.Top, Height = 20, AutoSize = false };
            var txtLog = new TextBox { Multiline = true, Dock = DockStyle.Bottom, Height = 360, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10.5F), ScrollBars = ScrollBars.Vertical, Text = "SQL> -- Sẵn sàng. Thực hiện Bước 1 → 2 → 3.\r\n" };

            string savedTsFT = null;

            btnS1.Click += (s, e) =>
            {
                string tbl = cmbTbl.SelectedItem.ToString();
                try
                {
                    // 08.sql [07-FLASHBACK-DEMO]: ghi lại mốc thời gian + xem dòng đầu bảng
                    var tsRow = service.Query("SELECT TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD HH24:MI:SS') AS TS FROM DUAL");
                    savedTsFT = tsRow.Rows[0]["TS"].ToString();
                    if (DateTime.TryParseExact(savedTsFT, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime ts))
                        dtpFT.Value = ts;

                    txtLog.AppendText($"\r\nSQL> SELECT TO_CHAR(SYSTIMESTAMP, 'YYYY-MM-DD HH24:MI:SS') AS TS FROM DUAL;\r\nTS: {savedTsFT}\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT * FROM {tbl} WHERE ROWNUM = 1;\r\n");
                    var dt = service.Query($"SELECT * FROM {tbl} WHERE ROWNUM = 1");
                    if (dt.Rows.Count > 0)
                        foreach (DataColumn c in dt.Columns) txtLog.AppendText($"  {c.ColumnName}: {dt.Rows[0][c]}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 1 HOÀN THÀNH] Timestamp: {savedTsFT}\r\n   => Bấm Bước 2 để giả lập sửa nhầm.\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI Bước 1]: {ex.Message}\r\n"); }
            };

            btnS2.Click += (s, e) =>
            {
                string tbl = cmbTbl.SelectedItem.ToString();
                if (savedTsFT == null) { Err("Vui lòng thực hiện Bước 1 trước."); return; }
                // 08.sql [07-FLASHBACK-DEMO]: UPDATE để giả lập dữ liệu bị hỏng
                string corruptSql = tbl == "QLBV.DONTHUOC"
                    ? $"UPDATE {tbl} SET LIEUDUNG = N'Dữ liệu bị sửa nhầm trong demo Flashback Table' WHERE ROWNUM = 1"
                    : $"UPDATE {tbl} SET SONHA = N'DEMO: DỮ LIỆU BỊ HỎNG' WHERE ROWNUM = 1";
                if (MessageBox.Show($"Sẽ thực thi:\r\n{corruptSql}\r\n\r\nXác nhận?", "Bước 2 - Giả Lập", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    txtLog.AppendText($"\r\nSQL> {corruptSql};\r\n");
                    service.ExecuteNonQuery(corruptSql);
                    service.ExecuteNonQuery("COMMIT");
                    txtLog.AppendText("1 row updated.\r\nCommit complete.\r\n>> [BƯỚC 2 HOÀN THÀNH] Dữ liệu đã bị hỏng.\r\n   => Điều chỉnh DateTimePicker rồi bấm Bước 3.\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI Bước 2]: {ex.Message}\r\n"); }
            };

            btnS3.Click += (s, e) =>
            {
                string tbl     = cmbTbl.SelectedItem.ToString();
                string flashTs = dtpFT.Value.ToString("yyyy-MM-dd HH:mm:ss");
                if (savedTsFT == null) { Err("Vui lòng thực hiện Bước 1 trước."); return; }
                if (MessageBox.Show($"Sẽ khôi phục bảng {tbl}\nvề thời điểm: {flashTs}\n(Yêu cầu ROW MOVEMENT enabled)\n\nXác nhận?", "Flashback Table", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    // 08.sql [07-FLASHBACK-DEMO]: ENABLE ROW MOVEMENT + FLASHBACK TABLE
                    txtLog.AppendText($"\r\nSQL> ALTER TABLE {tbl} ENABLE ROW MOVEMENT;\r\n");
                    service.ExecuteNonQuery($"ALTER TABLE {tbl} ENABLE ROW MOVEMENT");
                    txtLog.AppendText("Table altered.\r\n");

                    txtLog.AppendText($"\r\nSQL> FLASHBACK TABLE {tbl}\r\n  2  TO TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS');\r\n");
                    service.ExecuteNonQuery($"FLASHBACK TABLE {tbl} TO TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS')");
                    txtLog.AppendText("Flashback complete.\r\n");

                    txtLog.AppendText($"\r\nSQL> SELECT * FROM {tbl} WHERE ROWNUM = 1;\r\n");
                    var dt = service.Query($"SELECT * FROM {tbl} WHERE ROWNUM = 1");
                    if (dt.Rows.Count > 0)
                        foreach (DataColumn c in dt.Columns) txtLog.AppendText($"  {c.ColumnName}: {dt.Rows[0][c]}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 3 HOÀN THÀNH] ✓ Bảng {tbl} đã được khôi phục!\r\n");

                    // Ghi nhận vào RESTORE_HISTORY — 08.sql [07-RESTORE-HISTORY]
                    try
                    {
                        service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.RESTORE_HISTORY (BACKUP_NAME, RESTORE_OBJECT, INCIDENT_TIME, INCIDENT_USER, INCIDENT_ACTION, AUDIT_OBJECT_NAME, NOTE) " +
                            $"VALUES ('FlashbackTable', '{tbl}', TO_TIMESTAMP('{savedTsFT}','YYYY-MM-DD HH24:MI:SS'), 'DEMO', 'UPDATE', '{tbl}', N'Demo FLASHBACK TABLE trong WinForms')");
                        service.ExecuteNonQuery("COMMIT");
                        txtLog.AppendText("   => Đã ghi nhận vào QLBV.RESTORE_HISTORY.\r\n");
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI Bước 3 - Flashback Table]: {ex.Message}\r\n");
                    txtLog.AppendText("   Gợi ý: Cần UNDO_RETENTION đủ lớn và dữ liệu chưa vượt quá retention window.\r\n");
                    txtLog.AppendText("   Kiểm tra: SHOW PARAMETER UNDO_RETENTION;\r\n");
                }
            };

            pnl.Controls.Add(flowInput);
            pnl.Controls.Add(flowSteps);
            pnl.Controls.Add(lblDtp);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(txtLog);
            tab.Controls.Add(pnl);
        }

        // ── 08.sql [07-AUTO-BACKUP]: DBMS_SCHEDULER + JOB_AUTO_BACKUP_SCHEMA
        private void BuildBackup_Scheduler(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Scheduler — Backup Tự Động  (08.sql [07-AUTO-BACKUP])", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };

            var btnRunNow  = QuickBtn("▶ Chạy Backup Ngay",    UiTheme.BrandeisBlue,        UiTheme.WhiteText, 188, 38);
            var btnEnable  = QuickBtn("✔ Bật Job",              Color.FromArgb(30, 160, 80),  UiTheme.WhiteText, 108, 38);
            var btnDisable = QuickBtn("✗ Tắt Job",              Color.FromArgb(206, 17, 38),  UiTheme.WhiteText,  98, 38);
            var btnRefresh = QuickBtn("Tải Lại",               Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,   88, 38);
            var flowBtn    = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 4), BackColor = Color.White };
            flowBtn.Controls.AddRange(new Control[] { btnRunNow, btnEnable, btnDisable, btnRefresh,
                Note("JOB: QLBV.JOB_AUTO_BACKUP_SCHEMA — lịch chạy 23:30 hằng ngày. Procedure: QLBV.PR_AUTO_BACKUP_SCHEMA") });

            // Grid job status + grid job log
            var gridJob = MakeGrid(true);
            var gridLog = MakeGrid(true);
            var split   = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 120, BackColor = Color.White };
            split.Panel1.Controls.Add(gridJob);
            split.Panel1.Controls.Add(new Label { Text = "Trạng Thái Job — ALL_SCHEDULER_JOBS WHERE JOB_NAME = 'JOB_AUTO_BACKUP_SCHEMA'", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 });
            split.Panel2.Controls.Add(gridLog);
            split.Panel2.Controls.Add(new Label { Text = "Nhật Ký Chạy Gần Nhất — ALL_SCHEDULER_JOB_LOG (20 dòng)", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 });

            var txtOut = new TextBox { Multiline = true, Dock = DockStyle.Bottom, Height = 120, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10F), ScrollBars = ScrollBars.Vertical, Text = "C:\\Oracle> Scheduler console sẵn sàng.\r\n" };

            void RefreshAll()
            {
                try
                {
                    // 08.sql [07-AUTO-BACKUP]: kiểm tra trạng thái job
                    gridJob.DataSource = service.Query("SELECT OWNER, JOB_NAME, ENABLED, STATE, RUN_COUNT, FAILURE_COUNT, TO_CHAR(LAST_START_DATE,'DD/MM/YYYY HH24:MI') AS LAST_RUN, TO_CHAR(NEXT_RUN_DATE,'DD/MM/YYYY HH24:MI') AS NEXT_RUN, REPEAT_INTERVAL, COMMENTS FROM ALL_SCHEDULER_JOBS WHERE OWNER = 'QLBV' AND JOB_NAME = 'JOB_AUTO_BACKUP_SCHEMA'");
                    UiTheme.StyleGrid(gridJob);
                }
                catch { }
                try
                {
                    gridLog.DataSource = service.Query("SELECT TO_CHAR(LOG_DATE,'DD/MM/YYYY HH24:MI:SS') AS LOG_DATE, JOB_NAME, STATUS, ERROR# AS ERR, RUN_DURATION FROM ALL_SCHEDULER_JOB_LOG WHERE JOB_NAME = 'JOB_AUTO_BACKUP_SCHEMA' ORDER BY LOG_DATE DESC FETCH FIRST 20 ROWS ONLY");
                    UiTheme.StyleGrid(gridLog);
                }
                catch { }
            }

            btnRefresh.Click += (s, e) => RefreshAll();

            btnRunNow.Click += (s, e) =>
            {
                if (MessageBox.Show("Sẽ thực thi QLBV.PR_AUTO_BACKUP_SCHEMA ngay.\n(Procedure tạo file .dmp trong BACKUP_DIR và ghi vào BACKUP_HISTORY)\nXác nhận?", "Chạy Backup Ngay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    // 08.sql [07-AUTO-BACKUP]: gọi procedure pr_auto_backup_schema
                    txtOut.AppendText($"\r\nSQL> BEGIN QLBV.PR_AUTO_BACKUP_SCHEMA; END;\r\n");
                    service.ExecuteNonQuery("BEGIN QLBV.PR_AUTO_BACKUP_SCHEMA; END;");
                    txtOut.AppendText($"[{DateTime.Now:HH:mm:ss}] Procedure thực thi thành công.\r\n");
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    txtOut.AppendText($"[LỖI]: {ex.Message}\r\n");
                    txtOut.AppendText("   Kiểm tra: BACKUP_DIR đã tạo? Đã cấp DATAPUMP_EXP_FULL_DATABASE cho QLBV?\r\n");
                }
            };

            btnEnable.Click += (s, e) =>
            {
                try
                {
                    // 08.sql [07-AUTO-BACKUP]: DBMS_SCHEDULER.ENABLE
                    txtOut.AppendText($"\r\nSQL> BEGIN DBMS_SCHEDULER.ENABLE('QLBV.JOB_AUTO_BACKUP_SCHEMA'); END;\r\n");
                    service.ExecuteNonQuery("BEGIN DBMS_SCHEDULER.ENABLE('QLBV.JOB_AUTO_BACKUP_SCHEMA'); END;");
                    txtOut.AppendText($"[{DateTime.Now:HH:mm:ss}] Job đã được BẬT.\r\n");
                    RefreshAll();
                }
                catch (Exception ex) { txtOut.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            btnDisable.Click += (s, e) =>
            {
                try
                {
                    // 08.sql [07-AUTO-BACKUP]: DBMS_SCHEDULER.DISABLE
                    txtOut.AppendText($"\r\nSQL> BEGIN DBMS_SCHEDULER.DISABLE('QLBV.JOB_AUTO_BACKUP_SCHEMA'); END;\r\n");
                    service.ExecuteNonQuery("BEGIN DBMS_SCHEDULER.DISABLE('QLBV.JOB_AUTO_BACKUP_SCHEMA'); END;");
                    txtOut.AppendText($"[{DateTime.Now:HH:mm:ss}] Job đã được TẮT.\r\n");
                    RefreshAll();
                }
                catch (Exception ex) { txtOut.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            pnl.Controls.Add(txtOut);
            pnl.Controls.Add(split);
            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
            // Lazy load: chỉ query khi user mở tab lần đầu
            bool _schedLoaded = false;
            tab.Enter += (s, e) => { if (_schedLoaded) return; _schedLoaded = true; RefreshAll(); };
        }

        // =====================================================================
        //  QUẢN LÝ TÀI KHOẢN — Tạo NV / BN theo run/03.sql
        // =====================================================================
        private void BuildAdmin_QuanLyTK(TabPage tab)
        {
            var grid   = MakeGrid(true);
            var btnNV  = QuickBtn("+ Tạo Nhân Viên", UiTheme.BrandeisBlue, UiTheme.WhiteText, 155);
            var btnBN  = QuickBtn("+ Tạo Bệnh Nhân", UiTheme.PastelGreen, UiTheme.DeepBlue, 152);
            var btnRe  = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnNV.Click += (s, e) => TaoTaiKhoanNhanVien(grid);
            btnBN.Click += (s, e) => TaoTaiKhoanBenhNhan(grid);
            btnRe.Click += (s, e) => LoadDanhSachTaiKhoan(grid);

            tab.Controls.Add(Wrap(grid, Toolbar(btnNV, btnBN, btnRe,
                Note("Tạo tài khoản Oracle cho Nhân Viên (ROLE_DPV/BACSI/KTV) và Bệnh Nhân (ROLE_BENHNHAN)."))));
            // Lazy load: chỉ query NHANVIEN + BENHNHAN khi user mở tab lần đầu
            bool _tkLoaded = false;
            tab.Enter += (s, e) => { if (_tkLoaded) return; _tkLoaded = true; LoadDanhSachTaiKhoan(grid); };
        }

        private void LoadDanhSachTaiKhoan(DataGridView grid)
        {
            string sql = @"
                SELECT 'Nhân viên' AS ""LOẠI"", MANV AS ""MÃ"", HOTEN AS ""HỌ TÊN"",
                       VAITRO AS ""VAI TRÒ"", CAPBAC AS ""CẤP BẬC"", COSO AS ""CƠ SỞ"", MAKHOA AS ""KHOA""
                FROM QLBV.NHANVIEN
                UNION ALL
                SELECT 'Bệnh nhân' AS ""LOẠI"", MABN AS ""MÃ"", TENBN AS ""HỌ TÊN"",
                       NULL AS ""VAI TRÒ"", NULL AS ""CẤP BẬC"", NULL AS ""CƠ SỞ"", NULL AS ""KHOA""
                FROM QLBV.BENHNHAN
                ORDER BY 1, 2";
            LoadGrid(grid, sql);
        }

        // Tìm mã chưa dùng nhỏ nhất trong bảng — Binary search qua HashSet O(n) worst case
        private string FindNextId(string prefix, int digits, string table, string column)
        {
            try
            {
                var dt = service.Query(
                    $"SELECT {column} FROM {table} WHERE {column} LIKE '{prefix}%'");
                var existing = new System.Collections.Generic.HashSet<int>();
                foreach (DataRow r in dt.Rows)
                {
                    string id = r[column].ToString();
                    if (id.Length > prefix.Length &&
                        int.TryParse(id.Substring(prefix.Length), out int num))
                        existing.Add(num);
                }
                int next = 1;
                while (existing.Contains(next)) next++;
                return prefix + next.ToString("D" + digits);
            }
            catch { return prefix + "0001"; }
        }

        private string NextAvailableNvId(string vaitro)
        {
            if (vaitro == "Bác sĩ/Y sĩ")       return FindNextId("BS",  4, "QLBV.NHANVIEN", "MANV");
            if (vaitro == "Kỹ thuật viên")       return FindNextId("KTV", 3, "QLBV.NHANVIEN", "MANV");
            return FindNextId("NV", 4, "QLBV.NHANVIEN", "MANV");
        }

        private string NextAvailableBnId() =>
            FindNextId("BN", 6, "QLBV.BENHNHAN", "MABN");

        // Tính OLS label theo run/06.sql — khớp hoàn toàn với logic PL/SQL trong DB
        private string ComputeOlsLabel(string capBac, string maKhoa, string coSo)
        {
            string level;
            if (capBac == "Ban Giám đốc")   level = "BGD";
            else if (capBac == "Lãnh đạo khoa")  level = "LDK";
            else if (capBac == "Lãnh đạo phòng") level = "LDP";
            else                                  level = "NV";

            string comp = null;
            if (maKhoa == "K001") comp = "TH";
            else if (maKhoa == "K002") comp = "TK";
            else if (maKhoa == "K003") comp = "TM";

            string grp = null;
            if (coSo == "Hồ Chí Minh")    grp = "HCM";
            else if (coSo == "Hải Phòng")  grp = "HP";
            else if (coSo == "Hà Nội")     grp = "HN";

            if (level == "BGD")                       return "BGD:TH,TK,TM:HCM,HP,HN";
            if (level == "LDP" && comp == null)        return "LDP:TH,TK,TM:HCM,HP,HN";
            if (comp != null && grp != null)           return level + ":" + comp + ":" + grp;
            if (comp != null)                          return level + ":" + comp;
            if (grp != null)                           return level + "::" + grp;
            return level;
        }

        // ── Tạo tài khoản Nhân Viên ──────────────────────────────────────────
        // Tuân theo run/03.sql: INSERT NHANVIEN → CREATE USER → GRANT CREATE SESSION → GRANT ROLE
        // Nếu CAPBAC/MAKHOA/COSO có giá trị: gán OLS label theo run/06.sql
        private void TaoTaiKhoanNhanVien(DataGridView grid)
        {
            // Load danh sách khoa để hiển thị trong dropdown
            var danhSachKhoa = new string[0];
            try
            {
                var dtK = service.Query("SELECT MAKHOA, TENKHOA FROM QLBV.KHOA ORDER BY MAKHOA");
                var list = new System.Collections.Generic.List<string>();
                foreach (DataRow r in dtK.Rows)
                    list.Add(r["MAKHOA"] + " - " + r["TENKHOA"]);
                danhSachKhoa = list.ToArray();
            }
            catch { }

            string suggestedId = NextAvailableNvId("Điều phối viên");

            using (var f = new TaoNhanVienForm(suggestedId, danhSachKhoa, vt => NextAvailableNvId(vt)))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string ngSinh  = f.NgaySinh.HasValue
                        ? $"TO_DATE('{f.NgaySinh.Value:dd/MM/yyyy}','DD/MM/YYYY')" : "NULL";
                    string maKhoa  = string.IsNullOrEmpty(f.MaKhoa) ? "NULL" : $"'{Esc(f.MaKhoa)}'";
                    string capBac  = string.IsNullOrEmpty(f.CapBac) ? "NULL" : $"N'{Esc(f.CapBac)}'";
                    string coSo    = string.IsNullOrEmpty(f.CoSo)   ? "NULL" : $"N'{Esc(f.CoSo)}'";
                    string phai    = string.IsNullOrEmpty(f.Phai)   ? "NULL" : $"N'{Esc(f.Phai)}'";

                    // 1. INSERT vào QLBV.NHANVIEN
                    service.ExecuteNonQuery(
                        $"INSERT INTO QLBV.NHANVIEN (MANV,HOTEN,PHAI,NGAYSINH,CMND,QUEQUAN,SODT,VAITRO,MAKHOA,CAPBAC,COSO) " +
                        $"VALUES('{Esc(f.MaNV)}',N'{Esc(f.HoTen)}',{phai},{ngSinh}," +
                        $"'{Esc(f.Cmnd)}',N'{Esc(f.QueQuan)}','{Esc(f.SoDt)}'," +
                        $"N'{Esc(f.VaiTro)}',{maKhoa},{capBac},{coSo})");

                    // 2. Tạo Oracle user — CREATE USER / GRANT CREATE SESSION (run/03.sql)
                    service.ExecuteNonQuery($"CREATE USER {f.MaNV} IDENTIFIED BY \"{f.MatKhau.Replace("\"", "\"\"")}\"");
                    service.ExecuteNonQuery($"GRANT CREATE SESSION TO {f.MaNV}");

                    // 3. Grant role theo vai trò (run/03.sql)
                    string role;
                    if      (f.VaiTro == "Bác sĩ/Y sĩ")   role = "ROLE_BACSI";
                    else if (f.VaiTro == "Kỹ thuật viên")  role = "ROLE_KTV";
                    else                                    role = "ROLE_DPV";
                    service.ExecuteNonQuery($"GRANT {role} TO {f.MaNV}");

                    // 4. Gán OLS label nếu CAPBAC có giá trị (run/06.sql)
                    if (!string.IsNullOrEmpty(f.CapBac))
                    {
                        string label = ComputeOlsLabel(f.CapBac, f.MaKhoa, f.CoSo);
                        try
                        {
                            service.ExecuteNonQuery(
                                $"BEGIN LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(" +
                                $"policy_name=>'OLS_QLBV_POLICY',user_name=>'{f.MaNV}'," +
                                $"max_read_label=>'{label}',def_label=>'{label}',row_label=>'{label}'); END;");
                        }
                        catch { /* OLS không bắt buộc — tiếp tục */ }
                    }

                    Ok($"Đã tạo tài khoản nhân viên {f.MaNV} thành công!\nRole Oracle: {role}");
                    LoadDanhSachTaiKhoan(grid);
                }
                catch (Exception ex) { Err(ex.Message); }
            }
        }

        // ── Tạo tài khoản Bệnh Nhân ──────────────────────────────────────────
        // Tuân theo run/03.sql: INSERT BENHNHAN → CREATE USER → GRANT CREATE SESSION → GRANT ROLE_BENHNHAN
        private void TaoTaiKhoanBenhNhan(DataGridView grid)
        {
            string suggestedId = NextAvailableBnId();

            using (var f = new TaoBenhNhanForm(suggestedId))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string ngSinh  = f.NgaySinh.HasValue
                        ? $"TO_DATE('{f.NgaySinh.Value:dd/MM/yyyy}','DD/MM/YYYY')" : "NULL";
                    string phai    = string.IsNullOrEmpty(f.Phai)   ? "NULL" : $"N'{Esc(f.Phai)}'";

                    // 1. INSERT vào QLBV.BENHNHAN
                    service.ExecuteNonQuery(
                        $"INSERT INTO QLBV.BENHNHAN " +
                        $"(MABN,TENBN,PHAI,NGAYSINH,CCCD,SONHA,TENDUONG,QUANHUYEN,TINHTP,TIENSUBENH,TIENSUBENHGD,DIUNGTHUOC) " +
                        $"VALUES('{Esc(f.MaBN)}',N'{Esc(f.TenBN)}',{phai},{ngSinh}," +
                        $"'{Esc(f.CCCD)}',N'{Esc(f.SoNha)}',N'{Esc(f.TenDuong)}'," +
                        $"N'{Esc(f.QuanHuyen)}',N'{Esc(f.TinhTP)}'," +
                        $"N'{Esc(f.TienSuBenh)}',N'{Esc(f.TienSuBenhGD)}',N'{Esc(f.DiUngThuoc)}')");

                    // 2. Tạo Oracle user (run/03.sql)
                    service.ExecuteNonQuery($"CREATE USER {f.MaBN} IDENTIFIED BY \"{f.MatKhau.Replace("\"", "\"\"")}\"");
                    service.ExecuteNonQuery($"GRANT CREATE SESSION TO {f.MaBN}");

                    // 3. Grant ROLE_BENHNHAN (run/03.sql)
                    service.ExecuteNonQuery($"GRANT ROLE_BENHNHAN TO {f.MaBN}");

                    Ok($"Đã tạo tài khoản bệnh nhân {f.MaBN} thành công!\nRole Oracle: ROLE_BENHNHAN");
                    LoadDanhSachTaiKhoan(grid);
                }
                catch (Exception ex) { Err(ex.Message); }
            }
        }

        // =====================================================================
        //  OLS BẢO MẬT — Quản lý thành phần nhãn + nhãn người dùng
        // =====================================================================
        private void BuildAdmin_OLS(TabPage tab)
        {
            var inner  = MakeTabs();
            var tComp  = MakeTab("Thành Phần Nhãn");
            var tUsers = MakeTab("Nhãn Người Dùng");

            BuildAdmin_OLS_Components(tComp);
            BuildAdmin_OLS_UserLabels(tUsers);

            inner.TabPages.AddRange(new[] { tComp, tUsers });
            tab.Controls.Add(inner);
        }

        // Ba grid song song: DBA_SA_LEVELS | DBA_SA_COMPARTMENTS | DBA_SA_GROUPS
        private void BuildAdmin_OLS_Components(TabPage tab)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
                BackColor = Color.White, Padding = new Padding(8)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label MkHdr(string t) => new Label
            {
                Text = t, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                BackColor = Color.FromArgb(215, 235, 255)
            };
            tbl.Controls.Add(MkHdr("LEVELS  (Cấp độ)"), 0, 0);
            tbl.Controls.Add(MkHdr("COMPARTMENTS  (Khoa)"), 1, 0);
            tbl.Controls.Add(MkHdr("GROUPS  (Cơ sở)"), 2, 0);

            var gridL = MakeGrid(true);
            var gridC = MakeGrid(true);
            var gridG = MakeGrid(true);

            tbl.Controls.Add(gridL, 0, 1);
            tbl.Controls.Add(gridC, 1, 1);
            tbl.Controls.Add(gridG, 2, 1);

            tab.Controls.Add(tbl);

            void SafeOlsLoad(DataGridView g, string q)
            {
                try { LoadGrid(g, q); UiTheme.StyleGrid(g); }
                catch { /* OLS view không truy cập được — grid rỗng */ }
            }

            // Lazy load: chỉ query DBA_SA_* khi user mở tab lần đầu
            bool _olsCompLoaded = false;
            tab.Enter += (s, e) =>
            {
                if (_olsCompLoaded) return;
                _olsCompLoaded = true;
                SafeOlsLoad(gridL, "SELECT * FROM DBA_SA_LEVELS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY LEVEL_NUM DESC");
                SafeOlsLoad(gridC, "SELECT * FROM DBA_SA_COMPARTMENTS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY COMP_NUM");
                SafeOlsLoad(gridG, "SELECT * FROM DBA_SA_GROUPS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY GROUP_NUM");
            };
        }

        // Grid DBA_SA_USER_LABELS + nút đồng bộ lại nhãn cho tất cả NV (run/06.sql)
        private void BuildAdmin_OLS_UserLabels(TabPage tab)
        {
            var grid    = MakeGrid(true);
            var btnRe   = QuickBtn("Tải Lại",          Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var txtS    = SearchBox("Tìm user...", 180);
            var btnS    = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 70);
            var btnSync = QuickBtn("Đồng Bộ Nhãn NV", UiTheme.BrandeisBlue, UiTheme.WhiteText, 175);

            btnRe.Click   += (s, e) => LoadOlsUserLabels(grid, "");
            btnS.Click    += (s, e) => LoadOlsUserLabels(grid, Val(txtS, "Tìm user..."));
            btnSync.Click += (s, e) => SyncOlsLabels(grid);

            tab.Controls.Add(Wrap(grid, Toolbar(btnRe, txtS, btnS, btnSync,
                Note("Nhãn OLS xác định hàng nào trong THONGBAO mỗi user được đọc.  |  'Đồng Bộ' tính lại theo CAPBAC/MAKHOA/COSO từ run/06.sql."))));
            // Lazy load: chỉ query DBA_SA_USER_LABELS khi user mở tab lần đầu
            bool _olsULLoaded = false;
            tab.Enter += (s, e) => { if (_olsULLoaded) return; _olsULLoaded = true; LoadOlsUserLabels(grid, ""); };
        }

        private void LoadOlsUserLabels(DataGridView grid, string filter)
        {
            // Dùng SELECT * để không phụ thuộc tên cột — cấu trúc DBA_SA_USER_LABELS
            // có thể khác nhau giữa các phiên bản Oracle XE
            string sql = "SELECT * FROM DBA_SA_USER_LABELS WHERE POLICY_NAME='OLS_QLBV_POLICY'";
            if (!string.IsNullOrEmpty(filter))
                sql += $" AND UPPER(USER_NAME) LIKE '%{filter.ToUpper().Replace("'", "''")}%'";
            sql += " ORDER BY USER_NAME";
            try { LoadGrid(grid, sql); UiTheme.StyleGrid(grid); }
            catch { grid.DataSource = null; /* view không truy cập được — grid rỗng */ }
        }

        // Đồng bộ nhãn OLS cho tất cả NV — lặp lại logic PL/SQL từ run/06.sql
        private void SyncOlsLabels(DataGridView grid)
        {
            try
            {
                service.ExecuteNonQuery(@"
DECLARE
    v_label VARCHAR2(200);
    v_level VARCHAR2(10);
    v_comp  VARCHAR2(10);
    v_grp   VARCHAR2(10);
BEGIN
    FOR nv IN (SELECT MANV, CAPBAC, MAKHOA, COSO FROM QLBV.NHANVIEN) LOOP
        CASE nv.CAPBAC
            WHEN N'Ban Giám đốc'   THEN v_level := 'BGD';
            WHEN N'Lãnh đạo khoa'  THEN v_level := 'LDK';
            WHEN N'Lãnh đạo phòng' THEN v_level := 'LDP';
            ELSE v_level := 'NV';
        END CASE;
        CASE nv.MAKHOA
            WHEN 'K001' THEN v_comp := 'TH';
            WHEN 'K002' THEN v_comp := 'TK';
            WHEN 'K003' THEN v_comp := 'TM';
            ELSE v_comp := NULL;
        END CASE;
        CASE nv.COSO
            WHEN N'Hồ Chí Minh' THEN v_grp := 'HCM';
            WHEN N'Hải Phòng'   THEN v_grp := 'HP';
            WHEN N'Hà Nội'      THEN v_grp := 'HN';
            ELSE v_grp := NULL;
        END CASE;
        IF v_level = 'BGD' THEN
            v_label := 'BGD:TH,TK,TM:HCM,HP,HN';
        ELSIF v_level = 'LDP' AND v_comp IS NULL THEN
            v_label := 'LDP:TH,TK,TM:HCM,HP,HN';
        ELSIF v_comp IS NOT NULL AND v_grp IS NOT NULL THEN
            v_label := v_level || ':' || v_comp || ':' || v_grp;
        ELSIF v_comp IS NOT NULL THEN
            v_label := v_level || ':' || v_comp;
        ELSIF v_grp IS NOT NULL THEN
            v_label := v_level || '::' || v_grp;
        ELSE
            v_label := v_level;
        END IF;
        BEGIN
            LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(
                policy_name    => 'OLS_QLBV_POLICY',
                user_name      => nv.MANV,
                max_read_label => v_label,
                def_label      => v_label,
                row_label      => v_label
            );
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;
END;");
                Ok("Đã đồng bộ nhãn OLS cho tất cả nhân viên theo CAPBAC/MAKHOA/COSO.");
                LoadOlsUserLabels(grid, "");
            }
            catch (Exception ex) { Err(ex.Message); }
        }

        private void BuildAdmin_BN(TabPage tab)
        {
            var grid = MakeGrid(true);
            var (tb, cmb) = BranchToolbar();
            var btnF = QuickBtn("Lọc", UiTheme.BrandeisBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var txtS = SearchBox("Tìm mã BN...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            btnF.Click += (s, e) => { string b = cmb.SelectedItem.ToString(); string sql = b == "Tất cả" ? "SELECT * FROM QLBV.BENHNHAN" : $"SELECT * FROM QLBV.BENHNHAN WHERE TINHTP LIKE N'%{Esc(b)}%'"; LoadGrid(grid, sql); };
            btnRe.Click += (s, e) => LoadGrid(grid, "SELECT * FROM QLBV.BENHNHAN");
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã BN..."); if (!string.IsNullOrEmpty(kw)) LoadGrid(grid, $"SELECT * FROM QLBV.BENHNHAN WHERE MABN LIKE '%{Esc(kw)}%'"); };
            tb.Controls.Add(btnF); tb.Controls.Add(btnRe); tb.Controls.Add(txtS); tb.Controls.Add(btnS);
            tab.Controls.Add(Wrap(grid, tb));
            LoadGrid(grid, "SELECT * FROM QLBV.BENHNHAN");
        }

        private void BuildAdmin_NV(TabPage tab)
        {
            var grid = MakeGrid(true);
            var (tb, cmb) = BranchToolbar();
            var btnF = QuickBtn("Lọc", UiTheme.BrandeisBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var txtS = SearchBox("Tìm mã NV...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            btnF.Click += (s, e) => { string b = cmb.SelectedItem.ToString(); string sql = b == "Tất cả" ? "SELECT * FROM QLBV.NHANVIEN" : $"SELECT * FROM QLBV.NHANVIEN WHERE QUEQUAN LIKE N'%{Esc(b)}%'"; LoadGrid(grid, sql); };
            btnRe.Click += (s, e) => LoadGrid(grid, "SELECT * FROM QLBV.NHANVIEN");
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã NV..."); if (!string.IsNullOrEmpty(kw)) LoadGrid(grid, $"SELECT * FROM QLBV.NHANVIEN WHERE MANV LIKE '%{Esc(kw)}%'"); };
            tb.Controls.Add(btnF); tb.Controls.Add(btnRe); tb.Controls.Add(txtS); tb.Controls.Add(btnS);
            tab.Controls.Add(Wrap(grid, tb));
            LoadGrid(grid, "SELECT * FROM QLBV.NHANVIEN");
        }
        // Hàm dùng chung để tạo nhanh các tab báo cáo chỉ xem (Read-only) cho Giám đốc
        private void BuildGiamDoc_ReportTab(TabPage tab, string sql, string searchCol, string searchHint)
        {
            var grid = MakeGrid(true); // true = Chỉ đọc, không cho sửa

            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            btnRe.Click += (s, e) => LoadGrid(grid, sql);

            var txtS = SearchBox(searchHint, 220);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            btnS.Click += (s, e) => {
                string kw = Val(txtS, searchHint);
                // Tìm kiếm chuẩn xác theo cột được truyền vào
                if (!string.IsNullOrEmpty(kw))
                    LoadGrid(grid, sql + $" WHERE {searchCol} LIKE '%{Esc(kw)}%'");
                else
                    LoadGrid(grid, sql);
            };

            var toolbar = Toolbar(txtS, btnS, btnRe, Note("Chế độ Xem Báo Cáo toàn hệ thống (Không bị giới hạn bởi VPD)"));

            tab.Controls.Add(Wrap(grid, toolbar));
            LoadGrid(grid, sql);
        }

        private static (FlowLayoutPanel tb, ComboBox cmb) BranchToolbar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(8, 9, 8, 9), BackColor = Color.FromArgb(238, 248, 255) };
            bar.Controls.Add(new Label { Text = "Chi nhánh:", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 6, 6, 0) });
            var cmb = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 8, 0), Font = UiTheme.BodyFont };
            cmb.Items.AddRange(new[] { "Tất cả", "Hồ Chí Minh", "Hải Phòng", "Hà Nội" });
            cmb.SelectedIndex = 0;
            bar.Controls.Add(cmb);
            return (bar, cmb);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  THÔNG BÁO (OLS) — dùng chung
        // ═══════════════════════════════════════════════════════════════════

        private void BuildThongBaoTab(Control parent, bool canSend)
        {
            var grid = MakeGrid(true); // Chỉ đọc
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            // ADMIN hiển thị thêm cột NHÃN OLS (LABEL_TO_CHAR) để demo OLS filtering
            string sqlWithLabel  = "SELECT NOIDUNG, NGAYGIO, DIADIEM, LABEL_TO_CHAR(OLS_COL) AS \"NHÃN OLS\" FROM QLBV.THONGBAO ORDER BY NGAYGIO DESC";
            string sqlNoLabel    = "SELECT NOIDUNG, NGAYGIO, DIADIEM FROM QLBV.THONGBAO ORDER BY NGAYGIO DESC";
            string sqlLoad = canSend ? sqlWithLabel : sqlNoLabel;

            void LoadThongBao()
            {
                try { LoadGrid(grid, sqlLoad); UiTheme.StyleGrid(grid); }
                catch
                {
                    // LABEL_TO_CHAR không khả dụng → fallback không có cột nhãn
                    try { LoadGrid(grid, sqlNoLabel); UiTheme.StyleGrid(grid); }
                    catch { /* grid rỗng */ }
                }
            }

            btnRe.Click += (s, e) => LoadThongBao();

            grid.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var row = grid.Rows[e.RowIndex];
                    string nd = row.Cells["NOIDUNG"].Value?.ToString();
                    string ng = row.Cells["NGAYGIO"].Value?.ToString();
                    string dd = row.Cells["DIADIEM"].Value?.ToString();

                    string msg = $"THỜI GIAN:\n{ng}\n\nĐỊA ĐIỂM:\n{dd}\n\nNỘI DUNG THÔNG BÁO:\n{nd}";
                    MessageBox.Show(msg, "Chi Tiết Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Panel toolbar;
            if (canSend)
            {
                var btnSend = QuickBtn("+ Gửi Thông Báo Khẩn", UiTheme.PastelGreen, UiTheme.DeepBlue, 190);
                btnSend.Click += (s, e) => GuiThongBao();
                toolbar = Toolbar(btnSend, btnRe, Note("Nhấn đúp (Double-click) vào thông báo để xem chi tiết."));
            }
            else
            {
                toolbar = Toolbar(btnRe, Note("Nhấn đúp (Double-click) vào thông báo để xem chi tiết."));
            }

            parent.Controls.Add(Wrap(grid, toolbar));
            LoadThongBao();
        }

        private void GuiThongBao()
        {
            using (var f = new ThongBaoForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        // Nếu QLBV chọn nhãn OLS → INSERT với CHAR_TO_LABEL để đích thị đúng đối tượng
                        // Nếu không chọn (rỗng) → Oracle tự dùng row_label mặc định của QLBV (BGD:...)
                        string olsSql = string.IsNullOrEmpty(f.OlsLabel)
                            ? $"INSERT INTO QLBV.THONGBAO(NOIDUNG,NGAYGIO,DIADIEM) " +
                              $"VALUES(N'{Esc(f.NoiDung)}',TO_TIMESTAMP('{f.NgayGio:dd/MM/yyyy HH:mm}','DD/MM/YYYY HH24:MI'),N'{Esc(f.DiaDiem)}')"
                            : $"INSERT INTO QLBV.THONGBAO(NOIDUNG,NGAYGIO,DIADIEM,OLS_COL) " +
                              $"VALUES(N'{Esc(f.NoiDung)}',TO_TIMESTAMP('{f.NgayGio:dd/MM/yyyy HH:mm}','DD/MM/YYYY HH24:MI'),N'{Esc(f.DiaDiem)}'," +
                              $"CHAR_TO_LABEL('OLS_QLBV_POLICY','{f.OlsLabel}'))";
                        service.ExecuteNonQuery(olsSql);
                        Ok($"Đã gửi thông báo khẩn!\nNhãn OLS: {(string.IsNullOrEmpty(f.OlsLabel) ? "(mặc định QLBV)" : f.OlsLabel)}");
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }

        // ── event handler helpers ────────────────────────────────────────

        /// Lock specific columns (cancel edit on them)
        private static DataGridViewCellCancelEventHandler LockCols(DataGridView grid, string[] locked)
            => (s, e) => { foreach (var c in locked) if (string.Equals(grid.Columns[e.ColumnIndex].Name, c, StringComparison.OrdinalIgnoreCase)) { e.Cancel = true; return; } };

        /// Allow only specific columns to be edited
        private static DataGridViewCellCancelEventHandler AllowOnly(DataGridView grid, string[] allowed)
            => (s, e) => { foreach (var c in allowed) if (string.Equals(grid.Columns[e.ColumnIndex].Name, c, StringComparison.OrdinalIgnoreCase)) return; e.Cancel = true; };

        // ═══════════════════════════════════════════════════════════════════
        //  THÔNG TIN CÁ NHÂN (Dùng chung cho NV: DPV, BACSI, KTV, ADMIN)
        // ═══════════════════════════════════════════════════════════════════

        private void BuildNV_Info(TabPage tab)
        {
            tab.BackColor = Color.FromArgb(240, 249, 255);
            var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(240, 249, 255) };

            var card = new Panel { Width = 740, AutoSize = true, BackColor = Color.White, Padding = new Padding(32, 24, 32, 24), Top = 24 };
            card.Paint += (s, e) => e.Graphics.DrawRectangle(new System.Drawing.Pen(Color.FromArgb(190, 220, 240), 1), new Rectangle(0, 0, card.Width - 1, card.Height - 1));

            var cardHeader = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.BrandeisBlue };
            cardHeader.Controls.Add(new Label { Text = "Hồ sơ nhân viên", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = UiTheme.WhiteText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });

            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(0, 16, 0, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460F));

            int row = 0;
            // Dùng hàm BNField tái sử dụng để tạo Layout nhanh
            BNField(layout, row++, "Họ và tên", out txtNvFullName, readOnly: true);
            BNField(layout, row++, "Giới tính", out txtNvGender, readOnly: true);
            BNField(layout, row++, "Ngày sinh", out txtNvDob, readOnly: true);
            BNField(layout, row++, "Số CMND", out txtNvCmnd, readOnly: true);
            BNField(layout, row++, "Vai trò", out txtNvRole, readOnly: true);
            BNField(layout, row++, "Khoa", out txtNvDept, readOnly: true);
            BNField(layout, row++, "Cơ sở", out txtNvCoSo, readOnly: true);
            BNField(layout, row++, "Quê quán", out txtNvAddress); // Cho phép sửa
            BNField(layout, row++, "Số điện thoại", out txtNvPhone); // Cho phép sửa

            var divider = new Panel { Height = 2, BackColor = Color.FromArgb(200, 230, 250), Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 8) };

            var btnSave = QuickBtn("Cập Nhật Thông Tin", UiTheme.PastelGreen, UiTheme.DeepBlue, 200, 42);
            btnSave.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnSave.Click += NV_Luu;

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            btnRow.Controls.Add(btnSave);

            card.Controls.Add(btnRow);
            card.Controls.Add(divider);
            card.Controls.Add(layout);
            card.Controls.Add(cardHeader);

            // Căn giữa card khi resize
            outer.Resize += (s, e) => { card.Left = Math.Max(0, (outer.Width - card.Width) / 2); };

            outer.Controls.Add(card);
            tab.Controls.Add(outer);
            LoadNVInfo();
        }

        private void LoadNVInfo()
        {
            try
            {
                // KTV chỉ có quyền trên VW_KTV_Xemthongtin (view tự filter theo session_user)
                string sql = userRole == UserRole.KTV
                    ? "SELECT * FROM QLBV.VW_KTV_Xemthongtin"
                    : $"SELECT * FROM QLBV.NHANVIEN WHERE MANV = '{currentUser}'";
                DataTable dt = service.Query(sql);
                if (dt.Rows.Count == 0) return;
                DataRow r = dt.Rows[0];
                txtNvFullName.Text = r["HOTEN"]?.ToString() ?? "";
                txtNvGender.Text = r["PHAI"]?.ToString() ?? "";
                if (DateTime.TryParse(r["NGAYSINH"]?.ToString(), out DateTime dob)) txtNvDob.Text = dob.ToString("dd/MM/yyyy");
                txtNvCmnd.Text = r["CMND"]?.ToString() ?? "";
                txtNvRole.Text = r["VAITRO"]?.ToString() ?? "";
                txtNvDept.Text = r["MAKHOA"]?.ToString() ?? "";
                txtNvCoSo.Text = r["COSO"]?.ToString() ?? "";
                txtNvAddress.Text = r["QUEQUAN"]?.ToString() ?? "";
                txtNvPhone.Text = r["SODT"]?.ToString() ?? "";
            }
            catch (Exception ex) { Err("Lỗi tải thông tin NV: " + ex.Message); }
        }

        private void NV_Luu(object sender, EventArgs e)
        {
            try
            {
                // KTV chỉ có quyền UPDATE (QUEQUAN, SODT) trên VW_KTV_Xemthongtin
                string sql = userRole == UserRole.KTV
                    ? $"UPDATE QLBV.VW_KTV_Xemthongtin SET QUEQUAN=N'{Esc(txtNvAddress.Text)}', SODT='{Esc(txtNvPhone.Text)}'"
                    : $"UPDATE QLBV.NHANVIEN SET QUEQUAN=N'{Esc(txtNvAddress.Text)}', SODT='{Esc(txtNvPhone.Text)}' WHERE MANV='{currentUser}'";
                service.ExecuteNonQuery(sql);
                Ok("Cập nhật thông tin cá nhân thành công!");
            }
            catch (Exception ex) { Err("Lỗi: " + ex.Message); }
        }
    }



    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Thêm Bệnh Nhân
    // ════════════════════════════════════════════════════════════════════════

    public class BenhNhanAddForm : Form
    {
        public string MaBN { get; private set; }
        public string TenBN { get; private set; }
        public string Phai { get; private set; }
        public DateTime NgaySinh { get; private set; }
        public string CCCD { get; private set; }
        public string SoNha { get; private set; }
        public string TenDuong { get; private set; }
        public string QuanHuyen { get; private set; }
        public string TinhTP { get; private set; }
        public string TienSuBenh { get; private set; }
        public string TienSuBenhGD { get; private set; }
        public string DiUngThuoc { get; private set; }

        public BenhNhanAddForm()
        {
            Text = "Thêm Bệnh Nhân Mới"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(650, 680); BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = UiTheme.JordyBlue, Padding = new Padding(16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));

            string[] lbs = { "Mã BN:", "Họ tên:", "Phái (Nam/Nữ):", "Ngày sinh (dd/mm/yyyy):", "CCCD:", "Số nhà:", "Tên đường:", "Quận/Huyện:", "Tỉnh/TP:", "Tiền sử bệnh:", "Tiền sử bệnh GĐ:", "Dị ứng thuốc:" };
            var flds = new TextBox[lbs.Length];
            for (int i = 0; i < lbs.Length; i++) { layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 6, 6, 0) }, 0, i); flds[i] = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) }; if (i >= 9) { flds[i].Multiline = true; flds[i].Height = 54; } layout.Controls.Add(flds[i], 1, i); }
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 6, 0, 0) };
            var ok = new Button { Text = "Thêm", Width = 110, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;
            ok.Click += (s, e) => { if (string.IsNullOrWhiteSpace(flds[0].Text) || string.IsNullOrWhiteSpace(flds[1].Text)) { MessageBox.Show("Nhập Mã BN và Họ tên."); return; } if (!DateTime.TryParseExact(flds[3].Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dob)) { MessageBox.Show("Ngày sinh không hợp lệ (dd/mm/yyyy)."); return; } MaBN = flds[0].Text.Trim(); TenBN = flds[1].Text.Trim(); Phai = flds[2].Text.Trim(); NgaySinh = dob; CCCD = flds[4].Text.Trim(); SoNha = flds[5].Text.Trim(); TenDuong = flds[6].Text.Trim(); QuanHuyen = flds[7].Text.Trim(); TinhTP = flds[8].Text.Trim(); TienSuBenh = flds[9].Text.Trim(); TienSuBenhGD = flds[10].Text.Trim(); DiUngThuoc = flds[11].Text.Trim(); DialogResult = DialogResult.OK; Close(); };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            ft.Controls.Add(ok); ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, lbs.Length); layout.SetColumnSpan(ft, 2);
            scroll.Controls.Add(layout); Controls.Add(scroll); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Tạo HSBA  (Điều phối viên điền — CHẨNĐOÁN/ĐIỀUTRỊ/KẾTLUẬN do bác sĩ cập nhật sau)
    // ════════════════════════════════════════════════════════════════════════

    public class HsbaAddForm : Form
    {
        public string MaHSBA { get; private set; }
        public string MaBN { get; private set; }
        public DateTime Ngay { get; private set; }
        public string MaBS { get; private set; }
        public string MaKhoa { get; private set; }
        // CHANDOAN / DIEUTRI / KETLUAN do bác sĩ cập nhật sau — luôn rỗng khi tạo mới
        public string ChanDoan => string.Empty;
        public string DieuTri => string.Empty;
        public string KetLuan => string.Empty;

        public HsbaAddForm()
        {
            Text = "Tạo Hồ Sơ Bệnh Án Mới";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(480, 320);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            string[] lbs = { "Mã HSBA:", "Mã bệnh nhân:", "Ngày (dd/mm/yyyy):", "Mã bác sĩ:", "Mã khoa:" };
            var flds = new TextBox[lbs.Length];
            for (int i = 0; i < lbs.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 8, 0) }, 0, i);
                flds[i] = new TextBox { Dock = DockStyle.Fill };
                layout.Controls.Add(flds[i], 1, i);
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            var note = new Label { Text = "Chẩn đoán / Điều trị / Kết luận do Y bác sĩ cập nhật sau.", ForeColor = Color.FromArgb(100, 80, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            layout.Controls.Add(note, 0, lbs.Length);
            layout.SetColumnSpan(note, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Tạo HSBA", Width = 120, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText };
            cn.FlatAppearance.BorderSize = 0;
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(flds[0].Text) || string.IsNullOrWhiteSpace(flds[1].Text)) { MessageBox.Show("Nhập Mã HSBA và Mã bệnh nhân."); return; }
                if (!DateTime.TryParseExact(flds[2].Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime ng)) { MessageBox.Show("Ngày không hợp lệ (dd/mm/yyyy)."); return; }
                MaHSBA = flds[0].Text.Trim(); MaBN = flds[1].Text.Trim(); Ngay = ng;
                MaBS = flds[3].Text.Trim(); MaKhoa = flds[4].Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            ft.Controls.Add(ok); ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, lbs.Length + 1);
            layout.SetColumnSpan(ft, 2);
            Controls.Add(layout);
            AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Điều phối / Thêm HSBA_DV
    // ════════════════════════════════════════════════════════════════════════

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Điều phối / Thêm HSBA_DV (Tự động thích ứng theo Role)
    // ════════════════════════════════════════════════════════════════════════

    public class HsbaDvAddForm : Form
    {
        public string MaHSBA { get; private set; }
        public string LoaiDV { get; private set; }
        public DateTime NgayDV { get; private set; }
        public string MaKTV { get; private set; }

        public HsbaDvAddForm(bool isDoctor = false)
        {
            Text = isDoctor ? "Bác Sĩ Chỉ Định Dịch Vụ" : "Điều Phối / Thêm Dịch Vụ";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(460, 295); 
            BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // BS cũng cần nhập MAKTV đề xuất vì schema MAKTV NOT NULL + FK→NHANVIEN
            string[] lbs = isDoctor
                ? new string[] { "Mã HSBA:", "Loại dịch vụ:", "Ngày DV (dd/mm/yyyy):", "Mã KTV đề xuất:" }
                : new string[] { "Mã HSBA:", "Loại dịch vụ:", "Ngày DV (dd/mm/yyyy):", "Mã KTV:" };
            var flds = new TextBox[lbs.Length];

            for (int i = 0; i < lbs.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, i);
                flds[i] = new TextBox { Dock = DockStyle.Fill };
                layout.Controls.Add(flds[i], 1, i);
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            string noteText = isDoctor ? "DPV có thể thay đổi KTV được phân công sau (UPDATE MAKTV)." : "Kết quả do KTV nhập sau.";
            var nt = new Label { Text = noteText, ForeColor = Color.FromArgb(160, 80, 0), AutoSize = true };
            layout.Controls.Add(nt, 0, lbs.Length); layout.SetColumnSpan(nt, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = isDoctor ? "Chỉ Định" : "Điều Phối", Width = 120, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(flds[0].Text)) { MessageBox.Show("Nhập Mã HSBA."); return; }
                if (!DateTime.TryParseExact(flds[2].Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime ng)) { MessageBox.Show("Ngày không hợp lệ."); return; }
                if (string.IsNullOrWhiteSpace(flds[3].Text)) { MessageBox.Show("Nhập Mã KTV."); return; }
                MaHSBA = flds[0].Text.Trim(); LoaiDV = flds[1].Text.Trim(); NgayDV = ng;
                MaKTV = flds[3].Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, lbs.Length + 1); layout.SetColumnSpan(ft, 2); Controls.Add(layout); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Thêm Đơn Thuốc
    // ════════════════════════════════════════════════════════════════════════

    public class DonThuocAddForm : Form
    {
        public string MaHSBA { get; private set; }
        public DateTime NgayDT { get; private set; }
        public string TenThuoc { get; private set; }
        public string LieuDung { get; private set; }

        public DonThuocAddForm()
        {
            Text = "Thêm Đơn Thuốc"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(440, 260);
            BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            string[] lbs = { "Mã HSBA:", "Ngày (dd/mm/yyyy):", "Tên thuốc:", "Liều dùng:" };
            var flds = new TextBox[lbs.Length];
            for (int i = 0; i < lbs.Length; i++) { layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F)); layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, i); flds[i] = new TextBox { Dock = DockStyle.Fill }; layout.Controls.Add(flds[i], 1, i); }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Thêm", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(flds[0].Text)) { MessageBox.Show("Nhập Mã HSBA."); return; }
                if (!DateTime.TryParseExact(flds[1].Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime ng)) { MessageBox.Show("Ngày không hợp lệ."); return; }
                MaHSBA = flds[0].Text.Trim(); NgayDT = ng; TenThuoc = flds[2].Text.Trim(); LieuDung = flds[3].Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, lbs.Length); layout.SetColumnSpan(ft, 2); Controls.Add(layout); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Gửi Thông Báo Khẩn — Chọn Nhãn OLS (run/01.sql components)
    //  Level:       RadioButton (BGD / LDK / LDP / NV)
    //  Compartment: CheckBox    (TH / TK / TM / Tất cả khoa)
    //  Group:       CheckBox    (HCM / HP / HN / Tất cả cơ sở)
    //  Preview:     Label hiển thị nhãn kết quả real-time
    // ════════════════════════════════════════════════════════════════════════
    public class ThongBaoForm : Form
    {
        public string   NoiDung  { get; private set; }
        public DateTime NgayGio  { get; private set; }
        public string   DiaDiem  { get; private set; }
        public string   OlsLabel { get; private set; }   // "BGD:TH,TK,TM:HCM,HP,HN" etc.

        public ThongBaoForm()
        {
            Text = "Gửi Thông Báo Khẩn — Chọn Nhãn OLS";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(850, 600);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            // ── Outer layout: left (fields) | right (OLS picker) ──────────
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(14), BackColor = UiTheme.JordyBlue
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // ── LEFT: nội dung / ngày giờ / địa điểm ─────────────────────
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 10, 0)
            };

            Label MkLbl(string t) => new Label
            {
                Text = t, AutoSize = true, Font = UiTheme.HeaderFont,
                ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 6, 0, 2),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            leftPanel.Controls.Add(MkLbl("Nội dung thông báo *:"));

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var txN = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            leftPanel.Controls.Add(txN);

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            leftPanel.Controls.Add(MkLbl("Ngày giờ:"));

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            var dtpNgayGio = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy HH:mm",
                ShowUpDown = true,
                Value = DateTime.Now
            };
            leftPanel.Controls.Add(dtpNgayGio);

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            leftPanel.Controls.Add(MkLbl("Địa điểm:"));

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            var txL = new TextBox { Dock = DockStyle.Fill };
            leftPanel.Controls.Add(txL);

            outer.Controls.Add(leftPanel, 0, 0);

            // ── RIGHT: OLS Label Picker ───────────────────────────────────
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 242, 255),
                Padding = new Padding(10)
            };

            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent
            };

            // Title
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            rightLayout.Controls.Add(new Label
            {
                Text = "Nhãn OLS (Đối tượng nhận)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Fill
            });

            // ── LEVEL GroupBox ──
            var gbLevel = new GroupBox
            {
                Text = "Cấp độ (Level)", Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Height = 100
            };
            var flowLevel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };

            var levelDefs = new (string Code, string Label)[]
            {
                ("BGD", "Ban Giám đốc"), ("LDK", "Lãnh đạo khoa"),
                ("LDP", "Lãnh đạo phòng"), ("NV", "Nhân viên")
            };
            var rbLevels = new RadioButton[levelDefs.Length];
            for (int i = 0; i < levelDefs.Length; i++)
            {
                rbLevels[i] = new RadioButton
                {
                    Text = $"{levelDefs[i].Label} ({levelDefs[i].Code})",
                    Tag = levelDefs[i].Code,
                    AutoSize = true, Margin = new Padding(4, 2, 4, 2),
                    Font = new Font("Segoe UI", 8.5F)
                };
                flowLevel.Controls.Add(rbLevels[i]);
            }
            rbLevels[3].Checked = true; // Default: NV
            gbLevel.Controls.Add(flowLevel);
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95F));
            rightLayout.Controls.Add(gbLevel);

            // ── COMPARTMENT GroupBox ──
            var gbComp = new GroupBox
            {
                Text = "Khoa (Compartment)", Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Height = 90
            };
            var flowComp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            var compDefs = new (string Code, string Label)[]
            {
                ("TH", "Tiêu hóa (TH)"), ("TK", "Thần kinh (TK)"),
                ("TM", "Tim mạch (TM)"), ("ALL", "Tất cả khoa")
            };
            var cbComps = new CheckBox[compDefs.Length];
            for (int i = 0; i < compDefs.Length; i++)
            {
                cbComps[i] = new CheckBox
                {
                    Text = compDefs[i].Label, Tag = compDefs[i].Code,
                    AutoSize = true, Margin = new Padding(4, 2, 4, 2),
                    Font = new Font("Segoe UI", 8.5F)
                };
                flowComp.Controls.Add(cbComps[i]);
            }
            gbComp.Controls.Add(flowComp);
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            rightLayout.Controls.Add(gbComp);

            // ── GROUP GroupBox ──
            var gbGrp = new GroupBox
            {
                Text = "Cơ sở (Group)", Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Height = 90
            };
            var flowGrp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            var grpDefs = new (string Code, string Label)[]
            {
                ("HCM", "Hồ Chí Minh (HCM)"), ("HP", "Hải Phòng (HP)"),
                ("HN", "Hà Nội (HN)"),         ("ALL", "Tất cả cơ sở")
            };
            var cbGrps = new CheckBox[grpDefs.Length];
            for (int i = 0; i < grpDefs.Length; i++)
            {
                cbGrps[i] = new CheckBox
                {
                    Text = grpDefs[i].Label, Tag = grpDefs[i].Code,
                    AutoSize = true, Margin = new Padding(4, 2, 4, 2),
                    Font = new Font("Segoe UI", 8.5F)
                };
                flowGrp.Controls.Add(cbGrps[i]);
            }
            gbGrp.Controls.Add(flowGrp);
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            rightLayout.Controls.Add(gbGrp);

            // ── Preview nhãn ──
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            rightLayout.Controls.Add(new Label
            {
                Text = "Xem trước nhãn OLS:", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 130), Dock = DockStyle.Fill
            });

            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            var lblPreview = new Label
            {
                Text = "NV", Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                ForeColor = Color.DarkGreen,
                BackColor = Color.FromArgb(220, 255, 220),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };
            rightLayout.Controls.Add(lblPreview);

            rightPanel.Controls.Add(rightLayout);
            outer.Controls.Add(rightPanel, 1, 0);

            // ── Hàm tính và cập nhật preview ─────────────────────────────
            void UpdatePreview()
            {
                string level = "NV";
                foreach (var rb in rbLevels)
                    if (rb.Checked) { level = rb.Tag.ToString(); break; }

                // BGD: không cần chọn comp/group — full access
                if (level == "BGD") { lblPreview.Text = "BGD:TH,TK,TM:HCM,HP,HN"; return; }

                // Compartments
                bool allComp = false;
                var selComps = new System.Collections.Generic.List<string>();
                foreach (var cb in cbComps)
                {
                    if (!cb.Checked) continue;
                    if (cb.Tag.ToString() == "ALL") { allComp = true; break; }
                    selComps.Add(cb.Tag.ToString());
                }
                string compPart = allComp ? "TH,TK,TM" : (selComps.Count > 0 ? string.Join(",", selComps) : "");

                // Groups
                bool allGrp = false;
                var selGrps = new System.Collections.Generic.List<string>();
                foreach (var cb in cbGrps)
                {
                    if (!cb.Checked) continue;
                    if (cb.Tag.ToString() == "ALL") { allGrp = true; break; }
                    selGrps.Add(cb.Tag.ToString());
                }
                string grpPart = allGrp ? "HCM,HP,HN" : (selGrps.Count > 0 ? string.Join(",", selGrps) : "");

                // Ghép nhãn
                string lbl = level;
                if (!string.IsNullOrEmpty(compPart) && !string.IsNullOrEmpty(grpPart))
                    lbl = $"{level}:{compPart}:{grpPart}";
                else if (!string.IsNullOrEmpty(compPart))
                    lbl = $"{level}:{compPart}";
                else if (!string.IsNullOrEmpty(grpPart))
                    lbl = $"{level}::{grpPart}";

                lblPreview.Text = lbl;
            }

            // Wire up events
            foreach (var rb in rbLevels)
            {
                rb.CheckedChanged += (s, e) =>
                {
                    bool isBgd = rb.Tag.ToString() == "BGD" && rb.Checked;
                    gbComp.Enabled = !isBgd;
                    gbGrp.Enabled  = !isBgd;
                    UpdatePreview();
                };
            }
            foreach (var cb in cbComps) cb.CheckedChanged += (s, e) =>
            {
                // "Tất cả khoa" mutex
                if (cb.Tag.ToString() == "ALL" && cb.Checked)
                    foreach (var c in cbComps) if (c.Tag.ToString() != "ALL") c.Checked = false;
                else if (cb.Tag.ToString() != "ALL" && cb.Checked)
                    cbComps[3].Checked = false;
                UpdatePreview();
            };
            foreach (var cb in cbGrps) cb.CheckedChanged += (s, e) =>
            {
                // "Tất cả cơ sở" mutex
                if (cb.Tag.ToString() == "ALL" && cb.Checked)
                    foreach (var c in cbGrps) if (c.Tag.ToString() != "ALL") c.Checked = false;
                else if (cb.Tag.ToString() != "ALL" && cb.Checked)
                    cbGrps[3].Checked = false;
                UpdatePreview();
            };

            UpdatePreview();

            // ── Buttons ───────────────────────────────────────────────────
            var btmPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom, Height = 54,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = UiTheme.JordyBlue, Padding = new Padding(8)
            };
            var ok = new Button
            {
                Text = "Gửi Thông Báo", Width = 155, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText
            };
            cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txN.Text)) { MessageBox.Show("Nhập nội dung thông báo."); return; }

                NoiDung = txN.Text.Trim();
                NgayGio = dtpNgayGio.Value;
                DiaDiem = txL.Text.Trim();
                OlsLabel = lblPreview.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btmPanel.Controls.Add(ok);
            btmPanel.Controls.Add(cn);

            Controls.Add(outer);
            Controls.Add(btmPanel);
            AcceptButton = ok;
        }
    }
    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Cập nhật thông tin (Dùng chung cho mọi Role)
    // ════════════════════════════════════════════════════════════════════════
    public class EditRowForm : Form
    {
        public Dictionary<string, string> NewValues { get; private set; } = new Dictionary<string, string>();

        public EditRowForm(string title, Dictionary<string, string> fields)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            Width = 480; BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            int rowCount = fields.Count;
            Height = 110 + (rowCount * 50); // Tự động kéo dài form theo số lượng ô nhập

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var inputs = new Dictionary<string, TextBox>();
            int i = 0;
            foreach (var kvp in fields)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label { Text = kvp.Key + ":", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, i);
                var txt = new TextBox { Dock = DockStyle.Fill, Text = kvp.Value };
                inputs.Add(kvp.Key, txt);
                layout.Controls.Add(txt, 1, i);
                i++;
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Cập nhật", Width = 110, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                foreach (var kvp in inputs) NewValues[kvp.Key] = kvp.Value.Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, rowCount); layout.SetColumnSpan(ft, 2);
            Controls.Add(layout); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Cập Nhật Đơn Thuốc
    // ════════════════════════════════════════════════════════════════════════
    public class EditDonThuocForm : Form
    {
        public Dictionary<string, string> NewValues { get; } = new Dictionary<string, string>();

        public EditDonThuocForm(string title, Dictionary<string, string> fields)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(500, 185);
            BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var controls = new Dictionary<string, Control>();
            int row = 0;
            foreach (var kv in fields)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label { Text = kv.Key + ":", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, row);
                var txt = new TextBox { Dock = DockStyle.Fill, Text = kv.Value ?? "" };
                layout.Controls.Add(txt, 1, row);
                controls[kv.Key] = txt;
                row++;
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Lưu", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                foreach (var kv in controls)
                    NewValues[kv.Key] = ((TextBox)kv.Value).Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, row); layout.SetColumnSpan(ft, 2);
            Controls.Add(layout); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Tạo Tài Khoản Nhân Viên (QLBV Admin)
    //  — Dropdown VAITRO/PHAI/CAPBAC/COSO/MAKHOA theo schema_phanhe2.sql
    //  — Auto-suggest MANV nhỏ nhất chưa dùng, QLBV có thể sửa
    //  — Password đề xuất mặc định "nv123", QLBV tự thay
    // ════════════════════════════════════════════════════════════════════════
    public class TaoNhanVienForm : Form
    {
        public string   MaNV     { get; private set; }
        public string   HoTen    { get; private set; }
        public string   VaiTro   { get; private set; }
        public string   Phai     { get; private set; }
        public DateTime? NgaySinh { get; private set; }
        public string   Cmnd     { get; private set; }
        public string   QueQuan  { get; private set; }
        public string   SoDt     { get; private set; }
        public string   MaKhoa   { get; private set; }
        public string   CapBac   { get; private set; }
        public string   CoSo     { get; private set; }
        public string   MatKhau  { get; private set; }

        public TaoNhanVienForm(string suggestedId, string[] danhSachKhoa, Func<string, string> getNextId)
        {
            Text = "Tạo Tài Khoản Nhân Viên";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(610, 640);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel
            {
                ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top,
                BackColor = UiTheme.JordyBlue, Padding = new Padding(18)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));

            int row = 0;
            string _lastSuggestedId = suggestedId;

            void AddLbl(string text, int r, int height = 46)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                layout.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0)
                }, 0, r);
            }

            TextBox MakeTxt(string val = "", bool multi = false, int h = 0)
            {
                var t = new TextBox { Dock = DockStyle.Fill, Text = val, Margin = new Padding(0, 4, 0, 4) };
                if (multi) { t.Multiline = true; t.Height = h > 0 ? h : 56; }
                return t;
            }

            ComboBox MakeCmb(string[] items, bool blank = false)
            {
                var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                if (blank) c.Items.Add("");
                foreach (var it in items) c.Items.Add(it);
                c.SelectedIndex = 0;
                return c;
            }

            // ── MANV ──
            AddLbl("Mã nhân viên *:", row);
            var txtMaNV = MakeTxt(suggestedId);
            layout.Controls.Add(txtMaNV, 1, row++);

            // ── HỌ TÊN ──
            AddLbl("Họ tên *:", row);
            var txtHoTen = MakeTxt();
            layout.Controls.Add(txtHoTen, 1, row++);

            // ── VAI TRÒ ──
            AddLbl("Vai trò *:", row);
            var cmbVaiTro = MakeCmb(new[] { "Điều phối viên", "Bác sĩ/Y sĩ", "Kỹ thuật viên" });
            layout.Controls.Add(cmbVaiTro, 1, row++);

            // Khi VAITRO thay đổi → tính lại mã đề xuất (chỉ thay nếu QLBV chưa sửa)
            cmbVaiTro.SelectedIndexChanged += (s, e) =>
            {
                string vt = cmbVaiTro.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(vt)) return;
                if (string.Equals(txtMaNV.Text.Trim(), _lastSuggestedId, StringComparison.OrdinalIgnoreCase))
                {
                    try { _lastSuggestedId = getNextId(vt); txtMaNV.Text = _lastSuggestedId; }
                    catch { }
                }
            };

            // ── PHÁI ──
            AddLbl("Phái:", row);
            var cmbPhai = MakeCmb(new[] { "Nam", "Nữ" }, blank: true);
            layout.Controls.Add(cmbPhai, 1, row++);

            // ── NGÀY SINH ──
            AddLbl("Ngày sinh (dd/mm/yyyy):", row);
            var txtNgaySinh = MakeTxt();
            layout.Controls.Add(txtNgaySinh, 1, row++);

            // ── CMND ──
            AddLbl("CMND/CCCD:", row);
            var txtCmnd = MakeTxt();
            layout.Controls.Add(txtCmnd, 1, row++);

            // ── QUÊ QUÁN ──
            AddLbl("Quê quán:", row);
            var txtQueQuan = MakeTxt();
            layout.Controls.Add(txtQueQuan, 1, row++);

            // ── SỐ ĐT ──
            AddLbl("Số điện thoại:", row);
            var txtSoDt = MakeTxt();
            layout.Controls.Add(txtSoDt, 1, row++);

            // ── KHOA ──
            AddLbl("Khoa (MAKHOA):", row);
            var cmbKhoa = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbKhoa.Items.Add("-- Không chọn --");
            foreach (var k in danhSachKhoa) cmbKhoa.Items.Add(k);
            cmbKhoa.SelectedIndex = 0;
            layout.Controls.Add(cmbKhoa, 1, row++);

            // ── CẤP BẬC ──
            AddLbl("Cấp bậc (OLS):", row);
            var cmbCapBac = MakeCmb(
                new[] { "Ban Giám đốc", "Lãnh đạo khoa", "Lãnh đạo phòng", "Nhân viên" },
                blank: true);
            layout.Controls.Add(cmbCapBac, 1, row++);

            // ── CƠ SỞ ──
            AddLbl("Cơ sở (OLS):", row);
            var cmbCoSo = MakeCmb(new[] { "Hồ Chí Minh", "Hải Phòng", "Hà Nội" }, blank: true);
            layout.Controls.Add(cmbCoSo, 1, row++);

            // ── MẬT KHẨU ──
            AddLbl("Mật khẩu:", row);
            var txtMatKhau = MakeTxt("nv123");
            layout.Controls.Add(txtMatKhau, 1, row++);

            // Note
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "* Bắt buộc. Mã NV tự động đề xuất nhỏ nhất chưa dùng theo tiền tố vai trò (BS/KTV/NV).",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2); row++;

            // Buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Tạo Tài Khoản", Width = 155, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText
            };
            cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtMaNV.Text))   { MessageBox.Show("Nhập Mã nhân viên."); return; }
                if (string.IsNullOrWhiteSpace(txtHoTen.Text))  { MessageBox.Show("Nhập Họ tên."); return; }
                if (string.IsNullOrWhiteSpace(txtMatKhau.Text)){ MessageBox.Show("Nhập Mật khẩu."); return; }

                DateTime? dob = null;
                if (!string.IsNullOrWhiteSpace(txtNgaySinh.Text))
                {
                    if (!DateTime.TryParseExact(txtNgaySinh.Text, "dd/MM/yyyy", null,
                        System.Globalization.DateTimeStyles.None, out DateTime d))
                    { MessageBox.Show("Ngày sinh không hợp lệ (dd/MM/yyyy)."); return; }
                    dob = d;
                }

                MaNV    = txtMaNV.Text.Trim();
                HoTen   = txtHoTen.Text.Trim();
                VaiTro  = cmbVaiTro.SelectedItem?.ToString() ?? "Điều phối viên";
                Phai    = cmbPhai.SelectedItem?.ToString() ?? "";
                NgaySinh = dob;
                Cmnd    = txtCmnd.Text.Trim();
                QueQuan = txtQueQuan.Text.Trim();
                SoDt    = txtSoDt.Text.Trim();

                // Tách MAKHOA ra từ "K001 - TieuHoa"
                string khoaStr = cmbKhoa.SelectedItem?.ToString() ?? "";
                MaKhoa = khoaStr.Contains(" - ")
                    ? khoaStr.Split(new[] { " - " }, StringSplitOptions.None)[0]
                    : "";

                CapBac  = cmbCapBac.SelectedItem?.ToString() ?? "";
                CoSo    = cmbCoSo.SelectedItem?.ToString() ?? "";
                MatKhau = txtMatKhau.Text.Trim();

                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row); layout.SetColumnSpan(ft, 2);

            scroll.Controls.Add(layout);
            Controls.Add(scroll);
            AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Tạo Tài Khoản Bệnh Nhân (QLBV Admin)
    //  — Tất cả trường từ schema BENHNHAN (schema_phanhe2.sql)
    //  — Auto-suggest MABN nhỏ nhất chưa dùng, QLBV có thể sửa
    //  — Password đề xuất mặc định "bn123"
    // ════════════════════════════════════════════════════════════════════════
    public class TaoBenhNhanForm : Form
    {
        public string    MaBN         { get; private set; }
        public string    TenBN        { get; private set; }
        public string    Phai         { get; private set; }
        public DateTime? NgaySinh     { get; private set; }
        public string    CCCD         { get; private set; }
        public string    SoNha        { get; private set; }
        public string    TenDuong     { get; private set; }
        public string    QuanHuyen    { get; private set; }
        public string    TinhTP       { get; private set; }
        public string    TienSuBenh   { get; private set; }
        public string    TienSuBenhGD { get; private set; }
        public string    DiUngThuoc   { get; private set; }
        public string    MatKhau      { get; private set; }

        public TaoBenhNhanForm(string suggestedId)
        {
            Text = "Tạo Tài Khoản Bệnh Nhân";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(610, 730);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel
            {
                ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top,
                BackColor = UiTheme.JordyBlue, Padding = new Padding(18)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));

            int row = 0;

            void AddLbl(string text, int r, int height = 46)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                layout.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0)
                }, 0, r);
            }

            TextBox MakeTxt(string val = "") =>
                new TextBox { Dock = DockStyle.Fill, Text = val, Margin = new Padding(0, 4, 0, 4) };

            // ── MABN ──
            AddLbl("Mã bệnh nhân *:", row);
            var txtMaBN = MakeTxt(suggestedId);
            layout.Controls.Add(txtMaBN, 1, row++);

            // ── TÊN BN ──
            AddLbl("Họ tên *:", row);
            var txtTenBN = MakeTxt();
            layout.Controls.Add(txtTenBN, 1, row++);

            // ── PHÁI ──
            AddLbl("Phái:", row);
            var cmbPhai = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPhai.Items.AddRange(new object[] { "", "Nam", "Nữ" });
            cmbPhai.SelectedIndex = 0;
            layout.Controls.Add(cmbPhai, 1, row++);

            // ── NGÀY SINH ──
            AddLbl("Ngày sinh (dd/mm/yyyy):", row);
            var txtNgaySinh = MakeTxt();
            layout.Controls.Add(txtNgaySinh, 1, row++);

            // ── CCCD ──
            AddLbl("CCCD:", row);
            var txtCCCD = MakeTxt();
            layout.Controls.Add(txtCCCD, 1, row++);

            // ── SỐ NHÀ ──
            AddLbl("Số nhà:", row);
            var txtSoNha = MakeTxt();
            layout.Controls.Add(txtSoNha, 1, row++);

            // ── TÊN ĐƯỜNG ──
            AddLbl("Tên đường:", row);
            var txtTenDuong = MakeTxt();
            layout.Controls.Add(txtTenDuong, 1, row++);

            // ── QUẬN/HUYỆN ──
            AddLbl("Quận/Huyện:", row);
            var txtQuanHuyen = MakeTxt();
            layout.Controls.Add(txtQuanHuyen, 1, row++);

            // ── TỈNH/TP ──
            AddLbl("Tỉnh/TP:", row);
            var txtTinhTP = MakeTxt();
            layout.Controls.Add(txtTinhTP, 1, row++);

            // ── TIỀN SỬ BỆNH (multiline) ──
            AddLbl("Tiền sử bệnh:", row, 68);
            var txtTienSuBenh = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, Height = 56,
                ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(txtTienSuBenh, 1, row++);

            // ── TIỀN SỬ BỆNH GIA ĐÌNH (multiline) ──
            AddLbl("Tiền sử bệnh GĐ:", row, 68);
            var txtTienSuBenhGD = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, Height = 56,
                ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(txtTienSuBenhGD, 1, row++);

            // ── DỊ ỨNG THUỐC ──
            AddLbl("Dị ứng thuốc:", row);
            var txtDiUngThuoc = MakeTxt();
            layout.Controls.Add(txtDiUngThuoc, 1, row++);

            // ── MẬT KHẨU ──
            AddLbl("Mật khẩu:", row);
            var txtMatKhau = MakeTxt("bn123");
            layout.Controls.Add(txtMatKhau, 1, row++);

            // Note
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "* Bắt buộc. Mã BN tự động đề xuất nhỏ nhất chưa dùng (BN000001, BN000002, ...).",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2); row++;

            // Buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Tạo Tài Khoản", Width = 155, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText
            };
            cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtMaBN.Text))   { MessageBox.Show("Nhập Mã bệnh nhân."); return; }
                if (string.IsNullOrWhiteSpace(txtTenBN.Text))  { MessageBox.Show("Nhập Họ tên."); return; }
                if (string.IsNullOrWhiteSpace(txtMatKhau.Text)){ MessageBox.Show("Nhập Mật khẩu."); return; }

                DateTime? dob = null;
                if (!string.IsNullOrWhiteSpace(txtNgaySinh.Text))
                {
                    if (!DateTime.TryParseExact(txtNgaySinh.Text, "dd/MM/yyyy", null,
                        System.Globalization.DateTimeStyles.None, out DateTime d))
                    { MessageBox.Show("Ngày sinh không hợp lệ (dd/MM/yyyy)."); return; }
                    dob = d;
                }

                MaBN         = txtMaBN.Text.Trim();
                TenBN        = txtTenBN.Text.Trim();
                Phai         = cmbPhai.SelectedItem?.ToString() ?? "";
                NgaySinh     = dob;
                CCCD         = txtCCCD.Text.Trim();
                SoNha        = txtSoNha.Text.Trim();
                TenDuong     = txtTenDuong.Text.Trim();
                QuanHuyen    = txtQuanHuyen.Text.Trim();
                TinhTP       = txtTinhTP.Text.Trim();
                TienSuBenh   = txtTienSuBenh.Text.Trim();
                TienSuBenhGD = txtTienSuBenhGD.Text.Trim();
                DiUngThuoc   = txtDiUngThuoc.Text.Trim();
                MatKhau      = txtMatKhau.Text.Trim();

                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row); layout.SetColumnSpan(ft, 2);

            scroll.Controls.Add(layout);
            Controls.Add(scroll);
            AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Tạo HSBA Cấp Cứu — gọi sp_KhoiTaoHSBAKhancap
    // ════════════════════════════════════════════════════════════════════════
    // ✅ [GỘPCODE-D] Form nhập tham số cho stored procedure sp_KhoiTaoHSBAKhancap.
    //    Nguồn: sys_PH2.sql — AuditBSExecCapCuu ghi vết mỗi lần BS EXECUTE procedure này.
    public class CapCuuForm : Form
    {
        public string MaHSBA  { get; private set; }
        public string MaBN    { get; private set; }
        public string MaKhoa  { get; private set; }

        public CapCuuForm()
        {
            Text = "Tạo HSBA Cấp Cứu Khẩn Cấp"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(440, 250);
            BackColor = Color.FromArgb(255, 230, 230); Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = Color.FromArgb(255, 200, 200) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            string[] lbs = { "Mã HSBA mới:", "Mã Bệnh Nhân:", "Mã Khoa (K001/K002/K003):" };
            var flds = new TextBox[lbs.Length];
            for (int i = 0; i < lbs.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = Color.DarkRed, Anchor = AnchorStyles.Right }, 0, i);
                flds[i] = new TextBox { Dock = DockStyle.Fill };
                layout.Controls.Add(flds[i], 1, i);
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            var note = new Label { Text = "Chẩn đoán/điều trị sẽ được điền tự động: 'Cấp cứu khẩn cấp'.", ForeColor = Color.DarkRed, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            layout.Controls.Add(note, 0, lbs.Length); layout.SetColumnSpan(note, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Tạo Cấp Cứu", Width = 130, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(206, 17, 38), ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(flds[0].Text)) { MessageBox.Show("Nhập Mã HSBA."); return; }
                if (string.IsNullOrWhiteSpace(flds[1].Text)) { MessageBox.Show("Nhập Mã Bệnh Nhân."); return; }
                if (string.IsNullOrWhiteSpace(flds[2].Text)) { MessageBox.Show("Nhập Mã Khoa."); return; }
                MaHSBA = flds[0].Text.Trim(); MaBN = flds[1].Text.Trim(); MaKhoa = flds[2].Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, lbs.Length + 1); layout.SetColumnSpan(ft, 2);
            Controls.Add(layout); AcceptButton = ok;
        }
    }
}