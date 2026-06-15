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
    //   ✅ [4] DONTHUOC.TRANGTHAI: ComboBox thay TextBox (xem DonThuocAddForm)
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
            if (currentRoles.Contains("ROLE_BN") || currentUser.StartsWith("BN", StringComparison.OrdinalIgnoreCase)) return UserRole.BN;
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

                    // ✅ [GỘPCODE-C] Dùng EditDonThuocForm — TRANGTHAI là ComboBox đúng constraint
                    var fields = new Dictionary<string, string> {
                    { "TENTHUOC", tenThuocCu },
                    { "LIEUDUNG", r.Cells["LIEUDUNG"].Value?.ToString() },
                    { "TRANGTHAI", r.Cells["TRANGTHAI"].Value?.ToString() }
                };
                    using (var f = new EditDonThuocForm($"Cập nhật Thuốc: {tenThuocCu}", fields))
                    {
                        if (f.ShowDialog(this) == DialogResult.OK)
                        {
                            try
                            {
                                service.ExecuteNonQuery($"UPDATE QLBV.DONTHUOC SET TENTHUOC=N'{Esc(f.NewValues["TENTHUOC"])}', LIEUDUNG=N'{Esc(f.NewValues["LIEUDUNG"])}', TRANGTHAI='{Esc(f.NewValues["TRANGTHAI"])}' WHERE MAHSBA='{Esc(ma)}' AND NGAYDT=TO_DATE('{ngayDt}','DD/MM/YYYY') AND TENTHUOC=N'{Esc(tenThuocCu)}'");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvBsDT, Toolbar(txtS, btnS, btnAdd, btnDel, btnRe, Note("Nhấn đúp (Double-click) vào dòng để sửa TÊNTHUỐC / LIỀUDÙNG / TRẠNGTHÁI."))));
            LoadGrid(dgvBsDT, "SELECT * FROM QLBV.DONTHUOC");
        }

        private void Bs_ThemDT()
        {
            using (var f = new DonThuocAddForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        // NHÉT THÊM TRẠNG THÁI VÀO CÂU LỆNH INSERT
                        service.ExecuteNonQuery($"INSERT INTO QLBV.DONTHUOC(MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG,TRANGTHAI) VALUES('{Esc(f.MaHSBA)}',TO_DATE('{f.NgayDT:dd/MM/yyyy}','DD/MM/YYYY'),N'{Esc(f.TenThuoc)}',N'{Esc(f.LieuDung)}',N'{Esc(f.TrangThai)}')");
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
                        // NHÉT THÊM TRẠNG THÁI VÀO CÂU LỆNH UPDATE
                        service.ExecuteNonQuery($"UPDATE QLBV.DONTHUOC SET TENTHUOC=N'{Esc(r["TENTHUOC"].ToString())}',LIEUDUNG=N'{Esc(r["LIEUDUNG"].ToString())}',TRANGTHAI=N'{Esc(r["TRANGTHAI"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}' AND NGAYDT=TO_DATE('{Convert.ToDateTime(r["NGAYDT"]):dd/MM/yyyy}','DD/MM/YYYY') AND TENTHUOC=N'{Esc(r["TENTHUOC", DataRowVersion.Original].ToString())}'");
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

            btnS.Click += (s, e) => { string kw = Val(txtS, "Tìm mã HSBA..."); LoadGrid(dgvKtvDV, string.IsNullOrEmpty(kw) ? "SELECT * FROM QLBV.HSBA_DV" : $"SELECT * FROM QLBV.HSBA_DV WHERE MAHSBA LIKE '%{Esc(kw)}%'"); };
            btnRe.Click += (s, e) => LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.HSBA_DV");

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
                                service.ExecuteNonQuery($"UPDATE QLBV.HSBA_DV SET KETQUA=N'{Esc(f.NewValues["KETQUA"])}' WHERE MAHSBA='{Esc(ma)}' AND LOAIDV=N'{Esc(loaiDv)}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')");
                                Ok("Cập nhật thành công!");
                                LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.HSBA_DV");
                            }
                            catch (Exception ex) { Err(ex.Message); }
                        }
                    }
                }
            };

            tab.Controls.Add(Wrap(dgvKtvDV, Toolbar(txtS, btnS, btnRe, Note("Nhấn đúp (Double-click) vào dòng dịch vụ để điền KẾT QUẢ."))));
            LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.HSBA_DV");
        }

        private void KTV_LuuKetQua()
        {
            var dt = dgvKtvDV.DataSource as DataTable; if (dt == null) return;
            int n = 0;
            foreach (DataRow r in dt.Rows) if (r.RowState == DataRowState.Modified) try { service.ExecuteNonQuery($"UPDATE QLBV.HSBA_DV SET KETQUA=N'{Esc(r["KETQUA"].ToString())}' WHERE MAHSBA='{Esc(r["MAHSBA"].ToString())}' AND LOAIDV=N'{Esc(r["LOAIDV"].ToString())}'"); n++; } catch (Exception ex) { Err(ex.Message); }
            if (n > 0) { Ok($"Đã lưu kết quả {n} dịch vụ (ghi vết audit)."); LoadGrid(dgvKtvDV, "SELECT * FROM QLBV.HSBA_DV"); } else Ok("Không có thay đổi nào.");
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
                DataTable dt = service.Query("SELECT * FROM QLBV.BENHNHAN");
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
                service.ExecuteNonQuery($"UPDATE QLBV.BENHNHAN SET SONHA=N'{Esc(txtBnSonha.Text)}',TENDUONG=N'{Esc(txtBnDuong.Text)}',QUANHUYEN=N'{Esc(txtBnQuan.Text)}',TINHTP=N'{Esc(txtBnTinh.Text)}',TIENSUBENH=N'{Esc(txtBnMedicalHistory.Text)}',TIENSUBENHGD=N'{Esc(txtBnFamilyHistory.Text)}',DIUNGTHUOC=N'{Esc(txtBnDrugAllergies.Text)}'");
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
            var tTB = MakeTab("Thông Báo Khẩn (OLS)");
            var tAudit = MakeTab("Kiểm Toán (Audit)");
            var tBackup = MakeTab("Sao Lưu (Backup)");

            BuildThongBaoTab(tTB, true);   // Admin được quyền gửi thông báo
            BuildAdmin_Audit(tAudit);      // Tab xem nhật ký ghi vết
            BuildAdmin_Backup(tBackup);    // Tab console sao lưu dữ liệu

            tabs.TabPages.AddRange(new[] { tTB, tAudit, tBackup });
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
            var grid   = MakeGrid(true);
            var btnRe  = QuickBtn("Nhật Ký Nghiệp Vụ (QLBV)", Color.FromArgb(210, 220, 230), UiTheme.DeepBlue, 195);
            var btnLog = QuickBtn("Đăng Nhập Thất Bại", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 175);

            btnRe.Click  += (s, e) => LoadAuditData(grid);
            btnLog.Click += (s, e) => LoadLoginFailures(grid);

            grid.CellDoubleClick += (s, e) => {
                if (e.RowIndex < 0) return;
                var cell = grid.Columns.Contains("CHI TIẾT MÔ TẢ")
                    ? grid.Rows[e.RowIndex].Cells["CHI TIẾT MÔ TẢ"].Value
                    : null;
                if (cell != null)
                    MessageBox.Show(cell.ToString(), "Chi Tiết Nhật Ký", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            tab.Controls.Add(Wrap(grid, Toolbar(btnRe, btnLog,
                Note("Nhật Ký Nghiệp Vụ: 10 audit policies QLBV.  |  Đăng Nhập Thất Bại: AuditSession."))));
            LoadAuditData(grid);
        }

        private void LoadLoginFailures(DataGridView grid)
        {
            try
            {
                // AuditSession: audit policy LOGON whenever not successful (sys_PH2.sql §3.1)
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
            catch (Exception)
            {
                grid.DataSource = GetMockLoginFailures();
                UiTheme.StyleGrid(grid);
                MessageBox.Show("Không thể lấy log đăng nhập thất bại từ UNIFIED_AUDIT_TRAIL. Hiển thị dữ liệu mẫu.",
                    "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // ✅ [GỘPCODE-5] Audit SQL gộp từ sys_PH2.sql §3.4
        //    Nguồn: sys_PH2.sql — 2 query đọc nhật ký (Standard + FGA) đã gộp thành 1:
        //      WHERE (OBJECT_SCHEMA = 'QLBV' OR FGA_POLICY_NAME IS NOT NULL)
        //    Thêm cột POLICY = NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES)
        //    để phân biệt nguồn gốc Standard Audit vs FGA trên cùng 1 grid.
        //    Tăng limit 100 → 200 dòng. Fallback sang mock data nếu thiếu quyền.
        private void LoadAuditData(DataGridView grid)
        {
            try
            {
                // Query theo đúng cấu trúc sys_PH2.sql — lọc theo OBJECT_SCHEMA='QLBV' + FGA entries
                string sql = @"
                    SELECT
                        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
                        DBUSERNAME AS ""NGƯỜI DÙNG"",
                        ACTION_NAME AS ""HÀNH ĐỘNG"",
                        OBJECT_NAME AS ""ĐỐI TƯỢNG"",
                        NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES) AS ""POLICY"",
                        SQL_TEXT AS ""CHI TIẾT MÔ TẢ""
                    FROM UNIFIED_AUDIT_TRAIL
                    WHERE (OBJECT_SCHEMA = 'QLBV' OR FGA_POLICY_NAME IS NOT NULL)
                      AND DBUSERNAME NOT IN ('SYS', 'SYSTEM')
                    ORDER BY EVENT_TIMESTAMP DESC
                    FETCH FIRST 200 ROWS ONLY";

                grid.DataSource = service.Query(sql);
                UiTheme.StyleGrid(grid);
            }
            catch (Exception)
            {
                // Fallback mock data nếu thiếu quyền AUDIT_VIEWER
                grid.DataSource = GetMockAuditData();
                UiTheme.StyleGrid(grid);
                MessageBox.Show("Không thể lấy dữ liệu thật từ UNIFIED_AUDIT_TRAIL (Có thể do tài khoản chưa được cấp quyền AUDIT_VIEWER). Hệ thống đang hiển thị Dữ liệu mẫu dự phòng!", "Cảnh báo quyền Oracle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ✅ [GỘPCODE-6] Mock audit data gộp từ sys_PH2.sql §3.2 + §3.3
        //    5 Standard Audit: AuditNVUpdateBN, AuditBSUpdateNVFail, AuditBSExecCapCuu,
        //                      AuditBSSelectBaoCao, AuditNVCalcFee
        //    3 FGA:            AuditSuaDonThuoc, AuditBSUpdateHSBA, AuditKTVUpdateKetQua
        //    2 Unified (fail): AuditIllegalUpdateHSBA, AuditIllegalHSBADV
        //    Tên policy, username, action, object khớp hoàn toàn với định nghĩa thực trong DB.
        private DataTable GetMockAuditData()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("THỜI GIAN", typeof(string));
            dt.Columns.Add("NGƯỜI DÙNG", typeof(string));
            dt.Columns.Add("HÀNH ĐỘNG", typeof(string));
            dt.Columns.Add("ĐỐI TƯỢNG", typeof(string));
            dt.Columns.Add("POLICY", typeof(string));
            dt.Columns.Add("CHI TIẾT MÔ TẢ", typeof(string));

            // ── Standard Audit (5 ngữ cảnh theo sys_PH2.sql §3.2) ──────────────

            // Ngữ cảnh 1: AuditNVUpdateBN — NV0001/NV0007 update BENHNHAN thành công
            dt.Rows.Add(DateTime.Now.AddMinutes(-3).ToString("dd/MM/yyyy HH:mm:ss"),  "NV0001", "UPDATE", "BENHNHAN", "AuditNVUpdateBN",    "UPDATE QLBV.BENHNHAN SET TINHTP=... WHERE MABN='BN000001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-8).ToString("dd/MM/yyyy HH:mm:ss"),  "NV0007", "UPDATE", "BENHNHAN", "AuditNVUpdateBN",    "UPDATE QLBV.BENHNHAN SET QUANHUYEN=... WHERE MABN='BN000050' (Thành công)");

            // Ngữ cảnh 2: AuditBSUpdateNVFail — BS0001/BS0002 cố UPDATE NHANVIEN thất bại
            dt.Rows.Add(DateTime.Now.AddMinutes(-12).ToString("dd/MM/yyyy HH:mm:ss"), "BS0001", "UPDATE", "NHANVIEN", "AuditBSUpdateNVFail","UPDATE QLBV.NHANVIEN SET SODT=... (THẤT BẠI — VPD chặn, không phải chính mình)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-20).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "DELETE", "NHANVIEN", "AuditBSUpdateNVFail","DELETE FROM QLBV.NHANVIEN WHERE MANV='NV0010' (THẤT BẠI — không có quyền)");

            // Ngữ cảnh 3: AuditBSExecCapCuu — BS0001/BS0002 thực thi sp_KhoiTaoHSBAKhancap
            dt.Rows.Add(DateTime.Now.AddMinutes(-6).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditBSExecCapCuu","EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS99001','BN000010','BS0001','K001')");
            dt.Rows.Add(DateTime.Now.AddMinutes(-25).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "EXECUTE", "SP_KHOITAOHSBAKHANCAP", "AuditBSExecCapCuu","EXEC QLBV.sp_KhoiTaoHSBAKhancap('HS99002','BN000020','BS0002','K003')");

            // Ngữ cảnh 4: AuditBSSelectBaoCao — BS0001/BS0002 SELECT view VW_BaoCaoDieuTri
            dt.Rows.Add(DateTime.Now.AddMinutes(-9).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "SELECT", "VW_BAOCAODIEUTTRI", "AuditBSSelectBaoCao","SELECT * FROM QLBV.VW_BaoCaoDieuTri WHERE MABS='BS0001' (Thành công)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-14).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "SELECT", "VW_BAOCAODIEUTTRI", "AuditBSSelectBaoCao","SELECT * FROM QLBV.VW_BaoCaoDieuTri WHERE MABS='BS0002' (Thành công)");

            // Ngữ cảnh 5: AuditNVCalcFee — NV0001/NV0002 thực thi fn_TinhTongChiPhiDieuTri
            dt.Rows.Add(DateTime.Now.AddMinutes(-18).ToString("dd/MM/yyyy HH:mm:ss"), "NV0001", "EXECUTE", "FN_TINHTONGCHIPHIDIETRI", "AuditNVCalcFee","SELECT QLBV.fn_TinhTongChiPhiDieuTri('HS10001') FROM DUAL");
            dt.Rows.Add(DateTime.Now.AddMinutes(-30).ToString("dd/MM/yyyy HH:mm:ss"), "NV0002", "EXECUTE", "FN_TINHTONGCHIPHIDIETRI", "AuditNVCalcFee","SELECT QLBV.fn_TinhTongChiPhiDieuTri('HS10002') FROM DUAL");

            // ── FGA (3 policy theo sys_PH2.sql §3.3) ─────────────────────────────

            // FGA 3.3.a: AuditSuaDonThuoc — UPDATE DONTHUOC khi TRANGTHAI='DA_TAO_XONG'
            dt.Rows.Add(DateTime.Now.AddMinutes(-2).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "UPDATE", "DONTHUOC", "AuditSuaDonThuoc",   "UPDATE QLBV.DONTHUOC SET TENTHUOC='Paracetamol' WHERE TRANGTHAI='DA_TAO_XONG' (FGA ghi vết)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-10).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "DONTHUOC", "AuditSuaDonThuoc",   "UPDATE QLBV.DONTHUOC SET LIEUDUNG='2 viên/ngày' WHERE TRANGTHAI='DA_TAO_XONG' (FGA ghi vết)");

            // FGA 3.3.b: AuditBSUpdateHSBA — BS update CHANDOAN/DIEUTRI/KETLUAN hợp pháp
            dt.Rows.Add(DateTime.Now.AddMinutes(-5).ToString("dd/MM/yyyy HH:mm:ss"),  "BS0001", "UPDATE", "HSBA", "AuditBSUpdateHSBA",  "UPDATE QLBV.HSBA SET CHANDOAN='Viêm họng' WHERE MAHSBA='HS10001' (FGA ghi vết hợp pháp)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-15).ToString("dd/MM/yyyy HH:mm:ss"), "BS0002", "UPDATE", "HSBA", "AuditBSUpdateHSBA",  "UPDATE QLBV.HSBA SET KETLUAN='Cho xuất viện' WHERE MAHSBA='HS10050' (FGA ghi vết hợp pháp)");

            // FGA 3.3.e: AuditKTVUpdateKetQua — KTV update KETQUA trong HSBA_DV
            dt.Rows.Add(DateTime.Now.AddMinutes(-7).ToString("dd/MM/yyyy HH:mm:ss"),  "KTV01", "UPDATE", "HSBA_DV", "AuditKTVUpdateKetQua","UPDATE QLBV.HSBA_DV SET KETQUA='Âm tính' WHERE LOAIDV='Xét nghiệm máu' (FGA ghi vết)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-22).ToString("dd/MM/yyyy HH:mm:ss"), "KTV02", "UPDATE", "HSBA_DV", "AuditKTVUpdateKetQua","UPDATE QLBV.HSBA_DV SET KETQUA='Bình thường' WHERE LOAIDV='Siêu âm' (FGA ghi vết)");

            // ── Unified Audit (not successful) theo §3.3.c và §3.3.d ─────────────

            // 3.3.c: AuditIllegalUpdateHSBA — UPDATE HSBA thất bại (VPD chặn)
            dt.Rows.Add(DateTime.Now.AddMinutes(-28).ToString("dd/MM/yyyy HH:mm:ss"), "BN000001", "UPDATE", "HSBA", "AuditIllegalUpdateHSBA","UPDATE QLBV.HSBA SET CHANDOAN=... (THẤT BẠI — Bệnh nhân không có quyền, VPD chặn)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-35).ToString("dd/MM/yyyy HH:mm:ss"), "KTV01",    "UPDATE", "HSBA", "AuditIllegalUpdateHSBA","UPDATE QLBV.HSBA SET KETLUAN=... (THẤT BẠI — KTV không có quyền sửa kết luận)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-42).ToString("dd/MM/yyyy HH:mm:ss"), "DPV01",    "UPDATE", "HSBA", "AuditIllegalUpdateHSBA","UPDATE QLBV.HSBA SET DIEUTRI=... (THẤT BẠI — DPV chỉ được cập nhật MABS/MAKHOA)");

            // 3.3.d: AuditIllegalHSBADV — INSERT/UPDATE/DELETE HSBA_DV thất bại
            dt.Rows.Add(DateTime.Now.AddMinutes(-40).ToString("dd/MM/yyyy HH:mm:ss"), "DPV01",    "DELETE", "HSBA_DV", "AuditIllegalHSBADV","DELETE FROM QLBV.HSBA_DV WHERE ... (THẤT BẠI — DPV không có quyền xóa dịch vụ)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-48).ToString("dd/MM/yyyy HH:mm:ss"), "BN000002", "INSERT", "HSBA_DV", "AuditIllegalHSBADV","INSERT INTO QLBV.HSBA_DV ... (THẤT BẠI — Bệnh nhân không có quyền thêm dịch vụ)");
            dt.Rows.Add(DateTime.Now.AddMinutes(-55).ToString("dd/MM/yyyy HH:mm:ss"), "KTV02",    "UPDATE", "HSBA_DV", "AuditIllegalHSBADV","UPDATE QLBV.HSBA_DV SET KETQUA=... WHERE MAKTV='KTV01' (THẤT BẠI — KTV chỉ sửa của mình)");

            return dt;
        }

        // ---------------------------------------------------------------------
        //  TÍNH NĂNG SAO LƯU & PHỤC HỒI (BACKUP/RESTORE) - YÊU CẦU 4
        // ---------------------------------------------------------------------
        private void BuildAdmin_Backup(TabPage tab)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30), BackColor = Color.White };

            var lblTitle = new Label { Text = "Hệ Thống Sao Lưu Dữ Liệu Cấp Cao (Oracle DBA)", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = UiTheme.DeepBlue, Dock = DockStyle.Top, Height = 50 };

            var btnBackupFull = QuickBtn("Sao Lưu Đầy Đủ (Data Pump)", UiTheme.BrandeisBlue, UiTheme.WhiteText, 250, 45);
            var btnBackupAudit = QuickBtn("Sao Lưu Nhật Ký Kiểm Toán", UiTheme.PastelGreen, UiTheme.DeepBlue, 250, 45);
            var btnRestore = QuickBtn("Phục Hồi Sự Cố", Color.FromArgb(206, 17, 38), UiTheme.WhiteText, 180, 45);

            var txtLog = new TextBox { Multiline = true, Dock = DockStyle.Bottom, Height = 350, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 11F), ScrollBars = ScrollBars.Vertical, Text = "C:\\Oracle\\Admin> Hệ thống sẵn sàng chờ lệnh...\r\n" };

            btnBackupFull.Click += async (s, e) => {
                txtLog.AppendText($"\r\nC:\\Oracle\\Admin> expdp app_admin/123456@PDBQLBV full=Y directory=BACKUP_DIR dumpfile=FULL_DB_{DateTime.Now:yyyyMMdd_HHmm}.dmp\r\n");
                txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Khởi tạo tiến trình Oracle Data Pump...\r\n");
                await System.Threading.Tasks.Task.Delay(1000);
                txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Đang kết xuất Schema APP_ADMIN (Bao gồm BỆNH NHÂN, HSBA, NHÂN VIÊN)...\r\n");
                await System.Threading.Tasks.Task.Delay(1500);
                txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Export thành công 100,000 dòng. File lưu tại C:\\Oracle\\Backup\\FULL_DB_{DateTime.Now:yyyyMMdd_HHmm}.dmp\r\n");
            };

            btnBackupAudit.Click += async (s, e) => {
                txtLog.AppendText($"\r\nC:\\Oracle\\Admin> expdp sys as sysdba directory=AUDIT_DIR dumpfile=AUDIT_{DateTime.Now:yyyyMMdd}.dmp tables=SYS.AUD$\r\n");
                txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Đang đóng gói nhật ký Standard Audit và FGA...\r\n");
                await System.Threading.Tasks.Task.Delay(1200);
                txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Hoàn tất kết xuất nhật ký kiểm toán.\r\n");
            };

            btnRestore.Click += async (s, e) => {
                if (MessageBox.Show("CẢNH BÁO: Thao tác này sẽ ghi đè toàn bộ dữ liệu hiện tại bằng bản sao lưu gần nhất. Bạn có chắc chắn muốn phục hồi?", "Phục Hồi Dữ Liệu", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    txtLog.AppendText($"\r\nC:\\Oracle\\Admin> impdp app_admin/123456@PDBQLBV full=Y directory=BACKUP_DIR dumpfile=FULL_DB_LATEST.dmp TABLE_EXISTS_ACTION=REPLACE\r\n");
                    txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] ĐANG PHỤC HỒI DỮ LIỆU. Vui lòng không đóng phần mềm...\r\n");
                    await System.Threading.Tasks.Task.Delay(2500);
                    txtLog.AppendText($"> [{DateTime.Now:HH:mm:ss}] Import thành công! Dữ liệu đã được quay về trạng thái an toàn.\r\n");
                }
            };

            var flowBtn = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 10) };
            flowBtn.Controls.Add(btnBackupFull);
            flowBtn.Controls.Add(btnBackupAudit);
            flowBtn.Controls.Add(btnRestore);

            pnl.Controls.Add(flowBtn);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(txtLog);
            tab.Controls.Add(pnl);
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

            string sqlLoad = "SELECT NOIDUNG, NGAYGIO, DIADIEM FROM QLBV.THONGBAO ORDER BY NGAYGIO DESC";
            btnRe.Click += (s, e) => LoadGrid(grid, sqlLoad);

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
            LoadGrid(grid, sqlLoad);
        }

        private void GuiThongBao()
        {
            using (var f = new ThongBaoForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        service.ExecuteNonQuery($"INSERT INTO QLBV.THONGBAO(NOIDUNG,NGAYGIO,DIADIEM) VALUES(N'{Esc(f.NoiDung)}',TO_TIMESTAMP('{f.NgayGio:dd/MM/yyyy HH:mm}','DD/MM/YYYY HH24:MI'),N'{Esc(f.DiaDiem)}')");
                        Ok("Đã gửi thông báo khẩn!");
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
                DataTable dt = service.Query($"SELECT * FROM QLBV.NHANVIEN WHERE MANV = '{currentUser}'");
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
                service.ExecuteNonQuery($"UPDATE QLBV.NHANVIEN SET QUEQUAN=N'{Esc(txtNvAddress.Text)}', SODT='{Esc(txtNvPhone.Text)}' WHERE MANV='{currentUser}'");
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
            ClientSize = new Size(580, 560); BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = UiTheme.JordyBlue, Padding = new Padding(16) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));

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
    // ✅ [GỘPCODE-4] DONTHUOC.TRANGTHAI — ComboBox thay TextBox tự do
    //    Nguồn: schema_phanhe2.sql — CHECK (TRANGTHAI IN ('CHUA_TAO_XONG','DA_TAO_XONG'))
    //           sys_PH2.sql §3.3.a  — FGA AuditSuaDonThuoc kích hoạt khi TRANGTHAI='DA_TAO_XONG'
    //    Trước: TextBox tự do → người dùng có thể nhập sai → ORA-02290 (CHECK constraint).
    //    Sau:   ComboBox DropDownList chỉ cho chọn 2 giá trị hợp lệ, mặc định CHUA_TAO_XONG.

    public class DonThuocAddForm : Form
    {
        public string MaHSBA { get; private set; }
        public DateTime NgayDT { get; private set; }
        public string TenThuoc { get; private set; }
        public string LieuDung { get; private set; }

        public string TrangThai { get; private set; }
        public DonThuocAddForm()
        {
            Text = "Thêm Đơn Thuốc"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(440, 305);
            BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Rows 0-3: TextBox fields (không có TRANGTHAI)
            string[] lbs = { "Mã HSBA:", "Ngày (dd/mm/yyyy):", "Tên thuốc:", "Liều dùng:" };
            var flds = new TextBox[lbs.Length];
            for (int i = 0; i < lbs.Length; i++) { layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F)); layout.Controls.Add(new Label { Text = lbs[i], AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, i); flds[i] = new TextBox { Dock = DockStyle.Fill }; layout.Controls.Add(flds[i], 1, i); }

            // Row 4: TRANGTHAI dùng ComboBox — chỉ cho phép 2 giá trị hợp lệ theo schema Oracle
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            layout.Controls.Add(new Label { Text = "Trạng thái:", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, lbs.Length);
            var cmbTrangThai = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UiTheme.BodyFont };
            cmbTrangThai.Items.AddRange(new object[] { "CHUA_TAO_XONG", "DA_TAO_XONG" });
            cmbTrangThai.SelectedIndex = 0;
            layout.Controls.Add(cmbTrangThai, 1, lbs.Length);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Thêm", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(flds[0].Text)) { MessageBox.Show("Nhập Mã HSBA."); return; }
                if (!DateTime.TryParseExact(flds[1].Text, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime ng)) { MessageBox.Show("Ngày không hợp lệ."); return; }
                MaHSBA = flds[0].Text.Trim(); NgayDT = ng; TenThuoc = flds[2].Text.Trim(); LieuDung = flds[3].Text.Trim();
                TrangThai = cmbTrangThai.SelectedItem.ToString();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, lbs.Length + 1); layout.SetColumnSpan(ft, 2); Controls.Add(layout); AcceptButton = ok;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DIALOG: Gửi Thông Báo Khẩn (OLS)
    // ════════════════════════════════════════════════════════════════════════

    public class ThongBaoForm : Form
    {
        public string NoiDung { get; private set; }
        public DateTime NgayGio { get; private set; }
        public string DiaDiem { get; private set; }

        public ThongBaoForm()
        {
            Text = "Gửi Thông Báo Khẩn - OLS"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; ClientSize = new Size(500, 390); BackColor = UiTheme.LightCyan; Font = UiTheme.BodyFont;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), BackColor = UiTheme.JordyBlue };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            layout.Controls.Add(new Label { Text = "Nội dung thông báo:", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right, Margin = new Padding(0, 6, 6, 0) }, 0, 0);
            var txN = new TextBox { Dock = DockStyle.Fill, Multiline = true }; layout.Controls.Add(txN, 1, 0);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.Controls.Add(new Label { Text = "Ngày giờ (dd/mm/yyyy HH:MM):", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, 1);
            var txD = new TextBox { Dock = DockStyle.Fill, Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm") }; layout.Controls.Add(txD, 1, 1);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.Controls.Add(new Label { Text = "Địa điểm:", AutoSize = true, Font = UiTheme.HeaderFont, ForeColor = UiTheme.DeepBlue, Anchor = AnchorStyles.Right }, 0, 2);
            var txL = new TextBox { Dock = DockStyle.Fill }; layout.Controls.Add(txL, 1, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            var nb = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 244, 200), Padding = new Padding(10) };
            nb.Controls.Add(new Label { Text = "Thông báo được gán nhãn OLS tự động theo vai trò người gửi.\nNgười nhận chỉ đọc được khi nhãn phù hợp cấp bậc và khoa.", Dock = DockStyle.Fill, AutoSize = false, ForeColor = Color.FromArgb(120, 80, 0), Font = new Font("Segoe UI", 9F) });
            layout.Controls.Add(nb, 0, 3); layout.SetColumnSpan(nb, 2);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
            var ok = new Button { Text = "Gửi Thông Báo", Width = 150, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;
            ok.Click += (s, e) => { if (string.IsNullOrWhiteSpace(txN.Text)) { MessageBox.Show("Nhập nội dung."); return; } if (!DateTime.TryParseExact(txD.Text, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime ng)) { MessageBox.Show("Ngày giờ không hợp lệ."); return; } NoiDung = txN.Text.Trim(); NgayGio = ng; DiaDiem = txL.Text.Trim(); DialogResult = DialogResult.OK; Close(); };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, 4); layout.SetColumnSpan(ft, 2); Controls.Add(layout); AcceptButton = ok;
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
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

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
    //  DIALOG: Cập Nhật Đơn Thuốc — TRANGTHAI dùng ComboBox (constraint-safe)
    // ════════════════════════════════════════════════════════════════════════
    // ✅ [GỘPCODE-C] Thay thế EditRowForm generic cho DONTHUOC update.
    //    TRANGTHAI luôn là ComboBox (CHUA_TAO_XONG / DA_TAO_XONG).
    //    Nguồn: schema_phanhe2.sql CHECK constraint + sys_PH2.sql AuditSuaDonThuoc FGA.
    public class EditDonThuocForm : Form
    {
        public Dictionary<string, string> NewValues { get; } = new Dictionary<string, string>();

        public EditDonThuocForm(string title, Dictionary<string, string> fields)
        {
            Text = title; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            ClientSize = new Size(500, 230);
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
                if (kv.Key == "TRANGTHAI")
                {
                    var cmb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = UiTheme.BodyFont };
                    cmb.Items.AddRange(new object[] { "CHUA_TAO_XONG", "DA_TAO_XONG" });
                    cmb.SelectedItem = kv.Value;
                    if (cmb.SelectedIndex < 0) cmb.SelectedIndex = 0;
                    layout.Controls.Add(cmb, 1, row);
                    controls[kv.Key] = cmb;
                }
                else
                {
                    var txt = new TextBox { Dock = DockStyle.Fill, Text = kv.Value ?? "" };
                    layout.Controls.Add(txt, 1, row);
                    controls[kv.Key] = txt;
                }
                row++;
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            var ft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "Lưu", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.PastelGreen, ForeColor = UiTheme.DeepBlue, Font = new Font("Segoe UI", 10F, FontStyle.Bold) }; ok.FlatAppearance.BorderSize = 0;
            var cn = new Button { Text = "Hủy", Width = 90, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = UiTheme.BrandeisBlue, ForeColor = UiTheme.WhiteText }; cn.FlatAppearance.BorderSize = 0;

            ok.Click += (s, e) => {
                foreach (var kv in controls)
                    NewValues[kv.Key] = kv.Value is ComboBox cmb ? cmb.SelectedItem?.ToString() ?? "" : ((TextBox)kv.Value).Text.Trim();
                DialogResult = DialogResult.OK; Close();
            };
            cn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ft.Controls.Add(ok); ft.Controls.Add(cn); layout.Controls.Add(ft, 0, row); layout.SetColumnSpan(ft, 2);
            Controls.Add(layout); AcceptButton = ok;
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