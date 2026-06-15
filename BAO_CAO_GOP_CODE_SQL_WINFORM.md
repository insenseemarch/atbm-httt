# Báo Cáo Gộp Code SQL vào WinForms — Phân Hệ 2

**Ngày thực hiện:** 14/06/2026  
**File nguồn SQL:** `sql_ph2/admin_ph2.sql`, `sql_ph2/sys_PH2.sql`, `sql_ph2/schema_phanhe2.sql`  
**File đích WinForms:** `winform/PhanHe1/SubSystem2Form.cs`  

---

## 1. Tổng Quan

Mục tiêu là đồng bộ hóa các câu lệnh SQL trong giao diện WinForms (`SubSystem2Form.cs`) với logic SQL đã được nhóm thống nhất trong 2 file `admin_ph2.sql` và `sys_PH2.sql`. Quá trình gộp bao gồm: sửa schema owner, sửa tên cột, sửa constraint UI, và cập nhật Audit SQL.

---

## 2. Những Thay Đổi Đã Gộp

### 2.1. Đổi Schema Prefix: `APP_ADMIN` → `QLBV` ✅

**Nguồn:** `schema_phanhe2.sql` — tất cả bảng được tạo dưới user `QLBV` (không phải `APP_ADMIN`)

**Vấn đề cũ:** `SubSystem2Form.cs` dùng `APP_ADMIN.BENHNHAN`, `APP_ADMIN.HSBA`, v.v. trong toàn bộ các câu truy vấn.

**Đã sửa:** Thay thế toàn bộ 107 lần tham chiếu bảng, đổi sang đúng schema `QLBV`:

| Cũ | Mới |
|---|---|
| `APP_ADMIN.BENHNHAN` | `QLBV.BENHNHAN` |
| `APP_ADMIN.NHANVIEN` | `QLBV.NHANVIEN` |
| `APP_ADMIN.HSBA` | `QLBV.HSBA` |
| `APP_ADMIN.HSBA_DV` | `QLBV.HSBA_DV` |
| `APP_ADMIN.DONTHUOC` | `QLBV.DONTHUOC` |
| `APP_ADMIN.THONGBAO` | `QLBV.THONGBAO` |

**Lý do quan trọng:** VPD policy (`fn_vpdBenhNhan`, `fn_vpdHSBA`, ...) và OLS policy (`OLS_QLBV_POLICY`) chỉ được apply trên các bảng thuộc schema `QLBV`. Nếu query sai schema thì các cơ chế bảo mật không hoạt động.

---

### 2.2. Sửa Tên Cột Sai Trong Interface Kỹ Thuật Viên ✅

**Nguồn:** `schema_phanhe2.sql` — định nghĩa bảng `HSBA_DV`:
```sql
CREATE TABLE HSBA_DV (
    MAHSBA  VARCHAR2(20)   NOT NULL,
    LOAIDV  NVARCHAR2(100) NOT NULL,   -- đúng tên
    NGAYDV  DATE           NOT NULL,   -- đúng tên
    MAKTV   VARCHAR2(20)   NOT NULL,
    KETQUA  NVARCHAR2(2000),
    CONSTRAINT PK_HSBA_DV PRIMARY KEY (MAHSBA, LOAIDV, NGAYDV)
);
```

**Vấn đề cũ:** KTV double-click để cập nhật `KETQUA` dùng tên cột sai:
```csharp
// SAI
string maDv = r.Cells["MADV"].Value?.ToString();
string ngay = r.Cells["NGAY"].Value...;
service.ExecuteNonQuery("... AND MADV=... AND NGAY=...");
```

**Đã sửa:**
```csharp
// ĐÚNG
string loaiDv = r.Cells["LOAIDV"].Value?.ToString();
string ngayDv = r.Cells["NGAYDV"].Value != null
    ? Convert.ToDateTime(r.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy") : "";
service.ExecuteNonQuery(
    $"UPDATE QLBV.HSBA_DV SET KETQUA=N'{...}'
      WHERE MAHSBA='{ma}' AND LOAIDV=N'{loaiDv}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')"
);
```

---

### 2.3. Sửa BS Delete HSBA_DV — Thêm NGAYDV vào WHERE ✅

**Nguồn:** `schema_phanhe2.sql` — PK composite `(MAHSBA, LOAIDV, NGAYDV)`

**Vấn đề cũ:** DELETE chỉ dùng `MAHSBA + LOAIDV` → có thể xóa nhầm nhiều dòng nếu cùng một loại dịch vụ xuất hiện nhiều ngày:
```csharp
// SAI — thiếu NGAYDV, xóa tất cả dịch vụ cùng tên
service.ExecuteNonQuery(
    $"DELETE FROM QLBV.HSBA_DV WHERE MAHSBA='{ma}' AND LOAIDV=N'{dv}'"
);
```

**Đã sửa:**
```csharp
// ĐÚNG — xóa đúng 1 dòng theo PK đầy đủ
string ngayDv = dgvBsDV.CurrentRow.Cells["NGAYDV"].Value != null
    ? Convert.ToDateTime(dgvBsDV.CurrentRow.Cells["NGAYDV"].Value).ToString("dd/MM/yyyy") : "";
service.ExecuteNonQuery(
    $"DELETE FROM QLBV.HSBA_DV
      WHERE MAHSBA='{ma}' AND LOAIDV=N'{dv}' AND NGAYDV=TO_DATE('{ngayDv}','DD/MM/YYYY')"
);
```

---

### 2.4. DONTHUOC.TRANGTHAI: TextBox → ComboBox ✅

**Nguồn:** `schema_phanhe2.sql` — constraint hạn chế giá trị:
```sql
TRANGTHAI VARCHAR2(20) DEFAULT 'CHUA_TAO_XONG' NOT NULL,
CONSTRAINT CHK_DT_TRANGTHAI CHECK (TRANGTHAI IN ('CHUA_TAO_XONG', 'DA_TAO_XONG'))
```

**Nguồn:** `sys_PH2.sql` §3.3.a — FGA policy kích hoạt khi cập nhật đơn thuốc đã hoàn thành:
```sql
dbms_fga.add_policy(
    object_name     => 'DONTHUOC',
    policy_name     => 'AuditSuaDonThuoc',
    audit_condition => 'TRANGTHAI = ''DA_TAO_XONG''',  -- phân biệt 2 trạng thái
    statement_types => 'UPDATE'
);
```

**Vấn đề cũ:** `DonThuocAddForm` dùng TextBox tự do cho `TRANGTHAI` → người dùng có thể nhập bất kỳ giá trị nào, vi phạm CHECK constraint Oracle → ORA-02290.

**Đã sửa:** Thay TextBox bằng ComboBox với 2 giá trị cố định:
```csharp
var cmbTrangThai = new ComboBox {
    DropDownStyle = ComboBoxStyle.DropDownList
};
cmbTrangThai.Items.AddRange(new object[] { "CHUA_TAO_XONG", "DA_TAO_XONG" });
cmbTrangThai.SelectedIndex = 0;  // mặc định CHUA_TAO_XONG
// ...
TrangThai = cmbTrangThai.SelectedItem.ToString();
```

---

### 2.5. Cập Nhật Audit SQL ✅

**Nguồn:** `sys_PH2.sql` §3.4 — query đọc nhật ký kiểm toán:
```sql
-- Standard/Unified Audit
SELECT unified_audit_policies, dbusername, action_name,
       object_schema, object_name, return_code, event_timestamp
FROM unified_audit_trail
WHERE object_schema = 'QLBV'
ORDER BY event_timestamp DESC;

-- FGA
SELECT event_timestamp, dbusername, fga_policy_name, sql_text
FROM unified_audit_trail
WHERE fga_policy_name IS NOT NULL
ORDER BY event_timestamp DESC;
```

**Vấn đề cũ:**
```csharp
// SAI — lọc theo user thay vì schema, bỏ qua các entry FGA
WHERE DBUSERNAME NOT IN ('SYS','SYSTEM')
```

**Đã sửa (kết hợp 2 query thành 1):**
```csharp
string sql = @"
    SELECT
        TO_CHAR(EVENT_TIMESTAMP, 'DD/MM/YYYY HH24:MI:SS') AS ""THỜI GIAN"",
        DBUSERNAME                                         AS ""NGƯỜI DÙNG"",
        ACTION_NAME                                        AS ""HÀNH ĐỘNG"",
        OBJECT_NAME                                        AS ""ĐỐI TƯỢNG"",
        NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES)       AS ""POLICY"",
        SQL_TEXT                                           AS ""CHI TIẾT MÔ TẢ""
    FROM UNIFIED_AUDIT_TRAIL
    WHERE (OBJECT_SCHEMA = 'QLBV' OR FGA_POLICY_NAME IS NOT NULL)
      AND DBUSERNAME NOT IN ('SYS', 'SYSTEM')
    ORDER BY EVENT_TIMESTAMP DESC
    FETCH FIRST 200 ROWS ONLY";
```

**Thêm cột `POLICY`:** hiển thị tên policy kích hoạt (Standard hoặc FGA), giúp phân biệt nguồn gốc audit entry.

---

### 2.6. Cập Nhật Mock Audit Data ✅

**Nguồn:** `sys_PH2.sql` §3.2 (5 Standard Audit) + §3.3 (3 FGA + 2 Unified not-successful)

**Vấn đề cũ:** Mock data cũ dùng tên policy giả, không khớp với 10 policies thực được định nghĩa.

**Đã cập nhật mock data phản ánh đúng 10 policies:**

| Loại | Policy Name | Ngữ cảnh |
|---|---|---|
| Standard ✅ | `AuditNVUpdateBN` | NV0001/NV0007 UPDATE BENHNHAN thành công |
| Standard ❌ | `AuditBSUpdateNVFail` | BS0001/BS0002 cố UPDATE/DELETE NHANVIEN thất bại |
| Standard | `AuditBSExecCapCuu` | BS0001/BS0002 EXECUTE `sp_KhoiTaoHSBAKhancap` |
| Standard ✅ | `AuditBSSelectBaoCao` | BS0001/BS0002 SELECT `VW_BaoCaoDieuTri` thành công |
| Standard | `AuditNVCalcFee` | NV0001/NV0002 EXECUTE `fn_TinhTongChiPhiDieuTri` |
| FGA | `AuditSuaDonThuoc` | UPDATE DONTHUOC khi `TRANGTHAI='DA_TAO_XONG'` |
| FGA | `AuditBSUpdateHSBA` | BS UPDATE `CHANDOAN/DIEUTRI/KETLUAN` của HSBA mình phụ trách |
| FGA | `AuditKTVUpdateKetQua` | KTV UPDATE `KETQUA` trong `HSBA_DV` |
| Unified ❌ | `AuditIllegalUpdateHSBA` | UPDATE HSBA thất bại (VPD chặn) |
| Unified ❌ | `AuditIllegalHSBADV` | INSERT/UPDATE/DELETE `HSBA_DV` thất bại |

*(✅ whenever successful, ❌ whenever not successful)*

---

## 3. Những Phần Chưa Gộp Được

### 3.1. Constraint `MAKTV NOT NULL` — Bác Sĩ Tạo HSBA_DV ⚠️

**Vấn đề:** `schema_phanhe2.sql` định nghĩa `MAKTV VARCHAR2(20) NOT NULL` và FK `MAKTV REFERENCES NHANVIEN(MANV)`. Nhưng trong quy trình nghiệp vụ, Bác sĩ chỉ định dịch vụ và **DPV mới phân công KTV sau**. Form bác sĩ tạo HSBA_DV hiện insert không có `MAKTV` → **ORA-01400: cannot insert NULL**.

**Cần bổ sung:**
- **Cách 1 (sửa schema):** Đổi `MAKTV VARCHAR2(20) NOT NULL` → `MAKTV VARCHAR2(20)` trong `schema_phanhe2.sql` để cho phép NULL tạm thời khi bác sĩ chỉ định. DPV cập nhật MAKTV sau khi phân công.
- **Cách 2 (sửa UI):** Thêm field `MAKTV` vào `HsbaDvAddForm` khi `isDoctor=true`, bắt buộc nhập. Tuy nhiên điều này trái với quy trình nghiệp vụ thực tế.

> **Khuyến nghị:** Chọn Cách 1 — sửa schema cho `MAKTV NULL`, giữ đúng quy trình DPV phân công KTV.

---

### 3.2. TRANGTHAI Trong Form Cập Nhật Đơn Thuốc ⚠️

**Vấn đề:** Form **thêm** đơn thuốc đã fix dùng ComboBox (mục 2.4). Nhưng form **cập nhật** (`EditRowForm`) là generic — dùng `Dictionary<string,string>` → tất cả field đều là TextBox tự do. Người dùng vẫn có thể nhập `TRANGTHAI` sai khi edit → vi phạm CHECK constraint.

**Cần bổ sung:**
- Tạo form chuyên biệt `EditDonThuocForm` với ComboBox cho `TRANGTHAI`.
- Hoặc mở rộng `EditRowForm` để hỗ trợ truyền vào danh sách giá trị cho field cụ thể (DropDown mode).

---

### 3.3. OLS Label Tự Động Khi Gửi Thông Báo ⚠️

**Vấn đề:** `sys_PH2.sql` apply policy `OLS_QLBV_POLICY` với option `WRITE_CONTROL` lên bảng `THONGBAO`. Khi insert, Oracle tự động gán `OLS_COL` theo `row_label` hiện tại của user. Điều này **chỉ hoạt động đúng nếu Oracle OLS đã được setup đầy đủ** và user đã được gán nhãn qua `sa_user_admin.set_user_labels`.

Nếu OLS chưa setup, câu INSERT từ WinForms sẽ thất bại vì `OLS_COL` không có giá trị.

**Cần bổ sung:**
- Đảm bảo chạy **đầy đủ** `sys_PH2.sql` trước khi test WinForms (phần gán nhãn từ dòng 182–237).
- Nếu muốn WinForms hoạt động độc lập (không phụ thuộc OLS), cần thêm logic gọi `SA_SESSION.SET_LABEL('OLS_QLBV_POLICY', ...)` hoặc truyền label vào INSERT — nhưng cần biết nhãn của từng user trước.

---

### 3.4. Detect Giám Đốc Qua `CAPBAC` Thay Vì `ROLE_GIAMDOC` ⚠️

**Vấn đề:** Code hiện tại phát hiện Giám đốc qua:
```csharp
if (currentRoles.Contains("ROLE_GIAMDOC") || currentUser.StartsWith("GD", ...))
    return UserRole.GIAMDOC;
```
Nhưng `admin_ph2.sql` và `sys_PH2.sql` **không tạo `ROLE_GIAMDOC`**. Giám đốc được phân biệt qua `CAPBAC = 'Ban Giám đốc'` trong bảng `NHANVIEN` và OLS label `BGD`. Test account `GD0001` có thể không tồn tại trong data thật.

**Cần bổ sung:**
```csharp
// Sau khi login thành công, query thêm CAPBAC
var dt = service.Query(
    $"SELECT CAPBAC FROM QLBV.NHANVIEN WHERE MANV = '{currentUser}'"
);
if (dt.Rows.Count > 0 && dt.Rows[0]["CAPBAC"]?.ToString() == "Ban Giám đốc")
    return UserRole.GIAMDOC;
```

---

### 3.5. Tích Hợp `VW_BaoCaoDieuTri` và `sp_KhoiTaoHSBAKhancap` ⚠️

**Vấn đề:** `sys_PH2.sql` tạo các đối tượng phục vụ audit:
- View `QLBV.VW_BaoCaoDieuTri` — được audit bởi `AuditBSSelectBaoCao`
- Procedure `QLBV.sp_KhoiTaoHSBAKhancap` — được audit bởi `AuditBSExecCapCuu`
- Function `QLBV.fn_TinhTongChiPhiDieuTri` — được audit bởi `AuditNVCalcFee`

WinForms chưa có UI nào gọi các đối tượng này → audit policies tương ứng không bao giờ kích hoạt khi chạy WinForms.

**Cần bổ sung:**
| Đối tượng | Cần thêm vào UI |
|---|---|
| `VW_BaoCaoDieuTri` | Tab "Báo cáo điều trị" trong interface Bác sĩ: `SELECT * FROM QLBV.VW_BaoCaoDieuTri WHERE MABS = SESSION_USER` |
| `sp_KhoiTaoHSBAKhancap` | Nút "Tạo HSBA cấp cứu" trong interface Bác sĩ: gọi procedure với tham số MAHSBA, MABN, MABS, MAKHOA |
| `fn_TinhTongChiPhiDieuTri` | Nút "Tính chi phí" trong tab HSBA của DPV: `SELECT QLBV.fn_TinhTongChiPhiDieuTri(:mahsba) FROM DUAL` |

---

### 3.6. `AuditSession` — Kiểm Toán Đăng Nhập Thất Bại ⚠️

**Vấn đề:** `sys_PH2.sql` tạo:
```sql
create audit policy AuditSession actions logon;
audit policy AuditSession whenever not successful;
```
WinForms không có UI xem log đăng nhập thất bại. Mock data cũng chưa có loại entry này.

**Cần bổ sung:**
- Thêm tab riêng "Đăng nhập thất bại" trong Audit UI, query:
  ```sql
  SELECT TO_CHAR(EVENT_TIMESTAMP,'DD/MM/YYYY HH24:MI:SS'), DBUSERNAME,
         ACTION_NAME, RETURN_CODE
  FROM UNIFIED_AUDIT_TRAIL
  WHERE ACTION_NAME = 'LOGON' AND RETURN_CODE != 0
  ORDER BY EVENT_TIMESTAMP DESC
  ```
- Hoặc thêm mock row loại `LOGON` vào mock data hiện tại.

---

## 4. Tóm Tắt

| Hạng mục | Trạng thái |
|---|---|
| Schema prefix `APP_ADMIN` → `QLBV` (107 vị trí) | ✅ Đã gộp |
| KTV cột `MADV`→`LOAIDV`, `NGAY`→`NGAYDV` | ✅ Đã gộp |
| BS delete HSBA_DV thêm `NGAYDV` vào WHERE | ✅ Đã gộp |
| DONTHUOC TRANGTHAI TextBox → ComboBox | ✅ Đã gộp |
| Audit SQL filter theo `OBJECT_SCHEMA='QLBV'` + FGA | ✅ Đã gộp |
| Mock data khớp 10 audit policies `sys_PH2.sql` | ✅ Đã gộp |
| `MAKTV NOT NULL` mâu thuẫn với workflow bác sĩ | ⚠️ Chưa gộp — cần sửa schema |
| TRANGTHAI trong EditRowForm (form cập nhật) | ⚠️ Chưa gộp — cần form chuyên biệt |
| OLS auto-label khi INSERT THONGBAO | ⚠️ Chưa gộp — phụ thuộc DB setup đúng |
| Detect Giám đốc qua `CAPBAC` thay `ROLE_GIAMDOC` | ⚠️ Chưa gộp — cần sửa login logic |
| UI gọi `VW_BaoCaoDieuTri`, `sp_KhoiTaoHSBAKhancap`, `fn_TinhTongChiPhiDieuTri` | ⚠️ Chưa gộp — cần thêm tab/nút mới |
| UI xem log `AuditSession` (LOGON thất bại) | ⚠️ Chưa gộp — cần thêm tab Audit |
