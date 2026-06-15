# Báo Cáo Gộp Code SQL → WinForms — Vòng 2 (Bổ Sung)

**Ngày thực hiện:** 14/06/2026  
**Căn cứ:** `BAO_CAO_GOP_CODE_SQL_WINFORM.md` (danh mục chưa gộp từ vòng 1)  
**File đích WinForms:** `winform/PhanHe1/SubSystem2Form.cs`  

---

## 1. Tổng Kết Vòng 2

Vòng 2 xử lý toàn bộ **6 hạng mục chưa gộp** từ vòng 1, chọn cách tiếp cận đúng nhất theo nghiệp vụ bệnh viện.

---

## 2. Những Thay Đổi Đã Gộp Trong Vòng 2

### 2.1. Detect Giám Đốc Qua `CAPBAC` Thay Vì `ROLE_GIAMDOC` ✅

**Nguồn:** `admin_ph2.sql` §YC2 — `UPDATE NHANVIEN SET CAPBAC='Ban Giám đốc'` (u1: NV0001)

**Vấn đề cũ:** Code detect Giám đốc qua `ROLE_GIAMDOC` hoặc username `GD*`. SQL không tạo `ROLE_GIAMDOC`, Giám đốc là nhân viên có `CAPBAC='Ban Giám đốc'` + OLS label `BGD`.

**Lựa chọn tiếp cận:** Query `QLBV.NHANVIEN` sau login để đọc `CAPBAC` — đây là cách **đúng nghiệp vụ nhất** vì:
- Không phụ thuộc vào tên username
- Khớp chính xác với dữ liệu thực trong bảng
- Tự động hoạt động với mọi nhân viên có CAPBAC = 'Ban Giám đốc'

**Đã sửa trong `DetermineUserRole()`:**
```csharp
// Detect Ban Giám đốc qua CAPBAC trong QLBV.NHANVIEN
try {
    var dtCap = service.Query($"SELECT CAPBAC FROM QLBV.NHANVIEN WHERE MANV = '{currentUser}'");
    if (dtCap.Rows.Count > 0 && dtCap.Rows[0]["CAPBAC"]?.ToString() == "Ban Giám đốc")
        return UserRole.GIAMDOC;
} catch { }
// Fallback cho tài khoản test GD* chưa có trong QLBV.NHANVIEN
if (currentUser.StartsWith("GD", ...)) return UserRole.GIAMDOC;
```

---

### 2.2. `MAKTV NOT NULL` — BS Chỉ Định Dịch Vụ ✅

**Nguồn:** `schema_phanhe2.sql` — `MAKTV VARCHAR2(20) NOT NULL` + `FK→NHANVIEN(MANV)`  
`admin_ph2.sql` — `grant update(MAKTV) on HSBA_DV to ROLE_DPV` (DPV có thể đổi KTV sau)

**Phân tích nghiệp vụ:**
- BS chỉ định dịch vụ và đề xuất KTV thực hiện → cần MAKTV ngay khi INSERT
- DPV có thể cập nhật lại MAKTV sau (được grant `UPDATE(MAKTV)`)
- Đây là quy trình hợp lý: BS → đề xuất KTV → DPV phân công chính thức

**Lựa chọn tiếp cận:** Thêm field "Mã KTV đề xuất:" vào `HsbaDvAddForm(isDoctor=true)` — **đúng nghiệp vụ và đúng constraint DB.**

**Đã sửa:**
- `HsbaDvAddForm(isDoctor=true)`: thêm field "Mã KTV đề xuất:", note cập nhật thành *"DPV có thể thay đổi KTV được phân công sau (UPDATE MAKTV)"*
- `Bs_ThemDV()`: INSERT bao gồm MAKTV: `INSERT INTO QLBV.HSBA_DV(MAHSBA,LOAIDV,NGAYDV,MAKTV) VALUES(...)`

---

### 2.3. TRANGTHAI Trong Form Cập Nhật Đơn Thuốc ✅

**Nguồn:** `schema_phanhe2.sql` — `CHECK (TRANGTHAI IN ('CHUA_TAO_XONG','DA_TAO_XONG'))`  
`sys_PH2.sql` §3.3.a — FGA `AuditSuaDonThuoc` trigger khi UPDATE với `TRANGTHAI='DA_TAO_XONG'`

**Vấn đề cũ:** Form cập nhật đơn thuốc (double-click) dùng `EditRowForm` generic → TRANGTHAI là TextBox tự do, người dùng có thể nhập sai giá trị → ORA-02290.

**Lựa chọn tiếp cận:** Tạo class chuyên biệt `EditDonThuocForm` — **đúng hơn** vì:
- Giữ TENTHUOC, LIEUDUNG là TextBox tự do (hợp lý)
- Chỉ TRANGTHAI là ComboBox (constrained đúng)
- Không phá vỡ `EditRowForm` generic dùng cho các form khác

**Đã thêm:** Class `EditDonThuocForm` mới (cuối file), thay thế `EditRowForm` trong `BuildBs_DT.CellDoubleClick`.

---

### 2.4. Tab "Báo Cáo Điều Trị" + Nút "Tạo HSBA Cấp Cứu" Cho Bác Sĩ ✅

**Nguồn:** `sys_PH2.sql` §3.2:
- `AuditBSSelectBaoCao` — BS SELECT `VW_BaoCaoDieuTri` → **phải có UI mới trigger**
- `AuditBSExecCapCuu` — BS EXECUTE `sp_KhoiTaoHSBAKhancap` → **phải có UI mới trigger**

**Đã thêm:**
- Method `BuildBs_BaoCao(TabPage tab)` mới — hiển thị dữ liệu từ `QLBV.VW_BaoCaoDieuTri`
- Nút "🚨 Tạo HSBA Cấp Cứu" gọi: `BEGIN QLBV.sp_KhoiTaoHSBAKhancap(...); END;`
- Tab mới **"Báo Cáo Điều Trị"** được thêm vào BS interface (sau tab Đơn Thuốc, trước Thông Báo)
- Class `CapCuuForm` mới — form nhập MAHSBA, MABN, MAKHOA cho ca cấp cứu

**Kết quả audit:** 2 policies `AuditBSSelectBaoCao` và `AuditBSExecCapCuu` giờ sẽ kích hoạt khi BS sử dụng tab này.

---

### 2.5. Nút "Tính Chi Phí Điều Trị" Cho DPV ✅

**Nguồn:** `sys_PH2.sql` §3.2 — `AuditNVCalcFee` ghi vết khi NV0001/NV0002 gọi `fn_TinhTongChiPhiDieuTri`

**Đã thêm:** Nút "Tính Chi Phí" màu vàng trong toolbar DPV HSBA tab. Khi click:
1. Đọc MAHSBA của dòng đang chọn
2. Gọi: `SELECT QLBV.fn_TinhTongChiPhiDieuTri('MAHSBA') AS TONG_DV FROM DUAL`
3. Hiển thị MessageBox với tổng số dịch vụ + thông báo audit

**Kết quả audit:** Policy `AuditNVCalcFee` giờ sẽ kích hoạt khi DPV/NV dùng nút này.

---

### 2.6. Tab Kiểm Toán Đăng Nhập Thất Bại ✅

**Nguồn:** `sys_PH2.sql` §3.1 — `AuditSession` policy: `actions logon; whenever not successful`

**Vấn đề cũ:** `BuildAdmin_Audit` chỉ có 1 nút xem nhật ký nghiệp vụ. `AuditSession` là policy riêng biệt không liên quan schema QLBV.

**Đã sửa `BuildAdmin_Audit`:**
- Nút **"Nhật Ký Nghiệp Vụ (QLBV)"** — giữ query cũ (10 policies QLBV)
- Nút **"Đăng Nhập Thất Bại"** — query mới cho `AuditSession`:
  ```sql
  SELECT EVENT_TIMESTAMP, DBUSERNAME, ACTION_NAME, RETURN_CODE, OS_USERNAME, ...
  FROM UNIFIED_AUDIT_TRAIL
  WHERE ACTION_NAME = 'LOGON' AND RETURN_CODE <> 0
  ORDER BY EVENT_TIMESTAMP DESC
  ```
- Method `LoadLoginFailures()` + `GetMockLoginFailures()` với các trường hợp mẫu:
  - ORA-1017: invalid username/password
  - ORA-28000: account locked

---

## 3. Bảng Tổng Hợp Toàn Bộ 2 Vòng Gộp

| # | Hạng Mục | Vòng | Trạng Thái |
|---|---|---|---|
| 1 | Schema prefix `APP_ADMIN` → `QLBV` (107 vị trí) | 1 | ✅ Đã gộp |
| 2 | KTV tên cột `MADV`→`LOAIDV`, `NGAY`→`NGAYDV` | 1 | ✅ Đã gộp |
| 3 | BS delete HSBA_DV: thêm `NGAYDV` vào WHERE | 1 | ✅ Đã gộp |
| 4 | DONTHUOC TRANGTHAI thêm: TextBox → ComboBox | 1 | ✅ Đã gộp |
| 5 | Audit SQL: filter `OBJECT_SCHEMA='QLBV'` + FGA | 1 | ✅ Đã gộp |
| 6 | Mock audit data: 10 policies từ `sys_PH2.sql` | 1 | ✅ Đã gộp |
| 7 | Detect Giám đốc qua `CAPBAC` (không phải ROLE_GIAMDOC) | 2 | ✅ Đã gộp |
| 8 | `MAKTV NOT NULL`: BS thêm dịch vụ phải nhập MAKTV đề xuất | 2 | ✅ Đã gộp |
| 9 | DONTHUOC TRANGTHAI sửa: `EditDonThuocForm` với ComboBox | 2 | ✅ Đã gộp |
| 10 | Tab "Báo Cáo Điều Trị": `VW_BaoCaoDieuTri` + `sp_KhoiTaoHSBAKhancap` | 2 | ✅ Đã gộp |
| 11 | Nút "Tính Chi Phí": `fn_TinhTongChiPhiDieuTri` cho DPV | 2 | ✅ Đã gộp |
| 12 | Audit "Đăng Nhập Thất Bại": `AuditSession` LOGON failures | 2 | ✅ Đã gộp |

---

## 4. Hạng Mục Không Gộp Vào WinForms (Không Cần Thiết)

| Hạng Mục | Lý Do Không Gộp |
|---|---|
| OLS `SA_*` API calls (`set_user_labels`, `set_user_privs`) | Tác vụ DB admin, chạy 1 lần bởi SYS/QLBV. WinForms chỉ cần DB đã setup đúng rồi INSERT THONGBAO bình thường — Oracle tự gán `OLS_COL` theo `row_label`. |
| Tạo OLS policy, levels, compartments, groups | Cấu hình DB thuần túy (`sys_PH2.sql` §Yêu cầu 2). Không thể và không nên thực hiện từ WinForms. |
| Flashback Recovery demo | Thao tác DBA/SQL Developer. Không phải tính năng end-user. |
| `CREATE USER`, `GRANT DBA`, `GRANT LBAC_DBA` | Thuộc `sys_PH2.sql` §setup — phạm vi DBA, ngoài tầm WinForms. |
| `AuditSession` — chạy lệnh `CREATE AUDIT POLICY` | Setup DB, không phải query UI. WinForms chỉ đọc kết quả audit (đã thêm tab). |

---

## 5. Điều Kiện Tiên Quyết Để WinForms Hoạt Động Đúng

Để toàn bộ các tính năng WinForms hoạt động đúng với DB Oracle, cần đảm bảo đã chạy theo đúng thứ tự:

```
[1] sys_PH2.sql    (Lines 1–230)   : Tạo QLBV user, cấp quyền, khởi tạo OLS policy
[2] admin_ph2.sql  (Toàn bộ file)  : Tạo bảng, data, roles, VPD, OLS labels
[3] sys_PH2.sql    (Lines 231+)    : FGA + Unified Audit + Standard Audit policies
```

Sau đó login WinForms bằng các tài khoản tương ứng (DPV01, BS0001, KTV01, BN000001, NV0001...).
