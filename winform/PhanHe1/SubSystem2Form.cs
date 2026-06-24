using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PhanHe1.Forms
{
    // ════════════════════════════════════════════════════════════════════════
    // Phân hệ 2 — đồng bộ với sql_ph2/run/ (schema QLBV, audit 06.sql, backup 08.sql)
    // ════════════════════════════════════════════════════════════════════════
    public class SubSystem2Form : Form
    {
        private enum UserRole { Unknown, DPV, BACSI, KTV, BN, ADMIN, GIAMDOC }

        private enum ThongBaoSendMode { ViewOnly, AdminOlsPicker, BgdProcedure }

        // run/06.sql §3.2 NC1–NC9 — khớp 08.sql PR_AUTO_ARCHIVE_AUDIT_LOG
        private const string AuditStandardPolicyInList =
            "'AUDITSUCDPVUPDATEBN','AUDITDPVUPDATEHSBA','AUDITSUCKTVUPDATEDV','AUDITSUCBSUPDATEDT'," +
            "'AUDITFAILBSUPDATENV','AUDITDIEUPHOINHANSU','AUDITSUCBSEXECPROC','AUDITSUCBSEXECFUNC'," +
            "'AUDITDPVXEMLICHSUBN','AUDITDPVXEMLICHSUBN_FAIL'";

        // run/06.sql §3.3 FGA — cũng được archive (08.sql OR fga_policy_name)
        private const string AuditFgaPolicyInList =
            "'AUDITSUADONTHUOC','AUDITBSUPDATEHSBA_HOPPHAP'";

        // run/06.sql §3.4.4 illegal policies
        private const string AuditIllegalPolicyInList =
            "'AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV'";

        // run/08.sql [08-QLBV-02] — unified policies được gom vào AUDIT_ARCHIVE_LOG
        private const string AuditArchiveUnifiedPolicyInList =
            AuditStandardPolicyInList + "," + AuditIllegalPolicyInList;

        private const string SqlArchiveBackupHistory =
            "SELECT BACKUP_ID, " +
            "TO_CHAR(BACKUP_TIME, 'DD/MM/YYYY HH24:MI:SS') AS BACKUP_TIME, " +
            "BACKUP_TYPE, FILE_NAME, STATUS, DESCRIPTION " +
            "FROM QLBV.BACKUP_HISTORY WHERE BACKUP_TYPE = 'AUDIT_LOG_AUTO' " +
            "ORDER BY BACKUP_TIME DESC FETCH FIRST 15 ROWS ONLY";

        // run/08.sql [08-QLBV-01] — cột backup_type, file_name, status, description
        private const string SqlBackupHistory =
            "SELECT BACKUP_ID, " +
            "TO_CHAR(BACKUP_TIME, 'DD/MM/YYYY HH24:MI:SS') AS BACKUP_TIME, " +
            "BACKUP_TYPE, FILE_NAME, STATUS, DESCRIPTION " +
            "FROM QLBV.BACKUP_HISTORY ORDER BY BACKUP_TIME DESC FETCH FIRST 30 ROWS ONLY";

        private const string SqlRestoreHistory =
            "SELECT RESTORE_ID, " +
            "TO_CHAR(RESTORE_TIME, 'DD/MM/YYYY HH24:MI:SS') AS RESTORE_TIME, " +
            "RESTORE_TYPE, FILE_SRC, EXECUTED_BY, STATUS " +
            "FROM QLBV.RESTORE_HISTORY ORDER BY RESTORE_TIME DESC FETCH FIRST 30 ROWS ONLY";

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
            ApplyAuditClientIdentifier();

            Text = $"DocCare — {GetRoleTitle()}";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 700);
            Font = UiTheme.BodyFont;
            BackColor = UiTheme.LightCyan;
            BuildUi();
        }

        private static void SafeBalanceHorizontalSplit(SplitContainer split, double topRatio = 0.5, int minTop = 60, int minBottom = 60)
        {
            if (split == null || split.IsDisposed) return;
            int h = split.Height - split.SplitterWidth;
            if (h < minTop + minBottom + 8) return;
            int top = (int)(h * topRatio);
            top = Math.Max(minTop, Math.Min(h - minBottom, top));
            try
            {
                if (Math.Abs(split.SplitterDistance - top) > 2)
                    split.SplitterDistance = top;
            }
            catch { }
        }

        // ── role detection ───────────────────────────────────────────────
        // ✅ [GỘPCODE-1] ROLE_DPV, ROLE_BACSI, ROLE_KTV, ROLE_BENHNHAN đã đúng
        //    Nguồn: admin_ph2.sql — các role được tạo và grant trên bảng QLBV.*.
        // ✅ [GỘPCODE-A] GIAMDOC: detect via CAPBAC='Ban Giám đốc' từ QLBV.NHANVIEN (GD0001–GD0003)
        private UserRole DetermineUserRole()
        {
            if (currentRoles.Contains("DBA") || string.Equals(currentUser, "APP_ADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentUser, "QLBV", StringComparison.OrdinalIgnoreCase))
                return UserRole.ADMIN;

            // Giám đốc trước DPV — CAPBAC 'Ban Giám đốc' (insert_nhanvien.sql GD0001–GD0003)
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

        // run/09.sql: EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_*') trước mỗi thao tác audit
        private void ApplyAuditClientIdentifier()
        {
            string roleId = null;
            switch (userRole)
            {
                case UserRole.DPV: 
                case UserRole.GIAMDOC: roleId = "ROLE_DPV"; break;
                case UserRole.BACSI:   roleId = "ROLE_BACSI"; break;
                case UserRole.KTV:     roleId = "ROLE_KTV"; break;
                case UserRole.BN:      roleId = "ROLE_BENHNHAN"; break;

            }
            if (roleId == null) return;
            try { service.SetClientIdentifier(roleId); } catch { }
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
            try
            {
                UseWaitCursor = true;
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex) { Err("Không thể tải dữ liệu:\n" + ex.Message); }
            finally { UseWaitCursor = false; }
        }

        // BS: HSBA_DV / DONTHUOC — lấy MAHSBA từ HSBA trước, tránh VPD full-scan bảng lớn
        private void LoadBsChildGridAsync(DataGridView grid, string table, string columns, string orderBy, string mahsbaKw = null)
        {
            if (grid == null) return;
            grid.Enabled = false;
            UseWaitCursor = true;

            System.Threading.Tasks.Task.Run(() =>
            {
                DataTable result = null;
                Exception err = null;
                try
                {
                    string hsbaSql = "SELECT MAHSBA FROM QLBV.HSBA WHERE MABS = USER";
                    if (!string.IsNullOrEmpty(mahsbaKw)) hsbaSql += $" AND MAHSBA LIKE '%{Esc(mahsbaKw)}%'";
                    hsbaSql += " FETCH FIRST 500 ROWS ONLY";

                    var dtHsba = service.Query(hsbaSql);
                    if (dtHsba.Rows.Count == 0)
                    {
                        result = new DataTable();
                    }
                    else
                    {
                        var inList = new StringBuilder();
                        for (int i = 0; i < dtHsba.Rows.Count; i++)
                        {
                            string ma = dtHsba.Rows[i]["MAHSBA"]?.ToString();
                            if (string.IsNullOrEmpty(ma)) continue;
                            if (inList.Length > 0) inList.Append(',');
                            inList.Append('\'').Append(Esc(ma)).Append('\'');
                        }
                        if (inList.Length == 0)
                        {
                            result = new DataTable();
                        }
                        else
                        {
                            string sql = $"SELECT {columns} FROM QLBV.{table} " +
                                         $"WHERE MAHSBA IN ({inList}) ORDER BY {orderBy} FETCH FIRST 200 ROWS ONLY";
                            result = service.Query(sql);
                        }
                    }
                }
                catch (Exception ex) { err = ex; }

                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed) return;
                        grid.Enabled = true;
                        UseWaitCursor = false;
                        if (err != null)
                        {
                            grid.DataSource = null;
                            Err("Không thể tải dữ liệu:\n" + err.Message);
                            return;
                        }
                        grid.DataSource = result;
                        UiTheme.StyleGrid(grid);
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        private void LoadBsHsbaDvGrid(DataGridView grid, string mahsbaKw = null) =>
            LoadBsChildGridAsync(grid, "HSBA_DV", "MAHSBA, LOAIDV, NGAYDV, MAKTV, KETQUA", "NGAYDV DESC", mahsbaKw);

        private void LoadBsDonThuocGrid(DataGridView grid, string mahsbaKw = null) =>
            LoadBsChildGridAsync(grid, "DONTHUOC", "MAHSBA, NGAYDT, TENTHUOC, LIEUDUNG", "NGAYDT DESC", mahsbaKw);

        private void LoadBackupHistoryGrid(DataGridView grid)
        {
            try
            {
                grid.DataSource = service.Query(SqlBackupHistory);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải QLBV.BACKUP_HISTORY:\n" + ex.Message +
                    "\n\nGợi ý: kiểm tra bảng BACKUP_HISTORY đã được tạo trên schema QLBV.");
            }
        }

        private void LoadRestoreHistoryGrid(DataGridView grid)
        {
            try
            {
                grid.DataSource = service.Query(SqlRestoreHistory);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải QLBV.RESTORE_HISTORY:\n" + ex.Message +
                    "\n\nGợi ý: kiểm tra bảng RESTORE_HISTORY đã được tạo trên schema QLBV.");
            }
        }

        // expdp: 0=OK, 5=completed with errors (partial — thường ORA-39181 OLS/VPD), khác=FAILED
        private static string MapDataPumpStatus(int exitCode)
        {
            if (exitCode == 0) return "SUCCESS";
            if (exitCode == 5) return "PARTIAL";
            return "FAILED";
        }

        private static string MapDataPumpStatusNote(int exitCode)
        {
            if (exitCode == 0) return "expdp hoàn tất thành công (exit 0).";
            if (exitCode == 5) return "expdp hoàn tất với lỗi/partial (exit 5 — thường do OLS/VPD ORA-39181).";
            if (exitCode < 0) return "expdp không chạy được (không tạo hoặc chưa tạo file).";
            return $"expdp thất bại (exit {exitCode}).";
        }

        private void RecordBackupHistory(string backupType, string fileName, int exitCode, string context, TextBox log = null)
        {
            string status = MapDataPumpStatus(exitCode);
            string desc = $"{context} {MapDataPumpStatusNote(exitCode)}";
            service.ExecuteNonQuery(
                $"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_TYPE, FILE_NAME, STATUS, DESCRIPTION) " +
                $"VALUES ('{Esc(backupType)}', '{Esc(fileName)}', '{status}', N'{Esc(desc)}')");
            service.ExecuteNonQuery("COMMIT");
            log?.AppendText($"[{DateTime.Now:HH:mm:ss}] Đã ghi BACKUP_HISTORY (status={status}, exit={exitCode}).\r\n");
        }

        // TabControl lồng nhau: TabPage.Enter không luôn fire — hook cả SelectedIndexChanged
        private static void RegisterTabRefresh(TabControl owner, TabPage page, Action refresh)
        {
            page.Enter += (s, e) => refresh();
            owner.SelectedIndexChanged += (s, e) =>
            {
                if (owner.SelectedTab == page) refresh();
            };
        }

        private static void RegisterTabLazyLoad(TabControl owner, TabPage page, Action load)
        {
            bool loaded = false;
            void tryLoad()
            {
                if (loaded) return;
                loaded = true;
                load();
            }
            page.Enter += (s, e) => tryLoad();
            owner.SelectedIndexChanged += (s, e) =>
            {
                if (owner.SelectedTab == page) tryLoad();
            };
        }

        // BS: chỉ HSBA/DV/DT/BN thuộc bác sĩ đang login — tránh SELECT * full bảng (100k+ dòng)
        private string BsHsbaSql(string mabnKw = null)
        {
            string sql = "SELECT MAHSBA, MABN, NGAY, CHANDOAN, DIEUTRI, KETLUAN, MABS, MAKHOA FROM QLBV.HSBA WHERE MABS = USER";
            if (!string.IsNullOrEmpty(mabnKw)) sql += $" AND MABN LIKE '%{Esc(mabnKw)}%'";
            return sql + " ORDER BY NGAY DESC FETCH FIRST 200 ROWS ONLY";
        }

        private string BsBenhNhanSql(string mabnKw = null)
        {
            // Lọc BN qua HSBA của BS — tránh VPD full-scan BENHNHAN
            string sql = "SELECT MABN, TENBN, PHAI, NGAYSINH, CCCD, SONHA, TENDUONG, QUANHUYEN, TINHTP, TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC " +
                         "FROM QLBV.BENHNHAN WHERE MABN IN (SELECT MABN FROM QLBV.HSBA WHERE MABS = USER)";
            if (!string.IsNullOrEmpty(mabnKw)) sql += $" AND MABN LIKE '%{Esc(mabnKw)}%'";
            return sql + " ORDER BY MABN FETCH FIRST 200 ROWS ONLY";
        }

        // DPV: giới hạn 200 dòng — tránh SELECT * full bảng lúc mở form
        private string DpvBenhNhanSql(string mabnKw = null)
        {
            string sql = "SELECT MABN, TENBN, PHAI, NGAYSINH, CCCD, SONHA, TENDUONG, QUANHUYEN, TINHTP, TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC FROM QLBV.BENHNHAN";
            if (!string.IsNullOrEmpty(mabnKw)) sql += $" WHERE MABN LIKE '%{Esc(mabnKw)}%'";
            return sql + " ORDER BY MABN FETCH FIRST 200 ROWS ONLY";
        }

        private string DpvHsbaSql(string mahsbaKw = null)
        {
            string sql = "SELECT MAHSBA, MABN, NGAY, CHANDOAN, DIEUTRI, KETLUAN, MABS, MAKHOA FROM QLBV.HSBA";
            if (!string.IsNullOrEmpty(mahsbaKw)) sql += $" WHERE MAHSBA LIKE '%{Esc(mahsbaKw)}%'";
            return sql + " ORDER BY NGAY DESC FETCH FIRST 200 ROWS ONLY";
        }

        private string DpvHsbaDvSql(string mahsbaKw = null)
        {
            string sql = "SELECT MAHSBA, LOAIDV, NGAYDV, MAKTV, KETQUA FROM QLBV.HSBA_DV";
            if (!string.IsNullOrEmpty(mahsbaKw)) sql += $" WHERE MAHSBA LIKE '%{Esc(mahsbaKw)}%'";
            return sql + " ORDER BY MAHSBA, NGAYDV DESC FETCH FIRST 200 ROWS ONLY";
        }

        private void LoadAuditArchiveLogGrid(DataGridView grid)
        {
            LoadAuditArchiveData(grid, showSummary: false);
        }

        // run/08.sql [08-QLBV-01] — nhật ký đã archive từ PR_AUTO_ARCHIVE_AUDIT_LOG
        private void LoadAuditArchiveData(DataGridView grid, bool showSummary = true)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(ARCHIVE_DATE, 'DD/MM/YYYY') AS ""NGÀY ARCHIVE"",
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        RETURN_CODE               AS ""MÃ KQ"",
                        NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES) AS ""POLICY"",
                        SUBSTR(SQL_TEXT, 1, 500)  AS ""CHI TIẾT MÔ TẢ""
                    FROM QLBV.AUDIT_ARCHIVE_LOG
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                var dt = service.Query(sql);
                grid.DataSource = dt;
                UiTheme.StyleGrid(grid);

                if (showSummary)
                {
                    int archived = dt.Rows.Count;
                    int eligible = QueryEligibleAuditArchiveCount();
                    if (archived == 0 && eligible == 0)
                        MessageBox.Show(
                            "AUDIT_ARCHIVE_LOG đang trống.\n\nChưa có nhật ký audit đủ điều kiện archive.\n" +
                            "Thực hiện nghiệp vụ trên hệ thống rồi chạy archive tại Sao Lưu → Scheduler.",
                            "Archive Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else if (archived == 0 && eligible > 0)
                        MessageBox.Show(
                            $"AUDIT_ARCHIVE_LOG đang trống nhưng còn {eligible} dòng eligible trên live trail.\n\n" +
                            "→ Backup → Scheduler → ▶ Chạy Archive Ngay",
                            "Archive Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(
                            $"Đã archive: {archived} dòng (hiển thị tối đa 200).\nLive trail eligible: {eligible} dòng.",
                            "Archive Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải QLBV.AUDIT_ARCHIVE_LOG:\n" + ex.Message +
                    "\n\nGợi ý: Sao Lưu → Scheduler → Chạy Archive Ngay.");
            }
        }

        private void LoadArchiveBackupHistoryGrid(DataGridView grid)
        {
            try
            {
                grid.DataSource = service.Query(SqlArchiveBackupHistory);
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = null;
            }
        }

        // Khớp WHERE trong 08.sql PR_AUTO_ARCHIVE_AUDIT_LOG
        private int QueryEligibleAuditArchiveCount()
        {
            try
            {
                var dt = service.Query($@"
                    SELECT COUNT(*) AS CNT FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN ({AuditArchiveUnifiedPolicyInList})
                       OR UPPER(FGA_POLICY_NAME) IN ({AuditFgaPolicyInList})
                       OR ACTION_NAME = 'LOGON'");
                return Convert.ToInt32(dt.Rows[0]["CNT"]);
            }
            catch { return -1; }
        }

        private bool EnsureArchiveObjectsReady(out string error)
        {
            error = null;
            try
            {
                var dt = service.Query(@"
                    SELECT object_name, object_type, status FROM user_objects
                    WHERE object_name IN ('PR_AUTO_ARCHIVE_AUDIT_LOG', 'AUDIT_ARCHIVE_LOG')
                    ORDER BY object_name");
                bool hasProc = false, hasTbl = false;
                foreach (DataRow r in dt.Rows)
                {
                    if (string.Equals(r["OBJECT_NAME"]?.ToString(), "PR_AUTO_ARCHIVE_AUDIT_LOG", StringComparison.OrdinalIgnoreCase))
                        hasProc = string.Equals(r["STATUS"]?.ToString(), "VALID", StringComparison.OrdinalIgnoreCase);
                    if (string.Equals(r["OBJECT_NAME"]?.ToString(), "AUDIT_ARCHIVE_LOG", StringComparison.OrdinalIgnoreCase))
                        hasTbl = true;
                }
                if (!hasTbl || !hasProc)
                {
                    error = "Thiếu procedure hoặc bảng archive. Liên hệ quản trị viên để cài đặt PR_AUTO_ARCHIVE_AUDIT_LOG và AUDIT_ARCHIVE_LOG.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Gọi PR_AUTO_ARCHIVE_AUDIT_LOG — dùng chung Scheduler tab
        private bool RunArchiveAuditLog(Action<string> logLine, out string summary)
        {
            summary = null;
            if (!EnsureArchiveObjectsReady(out string prepErr))
            {
                summary = prepErr;
                logLine?.Invoke($"[LỖI]: {prepErr}");
                return false;
            }

            int eligibleBefore = QueryEligibleAuditArchiveCount();
            logLine?.Invoke("SQL> BEGIN QLBV.PR_AUTO_ARCHIVE_AUDIT_LOG; END;");
            try
            {
                service.ExecuteNonQuery("BEGIN QLBV.PR_AUTO_ARCHIVE_AUDIT_LOG; END;");
            }
            catch (Exception ex)
            {
                summary = ex.Message;
                logLine?.Invoke($"[LỖI]: {ex.Message}");
                return false;
            }

            try
            {
                var dt = service.Query(@"
                    SELECT STATUS, DESCRIPTION FROM QLBV.BACKUP_HISTORY
                    WHERE BACKUP_TYPE = 'AUDIT_LOG_AUTO'
                    ORDER BY BACKUP_TIME DESC FETCH FIRST 1 ROW ONLY");
                string status = dt.Rows.Count > 0 ? dt.Rows[0]["STATUS"]?.ToString() : "SUCCESS";
                if (dt.Rows.Count > 0)
                    summary = $"{status}: {dt.Rows[0]["DESCRIPTION"]}";
                else
                    summary = "Procedure hoàn tất (chưa ghi BACKUP_HISTORY — kiểm tra quyền INSERT).";

                if (eligibleBefore >= 0)
                    summary += $"\nLive trail eligible trước archive: {eligibleBefore} dòng.";
                logLine?.Invoke($"[{DateTime.Now:HH:mm:ss}] {summary.Replace("\n", " ")}");
                return !string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                summary = "Procedure hoàn tất.";
                if (eligibleBefore >= 0)
                    summary += $"\nLive trail eligible trước archive: {eligibleBefore} dòng.";
                logLine?.Invoke($"[{DateTime.Now:HH:mm:ss}] {summary}");
                return true;
            }
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

        private static readonly string[] DataPumpImportantTables =
        {
            "qlbv.benhnhan", "qlbv.hsba", "qlbv.hsba_dv", "qlbv.donthuoc"
        };

        private enum DataPumpDumpListKind
        {
            ExportSchema,
            ExportTables,
            ImportSchema,
            ImportTable
        }

        private void SplitDataPumpConnect(out string credential, out string hostDescriptor)
        {
            string cs = service.GetDataPumpConnectString();
            int at = cs.IndexOf('@');
            credential = at > 0 ? cs.Substring(0, at) : "qlbv/123";
            hostDescriptor = at > 0 ? cs.Substring(at + 1) : "localhost:1521/xepdb1";
        }

        private static void StyleDataPumpCombo(ComboBox cmb, int dropDownWidth = 520)
        {
            cmb.DropDownWidth = dropDownWidth;
            cmb.IntegralHeight = false;
        }

        private static void SelectComboPrefer(ComboBox cmb, string prefer)
        {
            if (cmb.Items.Count == 0) return;
            if (!string.IsNullOrEmpty(prefer))
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                    if (string.Equals(cmb.Items[i]?.ToString(), prefer, StringComparison.OrdinalIgnoreCase))
                    {
                        cmb.SelectedIndex = i;
                        return;
                    }
                cmb.Items.Insert(0, prefer);
            }
            cmb.SelectedIndex = 0;
        }

        private void PopulateDataPumpDumpCombo(ComboBox cmb, DataPumpDumpListKind kind, string preferFile = null)
        {
            cmb.Items.Clear();
            switch (kind)
            {
                case DataPumpDumpListKind.ExportSchema:
                    cmb.Items.Add($"qlbv_schema_{DateTime.Now:yyyyMMdd}.dmp");
                    cmb.Items.Add("qlbv_schema_latest.dmp");
                    break;
                case DataPumpDumpListKind.ExportTables:
                    cmb.Items.Add($"qlbv_important_tables_{DateTime.Now:yyyyMMdd_HHmm}.dmp");
                    cmb.Items.Add("qlbv_important_tables.dmp");
                    break;
                default:
                    string typeFilter = kind == DataPumpDumpListKind.ImportSchema
                        ? "'SCHEMA_QLBV'"
                        : "'FULL','SCHEMA_QLBV'";
                    try
                    {
                        var dt = service.Query(
                            "SELECT FILE_NAME FROM QLBV.BACKUP_HISTORY " +
                            $"WHERE BACKUP_TYPE IN ({typeFilter}) AND STATUS IN ('SUCCESS','PARTIAL') " +
                            "ORDER BY BACKUP_TIME DESC FETCH FIRST 20 ROWS ONLY");
                        foreach (DataRow r in dt.Rows)
                        {
                            string fn = r["FILE_NAME"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(fn) && !cmb.Items.Contains(fn))
                                cmb.Items.Add(fn);
                        }
                    }
                    catch { }

                    if (cmb.Items.Count == 0)
                    {
                        if (kind == DataPumpDumpListKind.ImportSchema)
                        {
                            cmb.Items.Add("qlbv_schema_latest.dmp");
                            cmb.Items.Add($"qlbv_schema_{DateTime.Now:yyyyMMdd}.dmp");
                        }
                        else
                        {
                            cmb.Items.Add("qlbv_important_tables.dmp");
                            cmb.Items.Add("qlbv_schema_latest.dmp");
                        }
                    }
                    break;
            }
            SelectComboPrefer(cmb, preferFile);
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

        // 07.sql [07-CMD] case 4 — impdp một bảng cụ thể từ file dump
        private (string table, string dumpFile)? PromptImpdpTableRestore(string preferDump = null)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Phục Hồi Một Bảng (impdp)";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ClientSize = new Size(520, 220);

                var lblHint = new Label
                {
                    Text = "Chọn bảng cần phục hồi và file .dmp trong BACKUP_DIR.\nThường dùng dump từ \"Backup Bảng Quan Trọng\".",
                    Location = new Point(12, 10),
                    AutoSize = true,
                    MaximumSize = new Size(490, 0),
                    ForeColor = Color.FromArgb(90, 90, 110)
                };
                var lblTable = new Label { Text = "Bảng:", Location = new Point(12, 58), AutoSize = true };
                var cmbTable = new ComboBox
                {
                    Location = new Point(12, 78),
                    Width = 488,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                StyleDataPumpCombo(cmbTable, 480);
                cmbTable.Items.AddRange(DataPumpImportantTables);
                cmbTable.SelectedIndex = 3;

                var lblDump = new Label { Text = "File dump:", Location = new Point(12, 108), AutoSize = true };
                var cmbDump = new ComboBox
                {
                    Location = new Point(12, 128),
                    Width = 408,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                StyleDataPumpCombo(cmbDump, 560);
                var btnRefreshDump = QuickBtn("↻ Tải lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 78, 28);
                btnRefreshDump.Location = new Point(422, 127);
                btnRefreshDump.Click += (s, e) => PopulateDataPumpDumpCombo(cmbDump, DataPumpDumpListKind.ImportTable, cmbDump.SelectedItem?.ToString());

                PopulateDataPumpDumpCombo(cmbDump, DataPumpDumpListKind.ImportTable, preferDump);

                var btnOk = new Button { Text = "Phục Hồi", DialogResult = DialogResult.OK, Location = new Point(332, 168), Width = 80 };
                var btnCancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Location = new Point(420, 168), Width = 80 };
                dlg.Controls.AddRange(new Control[] { lblHint, lblTable, cmbTable, lblDump, cmbDump, btnRefreshDump, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK) return null;

                string table = cmbTable.SelectedItem?.ToString()?.Trim();
                string dump = cmbDump.SelectedItem?.ToString()?.Trim();
                if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(dump))
                {
                    Err("Cần chọn bảng và file dump.");
                    return null;
                }
                return (table, dump);
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
            var tTB = MakeTab("Thông Báo");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo); 
            BuildDPV_BN(tBN, tabs); BuildDPV_HSBA(tHSBA, tabs); BuildDPV_DV(tDV, tabs); BuildThongBaoTab(tTB, tabs, ThongBaoSendMode.ViewOnly);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tBN, tHSBA, tDV, tTB, tDemo });
            parent.Controls.Add(tabs);
        }

        // DPV – Bệnh Nhân
        private void BuildDPV_BN(TabPage tab, TabControl ownerTabs)
        {
            dgvDpvBN = MakeGrid(true);

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Thêm Bệnh Nhân", UiTheme.PastelGreen, UiTheme.DeepBlue, 150);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvDpvBN, DpvBenhNhanSql(string.IsNullOrEmpty(kw) ? null : kw)); };
            btnAdd.Click += (s, e) => DPV_ThemBN();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvBN, DpvBenhNhanSql());

            dgvDpvBN.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvBN.Rows[e.RowIndex];
                    string ma = r.Cells["MABN"].Value?.ToString();
                    DateTime? ngaySinh = null;
                    if (r.Cells["NGAYSINH"].Value != null && r.Cells["NGAYSINH"].Value != DBNull.Value)
                        ngaySinh = Convert.ToDateTime(r.Cells["NGAYSINH"].Value);

                    var values = new BenhNhanFormValues
                    {
                        TenBN = r.Cells["TENBN"].Value?.ToString(),
                        Phai = r.Cells["PHAI"].Value?.ToString(),
                        NgaySinh = ngaySinh,
                        CCCD = r.Cells["CCCD"].Value?.ToString(),
                        SoNha = r.Cells["SONHA"].Value?.ToString(),
                        TenDuong = r.Cells["TENDUONG"].Value?.ToString(),
                        QuanHuyen = r.Cells["QUANHUYEN"].Value?.ToString(),
                        TinhTP = r.Cells["TINHTP"].Value?.ToString(),
                        TienSuBenh = r.Cells["TIENSUBENH"].Value?.ToString(),
                        TienSuBenhGD = r.Cells["TIENSUBENHGD"].Value?.ToString(),
                        DiUngThuoc = r.Cells["DIUNGTHUOC"].Value?.ToString()
                    };

                    using (var f = new BenhNhanAddForm(ma, values))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                string sqlUpdate = $"UPDATE QLBV.BENHNHAN SET " +
                                                   $"TENBN=N'{Esc(f.TenBN)}', " +
                                                   $"PHAI=N'{Esc(f.Phai)}', " +
                                                   $"NGAYSINH=TO_DATE('{f.NgaySinh:dd/MM/yyyy}','DD/MM/YYYY'), " +
                                                   $"CCCD='{Esc(f.CCCD)}', " +
                                                   $"SONHA=N'{Esc(f.SoNha)}', " +
                                                   $"TENDUONG=N'{Esc(f.TenDuong)}', " +
                                                   $"QUANHUYEN=N'{Esc(f.QuanHuyen)}', " +
                                                   $"TINHTP=N'{Esc(f.TinhTP)}', " +
                                                   $"TIENSUBENH=N'{Esc(f.TienSuBenh)}', " +
                                                   $"TIENSUBENHGD=N'{Esc(f.TienSuBenhGD)}', " +
                                                   $"DIUNGTHUOC=N'{Esc(f.DiUngThuoc)}' " +
                                                   $"WHERE MABN='{Esc(ma)}'";

                                service.ExecuteNonQuery(sqlUpdate);
                                Ok("Cập nhật toàn bộ thông tin bệnh nhân thành công!");
                                LoadGrid(dgvDpvBN, DpvBenhNhanSql());
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvBN, Toolbar(txtS, btnS, btnAdd, btnRe, Note("Nhấn đúp (Double-click) vào dòng để chỉnh sửa toàn bộ thông tin bệnh nhân."))));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadGrid(dgvDpvBN, DpvBenhNhanSql()));
        }

        private void DPV_ThemBN()
        {
            using (var f = new BenhNhanAddForm(NextAvailableBnId()))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.BENHNHAN(MABN,TENBN,PHAI,NGAYSINH,CCCD,SONHA,TENDUONG,QUANHUYEN,TINHTP,TIENSUBENH,TIENSUBENHGD,DIUNGTHUOC) VALUES('{Esc(f.MaBN)}',N'{Esc(f.TenBN)}',N'{Esc(f.Phai)}',TO_DATE('{f.NgaySinh:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.CCCD)}',N'{Esc(f.SoNha)}',N'{Esc(f.TenDuong)}',N'{Esc(f.QuanHuyen)}',N'{Esc(f.TinhTP)}',N'{Esc(f.TienSuBenh)}',N'{Esc(f.TienSuBenhGD)}',N'{Esc(f.DiUngThuoc)}')"); Ok("Đã thêm bệnh nhân!"); LoadGrid(dgvDpvBN, DpvBenhNhanSql()); } catch (Exception ex) { Err(ex.Message); }
        }

        private void DPV_LuuBN()
        {
            var dt = dgvDpvBN.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET SONHA=N'{Esc(r["SONHA"].ToString())}',TENDUONG=N'{Esc(r["TENDUONG"].ToString())}',QUANHUYEN=N'{Esc(r["QUANHUYEN"].ToString())}',TINHTP=N'{Esc(r["TINHTP"].ToString())}',TIENSUBENH=N'{Esc(r["TIENSUBENH"].ToString())}',TIENSUBENHGD=N'{Esc(r["TIENSUBENHGD"].ToString())}',DIUNGTHUOC=N'{Esc(r["DIUNGTHUOC"].ToString())}' WHERE MABN='{Esc(r["MABN"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu {n} thay đổi."); LoadGrid(dgvDpvBN, DpvBenhNhanSql()); } else Ok("Không có thay đổi nào.");
        }

        // DPV – HSBA
        private void BuildDPV_HSBA(TabPage tab, TabControl ownerTabs)
        {
            dgvDpvHSBA = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Tạo HSBA", UiTheme.PastelGreen, UiTheme.DeepBlue, 130);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            var btnCalc = QuickBtn("Đếm Dịch Vụ", Color.FromArgb(255, 200, 0), UiTheme.DeepBlue, 120);
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvDpvHSBA, DpvHsbaSql(string.IsNullOrEmpty(kw) ? null : kw)); };
            btnAdd.Click += (s, e) => DPV_ThemHSBA();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvHSBA, DpvHsbaSql());
            btnCalc.Click += (s, e) => {
                if (dgvDpvHSBA.CurrentRow == null) { Err("Chọn một HSBA."); return; }
                string maHsba = dgvDpvHSBA.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                try
                {
                    var dt = service.Query($"SELECT COUNT(*) AS TONG_DV FROM QLBV.HSBA_DV WHERE MAHSBA='{Esc(maHsba)}'");
                    string tongDv = dt.Rows[0]["TONG_DV"]?.ToString() ?? "0";
                    MessageBox.Show($"HSBA: {maHsba}\nSố dịch vụ điều trị: {tongDv}",
                        "Thống Kê Dịch Vụ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { Err(ex.Message); }
            };

            dgvDpvHSBA.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvHSBA.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string mabsCu = r.Cells["MABS"].Value?.ToString() ?? "";
                    string khoaCu = r.Cells["MAKHOA"].Value?.ToString() ?? "";

                    using (var f = new HsbaDieuPhoiForm(service, ma, khoaCu, mabsCu))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                bool doiKhoa = !string.Equals(f.MaKhoa, khoaCu, StringComparison.OrdinalIgnoreCase);
                                bool doiBs = !string.Equals(f.MaBS, mabsCu, StringComparison.OrdinalIgnoreCase);
                                if (!doiKhoa && !doiBs) { Ok("Không có thay đổi."); return; }

                                service.ExecuteNonQuery(
                                    $"UPDATE QLBV.HSBA SET MAKHOA='{Esc(f.MaKhoa)}', MABS='{Esc(f.MaBS)}' WHERE MAHSBA='{Esc(ma)}'");
                                Ok("Đã cập nhật khoa và bác sĩ phụ trách.");
                                LoadGrid(dgvDpvHSBA, DpvHsbaSql());
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvHSBA, Toolbar(txtS, btnS, btnAdd, btnRe, Note("Nhấn đúp → điều phối Khoa/Bác sĩ. + Tạo HSBA: mã tự sinh, chọn hoặc gõ MABN/khoa/BS."))));
        }

        private void DPV_ThemHSBA()
        {
            using (var f = new HsbaAddForm(service, NextAvailableHsbaId()))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA(MAHSBA,MABN,NGAY,MABS,MAKHOA) VALUES('{Esc(f.MaHSBA)}','{Esc(f.MaBN)}',TO_DATE('{f.Ngay:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaBS)}','{Esc(f.MaKhoa)}')"); Ok("Đã tạo HSBA! Y bác sĩ sẽ điền chẩn đoán/kết luận sau."); LoadGrid(dgvDpvHSBA, DpvHsbaSql()); } catch (Exception ex) { Err(ex.Message); }
        }

        private void DPV_LuuHSBA()
        {
            var dt = dgvDpvHSBA.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET MABS='{Esc(r["MABS"].ToString())}',MAKHOA='{Esc(r["MAKHOA"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu điều phối {n} HSBA."); LoadGrid(dgvDpvHSBA, DpvHsbaSql()); } else Ok("Không có thay đổi nào.");
        }

        // DPV – Điều phối KTV (không nhập kết quả)
        private void BuildDPV_DV(TabPage tab, TabControl ownerTabs)
        {
            dgvDpvDV = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);

            var btnAdd = QuickBtn("+ Thêm Dịch Vụ", UiTheme.PastelGreen, UiTheme.DeepBlue, 140);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvDpvDV, DpvHsbaDvSql(string.IsNullOrEmpty(kw) ? null : kw)); };
            btnAdd.Click += (s, e) => DPV_ThemDV();
            btnRe.Click += (s, e) => LoadGrid(dgvDpvDV, DpvHsbaDvSql());

            dgvDpvDV.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvDpvDV.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string loaiDv = r.Cells["LOAIDV"].Value?.ToString();
                    string ngayDv = Convert.ToDateTime(r.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy");


                    using (var f = new DpvAssignKtvForm(service, loaiDv, r.Cells["MAKTV"].Value?.ToString()))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.HSBA_DV SET MAKTV='{Esc(f.MaKTV)}' WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(loaiDv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')");
                                Ok("Điều phối KTV thành công!");
                                LoadGrid(dgvDpvDV, DpvHsbaDvSql());
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvDpvDV, Toolbar(txtS, btnS, btnAdd, btnRe, Note("Nhấn đúp (Double-click) để phân công / thay đổi Mã KTV phụ trách."))));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadGrid(dgvDpvDV, DpvHsbaDvSql()));
        }

        private void DPV_ThemDV()
        {
            using (var f = new HsbaDvAddForm(isDoctor: false, service: service))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA_DV(MAHSBA,LOAIDV,NGAYDV,MAKTV,KETQUA) VALUES('{Esc(f.MaHSBA)}',N'{Esc(f.LoaiDV)}',TO_DATE('{f.NgayDV:dd/MM/yyyy}','DD/MM/YYYY'),'{Esc(f.MaKTV)}',NULL)"); Ok("Đã điều phối KTV!"); LoadGrid(dgvDpvDV, DpvHsbaDvSql()); } catch (Exception ex) { Err(ex.Message); }
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
            var tTB = MakeTab("Thông Báo");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo);
            BuildBs_HSBA(tHSBA, tabs); BuildBs_BN(tBN, tabs); BuildBs_DV(tDV, tabs); BuildBs_DT(tDT, tabs);
            BuildThongBaoTab(tTB, tabs, ThongBaoSendMode.ViewOnly);
            BuildAuditDemoTab(tDemo);
            tabs.TabPages.AddRange(new[] { tInfo, tHSBA, tBN, tDV, tDT, tTB, tDemo });
            parent.Controls.Add(tabs);
        }

        // Bác sĩ – HSBA (không thêm, chỉ xóa + lưu CHANDOAN/DIEUTRI/KETLUAN)
        private void BuildBs_HSBA(TabPage tab, TabControl ownerTabs)
        {
            dgvBsHSBA = MakeGrid(true); // Cấm gõ trực tiếp lên lưới

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvBsHSBA, BsHsbaSql(string.IsNullOrEmpty(kw) ? null : kw)); };
            btnRe.Click += (s, e) => LoadGrid(dgvBsHSBA, BsHsbaSql());

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
                                LoadGrid(dgvBsHSBA, BsHsbaSql());
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsHSBA, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng hồ sơ để cập nhật thông tin."))));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadGrid(dgvBsHSBA, BsHsbaSql()));
        }


        private void Bs_LuuHSBA()
        {
            var dt = dgvBsHSBA.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET CHANDOAN=N'{Esc(r["CHANDOAN"].ToString())}',DIEUTRI=N'{Esc(r["DIEUTRI"].ToString())}',KETLUAN=N'{Esc(r["KETLUAN"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu {n} thay đổi."); LoadGrid(dgvBsHSBA, BsHsbaSql()); } else Ok("Không có thay đổi nào.");
        }

        // Bác sĩ – Bệnh Nhân
        private void BuildBs_BN(TabPage tab, TabControl ownerTabs)
        {
            dgvBsBN = MakeGrid(true); // Read-only

            var txtS = SearchBox("Tìm mã bệnh nhân...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã bệnh nhân..."); LoadGrid(dgvBsBN, BsBenhNhanSql(string.IsNullOrEmpty(kw) ? null : kw)); };
            btnRe.Click += (s, e) => LoadGrid(dgvBsBN, BsBenhNhanSql());

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
                                LoadGrid(dgvBsBN, BsBenhNhanSql());
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsBN, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng bệnh nhân để cập nhật tiền sử/dị ứng."))));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadGrid(dgvBsBN, BsBenhNhanSql()));
        }

        private void Bs_LuuBN()
        {
            var dt = dgvBsBN.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET TIENSUBENH=N'{Esc(r["TIENSUBENH"].ToString())}',TIENSUBENHGD=N'{Esc(r["TIENSUBENHGD"].ToString())}',DIUNGTHUOC=N'{Esc(r["DIUNGTHUOC"].ToString())}' WHERE MABN='{Esc(r["MABN"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã cập nhật {n} bệnh nhân."); LoadGrid(dgvBsBN, BsBenhNhanSql()); } else Ok("Không có thay đổi nào.");
        }

        // Bác sĩ – HSBA_DV
        private void BuildBs_DV(TabPage tab, TabControl ownerTabs)
        {
            dgvBsDV = MakeGrid(true);

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadBsHsbaDvGrid(dgvBsDV, string.IsNullOrEmpty(kw) ? null : kw); };

            var btnAdd = QuickBtn("+ Thêm Dịch Vụ", UiTheme.PastelGreen, UiTheme.DeepBlue);
            var btnDel = QuickBtn("Xóa Dòng", Color.FromArgb(206, 17, 38), UiTheme.WhiteText);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            btnAdd.Click += (s, e) => Bs_ThemDV(); btnDel.Click += (s, e) => Bs_XoaDV(); btnRe.Click += (s, e) => LoadBsHsbaDvGrid(dgvBsDV);

            tab.Controls.Add(Wrap(dgvBsDV, Toolbar(txtS, btnS, btnAdd, btnDel, btnRe)));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadBsHsbaDvGrid(dgvBsDV));
        }

        // BS chỉ định dịch vụ — MAKTV để NULL, DPV phân công KTV sau
        private void Bs_ThemDV()
        {
            using (var f = new HsbaDvAddForm(true, service))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        service.ExecuteNonQuery($"INSERT INTO QLBV.HSBA_DV(MAHSBA,LOAIDV,NGAYDV) VALUES('{Esc(f.MaHSBA)}',N'{Esc(f.LoaiDV)}',TO_DATE('{f.NgayDV:dd/MM/yyyy}','DD/MM/YYYY'))");
                        Ok("Đã chỉ định dịch vụ! Điều phối viên sẽ phân công KTV sau.");
                        LoadBsHsbaDvGrid(dgvBsDV);
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }
        // ✅ [GỘPCODE-3] DELETE HSBA_DV dùng đúng PK composite (MAHSBA, LOAIDV, NGAYDV)
        //    Nguồn: schema_phanhe2.sql — CONSTRAINT PK_HSBA_DV PRIMARY KEY (MAHSBA, LOAIDV, NGAYDV)
        //    Trước: WHERE chỉ có MAHSBA + LOAIDV → xóa nhầm nhiều dòng cùng dịch vụ.
        //    Sau: thêm AND NGAYDV=TO_DATE(...) → xóa đúng 1 dòng theo PK đầy đủ.
        private void Bs_XoaDV() { if (dgvBsDV.CurrentRow == null) { Err("Chọn dòng."); return; } string ma = dgvBsDV.CurrentRow.Cells["MAHSBA"].Value?.ToString(); string dv = dgvBsDV.CurrentRow.Cells["LOAIDV"].Value?.ToString(); string ngayDv = dgvBsDV.CurrentRow.Cells["NGAYDV"].Value != null ? Convert.ToDateTime(dgvBsDV.CurrentRow.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy") : ""; if (MessageBox.Show($"Xóa DV '{dv}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) try { service.ExecuteNonQuery($"DELETE FROM QLBV.HSBA_DV WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(dv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')"); Ok("Đã xóa."); LoadBsHsbaDvGrid(dgvBsDV); } catch (Exception ex) { Err(ex.Message); } }

        // Bác sĩ – Đơn Thuốc  (thêm + xóa + sửa TENTHUOC/LIEUDUNG, ghi vết qua trigger)
        private void BuildBs_DT(TabPage tab, TabControl ownerTabs)
        {
            dgvBsDT = MakeGrid(true); // Read-only

            var txtS = SearchBox("Tìm mã HSBA...", 200);
            var btnS = QuickBtn("Tìm", UiTheme.DeepBlue, UiTheme.WhiteText, 80);
            var btnAdd = QuickBtn("+ Thêm Đơn Thuốc", UiTheme.PastelGreen, UiTheme.DeepBlue, 155);
            var btnDel = QuickBtn("Xóa Dòng", Color.FromArgb(206, 17, 38), UiTheme.WhiteText);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadBsDonThuocGrid(dgvBsDT, string.IsNullOrEmpty(kw) ? null : kw); };
            btnAdd.Click += (s, e) => Bs_ThemDT();
            btnDel.Click += (s, e) => Bs_XoaDT();
            btnRe.Click += (s, e) => LoadBsDonThuocGrid(dgvBsDT);

            dgvBsDT.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0)
                {
                    var r = dgvBsDT.Rows[e.RowIndex];
                    string ma = r.Cells["MAHSBA"].Value?.ToString();
                    string tenThuocCu = r.Cells["TENTHUOC"].Value?.ToString();

                    using (var f = new EditDonThuocForm(service, ma, Convert.ToDateTime(r.Cells["NGAYDT"].Value), tenThuocCu, r.Cells["LIEUDUNG"].Value?.ToString()))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery(
                                    $"UPDATE QLBV.DONTHUOC SET MAHSBA='{Esc(f.MaHSBA)}', " +
                                    $"NGAYDT=TO_DATE('{f.NgayDT:dd/MM/yyyy}','DD/MM/YYYY'), " +
                                    $"TENTHUOC=N'{Esc(f.TenThuoc)}', LIEUDUNG=N'{Esc(f.LieuDung)}' " +
                                    $"WHERE MAHSBA='{Esc(f.OriginalMaHSBA)}' AND NGAYDT=TO_DATE('{f.OriginalNgayDT:dd/MM/yyyy}','DD/MM/YYYY') " +
                                    $"AND TENTHUOC=N'{Esc(f.OriginalTenThuoc)}'");
                                Ok("Cập nhật đơn thuốc thành công.");
                                LoadBsDonThuocGrid(dgvBsDT);
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsDT, Toolbar(txtS, btnS, btnAdd, btnDel, btnRe, Note("Nhấn đúp để sửa thông tin đơn thuốc."))));
            RegisterTabLazyLoad(ownerTabs, tab, () => LoadBsDonThuocGrid(dgvBsDT));
        }

        private void Bs_ThemDT()
        {
            using (var f = new DonThuocAddForm(service))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        service.ExecuteNonQuery($"INSERT INTO QLBV.DONTHUOC(MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG) VALUES('{Esc(f.MaHSBA)}',TO_DATE('{f.NgayDT:dd/MM/yyyy}','DD/MM/YYYY'),N'{Esc(f.TenThuoc)}',N'{Esc(f.LieuDung)}')");
                        Ok("Đã thêm đơn thuốc!"); LoadBsDonThuocGrid(dgvBsDT);
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }

        private void Bs_XoaDT()
        {
            if (dgvBsDT.CurrentRow == null) { Err("Chọn dòng."); return; }
            string ma = dgvBsDT.CurrentRow.Cells["MAHSBA"].Value?.ToString();
            string tn = dgvBsDT.CurrentRow.Cells["TENTHUOC"].Value?.ToString();
            if (MessageBox.Show($"Xóa thuốc '{tn}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                try { service.ExecuteNonQuery($"DELETE FROM QLBV.DONTHUOC WHERE MAHSBA='{Esc(ma)}' AND TENTHUOC=N'{Esc(tn)}'"); Ok("Đã xóa."); LoadBsDonThuocGrid(dgvBsDT); } catch (Exception ex) { Err(ex.Message); }
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
            if (n > 0) { Ok($"Đã lưu {n} thay đổi."); LoadBsDonThuocGrid(dgvBsDT); } else Ok("Không có thay đổi nào.");
        }

        //  CHỨC NĂNG MỞ RỘNG — UI nghiệp vụ bình thường, thao tác bị chặn → audit (09.sql)
        // ═══════════════════════════════════════════════════════════════════

        private void HandleDemoAction(string code, string policy, string category, Action action)
        {
            try
            {
                action();
                MessageBox.Show(
                    $"Thao tác đã gửi nhưng có thể không có hiệu lực hoặc bị từ chối.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                string friendly = MapAuditDemoError(ex, category);
                MessageBox.Show(
                    friendly,
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
                    Text = "Chức năng mở rộng dành cho tài khoản nghiệp vụ.\n\n" +
                           "Vui lòng đăng nhập bằng tài khoản điều phối viên, bác sĩ hoặc kỹ thuật viên.",
                    Font = new Font("Segoe UI", 10F), ForeColor = UiTheme.DeepBlue
                });
                return;
            }

            var subTabs = new TabControl { Dock = DockStyle.Fill };
            switch (userRole)
            {
                case UserRole.DPV: BuildDemo_DpvHsba(subTabs); BuildDemo_DpvLichSu(subTabs); break;
                case UserRole.BACSI: BuildDemo_BsChiPhi(subTabs); BuildDemo_BsNhanVien(subTabs); BuildDemo_BsHsbaKhac(subTabs); break;
                case UserRole.KTV: BuildDemo_KtvDichVu(subTabs); break;
                case UserRole.BN: break;
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
            var btnDel = QuickBtn("Sửa Chẩn Đoán", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 130);

            void Reload(string kw = "")
            {
                LoadGrid(grid, DpvHsbaSql(string.IsNullOrEmpty(kw) ? null : kw));
            }

            btnS.Click += (s, e) => Reload(Val(txtS, "Tìm mã HSBA..."));
            btnRe.Click += (s, e) => Reload();
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn một hồ sơ bệnh án."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                if (MessageBox.Show($"Cập nhật chẩn đoán trái phép cho '{ma}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("09-DPV-02", "AuditIllegalUpdateHSBA", "Vượt quyền UPDATE HSBA",
                    () => service.ExecuteNonQuery($"UPDATE QLBV.HSBA SET CHANDOAN=N'Demo trai phep' WHERE MAHSBA='{Esc(ma)}'"));
            };

            page.Controls.Add(Wrap(grid, Toolbar(txtS, btnS, btnRe, btnDel,
                Note("DPV chỉ được cập nhật MABS/MAKHOA — sửa chẩn đoán sẽ bị từ chối."))));
            parent.TabPages.Add(page);
            Reload();
        }


        private void BuildDemo_DpvLichSu(TabControl parent)
        {
            var page = MakeTab("Lịch Sử Điều Trị");
            var grid = MakeGrid(true);

            // ComboBox tìm kiếm và lọc BN
            var cboBenhNhan = new ComboBox
            {
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            var btnTraCuu = QuickBtn("Tra Cứu", UiTheme.PastelGreen, UiTheme.DeepBlue, 100);

            // Tải danh sách Bệnh nhân vào Dropdown
            try
            {
                var dtBN = service.Query("SELECT MABN, MABN || ' - ' || TENBN AS INFO FROM QLBV.BENHNHAN");
                cboBenhNhan.DataSource = dtBN;
                cboBenhNhan.DisplayMember = "INFO";
                cboBenhNhan.ValueMember = "MABN";
            }
            catch { }

            btnTraCuu.Click += (s, e) =>
            {
                if (cboBenhNhan.SelectedValue == null) { Err("Vui lòng chọn một bệnh nhân."); return; }
                string maBN = cboBenhNhan.SelectedValue.ToString();

                try
                {
                    // Lấy REFCURSOR từ Procedure và đẩy thẳng về C# qua DBMS_SQL (Bypass lỗi thiếu quyền SELECT trên DONTHUOC)
                    string sqlRunSP = $@"
                        DECLARE 
                            v_cur SYS_REFCURSOR; 
                        BEGIN 
                            QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN('{Esc(maBN)}', v_cur); 
                            DBMS_SQL.RETURN_RESULT(v_cur); 
                        END;";

                    var dtResult = service.Query(sqlRunSP);
                    grid.DataSource = dtResult;
                    UiTheme.StyleGrid(grid);

                    // --- ẨN CỘT THỪA & ĐỔI TÊN HEADER CHO ĐẸP DÁNG ---
                    string[] hideCols = { "MABN", "TENBN", "PHAI", "NGAYSINH", "TIENSUBENH", "TIENSUBENHGD", "DIUNGTHUOC", "DIEUTRI", "KETLUAN", "MA_BACSI", "KETQUA_DV", "LIEUDUNG", "TEN_KTV", "NGAYDT" };
                    foreach (var col in hideCols)
                        if (grid.Columns.Contains(col)) grid.Columns[col].Visible = false;

                    if (grid.Columns.Contains("MAHSBA")) grid.Columns["MAHSBA"].HeaderText = "Mã HSBA";
                    if (grid.Columns.Contains("NGAY_KHAM")) grid.Columns["NGAY_KHAM"].HeaderText = "Ngày Khám";
                    if (grid.Columns.Contains("CHANDOAN")) grid.Columns["CHANDOAN"].HeaderText = "Chẩn Đoán";
                    if (grid.Columns.Contains("TENKHOA")) grid.Columns["TENKHOA"].HeaderText = "Khoa";
                    if (grid.Columns.Contains("TEN_BACSI")) grid.Columns["TEN_BACSI"].HeaderText = "Bác Sĩ";
                    if (grid.Columns.Contains("LOAIDV")) grid.Columns["LOAIDV"].HeaderText = "Dịch Vụ";
                    if (grid.Columns.Contains("NGAYDV")) grid.Columns["NGAYDV"].HeaderText = "Ngày DV";
                    if (grid.Columns.Contains("TENTHUOC")) grid.Columns["TENTHUOC"].HeaderText = "Thuốc";

                    if (dtResult.Rows.Count > 0)
                        Ok($"Tra cứu thành công!\n👉 Nhấn đúp (double-click) vào dòng bất kỳ trên lưới để xem thông tin chi tiết.");
                    else
                        MessageBox.Show("Bệnh nhân này chưa có lịch sử điều trị nào.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    HandleDemoAction("DPV-LichSu", "AuditDPVXemLichSuBN_Fail", "Truy xuất lịch sử", () => { throw ex; });
                }
            };

            grid.CellDoubleClick += (senderGrid, ev) =>
            {
                if (ev.RowIndex < 0) return;
                var r = grid.Rows[ev.RowIndex];

                string Get(string col)
                {
                    if (!grid.Columns.Contains(col)) return "Không có";
                    var val = r.Cells[col].Value;
                    if (val == null || val == DBNull.Value) return "Không có";

                    if (val is DateTime d) return d.ToString("dd/MM/yyyy");

                    string s = val.ToString().Trim();
                    if (s.EndsWith(" 12:00:00 AM")) s = s.Replace(" 12:00:00 AM", "");
                    if (s.EndsWith(" 12:00:00 SA")) s = s.Replace(" 12:00:00 SA", "");

                    return string.IsNullOrWhiteSpace(s) ? "Không có" : s;
                }

                // 1. Tạo cửa sổ Form mới
                var f = new Form
                {
                    Text = $"Chi Tiết Lịch Sử Điều Trị - {Get("TENBN")}",
                    Size = new Size(550, 700),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    BackColor = UiTheme.LightCyan,
                    Font = UiTheme.BodyFont
                };

                // 2. Tạo Layout chuẩn của hệ thống (2 cột)
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    Padding = new Padding(15, 5, 25, 15),
                    BackColor = UiTheme.JordyBlue,
                    AutoScroll = true
                };
                layout.ColumnStyles.Clear();
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                int row = 0;

                void AddHeader(string text)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var lbl = new Label { Text = text, Font = UiTheme.HeaderFont, ForeColor = Color.DarkRed, AutoSize = true, Margin = new Padding(0, 15, 0, 5) };
                    layout.Controls.Add(lbl, 0, row); layout.SetColumnSpan(lbl, 2); row++;
                }

                void AddRow(string label, string val)
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var lbl = new Label { Text = label, Font = new Font(UiTheme.BodyFont, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(3, 7, 3, 3) };

                    var txt = new TextBox { Text = val, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, Multiline = true };
                    int lines = val.Split('\n').Length + (val.Length / 45);
                    txt.Height = Math.Max(28, (lines + 1) * 22);

                    layout.Controls.Add(lbl, 0, row); layout.Controls.Add(txt, 1, row); row++;
                }

                // 3. Đổ dữ liệu vào Form
                AddHeader("👤 THÔNG TIN BỆNH NHÂN");
                AddRow("Họ tên:", Get("TENBN"));
                AddRow("Giới tính:", Get("PHAI"));
                AddRow("Ngày sinh:", Get("NGAYSINH"));
                AddRow("Tiền sử bệnh:", Get("TIENSUBENH"));
                AddRow("Dị ứng thuốc:", Get("DIUNGTHUOC"));

                AddHeader($"🏥 LÂM SÀNG (Mã HSBA: {Get("MAHSBA")})");
                AddRow("Ngày khám:", Get("NGAY_KHAM"));
                AddRow("Phụ trách:", $"BS. {Get("TEN_BACSI")} ({Get("TENKHOA")})");
                AddRow("Chẩn đoán:", Get("CHANDOAN"));
                AddRow("Điều trị:", Get("DIEUTRI"));
                AddRow("Kết luận:", Get("KETLUAN"));

                AddHeader("🔬 DỊCH VỤ");
                AddRow("Dịch vụ:", $"{Get("LOAIDV")} (Ngày: {Get("NGAYDV")})");
                AddRow("KTV thực hiện:", Get("TEN_KTV"));
                AddRow("Kết quả:", Get("KETQUA_DV"));

                AddHeader("💊 ĐƠN THUỐC");
                AddRow("Thuốc:", $"{Get("TENTHUOC")} (Kê ngày: {Get("NGAYDT")})");
                AddRow("Liều dùng:", Get("LIEUDUNG"));

                // 4. Nút Đóng Form
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
                var pnlBtn = new Panel { Dock = DockStyle.Fill };
                var btnClose = new Button { Text = "Đóng", Width = 120, Height = 36, BackColor = UiTheme.BrandeisBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Location = new Point(190, 10);
                btnClose.Click += (sender, args) => f.Close();
                pnlBtn.Controls.Add(btnClose);

                layout.Controls.Add(pnlBtn, 0, row); layout.SetColumnSpan(pnlBtn, 2);

                f.Controls.Add(layout);
                f.ShowDialog();
            };

            page.Controls.Add(Wrap(grid, Toolbar(new Label { Text = "Chọn Bệnh Nhân:", AutoSize = true, Margin = new Padding(0, 10, 5, 0), Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue }, cboBenhNhan, btnTraCuu)));
            parent.TabPages.Add(page);
        }
        // run/09.sql + 06.sql: fn_KiemTraDiUngThuoc → AuditSucBSExecFunc
        private void BuildDemo_BsChiPhi(TabControl parent)
        {
            var page = MakeTab("Dị Ứng Thuốc");
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 100);
            var btnCalc = QuickBtn("Kiểm Tra Dị Ứng", Color.FromArgb(255, 200, 0), UiTheme.DeepBlue, 155);

            btnRe.Click += (s, e) => LoadGrid(grid, "SELECT MAHSBA, MABN, CHANDOAN FROM QLBV.HSBA WHERE MABS = USER");
            btnCalc.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn một HSBA trên lưới."); return; }
                string mabn = grid.CurrentRow.Cells["MABN"].Value?.ToString();
                if (string.IsNullOrEmpty(mabn)) { Err("Không có MABN."); return; }
                try
                {
                    var dt = service.Query($"SELECT QLBV.fn_KiemTraDiUngThuoc('{Esc(mabn)}') AS DIUNGTHUOC FROM DUAL");
                    object raw = dt.Rows.Count > 0 ? dt.Rows[0]["DIUNGTHUOC"] : null;
                    string diung = (raw == null || raw == DBNull.Value)
                        ? "Chưa khai báo"
                        : raw.ToString().Trim();
                    if (string.IsNullOrEmpty(diung)) diung = "Chưa khai báo";
                    Ok($"Mã BN: {mabn}\n\nDị ứng thuốc: {diung}");
                }
                catch (Exception ex)
                {
                    HandleDemoAction("09-BS-func", "AuditSucBSExecFunc", "EXECUTE function", () => { throw ex; });
                }
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe, btnCalc,
                Note("Chọn hồ sơ → Kiểm tra dị ứng thuốc."))));
            parent.TabPages.Add(page);
            try { LoadGrid(grid, "SELECT MAHSBA, MABN, CHANDOAN FROM QLBV.HSBA WHERE MABS = USER"); } catch { }
        }

        // run/06.sql NC9b: sp_BGD_ThongBaoOLS khi không phải BGD → AuditFailBGDTaoThongBao
        private void BuildDemo_BsBgdThongBao(TabControl parent)
        {
            var page = MakeTab("BGD Thông Báo (NC9b)");
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), BackColor = Color.White };
            var lbl = new Label
            {
                Text = "Tạo thông báo cấp Ban Giám đốc (chỉ dành cho tài khoản BGD).",
                AutoSize = true, MaximumSize = new Size(700, 0), Font = new Font("Segoe UI", 10F), ForeColor = UiTheme.DeepBlue
            };
            var txNd = new TextBox { Width = 420, Multiline = true, Height = 80, Text = "[Demo] Bác sĩ cố tạo thông báo BGD" };
            var txDd = new TextBox { Width = 420, Text = "Phòng họp A" };
            var tbl = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Location = new Point(0, 50), Padding = new Padding(0, 12, 0, 0) };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
            tbl.Controls.Add(new Label { Text = "Nội dung:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 0);
            tbl.Controls.Add(txNd, 1, 0);
            tbl.Controls.Add(new Label { Text = "Địa điểm:", AutoSize = true, Margin = new Padding(0, 8, 0, 0) }, 0, 1);
            tbl.Controls.Add(txDd, 1, 1);
            var btnRun = QuickBtn("Gọi sp_BGD_ThongBaoOLS", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 210, 40);
            btnRun.Location = new Point(120, 180);
            btnRun.Click += (s, e) =>
            {
                HandleDemoAction("NC9b", "AuditFailBGDTaoThongBao", "EXECUTE procedure",
                    () => service.ExecuteNonQuery(
                        $"BEGIN QLBV.sp_BGD_ThongBaoOLS(N'{Esc(txNd.Text)}', N'{Esc(txDd.Text)}'); END;"));
            };
            pnl.Controls.Add(btnRun);
            pnl.Controls.Add(tbl);
            pnl.Controls.Add(lbl);
            page.Controls.Add(pnl);
            parent.TabPages.Add(page);
        }

        // run/09.sql [09-BACSI-03]: fn_CheckUserVaiTro — helper kiểm tra vai trò động
        private void BuildDemo_BsVaiTro(TabControl parent)
        {
            var page = MakeTab("Kiểm Tra Vai Trò");
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), BackColor = Color.White };

            var lbl = new Label
            {
                Text = "Kiểm tra vai trò nghiệp vụ của tài khoản đang đăng nhập",
                AutoSize = true, MaximumSize = new Size(700, 0), Font = new Font("Segoe UI", 10F), ForeColor = UiTheme.DeepBlue
            };
            var cmb = new ComboBox { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(new object[] { "Điều phối viên", "Bác sĩ", "Kỹ thuật viên", "Ban Giám đốc" });
            cmb.SelectedIndex = 0;
            cmb.Location = new Point(0, 50);

            var btnCheck = QuickBtn("Kiểm Tra", UiTheme.BrandeisBlue, UiTheme.WhiteText, 140, 38);
            btnCheck.Location = new Point(330, 48);
            btnCheck.Click += (s, e) =>
            {
                string vt = cmb.SelectedItem?.ToString() ?? "";
                try
                {
                    var dt = service.Query($"SELECT QLBV.fn_CheckUserVaiTro(N'{Esc(vt)}') AS KETQUA FROM DUAL");
                    string kq = dt.Rows[0]["KETQUA"]?.ToString() ?? "";
                    Ok($"fn_CheckUserVaiTro('{vt}') = {kq}\n\nTài khoản hiện tại: {service.CurrentUser}");
                }
                catch (Exception ex) { Err(ex.Message); }
            };

            pnl.Controls.Add(btnCheck);
            pnl.Controls.Add(cmb);
            pnl.Controls.Add(lbl);
            page.Controls.Add(pnl);
            parent.TabPages.Add(page);
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
                catch (Exception ex) { HandleDemoAction("TB3-load", "AuditIllegalHSBADV", "Vượt quyền", () => { throw ex; }); }
            };
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn đơn thuốc cần xóa."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                string tn = grid.CurrentRow.Cells["TENTHUOC"].Value?.ToString();
                if (MessageBox.Show($"Xóa thuốc '{tn}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB3", "AuditIllegalHSBADV", "Vượt quyền DELETE",
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
                HandleDemoAction("TB6", "ORA-01031", "Vượt quyền SELECT",
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
                catch (Exception ex) { HandleDemoAction("TB2-load", "AuditIllegalUpdateHSBA", "Vượt quyền", () => { throw ex; }); }
            };
            btnDel.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) { Err("Chọn hồ sơ cần xóa."); return; }
                string ma = grid.CurrentRow.Cells["MAHSBA"].Value?.ToString();
                if (MessageBox.Show($"Xóa hồ sơ '{ma}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                HandleDemoAction("TB2", "AuditIllegalUpdateHSBA", "Vượt quyền DELETE",
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
                catch (Exception ex) { HandleDemoAction("TB5-load", "AuditIllegalHSBADV", "Vượt quyền", () => { throw ex; }); }
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
                    HandleDemoAction("TB5", "AuditIllegalHSBADV", "Vượt quyền UPDATE",
                        () => service.ExecuteNonQuery(
                            $"UPDATE QLBV.HSBA_DV SET KETQUA=N'{Esc(f.NewValues["KETQUA"])}' " +
                            $"WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(dv)}' AND NGAYDV=TO_DATE('{ngay}','DD/MM/YYYY')"));
                }
            };

            page.Controls.Add(Wrap(grid, Toolbar(btnRe,
                Note("Nhấp đúp vào dòng để cập nhật kết quả dịch vụ."))));
            parent.TabPages.Add(page);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  KỸ THUẬT VIÊN
        // ═══════════════════════════════════════════════════════════════════

        private DataGridView dgvKtvDV;

        private void BuildKTVInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân"); 
            var tDV = MakeTab("Dịch Vụ Được Giao");
            var tTB = MakeTab("Thông Báo");
            var tDemo = MakeTab("Chức Năng Mở Rộng");
            BuildNV_Info(tInfo);
            BuildKTV_DV(tDV); BuildThongBaoTab(tTB, tabs, ThongBaoSendMode.ViewOnly);
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
            if (n > 0) { Ok($"Đã lưu kết quả {n} dịch vụ."); LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.VW_KTV_XemHSBADV"); } else Ok("Không có thay đổi nào.");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BỆNH NHÂN  (TC#5)  — redesigned BG
        // ═══════════════════════════════════════════════════════════════════

        private void BuildBNInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân");
            BuildBN_Info(tInfo);
            tabs.TabPages.AddRange(new[] { tInfo});
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
        //  BAN GIÁM ĐỐC — thông tin cá nhân + gửi thông báo OLS chi nhánh
        // ═══════════════════════════════════════════════════════════════════
        private void BuildGiamDocInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tInfo = MakeTab("Thông Tin Cá Nhân");
            var tTB = MakeTab("Thông Báo");

            BuildNV_Info(tInfo);
            BuildThongBaoTab(tTB, tabs, ThongBaoSendMode.BgdProcedure);

            tabs.TabPages.AddRange(new[] { tInfo, tTB });
            parent.Controls.Add(tabs);
        }
        private void BuildAdminInterface(Panel parent)
        {
            var tabs = MakeTabs();
            var tTB     = MakeTab("Thông Báo");
            var tAudit  = MakeTab("Kiểm Toán");
            var tBackup = MakeTab("Sao Lưu");
            var tTK     = MakeTab("Quản Lý Tài Khoản");

            var tOLS = MakeTab("Phân Quyền OLS");

            BuildThongBaoTab(tTB, tabs, ThongBaoSendMode.AdminOlsPicker);
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
            var btnAll  = QuickBtn("Tất cả",                Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,                 120);
            var btnLog  = QuickBtn("Đăng nhập thất bại",    Color.FromArgb(206, 17,  38),  UiTheme.WhiteText,                165);
            var btnStd  = QuickBtn("Nghiệp vụ",            Color.FromArgb(0,  120, 180), UiTheme.WhiteText,                120);
            var btnFga  = QuickBtn("Chi tiết FGA",          Color.FromArgb(120, 80, 180),  UiTheme.WhiteText,                130);
            var btnDT   = QuickBtn("Đơn thuốc",             Color.FromArgb(30,  160, 100),  UiTheme.WhiteText,                115);
            var btnIll  = QuickBtn("Truy cập trái phép",    Color.FromArgb(180,  60,  60),  UiTheme.WhiteText,                165);

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

            // Toolbar
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
            bar.Controls.Add(Note("Nguồn: UNIFIED_AUDIT_TRAIL  |  Archive: Sao Lưu → Scheduler"));

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
                // run/09.sql Phần IV #1 — AuditSession LOGON
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                                         AS ""NGƯỜI DÙNG"",
                        ACTION_NAME                                        AS ""HÀNH ĐỘNG"",
                        RETURN_CODE                                        AS ""MÃ LỖI"",
                        OS_USERNAME                                        AS ""OS USER"",
                        USERHOST                                           AS ""HOST"",
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

        // ── §3.4.2: Standard Audit — khớp run/06.sql §3.2 + 09.sql query
        private void LoadStandardAudit(DataGridView grid)
        {
            try
            {
                string sql = $@"
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
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN ({AuditStandardPolicyInList})
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký nghiệp vụ:\n" + ex.Message);
            }
        }

        // ── §3.4.4: AuditIllegalUpdateHSBA + AuditIllegalHSBADV
        private void LoadIllegalAudit(DataGridView grid)
        {
            try
            {
                string sql = $@"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        RETURN_CODE               AS ""MÃ KQ"",
                        UNIFIED_AUDIT_POLICIES    AS ""POLICY"",
                        SQL_TEXT                  AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN ({AuditIllegalPolicyInList})
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

        // ── §3.4.3: FGA policies (run/06.sql §3.3.a/b)
        private void LoadFgaAudit(DataGridView grid)
        {
            try
            {
                // run/09.sql Phần IV #3 — FGA AuditSuaDonThuoc, AuditBSUpdateHSBA_HopPhap
                string sql = $@"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        TO_CHAR(DBUSERNAME)         AS ""NGƯỜI DÙNG"",
                        TO_CHAR(FGA_POLICY_NAME)    AS ""FGA POLICY"",
                        TO_CHAR(OBJECT_NAME)        AS ""ĐỐI TƯỢNG"",
                        TO_CHAR(RETURN_CODE)        AS ""MÃ KQ"",
                        TO_CHAR(SQL_TEXT)           AS ""CÂU SQL""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(FGA_POLICY_NAME) IN ({AuditFgaPolicyInList})

                    UNION ALL

                    SELECT
                        TO_CHAR(TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS'),
                        TO_CHAR(DB_USER),
                        TO_CHAR(UPPER(POLICY_NAME)),
                        TO_CHAR(OBJECT_NAME),
                        '0', 
                        TO_CHAR(SQL_TEXT)
                    FROM DBA_FGA_AUDIT_TRAIL
                    WHERE OBJECT_SCHEMA = 'QLBV'
                    AND UPPER(POLICY_NAME) IN ({AuditFgaPolicyInList})

                    ORDER BY 1 DESC 
                    FETCH FIRST 200 ROWS ONLY";
                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception ex)
            {
                grid.DataSource = null;
                Err("Không thể tải nhật ký chi tiết (FGA):\n" + ex.Message);
            }
        }

        // ── §3.3.a: FGA AuditSuaDonThuoc + Standard AuditSucBSUpdateDT (run/06.sql)
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
                        UPPER(FGA_POLICY_NAME) = 'AUDITSUADONTHUOC'
                        OR UPPER(UNIFIED_AUDIT_POLICIES) = 'AUDITSUCBSUPDATEDT'
                        OR (OBJECT_SCHEMA = 'QLBV' AND OBJECT_NAME = 'DONTHUOC'
                            AND ACTION_NAME IN ('INSERT','UPDATE'))
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
            var refreshPump = BuildBackup_DataPump(tPump, subTabs);
            BuildBackup_CmdPanel(tCmd);
            var refreshHist = BuildBackup_HistoryPanel(tHist, subTabs);
            BuildBackup_Flashback(tFlashQ);
            BuildBackup_FlashbackTable(tFlashT);
            BuildBackup_Scheduler(tSched);
            subTabs.TabPages.AddRange(new[] { tPump, tHist, tFlashQ, tFlashT, tSched, tCmd });
            tab.Controls.Add(subTabs);
            tab.Enter += (s, e) =>
            {
                if (subTabs.SelectedTab == tPump) refreshPump();
                else if (subTabs.SelectedTab == tHist) refreshHist();
            };
        }

        // ── 08.sql [08-QLBV-01]: Data Pump + BACKUP_HISTORY tracking
        private Action BuildBackup_DataPump(TabPage tab, TabControl subTabs)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Oracle Data Pump — Sao Lưu & Phục Hồi", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };

            var btnBackupSchema  = QuickBtn("Backup Schema QLBV",     UiTheme.BrandeisBlue,       UiTheme.WhiteText, 192, 38);
            var btnBackupTables  = QuickBtn("Backup Bảng Quan Trọng", Color.FromArgb(0, 140, 200), UiTheme.WhiteText, 200, 38);
            var btnRestoreSchema = QuickBtn("Restore Schema (impdp)", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 188, 38);
            var btnRestoreTable  = QuickBtn("Restore 1 Bảng (impdp)", Color.FromArgb(180, 60, 40), UiTheme.WhiteText, 178, 38);
            var btnCheckDir      = QuickBtn("Kiểm Tra BACKUP_DIR",    Color.FromArgb(80, 80, 120), UiTheme.WhiteText, 161, 38);
            var btnTableList     = QuickBtn("Liệt Kê Bảng NV",      Color.FromArgb(100, 120, 180), UiTheme.WhiteText, 155, 38);
            var btnRowCount      = QuickBtn("Đếm Số Dòng Bảng",      Color.FromArgb(50, 150, 80), UiTheme.WhiteText, 154, 38);

            var flowBtn = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 138, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White };
            foreach (var b in new Control[] { btnBackupSchema, btnBackupTables, btnRestoreSchema, btnRestoreTable, btnCheckDir, btnTableList, btnRowCount })
                flowBtn.Controls.Add(b);

            var lblHist  = new Label { Text = "Lịch Sử Sao Lưu — QLBV.BACKUP_HISTORY", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 };
            var btnReH   = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 78, 26);
            var barHist  = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barHist.Controls.Add(btnReH);
            var gridHist = MakeGrid(true);
            gridHist.Dock = DockStyle.Fill;
            var txtLog   = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10F), ScrollBars = ScrollBars.Vertical, Text = "C:\\> Sẵn sàng. Bấm nút để thực hiện.\r\n" };

            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.White,
                SplitterWidth = 6
            };
            void BalancePumpSplit() => SafeBalanceHorizontalSplit(splitMain, 0.62, 120, 160);
            splitMain.Resize += (s, e) => BalancePumpSplit();
            tab.Enter += (s, e) => BalancePumpSplit();

            var pnlGrid = new Panel { Dock = DockStyle.Fill };
            pnlGrid.Controls.Add(gridHist);
            pnlGrid.Controls.Add(barHist);
            pnlGrid.Controls.Add(lblHist);
            splitMain.Panel1.Controls.Add(pnlGrid);
            splitMain.Panel2.Controls.Add(txtLog);

            void RefreshHist() => LoadBackupHistoryGrid(gridHist);

            btnReH.Click += (s, e) => RefreshHist();
            RegisterTabRefresh(subTabs, tab, RefreshHist);

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
                        txtLog.AppendText("  (Chưa có BACKUP_DIR — cần tạo Oracle directory BACKUP_DIR bằng quyền SYSDBA)\r\n");
                    txtLog.AppendText("SQL> SELECT grantee, privilege FROM dba_tab_privs WHERE table_name = 'BACKUP_DIR';\r\n");
                    var dtG = service.Query("SELECT GRANTEE, PRIVILEGE FROM DBA_TAB_PRIVS WHERE TABLE_NAME = 'BACKUP_DIR' ORDER BY GRANTEE");
                    foreach (DataRow r in dtG.Rows) txtLog.AppendText($"  {r[0],-16} {r[1]}\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            // Liệt kê bảng nghiệp vụ (WinForms helper)
            btnTableList.Click += (s, e) =>
            {
                try
                {
                    var dt = service.Query(
                        "SELECT TABLE_NAME FROM USER_TABLES " +
                        "WHERE TABLE_NAME IN ('KHOA','BENHNHAN','NHANVIEN','HSBA','HSBA_DV','DONTHUOC','THONGBAO') " +
                        "ORDER BY TABLE_NAME");
                    txtLog.AppendText("\r\nSQL> Danh sách bảng nghiệp vụ:\r\n  TABLE_NAME\r\n  ----------\r\n");
                    foreach (DataRow r in dt.Rows) txtLog.AppendText($"  {r["TABLE_NAME"]}\r\n");
                    if (dt.Rows.Count == 0) txtLog.AppendText("  (Không tìm thấy bảng — kiểm tra schema QLBV)\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            // Đếm số dòng 4 bảng ưu tiên backup
            btnRowCount.Click += (s, e) =>
            {
                try
                {
                    var dt = service.Query(
                        "SELECT 'BENHNHAN' AS TABLE_NAME, COUNT(*) AS ROW_COUNT FROM QLBV.BENHNHAN " +
                        "UNION ALL SELECT 'HSBA',     COUNT(*) FROM QLBV.HSBA " +
                        "UNION ALL SELECT 'HSBA_DV',  COUNT(*) FROM QLBV.HSBA_DV " +
                        "UNION ALL SELECT 'DONTHUOC', COUNT(*) FROM QLBV.DONTHUOC");
                    txtLog.AppendText("\r\nSQL> Số dòng bảng ưu tiên backup:\r\n  TABLE_NAME    ROW_COUNT\r\n  ------------- ----------\r\n");
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
                    try
                    {
                        RecordBackupHistory("SCHEMA_QLBV", df, code, "Backup schema QLBV qua expdp (WinForms).", txtLog);
                        RefreshHist();
                    }
                    catch (Exception exHist)
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Không ghi được BACKUP_HISTORY: {exHist.Message}\r\n");
                    }
                    if (code == 0)
                        Ok($"Export thành công. File: {df}");
                    else
                        Err($"expdp exit {code} ({MapDataPumpStatus(code)}). Đã ghi BACKUP_HISTORY — xem log phía trên.");
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
                    try
                    {
                        RecordBackupHistory("FULL", df, code, "Backup bảng quan trọng qua expdp (WinForms).", txtLog);
                        RefreshHist();
                    }
                    catch (Exception exHist)
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Không ghi được BACKUP_HISTORY: {exHist.Message}\r\n");
                    }
                    if (code == 0)
                        Ok($"Export thành công. File: {df}");
                    else
                        Err($"expdp exit {code} ({MapDataPumpStatus(code)}). Đã ghi BACKUP_HISTORY — xem log phía trên.");
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
                    var dtDump = service.Query("SELECT FILE_NAME FROM QLBV.BACKUP_HISTORY WHERE BACKUP_TYPE = 'SCHEMA_QLBV' AND STATUS IN ('SUCCESS','PARTIAL') ORDER BY BACKUP_TIME DESC FETCH FIRST 1 ROW ONLY");
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

            // 07.sql [07-CMD] case 4 — impdp một bảng (table_exists_action=replace)
            btnRestoreTable.Click += async (s, e) =>
            {
                var sel = PromptImpdpTableRestore();
                if (sel == null) return;

                string table = sel.Value.table;
                string dumpFile = sel.Value.dumpFile;
                string tableShort = table.Contains(".") ? table.Substring(table.LastIndexOf('.') + 1) : table;

                if (MessageBox.Show(
                        $"CẢNH BÁO: Sẽ ghi đè dữ liệu bảng {table.ToUpper()} từ file:\n{dumpFile}\n\n" +
                        "Tham số: table_exists_action=replace\n\nBạn có chắc chắn?",
                        "Phục Hồi Một Bảng",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                SetDataPumpButtonsEnabled(flowBtn, false);
                try
                {
                    string conn = QuoteCliArg(service.GetDataPumpConnectString());
                    string logName = $"qlbv_table_{tableShort}_import.log";
                    string args = $"{conn} tables={table} directory=backup_dir dumpfile={dumpFile} logfile={logName} table_exists_action=replace";
                    int code = await RunDataPumpCliAsync("impdp", args, txtLog);
                    if (code == 0)
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Import bảng {table} thành công (exit 0). File: {dumpFile}\r\n");
                        try
                        {
                            service.ExecuteNonQuery(
                                $"INSERT INTO QLBV.RESTORE_HISTORY (RESTORE_TYPE, FILE_SRC, STATUS) " +
                                $"VALUES ('IMPDP_TABLE', '{Esc(dumpFile)}|{Esc(table)}', 'SUCCESS')");
                            service.ExecuteNonQuery("COMMIT");
                            txtLog.AppendText("   => Đã ghi nhận vào QLBV.RESTORE_HISTORY.\r\n");
                        }
                        catch (Exception exHist)
                        {
                            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Không ghi được RESTORE_HISTORY: {exHist.Message}\r\n");
                        }
                        Ok($"Đã phục hồi bảng {table} từ {dumpFile}");
                    }
                    else
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Import bảng {table} thất bại (exit {code}).\r\n");
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

            pnl.Controls.Add(splitMain);
            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
            return RefreshHist;
        }

        // ── 07.sql [07-CMD]: Tham chiếu lệnh expdp/impdp — sao chép hoặc chạy CMD thủ công (fallback)
        private void BuildBackup_CmdPanel(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Lệnh expdp / impdp — Tham Chiếu CMD", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };
            var lblDesc  = new Label
            {
                Text = "Tab tham chiếu / fallback: nghiệp vụ chính dùng tab Data Pump (1-click). Ở đây chỉ sao chép hoặc chạy lệnh CMD thủ công khi cần.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(90, 90, 110),
                Dock = DockStyle.Top,
                Height = 36,
                AutoSize = false
            };

            SplitDataPumpConnect(out _, out string sessionHost);

            const int cfgRowH = 34;
            var tblCfg = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = cfgRowH * 4 + 14,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(0, 6, 0, 8),
                BackColor = Color.White
            };
            tblCfg.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            tblCfg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
                tblCfg.RowStyles.Add(new RowStyle(SizeType.Absolute, cfgRowH));

            var cmbCmd = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = Padding.Empty };
            StyleDataPumpCombo(cmbCmd, 640);
            cmbCmd.Items.AddRange(new object[] {
                "1. Backup toàn Schema QLBV (expdp schemas)",
                "2. Backup Bảng Quan Trọng (expdp tables)",
                "3. Restore toàn Schema QLBV (impdp schemas)",
                "4. Restore Một Bảng cụ thể (impdp tables)"
            });
            cmbCmd.SelectedIndex = 0;

            var cmbHost = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = Padding.Empty };
            StyleDataPumpCombo(cmbHost, 640);
            cmbHost.Items.Add(sessionHost);
            cmbHost.SelectedIndex = 0;

            var dumpRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            dumpRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dumpRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            dumpRow.RowStyles.Add(new RowStyle(SizeType.Absolute, cfgRowH - 6));
            var cmbDump = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 6, 2) };
            StyleDataPumpCombo(cmbDump, 640);
            var btnRefreshDump = QuickBtn("↻ Dump", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 80, cfgRowH - 8);
            btnRefreshDump.Dock = DockStyle.Fill;
            btnRefreshDump.Margin = new Padding(0, 2, 0, 2);
            dumpRow.Controls.Add(cmbDump, 0, 0);
            dumpRow.Controls.Add(btnRefreshDump, 1, 0);

            var cmbTbl = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = Padding.Empty };
            StyleDataPumpCombo(cmbTbl, 480);
            cmbTbl.Items.AddRange(DataPumpImportantTables);
            cmbTbl.SelectedIndex = 3;

            void AddCfgRow(int row, string label, Control value)
            {
                tblCfg.Controls.Add(new Label
                {
                    Text = label,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(0, 0, 8, 0),
                    Font = new Font("Segoe UI", 9F)
                }, 0, row);
                tblCfg.Controls.Add(value, 1, row);
            }

            AddCfgRow(0, "Loại:", cmbCmd);
            AddCfgRow(1, "Host (phiên):", cmbHost);
            AddCfgRow(2, "File dump:", dumpRow);
            AddCfgRow(3, "Bảng (#4):", cmbTbl);

            DataPumpDumpListKind CurrentDumpKind()
            {
                switch (cmbCmd.SelectedIndex)
                {
                    case 0: return DataPumpDumpListKind.ExportSchema;
                    case 1: return DataPumpDumpListKind.ExportTables;
                    case 2: return DataPumpDumpListKind.ImportSchema;
                    default: return DataPumpDumpListKind.ImportTable;
                }
            }

            void SyncFieldState()
            {
                bool tableMode = cmbCmd.SelectedIndex == 3;
                cmbTbl.Enabled = tableMode;
                btnRefreshDump.Enabled = cmbCmd.SelectedIndex >= 2;
                PopulateDataPumpDumpCombo(cmbDump, CurrentDumpKind(), cmbDump.SelectedItem?.ToString());
            }

            var txtCmd = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, BackColor = Color.FromArgb(20, 26, 32), ForeColor = Color.FromArgb(190, 235, 150), Font = new Font("Consolas", 11F), WordWrap = false, Dock = DockStyle.Fill };

            var btnCopy = QuickBtn("Sao Chép Lệnh",     UiTheme.BrandeisBlue,       UiTheme.WhiteText, 152, 40);
            var btnRun  = QuickBtn("▶ Chạy trong CMD",  Color.FromArgb(30, 160, 60), UiTheme.WhiteText, 162, 40);
            var btnManual = QuickBtn("Ghi BACKUP_HISTORY (manual)", Color.FromArgb(0, 120, 180), UiTheme.WhiteText, 220, 40);
            var flowAct = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            flowAct.Controls.Add(btnCopy);
            flowAct.Controls.Add(btnRun);
            flowAct.Controls.Add(btnManual);
            flowAct.Controls.Add(Note("Export qua CMD → bấm 'Ghi BACKUP_HISTORY'. Import nên dùng tab Data Pump (tự ghi RESTORE_HISTORY)."));

            string GetCmd()
            {
                SplitDataPumpConnect(out string cred, out string host);
                if (cmbHost.SelectedItem != null)
                    host = cmbHost.SelectedItem.ToString().Trim();
                string dump = cmbDump.SelectedItem?.ToString()?.Trim() ?? "qlbv_schema_latest.dmp";
                string tbl = cmbTbl.SelectedItem?.ToString()?.Trim() ?? "qlbv.donthuoc";
                string tablesList = string.Join(",", DataPumpImportantTables);
                switch (cmbCmd.SelectedIndex)
                {
                    case 0: return $"expdp {cred}@{host} ^\r\n    schemas=qlbv ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_schema_export.log";
                    case 1: return $"expdp {cred}@{host} ^\r\n    tables={tablesList} ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_important_tables_export.log";
                    case 2: return $"impdp {cred}@{host} ^\r\n    schemas=qlbv ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_schema_import.log ^\r\n    table_exists_action=replace";
                    case 3: return $"impdp {cred}@{host} ^\r\n    tables={tbl} ^\r\n    directory=backup_dir ^\r\n    dumpfile={dump} ^\r\n    logfile=qlbv_table_import.log ^\r\n    table_exists_action=replace";
                    default: return "";
                }
            }

            EventHandler updatePreview = (s, e) => txtCmd.Text = GetCmd();
            cmbCmd.SelectedIndexChanged += (s, e) => { SyncFieldState(); updatePreview(s, e); };
            cmbHost.SelectedIndexChanged += updatePreview;
            cmbDump.SelectedIndexChanged += updatePreview;
            cmbTbl.SelectedIndexChanged += updatePreview;
            btnRefreshDump.Click += (s, e) => { SyncFieldState(); updatePreview(s, e); };
            tab.Enter += (s, e) => { SyncFieldState(); updatePreview(s, e); };
            SyncFieldState();
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

            // 08.sql [08-QLBV-01]: ghi lịch sử backup sau expdp thủ công qua CMD
            btnManual.Click += (s, e) =>
            {
                string dump = cmbDump.SelectedItem?.ToString()?.Trim();
                if (string.IsNullOrEmpty(dump)) { Err("Chọn file dump trước."); return; }
                string btype = cmbCmd.SelectedIndex == 0 ? "SCHEMA_QLBV"
                             : cmbCmd.SelectedIndex == 1 ? "FULL" : "SCHEMA_QLBV";
                string desc = cmbCmd.SelectedIndex == 0 ? "N'Backup schema QLBV thủ công qua expdp CMD (WinForms)'"
                            : cmbCmd.SelectedIndex == 1 ? "N'Backup bảng quan trọng thủ công qua expdp CMD (WinForms)'"
                            : "N'Backup thủ công qua expdp CMD (WinForms)'";
                try
                {
                    service.ExecuteNonQuery(
                        $"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_TYPE, FILE_NAME, STATUS, DESCRIPTION) " +
                        $"VALUES ('{btype}', '{Esc(dump)}', 'SUCCESS', {desc})");
                    service.ExecuteNonQuery("COMMIT");
                    Ok($"Đã ghi vào QLBV.BACKUP_HISTORY (backup_type={btype}).");
                }
                catch (Exception ex) { Err(ex.Message); }
            };

            pnl.Controls.Add(flowAct);
            pnl.Controls.Add(txtCmd);
            pnl.Controls.Add(tblCfg);
            pnl.Controls.Add(lblDesc);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
        }

        // ── 08.sql [08-QLBV-01]: Lịch sử sao lưu + phục hồi + phân tích sự cố
        private Action BuildBackup_HistoryPanel(TabPage tab, TabControl subTabs)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.White,
                SplitterWidth = 6
            };

            void BalanceHistorySplit() => SafeBalanceHorizontalSplit(split, 0.5, 80, 80);

            split.Resize += (s, e) => BalanceHistorySplit();
            tab.Enter += (s, e) => BalanceHistorySplit();

            // PANEL TOP: BACKUP_HISTORY
            var gridB = MakeGrid(true);
            gridB.Dock = DockStyle.Fill;
            var btnRB = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 78, 26);
            var barB  = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barB.Controls.Add(btnRB);
            var lblB = new Label { Text = "QLBV.BACKUP_HISTORY — Lịch Sử Sao Lưu", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 26 };
            split.Panel1.Controls.Add(gridB);
            split.Panel1.Controls.Add(barB);
            split.Panel1.Controls.Add(lblB);

            // PANEL BOTTOM: RESTORE_HISTORY + controls
            var gridR    = MakeGrid(true);
            gridR.Dock = DockStyle.Fill;
            var btnRR    = QuickBtn("Tải Lại",               Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,  78, 26);
            var btnAdd   = QuickBtn("+ Ghi Nhận Phục Hồi",  UiTheme.BrandeisBlue,          UiTheme.WhiteText, 188, 26);
            var btnAudit = QuickBtn("Phân Tích Sự Cố", Color.FromArgb(80, 80, 120), UiTheme.WhiteText, 160, 26);
            var barR     = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 2, 0, 2), BackColor = Color.White };
            barR.Controls.AddRange(new Control[] { btnRR, btnAdd, btnAudit });
            var lblR = new Label { Text = "QLBV.RESTORE_HISTORY — Lịch Sử Phục Hồi", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 26 };
            split.Panel2.Controls.Add(gridR);
            split.Panel2.Controls.Add(barR);
            split.Panel2.Controls.Add(lblR);

            void RefreshB() => LoadBackupHistoryGrid(gridB);
            void RefreshR() => LoadRestoreHistoryGrid(gridR);
            void RefreshAll() { RefreshB(); RefreshR(); }

            btnRB.Click += (s, e) => RefreshB();
            btnRR.Click += (s, e) => RefreshR();
            RegisterTabRefresh(subTabs, tab, RefreshAll);

            // 09.sql Phần IV: query audit trail xác định sự cố trước khi restore
            btnAudit.Click += (s, e) =>
            {
                var dlg    = new Form { Text = "Phân Tích Sự Cố — Audit Trail", Width = 1100, Height = 600, StartPosition = FormStartPosition.CenterParent };
                var gAudit = MakeGrid(true);
                var cmb    = new ComboBox { Width = 480, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 4, 8, 0) };
                cmb.Items.AddRange(new object[] {
                    "Theo tên bảng (BENHNHAN/HSBA/HSBA_DV/DONTHUOC/NHANVIEN)",
                    "Theo chính sách audit — UNIFIED_AUDIT_TRAIL",
                    "AUDIT_ARCHIVE_LOG — nhật ký đã archive",
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
                            // 09.sql query theo bảng nghiệp vụ
                            sql = "SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT FROM UNIFIED_AUDIT_TRAIL WHERE OBJECT_SCHEMA = 'QLBV' AND OBJECT_NAME IN ('BENHNHAN','HSBA','HSBA_DV','DONTHUOC','NHANVIEN') ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        else if (cmb.SelectedIndex == 1)
                        {
                            sql = $@"SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT
FROM UNIFIED_AUDIT_TRAIL
WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN ({AuditStandardPolicyInList},{AuditIllegalPolicyInList})
   OR UPPER(FGA_POLICY_NAME) IN ({AuditFgaPolicyInList})
ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        else if (cmb.SelectedIndex == 2)
                        {
                            sql = @"SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE,
       UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT
FROM QLBV.AUDIT_ARCHIVE_LOG
ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
                        }
                        else
                        {
                            sql = $@"SELECT TIMESTAMP AS EVENT_TIMESTAMP, DB_USER AS DBUSERNAME, OBJECT_SCHEMA, OBJECT_NAME,
    POLICY_NAME AS FGA_POLICY_NAME, STATEMENT_TYPE AS ACTION_NAME, SQL_TEXT
FROM DBA_FGA_AUDIT_TRAIL
WHERE OBJECT_SCHEMA = 'QLBV'
  AND UPPER(POLICY_NAME) IN ({AuditFgaPolicyInList})
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

            // 08.sql [08-QLBV-01]: INSERT vào restore_history sau impdp / flashback
            btnAdd.Click += (s, e) =>
            {
                var dlg    = new Form { Text = "Ghi Nhận Kết Quả Phục Hồi", Width = 520, Height = 340, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
                var layout = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
                int rowIdx = 0;

                TextBox MkField(string lbl, string def = "")
                {
                    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    layout.Controls.Add(new Label { Text = lbl + ":", AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 10, 8, 0), Anchor = AnchorStyles.Right }, 0, rowIdx);
                    var tb = new TextBox { Text = def, Width = 310, Font = new Font("Segoe UI", 9.5F), Margin = new Padding(0, 6, 0, 0) };
                    layout.Controls.Add(tb, 1, rowIdx);
                    rowIdx++;
                    return tb;
                }

                var cmbType  = new ComboBox { Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 6, 0, 0) };
                cmbType.Items.AddRange(new object[] { "IMPDP_SCHEMA", "IMPDP_TABLE", "FLASHBACK_TABLE", "FLASHBACK_QUERY" });
                cmbType.SelectedIndex = 0;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label { Text = "Loại phục hồi:", AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 10, 8, 0), Anchor = AnchorStyles.Right }, 0, rowIdx);
                layout.Controls.Add(cmbType, 1, rowIdx);
                rowIdx++;

                var tFileSrc = MkField("Nguồn (file/bảng)", "qlbv_schema_latest.dmp");
                var tStatus  = MkField("Trạng thái", "SUCCESS");

                var btnSave   = QuickBtn("Lưu",  UiTheme.BrandeisBlue,         UiTheme.WhiteText, 100, 36);
                var btnCancel = QuickBtn("Hủy",  Color.FromArgb(200, 200, 200), UiTheme.DeepBlue,   80, 36);
                var flowF = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(16, 8, 0, 0) };
                flowF.Controls.AddRange(new Control[] { btnSave, btnCancel });

                btnSave.Click += (ss, ee) =>
                {
                    try
                    {
                        service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.RESTORE_HISTORY (RESTORE_TYPE, FILE_SRC, STATUS) " +
                            $"VALUES ('{Esc(cmbType.SelectedItem.ToString())}', '{Esc(tFileSrc.Text)}', '{Esc(tStatus.Text)}')");
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
            tab.Resize += (s, e) => BalanceHistorySplit();
            return RefreshAll;
        }

        // Flashback Query — run/05.sql §Câu 4: Khôi phục dữ liệu theo thời điểm
        private void BuildBackup_Flashback(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 0), BackColor = Color.White };

            var lblTitle = new Label
            {
                Text = "Khôi Phục Dữ Liệu Qua Flashback Query",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Dock = DockStyle.Top, Height = 38
            };
            var lblDesc = new Label
            {
                Text = "Thực hiện theo 3 bước:  Bước 1 → ghi mốc thời gian + CCCD/NGAYSINH  |  Bước 2 → giả lập hỏng CCCD  |  Bước 3 → khôi phục AS OF TIMESTAMP",
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
            var txtMabn = new TextBox { Text = "BN000001", Width = 150, Font = UiTheme.BodyFont, Margin = new Padding(0, 4, 12, 0) };
            var btnSelect = QuickBtn("Xem Dữ Liệu", Color.FromArgb(100, 120, 180), UiTheme.WhiteText, 118, 34);
            flowInput.Controls.Add(lblMabn);
            flowInput.Controls.Add(txtMabn);
            flowInput.Controls.Add(btnSelect);

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
                Text = "SQL> -- Sẵn sàng. Bấm 'Xem Dữ Liệu' bất cứ lúc nào để kiểm tra CCCD/NGAYSINH hiện tại.\r\n"
            };

            // Closure state
            string savedTimestamp = null;

            btnSelect.Click += (s, e) =>
            {
                string mabn = Esc(txtMabn.Text.Trim());
                if (string.IsNullOrEmpty(mabn)) { Err("Vui lòng nhập MABN."); return; }
                try
                {
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD, NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    var dt = service.Query(
                        $"SELECT CCCD, TO_CHAR(NGAYSINH, 'YYYY-MM-DD') AS NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    if (dt.Rows.Count == 0)
                    {
                        txtLog.AppendText("(Không tìm thấy bản ghi)\r\n");
                        return;
                    }
                    txtLog.AppendText($"CCCD          NGAYSINH\r\n");
                    txtLog.AppendText($"{dt.Rows[0]["CCCD"],-13} {dt.Rows[0]["NGAYSINH"]}\r\n");
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI]: {ex.Message}\r\n");
                }
            };

            btnStep1.Click += (s, e) =>
            {
                string mabn = Esc(txtMabn.Text.Trim());
                if (string.IsNullOrEmpty(mabn)) { Err("Vui lòng nhập MABN."); return; }
                try
                {
                    var tsRow  = service.Query("SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS TS FROM DUAL");
                    savedTimestamp = tsRow.Rows[0]["TS"].ToString();

                    var dtRow  = service.Query(
                        $"SELECT CCCD, TO_CHAR(NGAYSINH, 'YYYY-MM-DD') AS NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    string cccd = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["CCCD"]?.ToString() ?? "(null)") : "(không tìm thấy)";
                    string ngaysinh = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["NGAYSINH"]?.ToString() ?? "(null)") : "(null)";

                    if (DateTime.TryParseExact(savedTimestamp, "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out DateTime ts))
                        dtpFlash.Value = ts;

                    txtLog.AppendText($"\r\nSQL> SELECT TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS') AS FLASH_TS FROM DUAL;\r\n");
                    txtLog.AppendText($"FLASH_TS\r\n-------------------\r\n{savedTimestamp}\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD, NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    txtLog.AppendText($"CCCD       NGAYSINH\r\n---------- ----------\r\n{cccd,-11}{ngaysinh}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 1 HOÀN THÀNH]\r\n");
                    txtLog.AppendText($"   FLASH_TS (mốc khôi phục): {savedTimestamp}\r\n");
                    txtLog.AppendText($"   CCCD tại mốc này       : {cccd}\r\n");
                    txtLog.AppendText($"   NGAYSINH tại mốc này   : {ngaysinh}\r\n");
                    txtLog.AppendText($"   (Bước 1 ghi nhận dữ liệu HIỆN TẠI trong DB — không đọc từ file insert)\r\n");
                    if (cccd == "999999999999")
                    {
                        txtLog.AppendText($"\r\n   ⚠ CCCD đang là 999999999999 — có thể đã chạy Bước 2 trước đó mà chưa khôi phục.\r\n");
                        txtLog.AppendText($"   Giá trị gốc BN000001: CCCD = 970000000001. Khôi phục trước khi chạy lại:\r\n");
                        txtLog.AppendText($"   UPDATE QLBV.BENHNHAN SET CCCD = '970000000001' WHERE MABN = 'BN000001'; COMMIT;\r\n");
                        Err("CCCD hiện tại là 999999999999. Khôi phục về 970000000001 trước khi chạy lại Bước 1 → 2 → 3.");
                        return;
                    }
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
                    $"Sẽ khôi phục CCCD + NGAYSINH của [{mabn}] về trạng thái tại:\n{flashTs}\n\nXác nhận?",
                    "Xác nhận Flashback", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    // run/05.sql §Câu 4 — Bước 3: SET (CCCD, NGAYSINH) AS OF TIMESTAMP
                    string restoreSql =
                        $"UPDATE QLBV.BENHNHAN SET (CCCD, NGAYSINH) = (" +
                        $"  SELECT CCCD, NGAYSINH FROM QLBV.BENHNHAN" +
                        $"  AS OF TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS')" +
                        $"  WHERE MABN = '{mabn}'" +
                        $") WHERE MABN = '{mabn}'";

                    txtLog.AppendText($"\r\nSQL> UPDATE QLBV.BENHNHAN\r\n");
                    txtLog.AppendText($"  2  SET (CCCD, NGAYSINH) = (\r\n");
                    txtLog.AppendText($"  3    SELECT CCCD, NGAYSINH FROM QLBV.BENHNHAN\r\n");
                    txtLog.AppendText($"  4    AS OF TIMESTAMP TO_TIMESTAMP('{flashTs}','YYYY-MM-DD HH24:MI:SS')\r\n");
                    txtLog.AppendText($"  5    WHERE MABN = '{mabn}'\r\n");
                    txtLog.AppendText($"  6  ) WHERE MABN = '{mabn}';\r\n");

                    service.ExecuteNonQuery(restoreSql);
                    service.ExecuteNonQuery("COMMIT");

                    var dtRow = service.Query(
                        $"SELECT CCCD, TO_CHAR(NGAYSINH, 'YYYY-MM-DD') AS NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}'");
                    string restoredCccd = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["CCCD"]?.ToString() ?? "(null)") : "(null)";
                    string restoredNs   = dtRow.Rows.Count > 0 ? (dtRow.Rows[0]["NGAYSINH"]?.ToString() ?? "(null)") : "(null)";
                    txtLog.AppendText($"1 row updated.\r\nSQL> COMMIT;\r\nCommit complete.\r\n");
                    txtLog.AppendText($"\r\nSQL> SELECT CCCD, NGAYSINH FROM QLBV.BENHNHAN WHERE MABN = '{mabn}';\r\n");
                    txtLog.AppendText($"CCCD       NGAYSINH\r\n---------- ----------\r\n{restoredCccd,-11}{restoredNs}\r\n");
                    txtLog.AppendText($"\r\n>> [BƯỚC 3 HOÀN THÀNH] ✓ DỮ LIỆU ĐÃ ĐƯỢC KHÔI PHỤC!\r\n");
                    txtLog.AppendText($"   CCCD sau khôi phục    : {restoredCccd}\r\n");
                    txtLog.AppendText($"   NGAYSINH sau khôi phục: {restoredNs}\r\n");

                    try
                    {
                        service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.RESTORE_HISTORY (RESTORE_TYPE, FILE_SRC, STATUS) " +
                            $"VALUES ('FLASHBACK_QUERY', 'QLBV.BENHNHAN/{Esc(mabn)}@{flashTs}', 'SUCCESS')");
                        service.ExecuteNonQuery("COMMIT");
                        txtLog.AppendText("   => Đã ghi nhận vào QLBV.RESTORE_HISTORY.\r\n");
                    }
                    catch { }
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

        // ── 08.sql [08-FLASHBACK]: FLASHBACK TABLE qlbv.donthuoc TO TIMESTAMP
        private void BuildBackup_FlashbackTable(TabPage tab)
        {
            const string tbl = "QLBV.DONTHUOC";
            const string corruptLieudung = "Dữ liệu bị sửa nhầm — cần phục hồi";

            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 0), BackColor = Color.White };
            var lblTitle = new Label
            {
                Text = "Flashback Table — Phục Hồi Bảng Đơn Thuốc",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue,
                Dock = DockStyle.Top, Height = 34
            };
            var lblDesc  = new Label
            {
                Text = "Khôi phục toàn bộ trạng thái bảng QLBV.DONTHUOC về mốc thời gian trước sự cố — không cần impdp. Yêu cầu ENABLE ROW MOVEMENT.",
                Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(90, 90, 110),
                Dock = DockStyle.Top, Height = 36, AutoSize = false
            };

            var flowInput = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0), BackColor = Color.White };
            flowInput.Controls.Add(new Label { Text = "Bảng:", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Margin = new Padding(0, 6, 8, 0) });
            flowInput.Controls.Add(new Label { Text = tbl, AutoSize = true, Font = new Font("Consolas", 10F), ForeColor = Color.FromArgb(40, 40, 60), Margin = new Padding(0, 6, 16, 0) });
            var btnSelect = QuickBtn("Xem Dữ Liệu", Color.FromArgb(100, 120, 180), UiTheme.WhiteText, 130, 36);
            flowInput.Controls.Add(btnSelect);

            var flowSteps = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White };
            var btnS1  = QuickBtn("Bước 1: Ghi Mốc An Toàn",     UiTheme.BrandeisBlue,        UiTheme.WhiteText, 220, 40);
            var btnS2  = QuickBtn("Bước 2: Giả Lập Sửa Nhầm",   Color.FromArgb(206, 17, 38),  UiTheme.WhiteText, 222, 40);
            var dtpFT  = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", ShowUpDown = true, Width = 210, Margin = new Padding(10, 5, 6, 0), Value = DateTime.Now };
            var btnS3  = QuickBtn("Bước 3: FLASHBACK TABLE",     Color.FromArgb(30, 160, 100),  UiTheme.WhiteText, 210, 40);
            flowSteps.Controls.AddRange(new Control[] { btnS1, btnS2, dtpFT, btnS3 });

            var lblDtp = new Label { Text = "← Mốc thời gian phục hồi (tự điền sau Bước 1)", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(100, 100, 120), Dock = DockStyle.Top, Height = 20, AutoSize = false };
            var txtLog = new TextBox { Multiline = true, Dock = DockStyle.Bottom, Height = 360, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10.5F), ScrollBars = ScrollBars.Vertical, Text = "SQL> Bấm 'Xem Dữ Liệu' bất cứ lúc nào, hoặc Bước 1 → 2 → 3.\r\n" };

            string savedTsFT = null;

            void AppendDonThuocSample(string prefix, bool singleRow = true)
            {
                string sql = singleRow
                    ? $"SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} WHERE ROWNUM = 1"
                    : $"SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} ORDER BY MAHSBA FETCH FIRST 20 ROWS ONLY";
                var dt = service.Query(sql);
                if (dt.Rows.Count == 0)
                {
                    txtLog.AppendText($"{prefix}(không có dòng nào trong {tbl})\r\n");
                    return;
                }
                if (singleRow)
                {
                    var r = dt.Rows[0];
                    txtLog.AppendText($"{prefix}MAHSBA={r["MAHSBA"]}  TENTHUOC={r["TENTHUOC"]}  LIEUDUNG={r["LIEUDUNG"]}\r\n");
                    return;
                }
                txtLog.AppendText($"{prefix}MAHSBA       TENTHUOC              LIEUDUNG\r\n");
                foreach (DataRow r in dt.Rows)
                {
                    string lieudung = r["LIEUDUNG"]?.ToString() ?? "";
                    if (lieudung.Length > 40) lieudung = lieudung.Substring(0, 37) + "...";
                    txtLog.AppendText($"{prefix}{r["MAHSBA"],-12}{r["TENTHUOC"],-22}{lieudung}\r\n");
                }
                if (dt.Rows.Count >= 20) txtLog.AppendText($"{prefix}... (hiển thị tối đa 20 dòng)\r\n");
            }

            btnSelect.Click += (s, e) =>
            {
                try
                {
                    txtLog.AppendText($"\r\nSQL> SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} ORDER BY MAHSBA FETCH FIRST 20 ROWS ONLY;\r\n");
                    AppendDonThuocSample("  ", singleRow: false);
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI Xem Dữ Liệu]: {ex.Message}\r\n"); }
            };

            btnS1.Click += (s, e) =>
            {
                try
                {
                    // 08.sql: ghi mốc thời gian an toàn + xem dòng đối chứng + bật ROW MOVEMENT
                    var tsRow = service.Query("SELECT TO_CHAR(SYSTIMESTAMP, 'yyyy-mm-dd hh24:mi:ss') AS BEFORE_INCIDENT_TIME FROM DUAL");
                    savedTsFT = tsRow.Rows[0]["BEFORE_INCIDENT_TIME"].ToString();
                    if (DateTime.TryParseExact(savedTsFT, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime ts))
                        dtpFT.Value = ts;

                    txtLog.AppendText($"\r\nSQL> SELECT TO_CHAR(SYSTIMESTAMP, 'yyyy-mm-dd hh24:mi:ss') AS BEFORE_INCIDENT_TIME FROM DUAL;\r\n");
                    txtLog.AppendText($"BEFORE_INCIDENT_TIME: {savedTsFT}\r\n");

                    txtLog.AppendText($"\r\nSQL> SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} WHERE ROWNUM = 1;\r\n");
                    AppendDonThuocSample("  ");

                    txtLog.AppendText($"\r\nSQL> ALTER TABLE {tbl} ENABLE ROW MOVEMENT;\r\n");
                    service.ExecuteNonQuery($"ALTER TABLE {tbl} ENABLE ROW MOVEMENT");
                    txtLog.AppendText("Table altered.\r\n");

                    txtLog.AppendText($"\r\n>> [BƯỚC 1 HOÀN THÀNH] Mốc an toàn: {savedTsFT}\r\n   => Bấm Bước 2 để giả lập sửa nhầm/phá hoại.\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI Bước 1]: {ex.Message}\r\n"); }
            };

            btnS2.Click += (s, e) =>
            {
                if (savedTsFT == null) { Err("Vui lòng thực hiện Bước 1 trước."); return; }
                string corruptSql = $"UPDATE {tbl} SET LIEUDUNG = N'{corruptLieudung}' WHERE ROWNUM = 1";
                if (MessageBox.Show($"Sẽ thực thi:\r\n\r\n{corruptSql};\r\nCOMMIT;\r\n\r\nXác nhận?", "Bước 2 - Giả Lập Sự Cố", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    txtLog.AppendText($"\r\nSQL> {corruptSql};\r\n");
                    service.ExecuteNonQuery(corruptSql);
                    service.ExecuteNonQuery("COMMIT");
                    txtLog.AppendText("1 row updated.\r\nCommit complete.\r\n");

                    txtLog.AppendText($"\r\nSQL> SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} WHERE ROWNUM = 1;\r\n");
                    AppendDonThuocSample("  ");

                    txtLog.AppendText($"\r\n>> [BƯỚC 2 HOÀN THÀNH] Dữ liệu đã bị sai lệch.\r\n   => Kiểm tra DateTimePicker rồi bấm Bước 3.\r\n");
                }
                catch (Exception ex) { txtLog.AppendText($"[LỖI Bước 2]: {ex.Message}\r\n"); }
            };

            btnS3.Click += (s, e) =>
            {
                string flashTs = dtpFT.Value.ToString("yyyy-MM-dd HH:mm:ss");
                if (savedTsFT == null) { Err("Vui lòng thực hiện Bước 1 trước."); return; }
                if (MessageBox.Show($"FLASHBACK TABLE {tbl}\nTO TIMESTAMP TO_TIMESTAMP('{flashTs}', 'yyyy-mm-dd hh24:mi:ss')\n\nXác nhận?", "Bước 3 - Phục Hồi", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    txtLog.AppendText($"\r\nSQL> FLASHBACK TABLE {tbl}\r\n  TO TIMESTAMP TO_TIMESTAMP('{flashTs}', 'yyyy-mm-dd hh24:mi:ss');\r\n");
                    service.ExecuteNonQuery($"FLASHBACK TABLE {tbl} TO TIMESTAMP TO_TIMESTAMP('{flashTs}', 'yyyy-mm-dd hh24:mi:ss')");
                    txtLog.AppendText("Flashback complete.\r\n");

                    txtLog.AppendText($"\r\nSQL> SELECT MAHSBA, TENTHUOC, LIEUDUNG FROM {tbl} WHERE ROWNUM = 1;\r\n");
                    AppendDonThuocSample("  ");
                    txtLog.AppendText($"\r\n>> [BƯỚC 3 HOÀN THÀNH] ✓ {tbl} đã quay về trạng thái trước sự cố.\r\n");

                    try
                    {
                        service.ExecuteNonQuery(
                            $"INSERT INTO QLBV.RESTORE_HISTORY (RESTORE_TYPE, FILE_SRC, STATUS) " +
                            $"VALUES ('FLASHBACK_TABLE', '{Esc(tbl)}', 'SUCCESS')");
                        service.ExecuteNonQuery("COMMIT");
                        txtLog.AppendText("   => Đã ghi nhận vào QLBV.RESTORE_HISTORY.\r\n");
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"[LỖI Bước 3 - Flashback Table]: {ex.Message}\r\n");
                    txtLog.AppendText("   Gợi ý: UNDO_RETENTION đủ lớn; mốc thời gian phải trước COMMIT ở Bước 2.\r\n");
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

        // ── 08.sql [08-QLBV-03]: DBMS_SCHEDULER + JOB_DAILY_ARCHIVE_AUDIT
        private void BuildBackup_Scheduler(TabPage tab)
        {
            var pnl     = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 0), BackColor = Color.White };
            var lblTitle = new Label { Text = "Scheduler — Archive Audit Tự Động", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 34 };

            var btnRunNow  = QuickBtn("▶ Chạy Archive Ngay",   UiTheme.BrandeisBlue,        UiTheme.WhiteText, 188, 38);
            var btnEnable  = QuickBtn("✔ Bật Job",              Color.FromArgb(30, 160, 80),  UiTheme.WhiteText, 108, 38);
            var btnDisable = QuickBtn("✗ Tắt Job",              Color.FromArgb(206, 17, 38),  UiTheme.WhiteText,  98, 38);
            var btnRefresh = QuickBtn("Tải Lại",               Color.FromArgb(210, 220, 230), UiTheme.DeepBlue,   88, 38);
            var flowBtn    = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 4), BackColor = Color.White };
            flowBtn.Controls.AddRange(new Control[] { btnRunNow, btnEnable, btnDisable, btnRefresh,
                Note("JOB: QLBV.JOB_DAILY_ARCHIVE_AUDIT — 23:00 hằng ngày. Procedure: QLBV.PR_AUTO_ARCHIVE_AUDIT_LOG") });

            // Grid job status + audit archive log
            var gridJob = MakeGrid(true);
            var gridArchive = MakeGrid(true);
            gridJob.Dock = DockStyle.Fill;
            gridArchive.Dock = DockStyle.Fill;

            var pnlJob = new Panel { Dock = DockStyle.Fill };
            pnlJob.Controls.Add(gridJob);
            pnlJob.Controls.Add(new Label { Text = "Trạng Thái Job — ALL_SCHEDULER_JOBS WHERE JOB_NAME = 'JOB_DAILY_ARCHIVE_AUDIT'", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 });

            var splitOuter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Color.White, SplitterWidth = 5 };
            splitOuter.Panel1.Controls.Add(pnlJob);
            var pnlArchive = new Panel { Dock = DockStyle.Fill };
            pnlArchive.Controls.Add(gridArchive);
            pnlArchive.Controls.Add(new Label { Text = "QLBV.AUDIT_ARCHIVE_LOG — Nhật ký đã archive", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 24 });
            splitOuter.Panel2.Controls.Add(pnlArchive);

            var txtOut = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10F), ScrollBars = ScrollBars.Vertical, Text = "C:\\Oracle> Scheduler console sẵn sàng.\r\n" };

            var splitConsole = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.White,
                SplitterWidth = 6
            };
            void BalanceSchedSplit()
            {
                SafeBalanceHorizontalSplit(splitConsole, 0.72, 220, 140);
                SafeBalanceHorizontalSplit(splitOuter, 0.28, 90, 140);
            }
            splitConsole.Resize += (s, e) => BalanceSchedSplit();
            splitConsole.Panel1.Controls.Add(splitOuter);
            splitConsole.Panel2.Controls.Add(txtOut);

            void RefreshAll()
            {
                try
                {
                    gridJob.DataSource = service.Query("SELECT OWNER, JOB_NAME, ENABLED, STATE, RUN_COUNT, FAILURE_COUNT, TO_CHAR(LAST_START_DATE,'DD/MM/YYYY HH24:MI') AS LAST_RUN, TO_CHAR(NEXT_RUN_DATE,'DD/MM/YYYY HH24:MI') AS NEXT_RUN, REPEAT_INTERVAL, COMMENTS FROM ALL_SCHEDULER_JOBS WHERE OWNER = 'QLBV' AND JOB_NAME = 'JOB_DAILY_ARCHIVE_AUDIT'");
                    UiTheme.StyleGrid(gridJob);
                }
                catch { }
                LoadAuditArchiveLogGrid(gridArchive);
            }

            btnRefresh.Click += (s, e) => RefreshAll();

            btnRunNow.Click += (s, e) =>
            {
                if (MessageBox.Show(
                    "Sẽ thực thi QLBV.PR_AUTO_ARCHIVE_AUDIT_LOG.\n\n" +
                    "Gom nhật ký audit từ UNIFIED_AUDIT_TRAIL vào AUDIT_ARCHIVE_LOG\n" +
                    "và ghi BACKUP_HISTORY (AUDIT_LOG_AUTO).\n\nXác nhận?",
                    "Chạy Archive Ngay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                bool ok = RunArchiveAuditLog(line => txtOut.AppendText(line + "\r\n"), out string summary);
                RefreshAll();
                if (ok)
                    Ok($"Archive audit hoàn tất.\n\n{summary}");
                else
                    Err($"Archive audit thất bại.\n\n{summary}");
            };

            btnEnable.Click += (s, e) =>
            {
                try
                {
                    txtOut.AppendText($"\r\nSQL> BEGIN DBMS_SCHEDULER.ENABLE('QLBV.JOB_DAILY_ARCHIVE_AUDIT'); END;\r\n");
                    service.ExecuteNonQuery("BEGIN DBMS_SCHEDULER.ENABLE('QLBV.JOB_DAILY_ARCHIVE_AUDIT'); END;");
                    txtOut.AppendText($"[{DateTime.Now:HH:mm:ss}] Job đã được BẬT.\r\n");
                    RefreshAll();
                }
                catch (Exception ex) { txtOut.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            btnDisable.Click += (s, e) =>
            {
                try
                {
                    txtOut.AppendText($"\r\nSQL> BEGIN DBMS_SCHEDULER.DISABLE('QLBV.JOB_DAILY_ARCHIVE_AUDIT'); END;\r\n");
                    service.ExecuteNonQuery("BEGIN DBMS_SCHEDULER.DISABLE('QLBV.JOB_DAILY_ARCHIVE_AUDIT'); END;");
                    txtOut.AppendText($"[{DateTime.Now:HH:mm:ss}] Job đã được TẮT.\r\n");
                    RefreshAll();
                }
                catch (Exception ex) { txtOut.AppendText($"[LỖI]: {ex.Message}\r\n"); }
            };

            pnl.Controls.Add(splitConsole);
            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
            // Lazy load: chỉ query khi user mở tab lần đầu
            bool _schedLoaded = false;
            tab.Enter += (s, e) =>
            {
                BalanceSchedSplit();
                if (_schedLoaded) return;
                _schedLoaded = true;
                RefreshAll();
            };
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

            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var row = grid.Rows[e.RowIndex];
                string loai = grid.Columns.Contains("LOẠI") ? row.Cells["LOẠI"].Value?.ToString() : null;
                string ma   = grid.Columns.Contains("MÃ")   ? row.Cells["MÃ"].Value?.ToString()   : null;
                if (string.IsNullOrWhiteSpace(ma)) return;
                ShowHoSoTaiKhoan(loai, ma);
            };

            tab.Controls.Add(Wrap(grid, Toolbar(btnNV, btnBN, btnRe,
                Note("Tạo tài khoản Oracle cho Nhân Viên / Bệnh Nhân. Double-click dòng để xem hồ sơ chi tiết."))));
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

        private static string HoSoField(object value)
        {
            if (value == null || value == DBNull.Value) return "—";
            if (value is DateTime d) return d.ToString("dd/MM/yyyy");
            string s = value.ToString().Trim();
            if (s.EndsWith(" 12:00:00 AM")) s = s.Replace(" 12:00:00 AM", "");
            if (s.EndsWith(" 12:00:00 SA")) s = s.Replace(" 12:00:00 SA", "");
            return string.IsNullOrWhiteSpace(s) ? "—" : s;
        }

        private void ShowHoSoTaiKhoan(string loai, string ma)
        {
            try
            {
                bool isNv = string.Equals(loai?.Trim(), "Nhân viên", StringComparison.OrdinalIgnoreCase);
                bool isBn = string.Equals(loai?.Trim(), "Bệnh nhân", StringComparison.OrdinalIgnoreCase);
                if (!isNv && !isBn) { Err("Không xác định được loại tài khoản."); return; }

                DataTable dt;
                if (isNv)
                {
                    dt = service.Query(
                        "SELECT n.MANV, n.HOTEN, n.PHAI, TO_CHAR(n.NGAYSINH,'DD/MM/YYYY') AS NGAYSINH, " +
                        "n.CMND, n.QUEQUAN, n.SODT, n.VAITRO, n.MAKHOA, k.TENKHOA, n.CAPBAC, n.COSO " +
                        $"FROM QLBV.NHANVIEN n LEFT JOIN QLBV.KHOA k ON n.MAKHOA = k.MAKHOA WHERE n.MANV = '{Esc(ma)}'");
                    if (dt.Rows.Count == 0) { Err($"Không tìm thấy nhân viên {ma}."); return; }
                    var r = dt.Rows[0];
                    string khoa = HoSoField(r["TENKHOA"]);
                    if (khoa == "—" && r["MAKHOA"] != DBNull.Value)
                        khoa = HoSoField(r["MAKHOA"]);

                    ShowHoSoTaiKhoanForm(
                        $"Hồ Sơ Nhân Viên — {ma}",
                        new (string Label, string Value)[]
                        {
                            ("Mã NV:", HoSoField(r["MANV"])),
                            ("Họ tên:", HoSoField(r["HOTEN"])),
                            ("Phái:", HoSoField(r["PHAI"])),
                            ("Ngày sinh:", HoSoField(r["NGAYSINH"])),
                            ("CMND/CCCD:", HoSoField(r["CMND"])),
                            ("Quê quán:", HoSoField(r["QUEQUAN"])),
                            ("Số điện thoại:", HoSoField(r["SODT"])),
                            ("Vai trò:", HoSoField(r["VAITRO"])),
                            ("Khoa:", khoa),
                            ("Cấp bậc (OLS):", HoSoField(r["CAPBAC"])),
                            ("Cơ sở (OLS):", HoSoField(r["COSO"]))
                        });
                }
                else
                {
                    dt = service.Query(
                        "SELECT MABN, TENBN, PHAI, TO_CHAR(NGAYSINH,'DD/MM/YYYY') AS NGAYSINH, CCCD, " +
                        "SONHA, TENDUONG, QUANHUYEN, TINHTP, TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC " +
                        $"FROM QLBV.BENHNHAN WHERE MABN = '{Esc(ma)}'");
                    if (dt.Rows.Count == 0) { Err($"Không tìm thấy bệnh nhân {ma}."); return; }
                    var r = dt.Rows[0];
                    string diaChi = string.Join(", ",
                        new[] { HoSoField(r["SONHA"]), HoSoField(r["TENDUONG"]), HoSoField(r["QUANHUYEN"]), HoSoField(r["TINHTP"]) }
                            .Where(x => x != "—"));

                    ShowHoSoTaiKhoanForm(
                        $"Hồ Sơ Bệnh Nhân — {ma}",
                        new (string Label, string Value)[]
                        {
                            ("Mã BN:", HoSoField(r["MABN"])),
                            ("Họ tên:", HoSoField(r["TENBN"])),
                            ("Phái:", HoSoField(r["PHAI"])),
                            ("Ngày sinh:", HoSoField(r["NGAYSINH"])),
                            ("CCCD:", HoSoField(r["CCCD"])),
                            ("Địa chỉ:", string.IsNullOrWhiteSpace(diaChi) ? "—" : diaChi),
                            ("Tiền sử bệnh:", HoSoField(r["TIENSUBENH"])),
                            ("Tiền sử bệnh GĐ:", HoSoField(r["TIENSUBENHGD"])),
                            ("Dị ứng thuốc:", HoSoField(r["DIUNGTHUOC"]))
                        });
                }
            }
            catch (Exception ex) { Err("Không thể tải hồ sơ:\n" + ex.Message); }
        }

        private void ShowHoSoTaiKhoanForm(string title, (string Label, string Value)[] rows)
        {
            var f = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = UiTheme.JordyBlue,
                Font = UiTheme.BodyFont,
                Padding = new Padding(14, 12, 14, 10)
            };

            var layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 332F));

            int rowIdx = 0;
            foreach (var (label, val) in rows)
            {
                string text = val ?? "—";
                bool multi = text.Contains("\n") || text.Length > 48;
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label
                {
                    Text = label,
                    Font = new Font(UiTheme.BodyFont, FontStyle.Bold),
                    ForeColor = UiTheme.DeepBlue,
                    AutoSize = true,
                    Anchor = AnchorStyles.Right | AnchorStyles.Top,
                    Margin = new Padding(0, 6, 8, 0)
                }, 0, rowIdx);

                int lines = multi ? Math.Max(2, text.Split('\n').Length + text.Length / 44) : 1;
                var txt = new TextBox
                {
                    Text = text,
                    ReadOnly = true,
                    Multiline = multi,
                    Width = 332,
                    Height = multi ? Math.Min(120, Math.Max(44, lines * 22)) : 26,
                    ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 3, 0, 3),
                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                };
                layout.Controls.Add(txt, 1, rowIdx++);
            }

            var btnClose = new Button
            {
                Text = "Đóng",
                Width = 100,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue,
                ForeColor = UiTheme.WhiteText,
                DialogResult = DialogResult.OK,
                Margin = new Padding(0, 8, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 0;

            const int contentWidth = 138 + 332;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var pnlBtn = new Panel
            {
                Width = contentWidth,
                Height = 40,
                Margin = new Padding(0, 6, 0, 0),
                BackColor = UiTheme.JordyBlue
            };
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(contentWidth - btnClose.Width, 4);
            pnlBtn.Controls.Add(btnClose);
            layout.Controls.Add(pnlBtn, 0, rowIdx);
            layout.SetColumnSpan(pnlBtn, 2);

            f.Controls.Add(layout);
            f.AcceptButton = btnClose;
            f.ShowDialog();
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

        private string NextAvailableHsbaId() =>
            FindNextId("HS", 6, "QLBV.HSBA", "MAHSBA");

        private static string CosoToOlsGroup(string coSo)
        {
            if (coSo == "Hồ Chí Minh") return "HCM";
            if (coSo == "Hải Phòng") return "HP";
            if (coSo == "Hà Nội") return "HN";
            return null;
        }

        private static string ComputeBgdWriteLabel(string coSo)
        {
            string grp = CosoToOlsGroup(coSo);
            return grp != null ? "BGD:TH,TK,TM:" + grp : "BGD:TH,TK,TM:HCM,HP,HN";
        }

        /// <summary>
        /// Giám đốc chỉ gửi trong chi nhánh (06.sql: level::group hoặc level:comp:group).
        /// Từ chối nhãn toàn hệ thống (NV/LDK/LDP thuần) hoặc nhiều cơ sở.
        /// </summary>
        private static string NormalizeGiamDocBranchLabel(string olsLabel, string branchCode)
        {
            if (string.IsNullOrWhiteSpace(branchCode)) return null;
            string lbl = (olsLabel ?? "").Trim();
            if (string.IsNullOrEmpty(lbl)) return null;

            if (lbl.StartsWith("BGD", StringComparison.OrdinalIgnoreCase))
                return lbl;

            // NV/LDK/LDP không kèm chi nhánh → gắn chi nhánh giám đốc (06.sql PHẦN I)
            if (lbl.Equals("NV", StringComparison.OrdinalIgnoreCase)
                || lbl.Equals("LDK", StringComparison.OrdinalIgnoreCase)
                || lbl.Equals("LDP", StringComparison.OrdinalIgnoreCase))
                return lbl + "::" + branchCode;

            if (lbl.Contains("::"))
            {
                string grpPart = lbl.Substring(lbl.IndexOf("::", StringComparison.Ordinal) + 2);
                if (grpPart.IndexOf(',') >= 0) return null;
                if (!grpPart.Equals(branchCode, StringComparison.OrdinalIgnoreCase)) return null;
                return lbl;
            }

            int firstColon = lbl.IndexOf(':');
            if (firstColon < 0) return null;
            int secondColon = lbl.IndexOf(':', firstColon + 1);
            if (secondColon < 0) return lbl + "::" + branchCode;

            string tail = lbl.Substring(secondColon + 1);
            if (tail.IndexOf(',') >= 0) return null;
            if (!tail.Equals(branchCode, StringComparison.OrdinalIgnoreCase)) return null;
            return lbl;
        }

        private string BuildAssignOlsLabelsSql(string manv, string capBac, string maKhoa, string coSo)
        {
            string nv = Esc(manv);
            string capSql = string.IsNullOrEmpty(capBac) ? "NULL" : "N'" + Esc(capBac) + "'";
            string khoaSql = string.IsNullOrEmpty(maKhoa) ? "NULL" : "'" + Esc(maKhoa) + "'";
            string cosoSql = string.IsNullOrEmpty(coSo) ? "NULL" : "N'" + Esc(coSo) + "'";
            return @"
DECLARE
    v_label       VARCHAR2(200);
    v_read_label  VARCHAR2(200);
    v_write_label VARCHAR2(200);
    v_level VARCHAR2(10);
    v_comp  VARCHAR2(10);
    v_grp   VARCHAR2(10);
    v_capbac NVARCHAR2(50) := " + capSql + @";
    v_makhoa VARCHAR2(20) := " + khoaSql + @";
    v_coso   NVARCHAR2(50) := " + cosoSql + @";
BEGIN
    IF v_capbac IS NULL THEN RETURN; END IF;
    CASE v_capbac
        WHEN N'Ban Giám đốc'   THEN v_level := 'BGD';
        WHEN N'Lãnh đạo khoa'  THEN v_level := 'LDK';
        WHEN N'Lãnh đạo phòng' THEN v_level := 'LDP';
        ELSE v_level := 'NV';
    END CASE;
    CASE v_makhoa WHEN 'K001' THEN v_comp := 'TH'; WHEN 'K002' THEN v_comp := 'TK'; WHEN 'K003' THEN v_comp := 'TM'; ELSE v_comp := NULL; END CASE;
    CASE v_coso WHEN N'Hồ Chí Minh' THEN v_grp := 'HCM'; WHEN N'Hải Phòng' THEN v_grp := 'HP'; WHEN N'Hà Nội' THEN v_grp := 'HN'; ELSE v_grp := NULL; END CASE;
    IF v_capbac = N'Ban Giám đốc' THEN
        v_read_label := 'BGD:TH,TK,TM:HCM,HP,HN';
        v_write_label := CASE WHEN v_grp IS NOT NULL THEN 'BGD:TH,TK,TM:' || v_grp ELSE v_read_label END;
        LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(policy_name=>'OLS_QLBV_POLICY',user_name=>'" + nv + @"',max_read_label=>v_read_label,max_write_label=>v_read_label,def_label=>v_write_label,row_label=>v_write_label);
        RETURN;
    END IF;
    IF v_capbac = N'Lãnh đạo phòng' AND v_makhoa IS NULL THEN v_label := 'LDP:TH,TK,TM:HCM,HP,HN';
    ELSIF v_comp IS NOT NULL AND v_grp IS NOT NULL THEN v_label := v_level || ':' || v_comp || ':' || v_grp;
    ELSIF v_comp IS NOT NULL THEN v_label := v_level || ':' || v_comp;
    ELSIF v_grp IS NOT NULL THEN v_label := v_level || '::' || v_grp;
    ELSE v_label := v_level; END IF;
    LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(policy_name=>'OLS_QLBV_POLICY',user_name=>'" + nv + @"',max_read_label=>v_label,max_write_label=>v_label,def_label=>v_label,row_label=>v_label);
END;";
        }

        // Tính nhãn ghi mặc định theo run/06.sql PHẦN I
        private string ComputeOlsLabel(string capBac, string maKhoa, string coSo)
        {
            if (capBac == "Ban Giám đốc")
                return ComputeBgdWriteLabel(coSo);

            string level;
            if (capBac == "Lãnh đạo khoa") level = "LDK";
            else if (capBac == "Lãnh đạo phòng") level = "LDP";
            else level = "NV";

            string comp = null;
            if (maKhoa == "K001") comp = "TH";
            else if (maKhoa == "K002") comp = "TK";
            else if (maKhoa == "K003") comp = "TM";

            string grp = CosoToOlsGroup(coSo);

            if (level == "LDP" && comp == null) return "LDP:TH,TK,TM:HCM,HP,HN";
            if (comp != null && grp != null) return level + ":" + comp + ":" + grp;
            if (comp != null) return level + ":" + comp;
            if (grp != null) return level + "::" + grp;
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

                    // 4. Gán OLS label — run/06.sql PHẦN I
                    if (!string.IsNullOrEmpty(f.CapBac))
                    {
                        try { service.ExecuteNonQuery(BuildAssignOlsLabelsSql(f.MaNV, f.CapBac, f.MaKhoa, f.CoSo)); }
                        catch { }
                    }

                    string labelHint = string.IsNullOrEmpty(f.CapBac) ? "" : "\nOLS: " + ComputeOlsLabel(f.CapBac, f.MaKhoa, f.CoSo);
                    Ok("Đã tạo tài khoản nhân viên " + f.MaNV + " thành công!\nRole Oracle: " + role + labelHint);
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
                Note("Nhãn OLS xác định hàng nào trong THONGBAO mỗi user được đọc.  |  'Đồng Bộ' tính lại theo CAPBAC/MAKHOA/COSO."))));
            // Lazy load: chỉ query DBA_SA_USER_LABELS khi user mở tab lần đầu
            bool _olsULLoaded = false;
            tab.Enter += (s, e) => { if (_olsULLoaded) return; _olsULLoaded = true; LoadOlsUserLabels(grid, ""); };
        }

        private void LoadOlsUserLabels(DataGridView grid, string filter)
        {
            // run/06.sql — SELECT user_name, max_read_label, max_write_label, ...
            string sql =
                "SELECT USER_NAME, MAX_READ_LABEL, MAX_WRITE_LABEL, MIN_WRITE_LABEL, " +
                "DEFAULT_READ_LABEL, DEFAULT_WRITE_LABEL, DEFAULT_ROW_LABEL " +
                "FROM DBA_SA_USER_LABELS WHERE POLICY_NAME='OLS_QLBV_POLICY'";
            if (!string.IsNullOrEmpty(filter))
                sql += $" AND UPPER(USER_NAME) LIKE '%{filter.ToUpper().Replace("'", "''")}%'";
            sql += " ORDER BY USER_NAME";
            try { LoadGrid(grid, sql); UiTheme.StyleGrid(grid); }
            catch { grid.DataSource = null; }
        }

        // run/06.sql PHẦN I — nguyên văn PL/SQL gán nhãn OLS (BGD tách read/write)
        private void SyncOlsLabels(DataGridView grid)
        {
            try
            {
                service.ExecuteNonQuery(@"
DECLARE
    v_label       VARCHAR2(200);
    v_read_label  VARCHAR2(200);
    v_write_label VARCHAR2(200);
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

        IF nv.CAPBAC = N'Ban Giám đốc' THEN
            v_read_label := 'BGD:TH,TK,TM:HCM,HP,HN';
            v_write_label := CASE WHEN v_grp IS NOT NULL
                                   THEN 'BGD:TH,TK,TM:' || v_grp
                                   ELSE v_read_label
                              END;
            BEGIN
                LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(
                    policy_name     => 'OLS_QLBV_POLICY',
                    user_name       => nv.MANV,
                    max_read_label  => v_read_label,
                    max_write_label => v_read_label,
                    def_label       => v_write_label,
                    row_label       => v_write_label
                );
            EXCEPTION WHEN OTHERS THEN NULL;
            END;
            CONTINUE;
        END IF;

        IF nv.CAPBAC = N'Lãnh đạo phòng' AND nv.MAKHOA IS NULL THEN
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
                policy_name     => 'OLS_QLBV_POLICY',
                user_name       => nv.MANV,
                max_read_label  => v_label,
                max_write_label => v_label,
                def_label       => v_label,
                row_label       => v_label
            );
        EXCEPTION WHEN OTHERS THEN NULL;
        END;
    END LOOP;
END;");
                Ok("Đã đồng bộ nhãn OLS cho toàn bộ nhân viên.");
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

        private void BuildThongBaoTab(TabPage tab, TabControl ownerTabs, ThongBaoSendMode sendMode)
        {
            var grid = MakeGrid(true);
            var btnRe = QuickBtn("Tải Lại", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 90);
            var lblInfo = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 120),
                Margin = new Padding(4, 10, 0, 0),
                Font = new Font("Segoe UI", 8.5F),
                Text = "Mở tab để tải thông báo."
            };

            bool showOlsColumn = sendMode != ThongBaoSendMode.ViewOnly;
            string sqlWithLabel =
                "SELECT NOIDUNG, TO_CHAR(NGAYGIO,'DD/MM/YYYY HH24:MI:SS') AS NGAYGIO, DIADIEM, " +
                "LABEL_TO_CHAR(OLS_COL) AS \"NHÃN OLS\" FROM QLBV.THONGBAO ORDER BY NGAYGIO DESC";
            string sqlNoLabel =
                "SELECT NOIDUNG, TO_CHAR(NGAYGIO,'DD/MM/YYYY HH24:MI:SS') AS NGAYGIO, DIADIEM " +
                "FROM QLBV.THONGBAO ORDER BY NGAYGIO DESC";

            void LoadThongBao()
            {
                try
                {
                    DataTable dt = null;
                    if (showOlsColumn)
                    {
                        try { dt = service.Query(sqlWithLabel); }
                        catch { dt = service.Query(sqlNoLabel); }
                    }
                    else
                        dt = service.Query(sqlNoLabel);

                    grid.DataSource = dt;
                    UiTheme.StyleGrid(grid);

                    int n = dt?.Rows.Count ?? 0;
                    if (sendMode == ThongBaoSendMode.BgdProcedure)
                        lblInfo.Text = $"Hiển thị {n} thông báo OLS. Gửi mới: NV/LDK/LDP trong chi nhánh (COSO) của giám đốc đăng nhập.";
                    else if (sendMode == ThongBaoSendMode.AdminOlsPicker)
                        lblInfo.Text = $"Hiển thị {n} thông báo (QLBV — INSERT thủ công + chọn nhãn OLS).";
                    else
                        lblInfo.Text = $"Hiển thị {n} thông báo OLS cho {service.CurrentUser}.";
                }
                catch (Exception ex)
                {
                    grid.DataSource = null;
                    lblInfo.Text = "Không tải được THONGBAO.";
                    Err("Không thể tải thông báo:\n" + ex.Message);
                }
            }

            btnRe.Click += (s, e) => LoadThongBao();

            grid.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0) return;
                var row = grid.Rows[e.RowIndex];
                string GetCell(string name)
                {
                    foreach (DataGridViewColumn col in grid.Columns)
                        if (string.Equals(col.Name, name, StringComparison.OrdinalIgnoreCase))
                            return row.Cells[col.Index].Value?.ToString();
                    return null;
                }
                string nd = GetCell("NOIDUNG");
                string ng = GetCell("NGAYGIO");
                string dd = GetCell("DIADIEM");
                string ols = GetCell("NHÃN OLS");
                string msg = $"THỜI GIAN:\n{ng}\n\nĐỊA ĐIỂM:\n{dd}\n\nNỘI DUNG:\n{nd}";
                if (!string.IsNullOrEmpty(ols)) msg += $"\n\nNHÃN OLS:\n{ols}";
                MessageBox.Show(msg, "Chi Tiết Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Panel toolbar;
            if (sendMode == ThongBaoSendMode.AdminOlsPicker)
            {
                var btnSend = QuickBtn("+ Gửi Thông Báo", UiTheme.PastelGreen, UiTheme.DeepBlue, 170);
                btnSend.Click += (s, e) => GuiThongBaoAdmin(LoadThongBao);
                toolbar = Toolbar(btnSend, btnRe, lblInfo, Note("QLBV: INSERT + chọn nhãn OLS."));
            }
            else if (sendMode == ThongBaoSendMode.BgdProcedure)
            {
                toolbar = Toolbar(btnRe, lblInfo, Note("Giám đốc xem thông báo OLS."));
            }
            else
            {
                toolbar = Toolbar(btnRe, lblInfo, Note("Nhấn đúp xem chi tiết. Chỉ thấy thông báo trong phạm vi nhãn OLS."));
            }

            tab.Controls.Add(Wrap(grid, toolbar));
            RegisterTabLazyLoad(ownerTabs, tab, LoadThongBao);
        }

        private void GuiThongBaoAdmin(Action afterSend = null)
        {
            using (var f = new ThongBaoForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        InsertThongBao(f.NoiDung, f.NgayGio, f.DiaDiem, f.OlsLabel);
                        Ok($"Đã gửi thông báo!\nNhãn OLS: {(string.IsNullOrEmpty(f.OlsLabel) ? "(mặc định QLBV)" : f.OlsLabel)}");
                        afterSend?.Invoke();
                    }
                    catch (Exception ex) { Err(ex.Message); }
        }

        private void InsertThongBao(string noiDung, DateTime ngayGio, string diaDiem, string olsLabel)
        {
            string olsSql = string.IsNullOrEmpty(olsLabel)
                ? $"INSERT INTO QLBV.THONGBAO(NOIDUNG,NGAYGIO,DIADIEM) " +
                  $"VALUES(N'{Esc(noiDung)}',TO_TIMESTAMP('{ngayGio:dd/MM/yyyy HH:mm}','DD/MM/YYYY HH24:MI'),N'{Esc(diaDiem)}')"
                : $"INSERT INTO QLBV.THONGBAO(NOIDUNG,NGAYGIO,DIADIEM,OLS_COL) " +
                  $"VALUES(N'{Esc(noiDung)}',TO_TIMESTAMP('{ngayGio:dd/MM/yyyy HH:mm}','DD/MM/YYYY HH24:MI'),N'{Esc(diaDiem)}'," +
                  $"CHAR_TO_LABEL('OLS_QLBV_POLICY','{olsLabel}'))";
            service.ExecuteNonQuery(olsSql);
        }

        // Giám đốc: cùng UI chọn nhãn OLS như QLBV, cơ sở cố định theo COSO tài khoản
        private void GuiThongBaoBgd(Action afterSend = null)
        {
            string coSo = null;
            string grpCode = null;
            try
            {
                var dt = service.Query($"SELECT COSO FROM QLBV.NHANVIEN WHERE MANV = '{Esc(currentUser)}'");
                if (dt.Rows.Count > 0)
                {
                    coSo = dt.Rows[0]["COSO"]?.ToString();
                    grpCode = CosoToOlsGroup(coSo);
                }
            }
            catch { }

            if (string.IsNullOrEmpty(grpCode))
            {
                Err("Tài khoản chưa được gán cơ sở (COSO) trong NHANVIEN.");
                return;
            }

            using (var f = new ThongBaoForm(grpCode, coSo))
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        string label = NormalizeGiamDocBranchLabel(f.OlsLabel, grpCode);
                        if (string.IsNullOrEmpty(label))
                        {
                            Err($"Chỉ được gửi thông báo NV/LDK/LDP trong chi nhánh {coSo} ({grpCode}).\n" +
                                "Không chọn nhiều cơ sở hoặc cơ sở khác chi nhánh của giám đốc.");
                            return;
                        }

                        if (label.StartsWith("BGD", StringComparison.OrdinalIgnoreCase))
                        {
                            service.ExecuteNonQuery(
                                $"BEGIN QLBV.sp_BGD_ThongBaoOLS(N'{Esc(f.NoiDung)}', N'{Esc(f.DiaDiem)}'); END;");
                            Ok($"Đã gửi thông báo!\n\nChi nhánh: {coSo}\nNhãn OLS: {ComputeBgdWriteLabel(coSo)}");
                        }
                        else
                        {
                            InsertThongBao(f.NoiDung, f.NgayGio, f.DiaDiem, label);
                            Ok($"Đã gửi thông báo!\n\nChi nhánh: {coSo}\nĐối tượng: {f.SelectedLevel}\nNhãn OLS: {label}");
                        }
                        afterSend?.Invoke();
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
        //  THÔNG TIN CÁ NHÂN (Dùng chung cho NV: DPV, BACSI, KTV, GIAMDOC)
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
    //  DIALOG: Thêm / Sửa Bệnh Nhân (DPV)
    // ════════════════════════════════════════════════════════════════════════

    public sealed class BenhNhanFormValues
    {
        public string TenBN { get; set; }
        public string Phai { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string CCCD { get; set; }
        public string SoNha { get; set; }
        public string TenDuong { get; set; }
        public string QuanHuyen { get; set; }
        public string TinhTP { get; set; }
        public string TienSuBenh { get; set; }
        public string TienSuBenhGD { get; set; }
        public string DiUngThuoc { get; set; }
    }

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

        public BenhNhanAddForm(string maBn, BenhNhanFormValues editValues = null)
        {
            bool isEdit = editValues != null;
            Text = isEdit ? "Cập Nhật Bệnh Nhân" : "Thêm Bệnh Nhân Mới";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(650, 620);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel
            {
                ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top,
                BackColor = UiTheme.JordyBlue, Padding = new Padding(16)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));

            int row = 0;

            Control MakeRequiredLabel(string text)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0), BackColor = Color.Transparent
                };
                panel.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Margin = new Padding(0)
                });
                panel.Controls.Add(new Label
                {
                    Text = "*", AutoSize = true, ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(2, 0, 0, 0)
                });
                return panel;
            }

            void AddOptionalLabel(string text, int r, int height = 46)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                layout.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0)
                }, 0, r);
            }

            void AddRequiredLabel(string text, int r, int height = 46)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                layout.Controls.Add(MakeRequiredLabel(text), 0, r);
            }

            TextBox MakeTxt(string val = "") =>
                new TextBox { Dock = DockStyle.Fill, Text = val, Margin = new Padding(0, 4, 0, 4) };

            AddRequiredLabel("Mã BN:", row);
            var txtMaBN = MakeTxt(maBn ?? "");
            txtMaBN.ReadOnly = true;
            txtMaBN.BackColor = Color.FromArgb(245, 245, 245);
            layout.Controls.Add(txtMaBN, 1, row++);

            AddRequiredLabel("Họ tên:", row);
            var txtTenBN = MakeTxt(editValues?.TenBN ?? "");
            layout.Controls.Add(txtTenBN, 1, row++);

            AddRequiredLabel("Phái:", row);
            var cmbPhai = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 0, 4)
            };
            cmbPhai.Items.AddRange(new object[] { "Nam", "Nữ" });
            cmbPhai.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(editValues?.Phai))
            {
                for (int i = 0; i < cmbPhai.Items.Count; i++)
                    if (string.Equals(cmbPhai.Items[i].ToString(), editValues.Phai, StringComparison.OrdinalIgnoreCase))
                    { cmbPhai.SelectedIndex = i; break; }
            }
            layout.Controls.Add(cmbPhai, 1, row++);

            AddRequiredLabel("Ngày sinh:", row);
            var dtpNgaySinh = new DateTimePicker
            {
                Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy", ShowUpDown = false,
                Margin = new Padding(0, 4, 0, 4),
                Value = editValues?.NgaySinh ?? DateTime.Today.AddYears(-30)
            };
            layout.Controls.Add(dtpNgaySinh, 1, row++);

            AddOptionalLabel("CCCD:", row);
            var txtCCCD = MakeTxt(editValues?.CCCD ?? "");
            layout.Controls.Add(txtCCCD, 1, row++);

            AddOptionalLabel("Số nhà:", row);
            var txtSoNha = MakeTxt(editValues?.SoNha ?? "");
            layout.Controls.Add(txtSoNha, 1, row++);

            AddOptionalLabel("Tên đường:", row);
            var txtTenDuong = MakeTxt(editValues?.TenDuong ?? "");
            layout.Controls.Add(txtTenDuong, 1, row++);

            AddOptionalLabel("Quận/Huyện:", row);
            var txtQuanHuyen = MakeTxt(editValues?.QuanHuyen ?? "");
            layout.Controls.Add(txtQuanHuyen, 1, row++);

            AddOptionalLabel("Tỉnh/TP:", row);
            var txtTinhTP = MakeTxt(editValues?.TinhTP ?? "");
            layout.Controls.Add(txtTinhTP, 1, row++);

            AddOptionalLabel("Tiền sử bệnh:", row, 68);
            var txtTienSuBenh = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, Height = 56,
                ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 4, 0, 4),
                Text = editValues?.TienSuBenh ?? ""
            };
            layout.Controls.Add(txtTienSuBenh, 1, row++);

            AddOptionalLabel("Tiền sử bệnh GĐ:", row, 68);
            var txtTienSuBenhGD = new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, Height = 56,
                ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 4, 0, 4),
                Text = editValues?.TienSuBenhGD ?? ""
            };
            layout.Controls.Add(txtTienSuBenhGD, 1, row++);

            AddOptionalLabel("Dị ứng thuốc:", row);
            var txtDiUngThuoc = MakeTxt(editValues?.DiUngThuoc ?? "");
            layout.Controls.Add(txtDiUngThuoc, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.Controls.Add(new Label
            {
                Text = isEdit
                    ? "Mã BN không được chỉnh sửa."
                    : "Mã BN tự động đề xuất nhỏ nhất chưa dùng (BN000001, BN000002, ...).",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = isEdit ? "Lưu" : "Thêm", Width = 110, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Hủy", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText
            };
            cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtMaBN.Text)) { MessageBox.Show("Mã BN không hợp lệ."); return; }
                if (string.IsNullOrWhiteSpace(txtTenBN.Text)) { MessageBox.Show("Nhập Họ tên."); return; }
                if (cmbPhai.SelectedIndex < 0) { MessageBox.Show("Chọn Phái."); return; }
                if (dtpNgaySinh.Value.Date > DateTime.Today) { MessageBox.Show("Ngày sinh không được ở tương lai."); return; }

                MaBN = txtMaBN.Text.Trim();
                TenBN = txtTenBN.Text.Trim();
                Phai = cmbPhai.SelectedItem.ToString();
                NgaySinh = dtpNgaySinh.Value.Date;
                CCCD = txtCCCD.Text.Trim();
                SoNha = txtSoNha.Text.Trim();
                TenDuong = txtTenDuong.Text.Trim();
                QuanHuyen = txtQuanHuyen.Text.Trim();
                TinhTP = txtTinhTP.Text.Trim();
                TienSuBenh = txtTienSuBenh.Text.Trim();
                TienSuBenhGD = txtTienSuBenhGD.Text.Trim();
                DiUngThuoc = txtDiUngThuoc.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            scroll.Controls.Add(layout);
            Controls.Add(scroll);
            AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Tạo HSBA  (Điều phối viên điền — CHẨNĐOÁN/ĐIỀUTRỊ/KẾTLUẬN do bác sĩ cập nhật sau)
    // ════════════════════════════════════════════════════════════════════════

    public class HsbaAddForm : Form
    {
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        private static readonly LookupItem[] FallbackKhoa =
        {
            new LookupItem("K001", "K001 — Khoa Tiêu hóa"),
            new LookupItem("K002", "K002 — Khoa Thần kinh"),
            new LookupItem("K003", "K003 — Khoa Tim mạch")
        };

        public string MaHSBA { get; private set; }
        public string MaBN { get; private set; }
        public DateTime Ngay { get; private set; }
        public string MaBS { get; private set; }
        public string MaKhoa { get; private set; }
        public string ChanDoan => string.Empty;
        public string DieuTri => string.Empty;
        public string KetLuan => string.Empty;

        private readonly OracleAdminService _service;
        private readonly TextBox _txtMaHsba;
        private readonly ComboBox _cmbBn;
        private readonly ComboBox _cmbKhoa;
        private readonly ComboBox _cmbBs;
        private bool _suppressSync;
        private bool _khoaItemsLoaded;

        private static string SqlLit(string s) => (s ?? "").Replace("'", "''");

        public HsbaAddForm(OracleAdminService service, string suggestedMaHsba)
        {
            _service = service;

            Text = "Tạo Hồ Sơ Bệnh Án Mới";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(540, 380);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(18),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); 

            Control MakeRequiredLabel(string text)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0), BackColor = Color.Transparent
                };
                panel.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Margin = new Padding(0)
                });
                panel.Controls.Add(new Label
                {
                    Text = "*", AutoSize = true, ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(2, 0, 0, 0)
                });
                return panel;
            }

            ComboBox MakeEditableCombo() => new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            int row = 0;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(new Label
            {
                Text = "Mã HSBA:", AutoSize = true, Font = UiTheme.HeaderFont,
                ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 8, 0)
            }, 0, row);
            _txtMaHsba = new TextBox
            {
                Text = suggestedMaHsba ?? "", ReadOnly = true, Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245), Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(_txtMaHsba, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Bệnh nhân:"), 0, row);
            _cmbBn = MakeEditableCombo();
            layout.Controls.Add(_cmbBn, 1, row++);

            

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Bác sĩ:"), 0, row);
            _cmbBs = MakeEditableCombo();
            layout.Controls.Add(_cmbBs, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Khoa:"), 0, row);
            _cmbKhoa = MakeEditableCombo();
            layout.Controls.Add(_cmbKhoa, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "Bác sĩ hoặc Khoa nhập trước đều được — chọn một bên sẽ gợi ý bên kia nếu có dữ liệu.",
                ForeColor = Color.FromArgb(100, 80, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "Chẩn đoán / Điều trị / Kết luận do Y bác sĩ cập nhật sau.",
                ForeColor = Color.FromArgb(100, 80, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "Ngày lập hồ sơ tự động lấy ngày hiện tại khi bấm Tạo HSBA thành công.",
                ForeColor = Color.FromArgb(100, 80, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Tạo HSBA", Width = 120, Height = 36, FlatStyle = FlatStyle.Flat,
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

            _cmbBs.SelectedIndexChanged += (s, e) => { if (!_suppressSync) OnBsInputChanged(false); };
            _cmbBs.Leave += (s, e) => { if (!_suppressSync) OnBsInputChanged(true); };

            _cmbKhoa.SelectedIndexChanged += (s, e) => { if (!_suppressSync) OnKhoaInputChanged(false); };
            _cmbKhoa.Leave += (s, e) => { if (!_suppressSync) OnKhoaInputChanged(true); };

            ok.Click += (s, e) =>
            {
                MaHSBA = (_txtMaHsba.Text ?? "").Trim();
                MaBN = ResolveLookupCode(_cmbBn);
                MaKhoa = ResolveLookupCode(_cmbKhoa);
                MaBS = ResolveLookupCode(_cmbBs);
                if (string.IsNullOrEmpty(MaHSBA)) { MessageBox.Show("Thiếu mã HSBA."); return; }
                if (string.IsNullOrEmpty(MaBN)) { MessageBox.Show("Nhập hoặc chọn mã bệnh nhân."); return; }
                if (string.IsNullOrEmpty(MaKhoa)) { MessageBox.Show("Nhập hoặc chọn khoa."); return; }
                if (string.IsNullOrEmpty(MaBS)) { MessageBox.Show("Nhập hoặc chọn bác sĩ phụ trách."); return; }
                Ngay = DateTime.Today;
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            LoadBenhNhan();
            PopulateKhoaCombo(null);
            PopulateBacSiCombo(null, null);
        }

        private void OnKhoaInputChanged(bool fromLeave)
        {
            string maKhoa = ResolveLookupCode(_cmbKhoa);
            string maBs = ResolveLookupCode(_cmbBs);
            _suppressSync = true;
            PopulateBacSiCombo(maKhoa, maBs);
            _suppressSync = false;
        }

        private void OnBsInputChanged(bool fromLeave)
        {
            string maBs = ResolveLookupCode(_cmbBs);
            if (string.IsNullOrEmpty(maBs)) return;

            string maKhoa = ResolveLookupCode(_cmbKhoa);
            string resolvedKhoa = TryResolveKhoaFromBs(maBs);

            _suppressSync = true;
            if (string.IsNullOrEmpty(maKhoa) && !string.IsNullOrEmpty(resolvedKhoa))
                SelectCombo(_cmbKhoa, resolvedKhoa);

            maKhoa = ResolveLookupCode(_cmbKhoa);
            PopulateBacSiCombo(maKhoa, maBs);
            _suppressSync = false;
        }

        private string TryResolveKhoaFromBs(string maBs)
        {
            if (string.IsNullOrEmpty(maBs)) return null;

            try
            {
                var dt = _service.Query(
                    "SELECT MAKHOA FROM QLBV.NHANVIEN " +
                    $"WHERE MANV = '{SqlLit(maBs)}' AND MAKHOA IS NOT NULL AND ROWNUM = 1");
                if (dt.Rows.Count > 0)
                {
                    string k = dt.Rows[0]["MAKHOA"]?.ToString();
                    if (!string.IsNullOrEmpty(k)) return k;
                }
            }
            catch { }

            try
            {
                var dt = _service.Query(
                    "SELECT MAKHOA FROM QLBV.HSBA " +
                    $"WHERE MABS = '{SqlLit(maBs)}' AND MAKHOA IS NOT NULL AND ROWNUM = 1");
                if (dt.Rows.Count > 0)
                {
                    string k = dt.Rows[0]["MAKHOA"]?.ToString();
                    if (!string.IsNullOrEmpty(k)) return k;
                }
            }
            catch { }

            if (maBs.Length > 2 && maBs.StartsWith("BS", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(maBs.Substring(2), out int num))
            {
                int kIdx = (num - 1) % 3 + 1;
                if (kIdx == 1) return "K001";
                if (kIdx == 2) return "K002";
                if (kIdx == 3) return "K003";
            }

            return null;
        }

        private void LoadBenhNhan()
        {
            _cmbBn.Items.Clear();
            try
            {
                var dt = _service.Query(
                    "SELECT MABN, TENBN FROM QLBV.BENHNHAN ORDER BY MABN FETCH FIRST 500 ROWS ONLY");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MABN"]?.ToString();
                    string ten = r["TENBN"]?.ToString();
                    if (!string.IsNullOrEmpty(code))
                        _cmbBn.Items.Add(new LookupItem(code, code + " — " + ten));
                }
            }
            catch { }
        }

        private void PopulateKhoaCombo(string selectCode)
        {
            if (!_khoaItemsLoaded)
            {
                _cmbKhoa.Items.Clear();
                bool loadedFromDb = false;
                try
                {
                    var dt = _service.Query("SELECT MAKHOA, TENKHOA FROM QLBV.KHOA ORDER BY MAKHOA");
                    if (dt.Rows.Count > 0)
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            string code = r["MAKHOA"]?.ToString();
                            string ten = r["TENKHOA"]?.ToString();
                            if (!string.IsNullOrEmpty(code))
                                _cmbKhoa.Items.Add(new LookupItem(code, code + " — " + ten));
                        }
                        loadedFromDb = _cmbKhoa.Items.Count > 0;
                    }
                }
                catch { }

                if (!loadedFromDb)
                {
                    foreach (var k in FallbackKhoa)
                        _cmbKhoa.Items.Add(k);
                }

                _khoaItemsLoaded = true;
            }

            if (!string.IsNullOrEmpty(selectCode))
                SelectCombo(_cmbKhoa, selectCode);
        }

        private void PopulateBacSiCombo(string maKhoaFilter, string selectCode)
        {
            string preserve = selectCode ?? ResolveLookupCode(_cmbBs);
            _cmbBs.Items.Clear();
            _cmbBs.Enabled = true;

            var added = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBs(string manv, string hoten)
            {
                if (string.IsNullOrEmpty(manv) || !added.Add(manv)) return;
                string label = string.IsNullOrEmpty(hoten) ? manv : manv + " — " + hoten;
                _cmbBs.Items.Add(new LookupItem(manv, label));
            }

            string khoaClause = string.IsNullOrEmpty(maKhoaFilter)
                ? ""
                : $" AND MAKHOA = '{SqlLit(maKhoaFilter)}'";

            try
            {
                var dt = _service.Query(
                    "SELECT MANV, HOTEN FROM QLBV.NHANVIEN " +
                    $"WHERE VAITRO = N'Bác sĩ/Y sĩ'{khoaClause} ORDER BY MANV");
                foreach (DataRow r in dt.Rows)
                    AddBs(r["MANV"]?.ToString(), r["HOTEN"]?.ToString());
            }
            catch { }

            try
            {
                string hsbaSql = string.IsNullOrEmpty(maKhoaFilter)
                    ? "SELECT DISTINCT MABS FROM QLBV.HSBA WHERE MABS IS NOT NULL ORDER BY MABS"
                    : "SELECT DISTINCT MABS FROM QLBV.HSBA " +
                      $"WHERE MAKHOA = '{SqlLit(maKhoaFilter)}' AND MABS IS NOT NULL ORDER BY MABS";
                var dtHsba = _service.Query(hsbaSql);
                foreach (DataRow r in dtHsba.Rows)
                    AddBs(r["MABS"]?.ToString(), null);
            }
            catch { }

            if (_cmbBs.Items.Count == 0)
            {
                if (!string.IsNullOrEmpty(maKhoaFilter))
                {
                    int kIdx = maKhoaFilter == "K001" ? 1 : maKhoaFilter == "K002" ? 2 : maKhoaFilter == "K003" ? 3 : 0;
                    if (kIdx > 0)
                    {
                        for (int i = 1; i <= 52; i++)
                        {
                            if ((i - 1) % 3 + 1 != kIdx) continue;
                            AddBs("BS" + i.ToString("D4"), null);
                        }
                    }
                }
                else
                {
                    for (int i = 1; i <= 52; i++)
                        AddBs("BS" + i.ToString("D4"), null);
                }
            }

            if (!string.IsNullOrEmpty(preserve))
                SelectCombo(_cmbBs, preserve);
        }

        private static bool SelectCombo(ComboBox cmb, string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is LookupItem item &&
                    string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return true;
                }
            }
            if (cmb.DropDownStyle == ComboBoxStyle.DropDown)
                cmb.Text = code;
            return false;
        }

        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Điều phối HSBA — chọn Khoa → Bác sĩ (dropdown)
    // ════════════════════════════════════════════════════════════════════════

    public class HsbaDieuPhoiForm : Form
    {
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        // insert_khoa.sql — DPV thường không có SELECT trên QLBV.KHOA (03.sql chưa grant)
        private static readonly LookupItem[] FallbackKhoa =
        {
            new LookupItem("K001", "K001 — Khoa Tiêu hóa"),
            new LookupItem("K002", "K002 — Khoa Thần kinh"),
            new LookupItem("K003", "K003 — Khoa Tim mạch")
        };

        public string MaKhoa { get; private set; }
        public string MaBS { get; private set; }

        private readonly OracleAdminService _service;
        private readonly ComboBox _cmbKhoa;
        private readonly ComboBox _cmbBs;
        private bool _suppressBsReload;

        private static string SqlLit(string s) => (s ?? "").Replace("'", "''");

        public HsbaDieuPhoiForm(OracleAdminService service, string maHsba, string maKhoaHienTai, string maBsHienTai)
        {
            _service = service;

            Text = "Điều Phối HSBA — " + maHsba;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(520, 280);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Control MakeRequiredLabel(string text)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0), BackColor = Color.Transparent
                };
                panel.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Margin = new Padding(0)
                });
                panel.Controls.Add(new Label
                {
                    Text = "*", AutoSize = true, ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(2, 0, 0, 0)
                });
                return panel;
            }

            int row = 0;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(new Label
            {
                Text = "Mã HSBA:", AutoSize = true, Font = UiTheme.HeaderFont,
                ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 8, 0)
            }, 0, row);
            layout.Controls.Add(new TextBox
            {
                Text = maHsba ?? "", ReadOnly = true, Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 245, 245), Margin = new Padding(0, 4, 0, 4)
            }, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Khoa:"), 0, row);
            _cmbKhoa = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4), AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(_cmbKhoa, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Bác sĩ:"), 0, row);
            _cmbBs = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4), AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(_cmbBs, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.Controls.Add(new Label
            {
                Text = "Chọn từ dropdown hoặc gõ mã (vd. K001, BS0004). Chọn khoa trước để lọc bác sĩ.",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 4, 0, 0)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Lưu", Width = 110, Height = 36, FlatStyle = FlatStyle.Flat,
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

            _cmbKhoa.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressBsReload) return;
                LoadBacSiTheoKhoa(ResolveLookupCode(_cmbKhoa), null);
            };
            _cmbKhoa.Leave += (s, e) =>
            {
                if (_suppressBsReload) return;
                LoadBacSiTheoKhoa(ResolveLookupCode(_cmbKhoa), ResolveLookupCode(_cmbBs));
            };

            ok.Click += (s, e) =>
            {
                MaKhoa = ResolveLookupCode(_cmbKhoa);
                MaBS = ResolveLookupCode(_cmbBs);
                if (string.IsNullOrEmpty(MaKhoa)) { MessageBox.Show("Nhập hoặc chọn khoa."); return; }
                if (string.IsNullOrEmpty(MaBS)) { MessageBox.Show("Nhập hoặc chọn bác sĩ phụ trách."); return; }
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            LoadKhoa(maKhoaHienTai, maBsHienTai);
        }

        private void LoadKhoa(string maKhoaHienTai, string maBsHienTai)
        {
            _suppressBsReload = true;
            _cmbKhoa.Items.Clear();

            bool loadedFromDb = false;
            try
            {
                var dt = _service.Query("SELECT MAKHOA, TENKHOA FROM QLBV.KHOA ORDER BY MAKHOA");
                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string code = r["MAKHOA"]?.ToString();
                        string ten = r["TENKHOA"]?.ToString();
                        if (!string.IsNullOrEmpty(code))
                            _cmbKhoa.Items.Add(new LookupItem(code, code + " — " + ten));
                    }
                    loadedFromDb = _cmbKhoa.Items.Count > 0;
                }
            }
            catch { }

            if (!loadedFromDb)
            {
                foreach (var k in FallbackKhoa)
                    _cmbKhoa.Items.Add(k);
            }

            SelectCombo(_cmbKhoa, maKhoaHienTai);
            _suppressBsReload = false;
            var khoa = _cmbKhoa.SelectedItem as LookupItem;
            LoadBacSiTheoKhoa(khoa?.Code ?? maKhoaHienTai, maBsHienTai);
        }

        private void LoadBacSiTheoKhoa(string maKhoa, string chonMaBs)
        {
            _cmbBs.Items.Clear();
            if (string.IsNullOrEmpty(maKhoa))
            {
                _cmbBs.Enabled = false;
                return;
            }

            _cmbBs.Enabled = true;
            var added = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBs(string manv, string hoten)
            {
                if (string.IsNullOrEmpty(manv) || !added.Add(manv)) return;
                string label = string.IsNullOrEmpty(hoten) ? manv : manv + " — " + hoten;
                _cmbBs.Items.Add(new LookupItem(manv, label));
            }

            // QLBV/ROLE_DPV: thử NHANVIEN (VPD có thể chỉ trả về chính user DPV)
            try
            {
                var dt = _service.Query(
                    "SELECT MANV, HOTEN FROM QLBV.NHANVIEN " +
                    $"WHERE VAITRO = N'Bác sĩ/Y sĩ' AND MAKHOA = '{SqlLit(maKhoa)}' ORDER BY MANV");
                foreach (DataRow r in dt.Rows)
                    AddBs(r["MANV"]?.ToString(), r["HOTEN"]?.ToString());
            }
            catch { }

            // Bổ sung từ HSBA — DPV có SELECT toàn bộ HSBA (fn_vpdHSBA → 1=1)
            try
            {
                var dtHsba = _service.Query(
                    "SELECT DISTINCT MABS FROM QLBV.HSBA " +
                    $"WHERE MAKHOA = '{SqlLit(maKhoa)}' AND MABS IS NOT NULL ORDER BY MABS");
                foreach (DataRow r in dtHsba.Rows)
                    AddBs(r["MABS"]?.ToString(), null);
            }
            catch { }

            // Fallback: bác sĩ seed insert_nhanvien (BSxxxx luân phiên K001/K002/K003)
            if (_cmbBs.Items.Count == 0)
            {
                int kIdx = maKhoa == "K001" ? 1 : maKhoa == "K002" ? 2 : maKhoa == "K003" ? 3 : 0;
                if (kIdx > 0)
                {
                    for (int i = 1; i <= 52; i++)
                    {
                        if ((i - 1) % 3 + 1 != kIdx) continue;
                        AddBs("BS" + i.ToString("D4"), null);
                    }
                }
            }

            if (!SelectCombo(_cmbBs, chonMaBs) && _cmbBs.Items.Count > 0)
                _cmbBs.SelectedIndex = 0;
        }

        private static bool SelectCombo(ComboBox cmb, string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is LookupItem item &&
                    string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return true;
                }
            }
            if (cmb.DropDownStyle == ComboBoxStyle.DropDown)
                cmb.Text = code;
            return false;
        }

        /// <summary>Chọn từ list (LookupItem) hoặc lấy mã từ text tự gõ (K001 / K001 — Khoa …).</summary>
        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
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
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        public string MaHSBA { get; private set; }
        public string LoaiDV { get; private set; }
        public DateTime NgayDV { get; private set; }
        public string MaKTV { get; private set; }

        public HsbaDvAddForm(bool isDoctor = false, OracleAdminService service = null)
        {
            if (isDoctor)
                BuildDoctorUi(service);
            else
                BuildDpvUi(service);
        }

        private void BuildDoctorUi(OracleAdminService service)
        {
            Text = "Bác Sĩ Chỉ Định Dịch Vụ";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(520, 280);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(16),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Control MakeRequiredLabel(string text)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0),
                    BackColor = Color.Transparent
                };
                panel.Controls.Add(new Label
                {
                    Text = text,
                    AutoSize = true,
                    Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue,
                    Margin = new Padding(0)
                });
                panel.Controls.Add(new Label
                {
                    Text = "*",
                    AutoSize = true,
                    ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Margin = new Padding(2, 0, 0, 0)
                });
                return panel;
            }

            int row = 0;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Mã HSBA:"), 0, row);
            var cmbHsba = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(cmbHsba, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Loại dịch vụ:"), 0, row);
            var txtLoaiDv = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            layout.Controls.Add(txtLoaiDv, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Ngày DV:"), 0, row);
            var dtpNgay = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy",
                ShowUpDown = false,
                MinDate = DateTime.Today,
                Value = DateTime.Today,
                Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(dtpNgay, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.Controls.Add(new Label
            {
                Text = "HSBA: chọn hoặc gõ mã hồ sơ bạn phụ trách. Ngày DV không được trước hôm nay.",
                ForeColor = Color.FromArgb(90, 70, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.Controls.Add(new Label
            {
                Text = "KTV do Điều phối viên phân công sau.",
                ForeColor = Color.FromArgb(160, 80, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Chỉ Định",
                Width = 120,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.PastelGreen,
                ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Hủy",
                Width = 90,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.BrandeisBlue,
                ForeColor = UiTheme.WhiteText
            };
            cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                MaHSBA = ResolveLookupCode(cmbHsba);
                LoaiDV = (txtLoaiDv.Text ?? "").Trim();
                if (string.IsNullOrEmpty(MaHSBA)) { MessageBox.Show("Nhập hoặc chọn mã HSBA."); return; }
                if (string.IsNullOrEmpty(LoaiDV)) { MessageBox.Show("Nhập loại dịch vụ."); return; }
                if (dtpNgay.Value.Date < DateTime.Today)
                {
                    MessageBox.Show("Ngày dịch vụ không được trước hôm nay.");
                    dtpNgay.Value = DateTime.Today;
                    return;
                }
                NgayDV = dtpNgay.Value.Date;
                MaKTV = null;
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            if (service != null)
                LoadDoctorHsbaCombo(service, cmbHsba);
        }

        private static void LoadDoctorHsbaCombo(OracleAdminService service, ComboBox cmb)
        {
            cmb.Items.Clear();
            try
            {
                var dt = service.Query(
                    "SELECT MAHSBA, MABN FROM QLBV.HSBA WHERE MABS = USER ORDER BY MAHSBA");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MAHSBA"]?.ToString();
                    string mabn = r["MABN"]?.ToString();
                    if (!string.IsNullOrEmpty(code))
                    {
                        string label = string.IsNullOrEmpty(mabn) ? code : code + " — " + mabn;
                        cmb.Items.Add(new LookupItem(code, label));
                    }
                }
            }
            catch { }
        }

        private static void LoadDpvHsbaCombo(OracleAdminService service, ComboBox cmb)
        {
            cmb.Items.Clear();
            if (service == null) return;
            try
            {
                var dt = service.Query(
                    "SELECT MAHSBA, MABN FROM QLBV.HSBA ORDER BY MAHSBA FETCH FIRST 500 ROWS ONLY");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MAHSBA"]?.ToString();
                    string mabn = r["MABN"]?.ToString();
                    if (!string.IsNullOrEmpty(code))
                    {
                        string label = string.IsNullOrEmpty(mabn) ? code : code + " — " + mabn;
                        cmb.Items.Add(new LookupItem(code, label));
                    }
                }
            }
            catch { }
        }

        private static void LoadDpvKtvCombo(OracleAdminService service, ComboBox cmb, string selectCode = null)
        {
            cmb.Items.Clear();
            if (service == null) return;
            try
            {
                var dt = service.Query(
                    "SELECT DISTINCT MAKTV FROM QLBV.HSBA_DV WHERE MAKTV IS NOT NULL ORDER BY MAKTV");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MAKTV"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(code))
                        cmb.Items.Add(new LookupItem(code, code));
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(selectCode))
            {
                string code = selectCode.Trim();
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (cmb.Items[i] is LookupItem item &&
                        string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                    {
                        cmb.SelectedIndex = i;
                        return;
                    }
                }
                cmb.Text = code;
            }
        }

        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
        }

        private void BuildDpvUi(OracleAdminService service)
        {
            Text = "Điều Phối / Thêm Dịch Vụ";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(500, 320);
            BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Clear();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;
            void AddLabel(string text) { layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F)); layout.Controls.Add(new Label { Text = text, AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 8, 0) }, 0, row); }

            AddLabel("Mã HSBA:");
            var cmbMaHsba = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(cmbMaHsba, 1, row++);

            AddLabel("Loại dịch vụ:");
            var txtLoaiDv = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) }; layout.Controls.Add(txtLoaiDv, 1, row++);

            AddLabel("Ngày DV:");
            var dtpNgay = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", MinDate = DateTime.Today, Margin = new Padding(0, 4, 0, 4) };
            layout.Controls.Add(dtpNgay, 1, row++);

            AddLabel("Mã KTV:");
            var cmbMaKtv = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(cmbMaKtv, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            var nt = new Label
            {
                Text = "HSBA: chọn từ danh sách hoặc gõ mã. KTV: gợi ý DISTINCT từ HSBA_DV (không cần SELECT hết NHANVIEN).",
                ForeColor = Color.FromArgb(90, 70, 0),
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Margin = new Padding(0, 2, 0, 2)
            };
            layout.Controls.Add(nt, 0, row); layout.SetColumnSpan(nt, 2); row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Điều Phối", Width = 120, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) =>
            {
                MaHSBA = ResolveLookupCode(cmbMaHsba);
                LoaiDV = (txtLoaiDv.Text ?? "").Trim();
                MaKTV = ResolveLookupCode(cmbMaKtv);
                if (string.IsNullOrEmpty(MaHSBA)) { MessageBox.Show("Nhập hoặc chọn mã HSBA."); return; }
                if (string.IsNullOrEmpty(LoaiDV)) { MessageBox.Show("Nhập loại dịch vụ."); return; }
                if (string.IsNullOrEmpty(MaKTV)) { MessageBox.Show("Nhập hoặc chọn mã KTV."); return; }
                NgayDV = dtpNgay.Value.Date;
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, row); layout.SetColumnSpan(ft, 2); Controls.Add(layout); AcceptButton = ok;

            LoadDpvHsbaCombo(service, cmbMaHsba);
            LoadDpvKtvCombo(service, cmbMaKtv);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: DPV phân công / đổi MAKTV trên HSBA_DV
    // ════════════════════════════════════════════════════════════════════════
    public class DpvAssignKtvForm : Form
    {
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        public string MaKTV { get; private set; }

        public DpvAssignKtvForm(OracleAdminService service, string loaiDv, string currentMaktv)
        {
            Text = $"Phân công KTV — {loaiDv}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(460, 200);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(new Label
            {
                Text = "Mã KTV:", AutoSize = true, Font = UiTheme.HeaderFont,
                ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 8, 8, 0)
            }, 0, 0);

            var cmbKtv = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(cmbKtv, 1, 0);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.Controls.Add(new Label
            {
                Text = "Chọn từ DISTINCT MAKTV (HSBA_DV) hoặc gõ mã KTV mới.",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                Margin = new Padding(0, 2, 0, 2)
            }, 0, 1);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 1), 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Cập nhật", Width = 110, Height = 36, FlatStyle = FlatStyle.Flat,
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
                MaKTV = ResolveLookupCode(cmbKtv);
                if (string.IsNullOrEmpty(MaKTV)) { MessageBox.Show("Nhập hoặc chọn mã KTV."); return; }
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, 2);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            LoadKtvCombo(service, cmbKtv, currentMaktv);
        }

        private static void LoadKtvCombo(OracleAdminService service, ComboBox cmb, string selectCode)
        {
            cmb.Items.Clear();
            if (service != null)
            {
                try
                {
                    var dt = service.Query(
                        "SELECT DISTINCT MAKTV FROM QLBV.HSBA_DV WHERE MAKTV IS NOT NULL ORDER BY MAKTV");
                    foreach (DataRow r in dt.Rows)
                    {
                        string code = r["MAKTV"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(code))
                            cmb.Items.Add(new LookupItem(code, code));
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(selectCode))
            {
                string code = selectCode.Trim();
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (cmb.Items[i] is LookupItem item &&
                        string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                    {
                        cmb.SelectedIndex = i;
                        return;
                    }
                }
                cmb.Text = code;
            }
        }

        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Thêm Đơn Thuốc
    // ════════════════════════════════════════════════════════════════════════

    public class DonThuocAddForm : Form
    {
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        public string MaHSBA { get; private set; }
        public DateTime NgayDT { get; private set; }
        public string TenThuoc { get; private set; }
        public string LieuDung { get; private set; }

        public DonThuocAddForm(OracleAdminService service = null)
        {
            Text = "Thêm Đơn Thuốc";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(520, 300);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Control MakeRequiredLabel(string text)
            {
                var panel = new FlowLayoutPanel
                {
                    AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0), BackColor = Color.Transparent
                };
                panel.Controls.Add(new Label
                {
                    Text = text, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Margin = new Padding(0)
                });
                panel.Controls.Add(new Label
                {
                    Text = "*", AutoSize = true, ForeColor = Color.Red,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(2, 0, 0, 0)
                });
                return panel;
            }

            int row = 0;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Mã HSBA:"), 0, row);
            var cmbHsba = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            layout.Controls.Add(cmbHsba, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Ngày:"), 0, row);
            var dtpNgay = new DateTimePicker
            {
                Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy", ShowUpDown = false,
                MinDate = DateTime.Today, Value = DateTime.Today,
                Margin = new Padding(0, 4, 0, 4)
            };
            layout.Controls.Add(dtpNgay, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Tên thuốc:"), 0, row);
            var txtTenThuoc = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            layout.Controls.Add(txtTenThuoc, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(MakeRequiredLabel("Liều dùng:"), 0, row);
            var txtLieuDung = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            layout.Controls.Add(txtLieuDung, 1, row++);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layout.Controls.Add(new Label
            {
                Text = "HSBA: chọn hoặc gõ mã hồ sơ bạn phụ trách. Ngày kê đơn không được trước hôm nay.",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Thêm", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat,
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
                MaHSBA = ResolveLookupCode(cmbHsba);
                TenThuoc = (txtTenThuoc.Text ?? "").Trim();
                LieuDung = (txtLieuDung.Text ?? "").Trim();
                if (string.IsNullOrEmpty(MaHSBA)) { MessageBox.Show("Nhập hoặc chọn mã HSBA."); return; }
                if (string.IsNullOrEmpty(TenThuoc)) { MessageBox.Show("Nhập tên thuốc."); return; }
                if (string.IsNullOrEmpty(LieuDung)) { MessageBox.Show("Nhập liều dùng."); return; }
                if (dtpNgay.Value.Date < DateTime.Today)
                {
                    MessageBox.Show("Ngày kê đơn không được trước hôm nay.");
                    dtpNgay.Value = DateTime.Today;
                    return;
                }
                NgayDT = dtpNgay.Value.Date;
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            if (service != null)
                LoadDoctorHsbaCombo(service, cmbHsba);
        }

        private static void LoadDoctorHsbaCombo(OracleAdminService service, ComboBox cmb)
        {
            cmb.Items.Clear();
            try
            {
                var dt = service.Query(
                    "SELECT MAHSBA, MABN FROM QLBV.HSBA WHERE MABS = USER ORDER BY MAHSBA");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MAHSBA"]?.ToString();
                    string mabn = r["MABN"]?.ToString();
                    if (!string.IsNullOrEmpty(code))
                    {
                        string label = string.IsNullOrEmpty(mabn) ? code : code + " — " + mabn;
                        cmb.Items.Add(new LookupItem(code, label));
                    }
                }
            }
            catch { }
        }

        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
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
        public string   SelectedLevel { get; private set; }

        private readonly string _fixedGroupCode;
        private readonly string _fixedCoSoDisplay;
        private readonly bool _bgdMode;

        /// <param name="fixedGroupCode">HCM/HP/HN — khóa cơ sở (Ban Giám đốc)</param>
        /// <param name="fixedCoSoDisplay">Tên hiển thị cơ sở, vd. Hồ Chí Minh</param>
        public ThongBaoForm(string fixedGroupCode = null, string fixedCoSoDisplay = null)
        {
            _fixedGroupCode = fixedGroupCode;
            _fixedCoSoDisplay = fixedCoSoDisplay;
            _bgdMode = !string.IsNullOrEmpty(fixedGroupCode);

            Text = _bgdMode
                ? "Gửi Thông Báo — Ban Giám Đốc"
                : "Gửi Thông Báo — Chọn Nhãn OLS";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(850, 700);
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
            var txN = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = _bgdMode ? "[BGD] Thông báo họp khẩn chi nhánh" : ""
            };
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
            var txL = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = _bgdMode ? "Phòng họp Ban Giám đốc" : ""
            };
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

            // Preset: gửi toàn bộ nhân viên (nhãn NV thuần — 05.sql t1)
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            var btnPresetAllNv = new Button
            {
                Text = _bgdMode ? "📢 Toàn bộ NV chi nhánh" : "📢 Toàn bộ nhân viên (NV)",
                Dock = DockStyle.Fill, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 230, 180),
                ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPresetAllNv.FlatAppearance.BorderSize = 0;
            rightLayout.Controls.Add(btnPresetAllNv);

            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            var btnPresetLdk = new Button
            {
                Text = _bgdMode ? "📢 Lãnh đạo khoa chi nhánh" : "📢 Lãnh đạo khoa (LDK)",
                Dock = DockStyle.Fill, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 240, 255),
                ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPresetLdk.FlatAppearance.BorderSize = 0;
            rightLayout.Controls.Add(btnPresetLdk);

            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            var btnPresetLdp = new Button
            {
                Text = _bgdMode ? "📢 Lãnh đạo phòng chi nhánh" : "📢 Lãnh đạo phòng (LDP)",
                Dock = DockStyle.Fill, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 240, 255),
                ForeColor = UiTheme.DeepBlue,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPresetLdp.FlatAppearance.BorderSize = 0;
            rightLayout.Controls.Add(btnPresetLdp);

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

            if (_bgdMode)
            {
                gbGrp.Text = "Cơ sở (Group) — " + (_fixedCoSoDisplay ?? _fixedGroupCode) + " (cố định)";
                gbGrp.Enabled = false;
                foreach (var cb in cbGrps)
                {
                    string code = cb.Tag.ToString();
                    cb.Enabled = false;
                    cb.Checked = code == _fixedGroupCode;
                }
            }

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

            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            rightLayout.Controls.Add(new Label
            {
                Text = _bgdMode
                    ? "Giám đốc chỉ gửi NV/LDK/LDP trong chi nhánh của mình (nhãn dạng NV::HCM hoặc NV:TH:HCM)."
                    : "Gửi toàn NV: chọn NV + Tất cả khoa/cơ sở → nhãn NV (không phải NV:TH,TK,TM:...).",
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(140, 80, 0),
                Font = new Font("Segoe UI", 8F), AutoSize = false
            });

            rightPanel.Controls.Add(rightLayout);
            outer.Controls.Add(rightPanel, 1, 0);

            // ── Hàm tính và cập nhật preview ─────────────────────────────
            void UpdatePreview()
            {
                string level = "NV";
                foreach (var rb in rbLevels)
                    if (rb.Checked) { level = rb.Tag.ToString(); break; }

                // Compartments
                bool allComp = false;
                var selComps = new System.Collections.Generic.List<string>();
                foreach (var cb in cbComps)
                {
                    if (!cb.Checked) continue;
                    if (cb.Tag.ToString() == "ALL") { allComp = true; break; }
                    selComps.Add(cb.Tag.ToString());
                }

                // Groups
                bool allGrp = false;
                var selGrps = new System.Collections.Generic.List<string>();
                if (_bgdMode)
                    selGrps.Add(_fixedGroupCode);
                else
                {
                    foreach (var cb in cbGrps)
                    {
                        if (!cb.Checked) continue;
                        if (cb.Tag.ToString() == "ALL") { allGrp = true; break; }
                        selGrps.Add(cb.Tag.ToString());
                    }
                }

                // Giám đốc: luôn gắn chi nhánh cố định (06.sql — level::group / level:comp:group)
                if (_bgdMode)
                {
                    if (level == "BGD")
                    {
                        lblPreview.Text = "BGD:TH,TK,TM:" + _fixedGroupCode;
                        return;
                    }

                    string grp = _fixedGroupCode;
                    if (allComp || selComps.Count == 0)
                    {
                        lblPreview.Text = level + "::" + grp;
                        return;
                    }

                    string compPart = string.Join(",", selComps);
                    lblPreview.Text = $"{level}:{compPart}:{grp}";
                    return;
                }

                // BGD level — toàn hệ thống (QLBV)
                if (level == "BGD")
                {
                    lblPreview.Text = "BGD:TH,TK,TM:HCM,HP,HN";
                    return;
                }

                // Chỉ chọn cấp độ (không chọn khoa/cơ sở) → nhãn thuần NV/LDK/LDP (05.sql t1/t3)
                if (!allComp && selComps.Count == 0 && !allGrp && selGrps.Count == 0)
                {
                    lblPreview.Text = level;
                    return;
                }

                // Tất cả khoa — chỉ dùng nhãn đã có trong 01.sql / 05.sql
                if (allComp && (allGrp || selGrps.Count == 0))
                {
                    if (level == "NV") { lblPreview.Text = "NV"; return; }
                    if (level == "LDK") { lblPreview.Text = "LDK"; return; }
                    if (level == "LDP")
                    {
                        lblPreview.Text = !allGrp ? "LDP" : "LDP:TH,TK,TM:HCM,HP,HN";
                        return;
                    }
                }

                // Ghép nhãn theo phạm vi cụ thể
                string compPartAdmin = allComp ? "TH,TK,TM" : (selComps.Count > 0 ? string.Join(",", selComps) : "");
                string grpPartAdmin = allGrp ? "HCM,HP,HN" : (selGrps.Count > 0 ? string.Join(",", selGrps) : "");

                string lbl = level;
                if (!string.IsNullOrEmpty(compPartAdmin) && !string.IsNullOrEmpty(grpPartAdmin))
                    lbl = $"{level}:{compPartAdmin}:{grpPartAdmin}";
                else if (!string.IsNullOrEmpty(compPartAdmin))
                    lbl = $"{level}:{compPartAdmin}";
                else if (!string.IsNullOrEmpty(grpPartAdmin))
                    lbl = $"{level}::{grpPartAdmin}";

                if (compPartAdmin == "TH,TK,TM" && level != "LDP")
                    lbl = level;
                else if (compPartAdmin == "TH,TK,TM" && grpPartAdmin == "HCM,HP,HN" && level == "LDK")
                    lbl = "LDK";

                lblPreview.Text = lbl;
            }

            // Wire up events
            foreach (var rb in rbLevels)
            {
                rb.CheckedChanged += (s, e) =>
                {
                    bool isBgdLevel = rb.Tag.ToString() == "BGD" && rb.Checked;
                    gbComp.Enabled = !isBgdLevel;
                    if (!_bgdMode) gbGrp.Enabled = !isBgdLevel;
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
            if (!_bgdMode)
            {
                foreach (var cb in cbGrps) cb.CheckedChanged += (s, e) =>
                {
                    // "Tất cả cơ sở" mutex
                    if (cb.Tag.ToString() == "ALL" && cb.Checked)
                        foreach (var c in cbGrps) if (c.Tag.ToString() != "ALL") c.Checked = false;
                    else if (cb.Tag.ToString() != "ALL" && cb.Checked)
                        cbGrps[3].Checked = false;
                    UpdatePreview();
                };
            }

            void ApplyBroadcastPreset(int levelIndex)
            {
                rbLevels[levelIndex].Checked = true;
                cbComps[3].Checked = true;
                foreach (var c in cbComps) if (c.Tag.ToString() != "ALL") c.Checked = false;
                if (!_bgdMode)
                {
                    cbGrps[3].Checked = true;
                    foreach (var c in cbGrps) if (c.Tag.ToString() != "ALL") c.Checked = false;
                }
                UpdatePreview();
            }

            btnPresetAllNv.Click += (s, e) => ApplyBroadcastPreset(3);
            btnPresetLdk.Click += (s, e) => ApplyBroadcastPreset(1);
            btnPresetLdp.Click += (s, e) => ApplyBroadcastPreset(2);

            if (_bgdMode)
            {
                // Giám đốc gửi tới NV/LDK/LDP trong chi nhánh — không chọn cơ sở khác
                if (rbLevels[0].Tag.ToString() == "BGD")
                    rbLevels[0].Visible = false;
                rbLevels[3].Checked = true;
            }

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
                SelectedLevel = "NV";
                foreach (var rb in rbLevels)
                    if (rb.Checked && rb.Visible) { SelectedLevel = rb.Tag.ToString(); break; }
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
            Width = 520; BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            int rowCount = fields.Count;
            Height = 110 + (rowCount * 50); // Tự động kéo dài form theo số lượng ô nhập

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

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
    //  DIALOG: Cập Nhật Đơn Thuốc (BS — sửa cả MAHSBA/NGAYDT cho FGA 3.a)
    // ════════════════════════════════════════════════════════════════════════
    public class EditDonThuocForm : Form
    {
        private sealed class LookupItem
        {
            public string Code { get; }
            public string Label { get; }
            public LookupItem(string code, string label) { Code = code; Label = label; }
            public override string ToString() => Label;
        }

        public string OriginalMaHSBA { get; }
        public DateTime OriginalNgayDT { get; }
        public string OriginalTenThuoc { get; }
        public string MaHSBA { get; private set; }
        public DateTime NgayDT { get; private set; }
        public string TenThuoc { get; private set; }
        public string LieuDung { get; private set; }

        public EditDonThuocForm(OracleAdminService service, string maHsba, DateTime ngayDt, string tenThuoc, string lieuDung)
        {
            OriginalMaHSBA = maHsba ?? "";
            OriginalNgayDT = ngayDt.Date;
            OriginalTenThuoc = tenThuoc ?? "";

            Text = "Cập Nhật Đơn Thuốc";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(540, 340);
            BackColor = UiTheme.LightCyan;
            Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16),
                BackColor = UiTheme.JordyBlue
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;

            void AddRow(string label, Control ctrl)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
                layout.Controls.Add(new Label
                {
                    Text = label, AutoSize = true, Font = UiTheme.HeaderFont,
                    ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right,
                    Margin = new Padding(0, 8, 8, 0)
                }, 0, row);
                layout.Controls.Add(ctrl, 1, row++);
            }

            var cmbHsba = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 4, 0, 4),
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            AddRow("Mã HSBA:", cmbHsba);

            var dtpNgay = new DateTimePicker
            {
                Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy", ShowUpDown = false,
                Value = ngayDt.Date, Margin = new Padding(0, 4, 0, 4)
            };
            AddRow("Ngày kê đơn:", dtpNgay);

            var txtTenThuoc = new TextBox
            {
                Dock = DockStyle.Fill, Text = tenThuoc ?? "",
                Margin = new Padding(0, 4, 0, 4)
            };
            AddRow("Tên thuốc:", txtTenThuoc);

            var txtLieuDung = new TextBox
            {
                Dock = DockStyle.Fill, Text = lieuDung ?? "",
                Margin = new Padding(0, 4, 0, 4)
            };
            AddRow("Liều dùng:", txtLieuDung);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.Controls.Add(new Label
            {
                Text = "Thay đổi mã HSBA hoặc ngày kê đơn sẽ được hệ thống ghi nhận trong nhật ký kiểm toán.",
                ForeColor = Color.FromArgb(90, 70, 0), AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), Margin = new Padding(0, 2, 0, 2)
            }, 0, row);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, row), 2);
            row++;

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button
            {
                Text = "Lưu", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat,
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
                MaHSBA = ResolveLookupCode(cmbHsba);
                TenThuoc = (txtTenThuoc.Text ?? "").Trim();
                LieuDung = (txtLieuDung.Text ?? "").Trim();
                NgayDT = dtpNgay.Value.Date;
                if (string.IsNullOrEmpty(MaHSBA)) { MessageBox.Show("Nhập hoặc chọn mã HSBA."); return; }
                if (string.IsNullOrEmpty(TenThuoc)) { MessageBox.Show("Nhập tên thuốc."); return; }
                if (string.IsNullOrEmpty(LieuDung)) { MessageBox.Show("Nhập liều dùng."); return; }
                DialogResult = DialogResult.OK;
                Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok);
            ft.Controls.Add(cn);
            layout.Controls.Add(ft, 0, row);
            layout.SetColumnSpan(ft, 2);

            Controls.Add(layout);
            AcceptButton = ok;

            if (service != null)
                LoadDoctorHsbaCombo(service, cmbHsba);
            SelectCombo(cmbHsba, maHsba);
        }

        private static void LoadDoctorHsbaCombo(OracleAdminService service, ComboBox cmb)
        {
            cmb.Items.Clear();
            try
            {
                var dt = service.Query(
                    "SELECT MAHSBA, MABN FROM QLBV.HSBA WHERE MABS = USER ORDER BY MAHSBA");
                foreach (DataRow r in dt.Rows)
                {
                    string code = r["MAHSBA"]?.ToString();
                    string mabn = r["MABN"]?.ToString();
                    if (!string.IsNullOrEmpty(code))
                        cmb.Items.Add(new LookupItem(code, code + " — " + mabn));
                }
            }
            catch { }
        }

        private static bool SelectCombo(ComboBox cmb, string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i] is LookupItem item &&
                    string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return true;
                }
            }
            cmb.Text = code;
            return false;
        }

        private static string ResolveLookupCode(ComboBox cmb)
        {
            if (cmb.SelectedItem is LookupItem item)
                return item.Code?.Trim();
            string text = (cmb.Text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;
            int sep = text.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? text.Substring(0, sep).Trim() : text;
        }
    }

    // ── DateTimePicker ngày sinh tùy chọn: trống khi mở form, chọn trên lịch, không quá hôm nay ──
    internal static class TaoTaiKhoanUi
    {
        internal static DateTimePicker CreateNgaySinhPicker()
        {
            var dtp = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = " ",
                MinDate = new DateTime(1900, 1, 1),
                MaxDate = DateTime.Today,
                Margin = new Padding(0, 4, 0, 4)
            };
            DateTime valueBeforeDrop = dtp.Value;

            dtp.DropDown += (s, e) => valueBeforeDrop = dtp.Value;
            dtp.CloseUp += (s, e) =>
            {
                if (dtp.Value.Date != valueBeforeDrop.Date)
                    dtp.CustomFormat = "dd/MM/yyyy";
            };
            return dtp;
        }

        internal static bool TryReadNgaySinh(DateTimePicker dtp, out DateTime? ngaySinh, out string error)
        {
            ngaySinh = null;
            error = null;
            if (dtp.CustomFormat == " ")
                return true;
            if (dtp.Value.Date > DateTime.Today)
            {
                error = "Ngày sinh không được ở tương lai.";
                return false;
            }
            ngaySinh = dtp.Value.Date;
            return true;
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
            AddLbl("Ngày sinh:", row);
            var dtpNgaySinh = TaoTaiKhoanUi.CreateNgaySinhPicker();
            layout.Controls.Add(dtpNgaySinh, 1, row++);

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

                if (!TaoTaiKhoanUi.TryReadNgaySinh(dtpNgaySinh, out DateTime? dob, out string dobErr))
                { MessageBox.Show(dobErr); return; }

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
            AddLbl("Ngày sinh:", row);
            var dtpNgaySinh = TaoTaiKhoanUi.CreateNgaySinhPicker();
            layout.Controls.Add(dtpNgaySinh, 1, row++);

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

                if (!TaoTaiKhoanUi.TryReadNgaySinh(dtpNgaySinh, out DateTime? dob, out string dobErr))
                { MessageBox.Show(dobErr); return; }

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
}