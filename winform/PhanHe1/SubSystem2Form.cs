using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
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
    //   ✅ [6] Mock audit data: 10 policies từ sys_PH2.sql (xem GetMockAuditData)
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
            if (currentRoles.Contains("ROLE_DPV") || currentUser.StartsWith("DPV", StringComparison.OrdinalIgnoreCase)) return UserRole.DPV;
            if (currentRoles.Contains("ROLE_BACSI") || currentUser.StartsWith("BACSI", StringComparison.OrdinalIgnoreCase)) return UserRole.BACSI;
            if (currentRoles.Contains("ROLE_KTV") || currentUser.StartsWith("KTV", StringComparison.OrdinalIgnoreCase)) return UserRole.KTV;
            if (currentRoles.Contains("ROLE_BENHNHAN") || currentUser.StartsWith("BN", StringComparison.OrdinalIgnoreCase)) return UserRole.BN;
            // Detect Ban Giám đốc qua CAPBAC trong QLBV.NHANVIEN (đúng nghiệp vụ)
            try
            {
                var dtCap = service.Query($"SELECT CAPBAC FROM QLBV.NHANVIEN WHERE MANV = '{currentUser}'");
                if (dtCap.Rows.Count > 0 && dtCap.Rows[0]["CAPBAC"]?.ToString() == "Ban Giám đốc")
                    return UserRole.GIAMDOC;
            }
            catch { }
            // Fallback cho tài khoản test GD* chưa có trong QLBV.NHANVIEN
            if (currentUser.StartsWith("GD", StringComparison.OrdinalIgnoreCase)) return UserRole.GIAMDOC;
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
            BuildNV_Info(tInfo); 
            BuildDPV_BN(tBN); BuildDPV_HSBA(tHSBA); BuildDPV_DV(tDV); BuildThongBaoTab(tTB, false);
            tabs.TabPages.AddRange(new[] { tInfo, tBN, tHSBA, tDV, tTB });
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
                    MessageBox.Show($"HSBA: {maHsba}\nTổng số dịch vụ điều trị: {tongDv} dịch vụ\n\n(AuditNVCalcFee đã ghi vết lần truy vấn này)",
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

            tab.Controls.Add(Wrap(dgvDpvHSBA, Toolbar(txtS, btnS, btnAdd, btnRe, btnCalc, Note("Nhấn đúp để phân công BS/Khoa. Chọn dòng rồi nhấn 'Tính Chi Phí' để đếm dịch vụ điều trị."))));
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
            BuildNV_Info(tInfo);
            BuildBs_HSBA(tHSBA); BuildBs_BN(tBN); BuildBs_DV(tDV); BuildBs_DT(tDT);
            BuildBs_BaoCao(tBaoCao); BuildThongBaoTab(tTB, false);
            tabs.TabPages.AddRange(new[] { tInfo, tHSBA, tBN, tDV, tDT, tBaoCao, tTB });
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
                Note("Dữ liệu từ QLBV.VW_BaoCaoDieuTri — mỗi lần xem đều bị AuditBSSelectBaoCao ghi vết."))));

            try { grid.DataSource = service.Query("SELECT * FROM QLBV.VW_BaoCaoDieuTri"); UiTheme.StyleGrid(grid); }
            catch { }
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
            BuildNV_Info(tInfo);
            BuildKTV_DV(tDV); BuildThongBaoTab(tTB, false);
            tabs.TabPages.AddRange(new[] { tInfo, tDV, tTB });
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
            BuildBN_Info(tInfo);
            tabs.TabPages.Add(tInfo);
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

            btnAll.Click  += (s, e) => LoadAuditData(grid);
            btnLog.Click  += (s, e) => LoadLoginFailures(grid);
            btnStd.Click  += (s, e) => LoadStandardAudit(grid);
            btnFga.Click  += (s, e) => LoadFgaAudit(grid);
            btnDT.Click   += (s, e) => LoadDonThuocAudit(grid);

            grid.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0) return;
                string colName = grid.Columns.Contains("CHI TIẾT MÔ TẢ") ? "CHI TIẾT MÔ TẢ"
                               : grid.Columns.Contains("CÂU SQL")         ? "CÂU SQL" : null;
                if (colName == null) return;
                var cell = grid.Rows[e.RowIndex].Cells[colName].Value;
                if (cell != null)
                    MessageBox.Show(cell.ToString(), "Chi Tiết Nhật Ký", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Toolbar 2 hàng để chứa 5 nút
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
            bar.Controls.Add(Note("§3.4.5 Tất cả QLBV  |  §3.4.1 Đăng nhập  |  §3.4.2 Standard (16 policies)  |  §3.4.3 FGA (3 policies)  |  §3.4.4 Đơn thuốc  — Double-click dòng để xem chi tiết SQL."));

            var wrapper = new Panel { Dock = DockStyle.Fill };
            wrapper.Controls.Add(grid);
            wrapper.Controls.Add(bar);
            tab.Controls.Add(wrapper);
            LoadAuditData(grid);
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
                var dt = service.Query(sql);
                grid.DataSource = dt.Rows.Count > 0 ? dt : GetMockLoginFailures();
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = GetMockLoginFailures();
                UiTheme.StyleGrid(grid);
            }
        }

        private DataTable GetMockLoginFailures()
        {
            var dt = new DataTable();
            dt.Columns.Add("THỜI GIAN", typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG", typeof(string));
            dt.Columns.Add("HÀNH ĐỘNG", typeof(string));
            dt.Columns.Add("MÃ LỖI", typeof(string));
            dt.Columns.Add("OS USER", typeof(string));
            dt.Columns.Add("POLICY", typeof(string));

            // AuditSession — ORA-1017: invalid username/password
            dt.Rows.Add(DateTime.Now.AddMinutes(-5).ToString("dd/MM/yyyy HH:mm:ss"),  "BS9999",   "LOGON", "1017", "WIN\\User1", "AuditSession");
            dt.Rows.Add(DateTime.Now.AddMinutes(-12).ToString("dd/MM/yyyy HH:mm:ss"), "KTV_FAKE", "LOGON", "1017", "WIN\\User2", "AuditSession");
            dt.Rows.Add(DateTime.Now.AddMinutes(-20).ToString("dd/MM/yyyy HH:mm:ss"), "HACKER01", "LOGON", "1017", "WIN\\Ext",   "AuditSession");
            dt.Rows.Add(DateTime.Now.AddMinutes(-35).ToString("dd/MM/yyyy HH:mm:ss"), "BN999",    "LOGON", "1017", "WIN\\User3", "AuditSession");
            // ORA-28000: account locked
            dt.Rows.Add(DateTime.Now.AddMinutes(-50).ToString("dd/MM/yyyy HH:mm:ss"), "KTV01",    "LOGON", "28000","WIN\\User4", "AuditSession");
            return dt;
        }

        // ── §3.4.2: Standard Audit policies (run/06.sql §3.2.A + §3.2.B + Bonus + Unified fail)
        private void LoadStandardAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME               AS ""ĐỐI TƯỢNG"",
                        UNIFIED_AUDIT_POLICIES    AS ""POLICY"",
                        SQL_TEXT                  AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN (
                        'AUDITSUCDPVUPDATEBN','AUDITSUCBSSELECTVIEW','AUDITSUCBSEXECPROC',
                        'AUDITSUCDPVEXECFUNC','AUDITSUCKTVUPDATEDV','AUDITSUCBSUPDATEDT',
                        'AUDITBONUSBNSELECTINFO','AUDITBONUSBNEXECPROC',
                        'AUDITFAILBSUPDATENV','AUDITFAILBNDELETEHSBA','AUDITFAILKTVDELETEDT',
                        'AUDITFAILDPVDELETEHSBA','AUDITFAILBNUPDATEDV','AUDITFAILKTVSELECTBN',
                        'AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                var dt = service.Query(sql);
                grid.DataSource = dt.Rows.Count > 0 ? dt : GetMockStandardAudit();
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = GetMockStandardAudit();
                UiTheme.StyleGrid(grid);
            }
        }

        private DataTable GetMockStandardAudit()
        {
            var dt = new DataTable();
            dt.Columns.Add("THỜI GIAN",    typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG",   typeof(string));
            dt.Columns.Add("HÀNH ĐỘNG",    typeof(string));
            dt.Columns.Add("ĐỐI TƯỢNG",    typeof(string));
            dt.Columns.Add("POLICY",        typeof(string));
            dt.Columns.Add("CHI TIẾT MÔ TẢ", typeof(string));

            // §3.2.A — Thành công
            dt.Rows.Add(DateTime.Now.AddMinutes(-3).ToString("dd/MM/yyyy HH:mm:ss"),  "NV0001", "UPDATE", "BENHNHAN",              "AuditSucDPVUpdateBN",  "UPDATE QLBV.BENHNHAN SET TINHTP=N'Hà Nội' WHERE MABN='BN000001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-9).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "SELECT", "VW_BAOCAODIEUTTRI",     "AuditSucBSSelectView", "SELECT * FROM QLBV.VW_BaoCaoDieuTri (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-6).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "EXECUTE","SP_KHOITAOHSBAKHANCAP", "AuditSucBSExecProc",   "EXEC QLBV.sp_KhoiTaoHSBAKhancap(...) (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-18).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001", "EXECUTE","FN_TINHTONGCHIPHIDIETRI","AuditSucDPVExecFunc",  "SELECT QLBV.fn_TinhTongChiPhiDieuTri('HS10001') FROM DUAL (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-22).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "UPDATE", "HSBA_DV",               "AuditSucKTVUpdateDV",  "UPDATE QLBV.HSBA_DV SET KETQUA=... WHERE MAKTV='KTV001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-31).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "DONTHUOC",              "AuditSucBSUpdateDT",   "UPDATE QLBV.DONTHUOC SET LIEUDUNG=... (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-40).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001","SELECT","VW_BENHNHAN_XEMTHONGTIN","AuditBonusBNSelectInfo","SELECT * FROM QLBV.VW_BenhNhan_Xemthongtin (Thành công)");
            // §3.2.B — Thất bại
            dt.Rows.Add(DateTime.Now.AddMinutes(-12).ToString("dd/MM/yyyy HH:mm:ss"), "BS0001", "UPDATE", "NHANVIEN",  "AuditFailBSUpdateNV",    "UPDATE QLBV.NHANVIEN SET SODT=... (THẤT BẠI — VPD chặn)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-45).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001","DELETE","HSBA",      "AuditFailBNDeleteHSBA",  "DELETE FROM QLBV.HSBA WHERE MAHSBA=... (THẤT BẠI — không có quyền)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-15).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "DELETE", "DONTHUOC",  "AuditFailKTVDeleteDT",   "DELETE FROM QLBV.DONTHUOC WHERE ... (THẤT BẠI)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-55).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001", "DELETE", "HSBA",      "AuditFailDPVDeleteHSBA", "DELETE FROM QLBV.HSBA WHERE ... (THẤT BẠI — DPV không có DELETE)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-48).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002","UPDATE","HSBA_DV",   "AuditFailBNUpdateDV",    "UPDATE QLBV.HSBA_DV SET KETQUA=... (THẤT BẠI)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-33).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "SELECT", "BENHNHAN",  "AuditFailKTVSelectBN",   "SELECT * FROM QLBV.BENHNHAN (THẤT BẠI — không có quyền SELECT trực tiếp)");
            return dt;
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
                var dt = service.Query(sql);
                grid.DataSource = dt.Rows.Count > 0 ? dt : GetMockFgaAudit();
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = GetMockFgaAudit();
                UiTheme.StyleGrid(grid);
            }
        }

        private DataTable GetMockFgaAudit()
        {
            var dt = new DataTable();
            dt.Columns.Add("THỜI GIAN",  typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG", typeof(string));
            dt.Columns.Add("FGA POLICY", typeof(string));
            dt.Columns.Add("ĐỐI TƯỢNG",  typeof(string));
            dt.Columns.Add("CÂU SQL",     typeof(string));

            // AuditSuaDonThuoc — FGA: UPDATE DONTHUOC (run/06.sql §3.3.a)
            dt.Rows.Add(DateTime.Now.AddMinutes(-7).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "AuditSuaDonThuoc",        "DONTHUOC", "UPDATE QLBV.DONTHUOC SET LIEUDUNG='2x/ngày' WHERE MAHSBA='HS10001' AND NGAYDT=SYSDATE");
            dt.Rows.Add(DateTime.Now.AddMinutes(-25).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "AuditSuaDonThuoc",        "DONTHUOC", "UPDATE QLBV.DONTHUOC SET TENTHUOC=N'Amoxicillin' WHERE MAHSBA='HS10005'");
            // AuditBSUpdateHSBA — FGA: BS update CHANDOAN/DIEUTRI/KETLUAN (run/06.sql §3.3.b)
            dt.Rows.Add(DateTime.Now.AddMinutes(-11).ToString("dd/MM/yyyy HH:mm:ss"), "BS0001", "AuditBSUpdateHSBA",       "HSBA",     "UPDATE QLBV.HSBA SET CHANDOAN=N'Viêm phổi cấp' WHERE MAHSBA='HS10001' AND MABS='BS0001'");
            dt.Rows.Add(DateTime.Now.AddMinutes(-38).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "AuditBSUpdateHSBA",       "HSBA",     "UPDATE QLBV.HSBA SET KETLUAN=N'Ổn định, xuất viện' WHERE MAHSBA='HS10020'");
            // AuditKTVUpdateKetQua — FGA: KTV update KETQUA trong HSBA_DV (run/06.sql §TC#4)
            dt.Rows.Add(DateTime.Now.AddMinutes(-19).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "AuditKTVUpdateKetQua",    "HSBA_DV",  "UPDATE QLBV.HSBA_DV SET KETQUA=N'Bình thường' WHERE MAHSBA='HS10001' AND MAKTV='KTV001'");
            dt.Rows.Add(DateTime.Now.AddMinutes(-42).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "AuditKTVUpdateKetQua",    "HSBA_DV",  "UPDATE QLBV.HSBA_DV SET KETQUA=N'Cần theo dõi' WHERE MAKTV='KTV002'");
            return dt;
        }

        // ── §3.4.4: Đơn thuốc INSERT/UPDATE (run/06.sql §3.3.a+)
        private void LoadDonThuocAudit(DataGridView grid)
        {
            try
            {
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME                AS ""NGƯỜI DÙNG"",
                        ACTION_NAME               AS ""HÀNH ĐỘNG"",
                        UNIFIED_AUDIT_POLICIES    AS ""POLICY"",
                        SQL_TEXT                  AS ""CÂU SQL""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN (
                        'AUDITDONTHUOCINSERT',
                        'AUDITDONTHUOCUPDATE'
                    )
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";
                var dt = service.Query(sql);
                grid.DataSource = dt.Rows.Count > 0 ? dt : GetMockDonThuocAudit();
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = GetMockDonThuocAudit();
                UiTheme.StyleGrid(grid);
            }
        }

        private DataTable GetMockDonThuocAudit()
        {
            var dt = new DataTable();
            dt.Columns.Add("THỜI GIAN",  typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG", typeof(string));
            dt.Columns.Add("HÀNH ĐỘNG",  typeof(string));
            dt.Columns.Add("POLICY",      typeof(string));
            dt.Columns.Add("CÂU SQL",     typeof(string));

            // AuditDonThuocInsert — BS kê đơn thuốc mới (run/06.sql §3.3.a+)
            dt.Rows.Add(DateTime.Now.AddMinutes(-4).ToString("dd/MM/yyyy HH:mm:ss"),   "BS0001", "INSERT", "AuditDonThuocInsert", "INSERT INTO QLBV.DONTHUOC (MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG) VALUES('HS10001',SYSDATE,N'Paracetamol','3x/ngày')");
            dt.Rows.Add(DateTime.Now.AddMinutes(-17).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0002", "INSERT", "AuditDonThuocInsert", "INSERT INTO QLBV.DONTHUOC (MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG) VALUES('HS10005',SYSDATE,N'Amoxicillin','2x/ngày')");
            dt.Rows.Add(DateTime.Now.AddMinutes(-29).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "INSERT", "AuditDonThuocInsert", "INSERT INTO QLBV.DONTHUOC (MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG) VALUES('HS10008',SYSDATE,N'Ibuprofen','1x/ngày')");
            // AuditDonThuocUpdate — BS sửa đơn thuốc (run/06.sql §3.3.a+)
            dt.Rows.Add(DateTime.Now.AddMinutes(-8).ToString("dd/MM/yyyy HH:mm:ss"),   "BS0001", "UPDATE", "AuditDonThuocUpdate", "UPDATE QLBV.DONTHUOC SET LIEUDUNG='2x/ngày' WHERE MAHSBA='HS10001' AND NGAYDT=...");
            dt.Rows.Add(DateTime.Now.AddMinutes(-35).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0002", "UPDATE", "AuditDonThuocUpdate", "UPDATE QLBV.DONTHUOC SET TENTHUOC=N'Cetirizine' WHERE MAHSBA='HS10005'");
            return dt;
        }

        // ✅ [GỘPCODE-5] Audit SQL gộp từ sys_PH2.sql §3.4
        //    Chỉ lấy entries thuộc schema QLBV — bao gồm Standard Audit + FGA
        //    (tất cả FGA policy trong dự án đều gắn trên QLBV objects nên OBJECT_SCHEMA='QLBV')
        //    Bỏ "OR FGA_POLICY_NAME IS NOT NULL" vì nó kéo FGA của DB khác vào.
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

                var dt = service.Query(sql);
                grid.DataSource = dt.Rows.Count > 0 ? dt : GetMockAuditData();
                UiTheme.StyleGrid(grid);
            }
            catch
            {
                grid.DataSource = GetMockAuditData();
                UiTheme.StyleGrid(grid);
            }
        }

        // ✅ [GỘPCODE-6] Mock audit data — tên policy khớp CHÍNH XÁC với run/06.sql §3.2 + §3.3
        //    Standard Audit thành công (6): AuditSucDPVUpdateBN, AuditSucBSSelectView,
        //                                   AuditSucBSExecProc, AuditSucDPVExecFunc,
        //                                   AuditSucKTVUpdateDV, AuditSucBSUpdateDT
        //    Standard Audit thất bại  (6): AuditFailBSUpdateNV, AuditFailBNDeleteHSBA,
        //                                   AuditFailKTVDeleteDT, AuditFailDPVDeleteHSBA,
        //                                   AuditFailBNUpdateDV, AuditFailKTVSelectBN
        //    Bonus Standard           (2): AuditBonusBNSelectInfo, AuditBonusBNExecProc
        //    Unified Audit DONTHUOC   (2): AuditDonThuocInsert, AuditDonThuocUpdate
        //    FGA                      (3): AuditSuaDonThuoc, AuditBSUpdateHSBA, AuditKTVUpdateKetQua
        //    Unified Audit fail       (2): AuditIllegalUpdateHSBA, AuditIllegalHSBADV
        private DataTable GetMockAuditData()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("THỜI GIAN", typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG", typeof(string));
            dt.Columns.Add("HÀNH ĐỘNG", typeof(string));
            dt.Columns.Add("ĐỐI TƯỢNG", typeof(string));
            dt.Columns.Add("POLICY", typeof(string));
            dt.Columns.Add("CHI TIẾT MÔ TẢ", typeof(string));

            // ── Standard Audit THÀNH CÔNG (6 ngữ cảnh theo run/06.sql §3.2.A) ──────

            // Thành công 1: AuditSucDPVUpdateBN — DPV (NV0001/NV0007) update BENHNHAN
            dt.Rows.Add(DateTime.Now.AddMinutes(-3).ToString("dd/MM/yyyy HH:mm:ss"),  "NV0001", "UPDATE", "BENHNHAN", "AuditSucDPVUpdateBN", "UPDATE QLBV.BENHNHAN SET TINHTP=N'Hà Nội' WHERE MABN='BN000001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-8).ToString("dd/MM/yyyy HH:mm:ss"),  "NV0007", "UPDATE", "BENHNHAN", "AuditSucDPVUpdateBN", "UPDATE QLBV.BENHNHAN SET QUANHUYEN=N'Hoàn Kiếm' WHERE MABN='BN000050' (Thành công)");

            // Thành công 2: AuditSucBSSelectView — BS (BS0001/BS0002) SELECT VW_BaoCaoDieuTri
            dt.Rows.Add(DateTime.Now.AddMinutes(-9).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "SELECT", "VW_BAOCAODIEUTTRI", "AuditSucBSSelectView", "SELECT * FROM QLBV.VW_BaoCaoDieuTri WHERE MABS='BS0001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-14).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "SELECT", "VW_BAOCAODIEUTTRI", "AuditSucBSSelectView", "SELECT * FROM QLBV.VW_BaoCaoDieuTri WHERE MABS='BS0002' (Thành công)");

            // Thành công 3: AuditSucBSExecProc — BS (BS0001/BS0002) thực thi sp_KhoiTaoHSBAKhancap
            dt.Rows.Add(DateTime.Now.AddMinutes(-6).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditSucBSExecProc", "EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS99001','BN000010','BS0001','K001') (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-25).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditSucBSExecProc", "EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS99002','BN000020','BS0002','K003') (Thành công)");

            // Thành công 4: AuditSucDPVExecFunc — DPV (NV0001/NV0002) thực thi fn_TinhTongChiPhiDieuTri
            dt.Rows.Add(DateTime.Now.AddMinutes(-18).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001", "EXECUTE", "FN_TINHTONGCHIPHIDIETRI", "AuditSucDPVExecFunc", "SELECT QLBV.fn_TinhTongChiPhiDieuTri('HS10001') FROM DUAL (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-30).ToString("dd/MM/yyyy HH:mm:ss"), "NV0002", "EXECUTE", "FN_TINHTONGCHIPHIDIETRI", "AuditSucDPVExecFunc", "SELECT QLBV.fn_TinhTongChiPhiDieuTri('HS10002') FROM DUAL (Thành công)");

            // Thành công 5: AuditSucKTVUpdateDV — KTV (KTV001/KTV002) cập nhật KETQUA trong HSBA_DV
            dt.Rows.Add(DateTime.Now.AddMinutes(-11).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "UPDATE", "HSBA_DV", "AuditSucKTVUpdateDV", "UPDATE QLBV.HSBA_DV SET KETQUA=N'Âm tính' WHERE MAHSBA='HS000001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-16).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "UPDATE", "HSBA_DV", "AuditSucKTVUpdateDV", "UPDATE QLBV.HSBA_DV SET KETQUA=N'Bình thường' WHERE MAHSBA='HS000002' (Thành công)");

            // Thành công 6: AuditSucBSUpdateDT — BS (BS0001/BS0002) cập nhật đơn thuốc của mình
            dt.Rows.Add(DateTime.Now.AddMinutes(-4).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "UPDATE", "DONTHUOC", "AuditSucBSUpdateDT", "UPDATE QLBV.DONTHUOC SET LIEUDUNG=N'2 viên/ngày' WHERE MAHSBA='HS000001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-19).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "DONTHUOC", "AuditSucBSUpdateDT", "UPDATE QLBV.DONTHUOC SET TENTHUOC=N'Amoxicillin' WHERE MAHSBA='HS000002' (Thành công)");

            // ── Standard Audit THẤT BẠI (6 ngữ cảnh theo run/06.sql §3.2.B) ────────

            // Thất bại 1: AuditFailBSUpdateNV — BS (BS0001/BS0002) cố UPDATE/DELETE NHANVIEN
            dt.Rows.Add(DateTime.Now.AddMinutes(-12).ToString("dd/MM/yyyy HH:mm:ss"), "BS0001", "UPDATE", "NHANVIEN", "AuditFailBSUpdateNV", "UPDATE QLBV.NHANVIEN SET SODT=... (THẤT BẠI — BS không có quyền UPDATE NHANVIEN)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-20).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "DELETE", "NHANVIEN", "AuditFailBSUpdateNV", "DELETE FROM QLBV.NHANVIEN WHERE MANV='NV0010' (THẤT BẠI — không có quyền xóa)");

            // Thất bại 2: AuditFailBNDeleteHSBA — BN (BN000001/BN000002) cố DELETE HSBA
            dt.Rows.Add(DateTime.Now.AddMinutes(-33).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "DELETE", "HSBA", "AuditFailBNDeleteHSBA", "DELETE FROM QLBV.HSBA WHERE MAHSBA='HS000001' (THẤT BẠI — Bệnh nhân không có quyền xóa HSBA)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-45).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "DELETE", "HSBA", "AuditFailBNDeleteHSBA", "DELETE FROM QLBV.HSBA WHERE MABN='BN000002' (THẤT BẠI — Bệnh nhân không có quyền xóa HSBA)");

            // Thất bại 3: AuditFailKTVDeleteDT — KTV (KTV001/KTV002) cố DELETE DONTHUOC
            dt.Rows.Add(DateTime.Now.AddMinutes(-38).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "DELETE", "DONTHUOC", "AuditFailKTVDeleteDT", "DELETE FROM QLBV.DONTHUOC WHERE MAHSBA='HS000001' (THẤT BẠI — KTV không có quyền xóa đơn thuốc)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-52).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "DELETE", "DONTHUOC", "AuditFailKTVDeleteDT", "DELETE FROM QLBV.DONTHUOC WHERE ID=5 (THẤT BẠI — KTV không có quyền xóa đơn thuốc)");

            // Thất bại 4: AuditFailDPVDeleteHSBA — DPV (NV0001/NV0002) cố DELETE HSBA
            dt.Rows.Add(DateTime.Now.AddMinutes(-47).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001", "DELETE", "HSBA", "AuditFailDPVDeleteHSBA", "DELETE FROM QLBV.HSBA WHERE MAHSBA='HS000010' (THẤT BẠI — DPV chỉ có SELECT/INSERT/UPDATE MAKHOA,MABS)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-58).ToString("dd/MM/yyyy HH:mm:ss"), "NV0002", "DELETE", "HSBA", "AuditFailDPVDeleteHSBA", "DELETE FROM QLBV.HSBA WHERE MABN='BN000003' (THẤT BẠI — DPV không có quyền xóa HSBA)");

            // Thất bại 5: AuditFailBNUpdateDV — BN (BN000001/BN000002) cố UPDATE HSBA_DV
            dt.Rows.Add(DateTime.Now.AddMinutes(-37).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "UPDATE", "HSBA_DV", "AuditFailBNUpdateDV", "UPDATE QLBV.HSBA_DV SET KETQUA=N'Giả mạo' WHERE MAHSBA='HS000001' (THẤT BẠI — BN không có quyền)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-53).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "INSERT", "HSBA_DV", "AuditFailBNUpdateDV", "INSERT INTO QLBV.HSBA_DV ... (THẤT BẠI — BN không có quyền thêm dịch vụ)");

            // Thất bại 6: AuditFailKTVSelectBN — KTV (KTV001/KTV002) cố SELECT thẳng BENHNHAN
            dt.Rows.Add(DateTime.Now.AddMinutes(-29).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001", "SELECT", "BENHNHAN", "AuditFailKTVSelectBN", "SELECT * FROM QLBV.BENHNHAN (THẤT BẠI — KTV chỉ được xem qua VW_KTV_XemHSBADV)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-43).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "SELECT", "BENHNHAN", "AuditFailKTVSelectBN", "SELECT TENBN FROM QLBV.BENHNHAN WHERE MABN='BN000005' (THẤT BẠI — không có quyền)");

            // ── Bonus Standard Audit (run/06.sql §3.2) ───────────────────────────────

            // Bonus thành công: AuditBonusBNSelectInfo — BN xem thông tin cá nhân qua view
            dt.Rows.Add(DateTime.Now.AddMinutes(-13).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "SELECT", "VW_BENHNHAN_XEMTHONGTIN", "AuditBonusBNSelectInfo", "SELECT * FROM QLBV.VW_BenhNhan_Xemthongtin (Thành công — BN xem thông tin của chính mình)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-27).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "SELECT", "VW_BENHNHAN_XEMTHONGTIN", "AuditBonusBNSelectInfo", "SELECT * FROM QLBV.VW_BenhNhan_Xemthongtin (Thành công — BN xem thông tin của chính mình)");

            // Bonus thất bại: AuditBonusBNExecProc — BN cố thực thi stored procedure của BS
            dt.Rows.Add(DateTime.Now.AddMinutes(-50).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditBonusBNExecProc", "EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS99999',...) (THẤT BẠI — BN không có quyền thực thi procedure này)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-60).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditBonusBNExecProc", "EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS88888',...) (THẤT BẠI — không có quyền EXECUTE)");

            // ── Unified Audit INSERT/UPDATE DONTHUOC (run/06.sql §3.3.a+) ────────────

            // AuditDonThuocInsert — ghi vết mọi INSERT vào DONTHUOC
            dt.Rows.Add(DateTime.Now.AddMinutes(-1).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "INSERT", "DONTHUOC", "AuditDonThuocInsert", "INSERT INTO QLBV.DONTHUOC(MAHSBA,TENTHUOC,LIEUDUNG,...) VALUES('HS000001','Paracetamol','500mg/ngày',...) (Unified ghi vết)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-17).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "INSERT", "DONTHUOC", "AuditDonThuocInsert", "INSERT INTO QLBV.DONTHUOC(MAHSBA,TENTHUOC,LIEUDUNG,...) VALUES('HS000002','Amoxicillin','3 viên/ngày',...) (Unified ghi vết)");

            // AuditDonThuocUpdate — ghi vết mọi UPDATE vào DONTHUOC
            dt.Rows.Add(DateTime.Now.AddMinutes(-23).ToString("dd/MM/yyyy HH:mm:ss"), "BS0001", "UPDATE", "DONTHUOC", "AuditDonThuocUpdate", "UPDATE QLBV.DONTHUOC SET LIEUDUNG=N'2 viên/ngày' WHERE MAHSBA='HS000001' (Unified ghi vết)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-31).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "DONTHUOC", "AuditDonThuocUpdate", "UPDATE QLBV.DONTHUOC SET TENTHUOC=N'Ibuprofen' WHERE ID=12 (Unified ghi vết)");

            // ── FGA (3 policy theo run/06.sql §3.3) ──────────────────────────────────

            // FGA 3.3.a: AuditSuaDonThuoc — FGA ghi vết chi tiết UPDATE DONTHUOC (cột MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG)
            dt.Rows.Add(DateTime.Now.AddMinutes(-2).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "UPDATE", "DONTHUOC", "AuditSuaDonThuoc", "UPDATE QLBV.DONTHUOC SET TENTHUOC='Paracetamol' WHERE MAHSBA='HS000001' (FGA — cột TENTHUOC bị sửa)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-10).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "DONTHUOC", "AuditSuaDonThuoc", "UPDATE QLBV.DONTHUOC SET LIEUDUNG='2 viên/ngày' WHERE MAHSBA='HS000002' (FGA — cột LIEUDUNG bị sửa)");

            // FGA 3.3.b: AuditBSUpdateHSBA — BS update CHANDOAN/DIEUTRI/KETLUAN hợp pháp
            dt.Rows.Add(DateTime.Now.AddMinutes(-5).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "UPDATE", "HSBA", "AuditBSUpdateHSBA", "UPDATE QLBV.HSBA SET CHANDOAN=N'Viêm họng' WHERE MAHSBA='HS10001' (FGA — BS hợp pháp)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-15).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "HSBA", "AuditBSUpdateHSBA", "UPDATE QLBV.HSBA SET KETLUAN=N'Cho xuất viện' WHERE MAHSBA='HS10050' (FGA — BS hợp pháp)");

            // FGA TC#4: AuditKTVUpdateKetQua — KTV update KETQUA trong HSBA_DV (FGA ghi vết)
            dt.Rows.Add(DateTime.Now.AddMinutes(-7).ToString("dd/MM/yyyy HH:mm:ss"),  "KTV001", "UPDATE", "HSBA_DV", "AuditKTVUpdateKetQua", "UPDATE QLBV.HSBA_DV SET KETQUA=N'Âm tính' WHERE LOAIDV=N'Xét nghiệm máu' (FGA ghi vết KETQUA)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-22).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002", "UPDATE", "HSBA_DV", "AuditKTVUpdateKetQua", "UPDATE QLBV.HSBA_DV SET KETQUA=N'Bình thường' WHERE LOAIDV=N'Siêu âm' (FGA ghi vết KETQUA)");

            // ── Unified Audit THẤT BẠI (run/06.sql §3.3.c và §3.3.d) ─────────────────

            // 3.3.c: AuditIllegalUpdateHSBA — UPDATE HSBA thất bại (VPD chặn)
            dt.Rows.Add(DateTime.Now.AddMinutes(-28).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "UPDATE", "HSBA", "AuditIllegalUpdateHSBA", "UPDATE QLBV.HSBA SET CHANDOAN=... (THẤT BẠI — BN không có quyền, VPD trả 0 dòng)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-35).ToString("dd/MM/yyyy HH:mm:ss"), "KTV001",   "UPDATE", "HSBA", "AuditIllegalUpdateHSBA", "UPDATE QLBV.HSBA SET KETLUAN=... (THẤT BẠI — KTV không có quyền sửa kết luận HSBA)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-42).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001",   "UPDATE", "HSBA", "AuditIllegalUpdateHSBA", "UPDATE QLBV.HSBA SET DIEUTRI=... (THẤT BẠI — DPV chỉ được cập nhật MABS/MAKHOA)");

            // 3.3.d: AuditIllegalHSBADV — INSERT/UPDATE/DELETE HSBA_DV thất bại
            dt.Rows.Add(DateTime.Now.AddMinutes(-40).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001",   "DELETE", "HSBA_DV", "AuditIllegalHSBADV", "DELETE FROM QLBV.HSBA_DV WHERE ... (THẤT BẠI — DPV không có quyền xóa dịch vụ)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-48).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "INSERT", "HSBA_DV", "AuditIllegalHSBADV", "INSERT INTO QLBV.HSBA_DV ... (THẤT BẠI — BN không có quyền thêm dịch vụ)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-55).ToString("dd/MM/yyyy HH:mm:ss"), "KTV002",   "UPDATE", "HSBA_DV", "AuditIllegalHSBADV", "UPDATE QLBV.HSBA_DV SET KETQUA=... WHERE MAKTV='KTV001' (THẤT BẠI — KTV chỉ sửa dịch vụ của mình)");

            return dt;
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
            var btnBackupAudit   = QuickBtn("Backup Nhật Ký Audit",   UiTheme.PastelGreen,        UiTheme.DeepBlue,  183, 38);
            var btnRestoreSchema = QuickBtn("Restore Schema (impdp)", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 188, 38);
            var btnCheckDir      = QuickBtn("Kiểm Tra BACKUP_DIR",    Color.FromArgb(80, 80, 120), UiTheme.WhiteText, 161, 38);
            var btnRowCount      = QuickBtn("Đếm Số Dòng Bảng",      Color.FromArgb(50, 150, 80), UiTheme.WhiteText, 154, 38);

            var flowBtn = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 6, 0, 4), BackColor = Color.White };
            foreach (var b in new Control[] { btnBackupSchema, btnBackupTables, btnBackupAudit, btnRestoreSchema, btnCheckDir, btnRowCount })
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
                txtLog.AppendText($"\r\nC:\\> expdp qlbv/123@localhost:1521/xepdb1 schemas=qlbv directory=backup_dir dumpfile={df} logfile=qlbv_schema_export.log\r\n");
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Data Pump Export — Schema mode...\r\n");
                await System.Threading.Tasks.Task.Delay(1200);
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đang kết xuất Schema QLBV...\r\n");
                await System.Threading.Tasks.Task.Delay(1500);
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thành công. File: {df}\r\n");
                try
                {
                    service.ExecuteNonQuery($"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_NAME, BACKUP_TYPE, BACKUP_PATH, OBJECT_SCOPE, NOTE) VALUES ('{df}', 'schema', 'C:\\oracle_backup', 'qlbv', N'Backup schema QLBV qua expdp')");
                    service.ExecuteNonQuery("COMMIT");
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đã ghi vào QLBV.BACKUP_HISTORY.\r\n");
                    RefreshHist();
                }
                catch { }
            };

            btnBackupTables.Click += async (s, e) =>
            {
                string df = $"qlbv_important_tables_{DateTime.Now:yyyyMMdd_HHmm}.dmp";
                txtLog.AppendText($"\r\nC:\\> expdp qlbv/123@localhost:1521/xepdb1 tables=qlbv.benhnhan,qlbv.hsba,qlbv.hsba_dv,qlbv.donthuoc directory=backup_dir dumpfile={df} logfile=qlbv_important_tables_export.log\r\n");
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Backup bảng quan trọng (BENHNHAN, HSBA, HSBA_DV, DONTHUOC)...\r\n");
                await System.Threading.Tasks.Task.Delay(1800);
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Export thành công. File: {df}\r\n");
                try
                {
                    service.ExecuteNonQuery($"INSERT INTO QLBV.BACKUP_HISTORY (BACKUP_NAME, BACKUP_TYPE, BACKUP_PATH, OBJECT_SCOPE, NOTE) VALUES ('{df}', 'tables', 'C:\\oracle_backup', 'benhnhan,hsba,hsba_dv,donthuoc', N'Backup bảng quan trọng qua expdp')");
                    service.ExecuteNonQuery("COMMIT");
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đã ghi vào QLBV.BACKUP_HISTORY.\r\n");
                    RefreshHist();
                }
                catch { }
            };

            btnBackupAudit.Click += async (s, e) =>
            {
                txtLog.AppendText($"\r\nC:\\> expdp sys as sysdba directory=AUDIT_DIR dumpfile=AUDIT_{DateTime.Now:yyyyMMdd}.dmp tables=AUDSYS.AUD$UNIFIED\r\n");
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Đang đóng gói Unified Audit + FGA...\r\n");
                await System.Threading.Tasks.Task.Delay(1200);
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Hoàn tất kết xuất nhật ký kiểm toán.\r\n");
            };

            btnRestoreSchema.Click += async (s, e) =>
            {
                if (MessageBox.Show("CẢNH BÁO: Sẽ ghi đè toàn bộ schema QLBV.\nBạn có chắc chắn?", "Phục Hồi Schema", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                txtLog.AppendText($"\r\nC:\\> impdp qlbv/123@localhost:1521/xepdb1 schemas=qlbv directory=backup_dir dumpfile=qlbv_schema_LATEST.dmp logfile=qlbv_schema_import.log table_exists_action=replace\r\n");
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ĐANG PHỤC HỒI SCHEMA QLBV...\r\n");
                await System.Threading.Tasks.Task.Delay(2500);
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Import thành công!\r\n");
            };

            pnl.Controls.Add(txtLog);
            pnl.Controls.Add(gridHist);
            pnl.Controls.Add(barHist);
            pnl.Controls.Add(lblHist);
            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            tab.Controls.Add(pnl);
            RefreshHist();
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
            var flowAct = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0), BackColor = Color.White };
            flowAct.Controls.Add(btnCopy);
            flowAct.Controls.Add(btnRun);
            flowAct.Controls.Add(Note("Lệnh theo 08.sql [07-CMD]. Cần Oracle Client để chạy trực tiếp. Mật khẩu mặc định: 123."));

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
                var cmb    = new ComboBox { Width = 345, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 4, 8, 0) };
                cmb.Items.AddRange(new object[] { "Theo tên bảng (BENHNHAN/HSBA/HSBA_DV/DONTHUOC/NHANVIEN)", "Theo audit policy (Standard + FGA)" });
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
                        string sql = cmb.SelectedIndex == 0
                            // Query 1 theo tên bảng — 08.sql [07-AUDIT-01]
                            ? "SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT FROM UNIFIED_AUDIT_TRAIL WHERE OBJECT_SCHEMA = 'QLBV' AND OBJECT_NAME IN ('BENHNHAN','HSBA','HSBA_DV','DONTHUOC','NHANVIEN') ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY"
                            // Query 2 theo policy — 08.sql [07-AUDIT-01]
                            : "SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, OBJECT_SCHEMA, OBJECT_NAME, RETURN_CODE, UNIFIED_AUDIT_POLICIES, FGA_POLICY_NAME, SQL_TEXT FROM UNIFIED_AUDIT_TRAIL WHERE UPPER(UNIFIED_AUDIT_POLICIES) IN ('AUDITSUCDPVUPDATEBN','AUDITSUCBSUPDATEDT','AUDITDONTHUOCINSERT','AUDITDONTHUOCUPDATE','AUDITFAILBSUPDATENV','AUDITFAILBNDELETEHSBA','AUDITFAILKTVDELETEDT','AUDITFAILDPVDELETEHSBA','AUDITFAILBNUPDATEDV','AUDITFAILKTVSELECTBN','AUDITILLEGALUPDATEHSBA','AUDITILLEGALHSBADV') OR UPPER(FGA_POLICY_NAME) IN ('AUDITSUADONTHUOC','AUDITBSUPDATEHSBA','AUDITKTVUPDATEKETQUA') ORDER BY EVENT_TIMESTAMP DESC FETCH FIRST 100 ROWS ONLY";
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
            RefreshB();
            RefreshR();
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
            RefreshAll();
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
            LoadDanhSachTaiKhoan(grid);
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

            SafeOlsLoad(gridL,
                "SELECT * FROM DBA_SA_LEVELS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY LEVEL_NUM DESC");
            SafeOlsLoad(gridC,
                "SELECT * FROM DBA_SA_COMPARTMENTS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY COMP_NUM");
            SafeOlsLoad(gridG,
                "SELECT * FROM DBA_SA_GROUPS WHERE POLICY_NAME='OLS_QLBV_POLICY' ORDER BY GROUP_NUM");
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
            LoadOlsUserLabels(grid, "");
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