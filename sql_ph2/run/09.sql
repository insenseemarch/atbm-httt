-- ============================================================================
-- 09.sql - Kịch bản thực thi giả lập sinh Nhật ký Kiểm toán (Demo)
-- Bám sát cấu hình chính sách kiểm toán trong 06.sql
-- ============================================================================
-- Hướng dẫn chạy:
--   1. Đảm bảo đã thực thi chuỗi file từ 01.sql -> 06.sql thành công.
--   2. Mở từng kết nối tương ứng với chỉ dẫn "CONNECTION" ở mỗi mục.
--   3. Copy và chạy các câu lệnh nghiệp vụ để sinh log.
-- ============================================================================

SET SERVEROUTPUT ON;
SET LINESIZE 200;

-- ============================================================================
-- PHẦN I: GIẢ LẬP KIỂM TOÁN HỆ THỐNG (MỤC 3.1 TRONG 06.SQL)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- [09-SYSTEM-01] Thử nghiệm Đăng nhập THẤT BẠI (Logon Failure)
-- CONNECTION: Hãy cố tình kết nối bằng một User bất kỳ nhưng gõ SAI mật khẩu
-- Ví dụ trên CMD hoặc SQL*Plus: sqlplus QLBV/sai_mat_khau@localhost:1521/XEPDB1
-- KẾT QUẢ MONG ĐỢI: Hệ thống từ chối đăng nhập và âm thầm ghi vết lỗi ORA-01017.
-- ----------------------------------------------------------------------------


-- ============================================================================
-- PHẦN II: STANDARD AUDIT - NGỮ CẢNH VAI TRÒ CHUẨN (MỤC 3.2 TRONG 06.SQL)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- [09-DPV-01] Ngữ cảnh 1: Điều phối viên cập nhật thông tin BENHNHAN thành công
-- CONNECTION: NV0001 (Tài khoản Điều phối viên) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditSucDPVUpdateBN
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG (Ghi log thành công)
-- ----------------------------------------------------------------------------
-- Giả lập ứng dụng WinForm kích hoạt định danh vai trò của Session
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_DPV');

-- Thực hiện cập nhật thông tin bệnh nhân (cập nhật số nhà thành chính nó)
UPDATE QLBV.BENHNHAN 
SET    SONHA = SONHA 
WHERE  MABN = 'BN000001';

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-DPV-02] Ngữ cảnh 2: Điều phối viên cập nhật thông tin trên HSBA thành công
-- CONNECTION: NV0001 (Tài khoản Điều phối viên) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditDPVUpdateHSBA
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG (Ghi log thành công)
-- ----------------------------------------------------------------------------
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_DPV');

-- Điều phối viên thực hiện quyền phân công Khoa điều trị trên Hồ sơ bệnh án
UPDATE QLBV.HSBA 
SET    MAKHOA = MAKHOA 
WHERE  MAHSBA = 'HS000001';

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-KTV-01] Ngữ cảnh 3: Kỹ thuật viên cập nhật dịch vụ qua View thành công
-- CONNECTION: KTV001 (Tài khoản Kỹ thuật viên khoa TK) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditSucKTVUpdateDV
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG (Ghi log thành công)
-- ----------------------------------------------------------------------------
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_KTV');

-- Kỹ thuật viên cập nhật kết quả dịch vụ cận lâm sàng thông qua View được phép
UPDATE QLBV.VW_KTV_XemHSBADV 
SET    KETQUA = KETQUA 
WHERE  MAHSBA = 'HS000001';

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-BACSI-01] Ngữ cảnh 4: Bác sĩ cập nhật đơn thuốc của mình thành công
-- CONNECTION: BS0001 (Bác sĩ điều trị) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditSucBSUpdateDT
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG (Ghi log thành công)
-- ----------------------------------------------------------------------------
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_BACSI');

-- Bác sĩ cập nhật liều dùng đơn thuốc do mình phụ trách
UPDATE QLBV.DONTHUOC 
SET    LIEUDUNG = LIEUDUNG 
WHERE  MAHSBA = 'HS000001';

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-BACSI-02] Ngữ cảnh 5: Bác sĩ cố tình cập nhật/xóa thông tin nhân viên
-- CONNECTION: BS0001 (Tài khoản Bác sĩ) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditFailBSUpdateNV
-- KẾT QUẢ MONG ĐỢI: THẤT BẠI (Bị chặn do thiếu đặc quyền ORA-01031 và ghi log)
-- ----------------------------------------------------------------------------
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_BACSI');

-- Bác sĩ cố tình sửa thông tin cơ sở của đồng nghiệp (Hành vi vượt quyền)
UPDATE QLBV.NHANVIEN 
SET    COSO = N'Hà Nội' 
WHERE  MANV = 'NV0001';



-- ----------------------------------------------------------------------------
-- [09-BACSI-FUNC-NEW] Ngữ cảnh 6 (Mới): Bác sĩ gọi hàm kiểm tra dị ứng thuốc thành công
-- CONNECTION: BS0001 (Tài khoản Bác sĩ) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditSucBSExecFunc
-- ----------------------------------------------------------------------------
EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_BACSI');

DECLARE
    v_result NVARCHAR2(500);
BEGIN
    v_result := QLBV.fn_KiemTraDiUngThuoc('BN000001');
    DBMS_OUTPUT.PUT_LINE('Kết quả dị ứng của BN: ' || v_result);
END;
/


-- ============================================================================
-- PHẦN III: TÌNH HUỐNG KIỂM TOÁN NGHIỆP VỤ CHI TIẾT (MỤC 3.3 TRONG 06.SQL)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- [09-FGA-SITU-A] Tình huống a: Sửa đổi ĐƠN THUỐC sau khi đã được tạo xong
-- CONNECTION: NV0006 (Bác sĩ phụ trách đơn thuốc này) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditSuaDonThuoc (FGA Policy)
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG chuyên môn nhưng sinh nhật ký FGA chi tiết
-- ----------------------------------------------------------------------------
-- Sửa đổi trường liều dùng lâm sàng của một đơn thuốc có sẵn
UPDATE QLBV.DONTHUOC 
SET    LIEUDUNG = LIEUDUNG
WHERE  MAHSBA = 'HS000001';

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-FGA-SITU-B] Tình huống b: Bác sĩ cập nhật hợp pháp trường lâm sàng trên HSBA
-- CONNECTION: BS0001 (Bác sĩ được phân công điều trị hồ sơ HS0001) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditBSUpdateHSBA_HopPhap (FGA Policy)
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG nghiệp vụ và ghi nhận log kiểm toán chi tiết
-- ----------------------------------------------------------------------------
-- Bác sĩ cập nhật diễn tiến bệnh án lâm sàng
UPDATE QLBV.HSBA 
SET    CHANDOAN = CHANDOAN 
WHERE  MAHSBA = 'HS000001' AND MABS = SYS_CONTEXT('USERENV', 'SESSION_USER');

COMMIT;


-- ----------------------------------------------------------------------------
-- [09-UNIFIED-SITU-C] Tình huống c: Cập nhật BẤT HỢP PHÁP trường y khoa lâm sàng
-- CONNECTION: NV0001 (Tài khoản Điều phối viên) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditIllegalUpdateHSBA (Unified Audit - WHENEVER NOT SUCCESSFUL)
-- KẾT QUẢ MONG ĐỢI: THẤT BẠI (Bị chính sách VPD chặn đứng, câu lệnh báo lỗi và ghi log)
-- ----------------------------------------------------------------------------
-- Nhân viên hành chính điều phối cố tình can thiệp sửa đổi Chẩn đoán y khoa
UPDATE QLBV.HSBA 
SET    CHANDOAN = CHANDOAN
WHERE  MAHSBA = 'HS0001';

ROLLBACK;

-- ============================================================================
-- PHẦN V: DEMO SP_XEM_LICHSU_DIEUTRI_BENHNHAN (BỔ SUNG)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- [09-DPV-01] DPV xem lịch sử điều trị khi có đơn khiếu nại
-- CONNECTION: NV0001 (hoặc QLBV) - Mật khẩu: nv123
-- POLICY KÍCH HOẠT: AuditGDXemLichSuBN
-- KẾT QUẢ MONG ĐỢI: THÀNH CÔNG + sinh audit log
-- ----------------------------------------------------------------------------

EXEC DBMS_SESSION.SET_IDENTIFIER('ROLE_DPV');

-- Gọi procedure để sinh log
DECLARE
    v_cur SYS_REFCURSOR;
BEGIN
    QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN(
        p_mabn   => 'BN000001',
        p_cursor => v_cur
    );
    CLOSE v_cur;
END;
/


-- ============================================================================
-- PHẦN IV: TRUY VẤN KIỂM TRA NHẬT KÝ KIỂM TOÁN (MỤC 3.4 TRONG 06.SQL)
-- ============================================================================
-- CONNECTION: QLBV (Có quyền SELECT ANY DICTIONARY) hoặc SYSDBA
-- ============================================================================

PROMPT === Querying Unified Audit Trail Results ===

-- 1. Xem nhật ký Đăng nhập thất bại (Hệ thống)
SELECT event_timestamp, dbusername, return_code, os_username, userhost
FROM   unified_audit_trail
WHERE  action_name = 'LOGON'
ORDER  BY event_timestamp DESC
FETCH FIRST 10 ROWS ONLY;

-- 2. Đọc dữ liệu Standard Audit của 6 ngữ cảnh vai trò chuẩn đã kiểm thử
SELECT event_timestamp, dbusername, action_name, object_name,
       return_code, unified_audit_policies
FROM   unified_audit_trail
WHERE  unified_audit_policies IN (
    'AUDITSUCDPVUPDATEBN',  -- NC1: Điều phối viên cập nhật BENHNHAN thành công
        'AUDITDPVUPDATEHSBA',   -- NC2: Điều phối viên cập nhật HSBA thành công
        'AUDITSUCKTVUPDATEDV',  -- NC3: Kỹ thuật viên cập nhật View dịch vụ thành công
        'AUDITSUCBSUPDATEDT',   -- NC4: Bác sĩ cập nhật ĐƠNTHUỐC thành công
        'AUDITFAILBSUPDATENV',  -- NC5: Bác sĩ cập nhật/xóa NHANVIEN (Thất bại - Vượt quyền)
        'AUDITDIEUPHOINHANSU',  -- NC6: Giám sát Điều phối viên thực thi sp_DieuPhoiNhanSu (Mới bổ sung)
        'AUDITSUCBSEXECPROC',   -- NC7: Bác sĩ thực thi sp_KhoiTaoHSBAKhancap thành công
        'AUDITSUCBSEXECFUNC',   -- NC8: Bác sĩ thực thi fn_KiemTraDiUngThuoc thành công
        'AUDITDPVXEMLICHSUBN',   -- NC9: Thành công
        'AUDITDPVXEMLICHSUBN_FAIL' -- NC9: Thất bại
        )
ORDER BY event_timestamp DESC;

-- 3. Đọc dữ liệu FGA của tình huống (a) Sửa Đơn Thuốc & (b) Bác sĩ Sửa HSBA hợp pháp
SELECT event_timestamp, dbusername, fga_policy_name, object_name,
       sql_text, return_code
FROM   unified_audit_trail
WHERE  fga_policy_name IN ('AUDITSUADONTHUOC', 'AUDITBSUPDATEHSBA_HOPPHAP')
ORDER  BY event_timestamp DESC;

-- 4. Đọc dữ liệu lỗi của tình huống (c) Sửa HSBA trái phép & (d) Can thiệp trái phép bảng HSBA_DV
SELECT event_timestamp, dbusername, action_name, object_name, return_code,
       unified_audit_policies, sql_text
FROM   unified_audit_trail
WHERE  unified_audit_policies IN ('AUDITILLEGALUPDATEHSBA', 'AUDITILLEGALHSBADV')
ORDER  BY event_timestamp DESC;