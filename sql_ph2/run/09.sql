-- ============================================================
-- 09.sql - Kịch bản chạy thử (lưu vết mẫu) cho TOÀN BỘ audit
--           policy đã cài đặt trong 06.sql
-- ============================================================
-- Mục đích:
--   06.sql chỉ TẠO ra các audit policy (Standard Audit + FGA),
--   chưa có thao tác nào thực sự được audit. File 09.sql này
--   thực thi LẦN LƯỢT từng ngữ cảnh (thành công + thất bại) đã
--   khai báo trong 06.sql để sinh bản ghi vào UNIFIED_AUDIT_TRAIL
--   (và DBA_FGA_AUDIT_TRAIL cho phần FGA), phục vụ minh họa /
--   demo cho Yêu cầu 3 và làm dữ liệu đầu vào cho [07-AUDIT-01].
--
-- File này chạy SAU:
--   01.sql -> 02.sql -> 03.sql -> 04.sql -> 05.sql -> 06.sql
--
-- Nguyên tắc đặt tên section: [09-<ROLE>-<STT>]
-- Mỗi section ghi rõ:
--   - Kết nối (user/role) cần dùng trong SQL Developer
--   - Policy (06.sql) mà thao tác bên dưới dự kiến kích hoạt
--   - Kết quả mong đợi: THÀNH CÔNG hay THẤT BẠI
--
-- LƯU Ý QUAN TRỌNG:
--   1. Phải mở MỖI section bằng một kết nối (connection/worksheet)
--      đúng với user được ghi trong tiêu đề section đó. Không
--      chạy toàn bộ file bằng 1 connection duy nhất, vì
--      sys_context('userenv','session_user') trong predicate
--      của từng audit policy ở 06.sql chính là điều kiện kích
--      hoạt audit.
--   2. Tên cột / PK của DONTHUOC, HSBA_DV... được giả định theo
--      ngữ cảnh xuất hiện trong 02.sql/06.sql/07.sql. Nếu schema
--      thực tế đặt tên khác, chỉnh lại giá trị cột cho khớp.
--   3. Các UPDATE/INSERT thành công đều ROLLBACK ngay sau khi
--      thực thi (trừ khi ghi chú khác) để không làm bẩn dữ liệu
--      demo — bản ghi audit vẫn được ghi nhận tại thời điểm câu
--      lệnh thực thi, không phụ thuộc COMMIT/ROLLBACK.
--   4. Sau khi chạy xong toàn bộ section, dùng lại các query ở
--      mục 3.4 của 06.sql (hoặc [09-VERIFY] cuối file) để xem
--      log.
-- ============================================================

SET SERVEROUTPUT ON
SET LINESIZE 260
SET PAGESIZE 100


-- ============================================================
-- [09-LOGON-01] Kết nối: dùng CMD/SQL*Plus, đăng nhập SAI mật khẩu
-- Policy kích hoạt: AuditSession (logon thất bại)
-- Kết quả mong đợi: THẤT BẠI (ORA-01017 invalid username/password)
-- ============================================================
-- Không thể tạo lỗi đăng nhập bằng 1 script đang chạy trong 1
-- session đã kết nối thành công, nên thao tác này thực hiện
-- THỦ CÔNG ngoài SQL Developer (terminal CMD/PowerShell):
--
--   sqlplus bs0001/sai_mat_khau@localhost:1521/xepdb1
--
-- Hoặc trong SQL Developer: tạo 1 connection mới với mật khẩu
-- sai cho bất kỳ user nghiệp vụ nào (vd BS0001, NV0001, KTV001,
-- BN000001...) rồi bấm Connect/Test, sẽ phát sinh 1 bản ghi
-- LOGON whenever not successful.


-- ============================================================
-- [TC1: Điều phối viên cập nhật thông tin bệnh nhân] 
-- Kết nối: NV0001 (hoặc NV0007) @ XEPDB1
-- Policy kích hoạt: AuditSucDPVUpdateBN
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
UPDATE QLBV.BENHNHAN
SET    SONHA = SONHA
WHERE  ROWNUM = 1;

ROLLBACK;


-- ============================================================
-- [TC2: Bác sĩ xem view báo cáo điều trị] 
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditSucBSSelectView
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
SELECT *
FROM   QLBV.VW_BaoCaoDieuTri
WHERE  ROWNUM <= 5;


-- ============================================================
-- [TC3: Bác sĩ thực thi stored procedure khởi tạo HSBA khẩn cấp] 
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditSucBSExecProc
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
-- Đổi mã hồ sơ (p_mahsba)/bệnh nhân (p_mabn)/khoa (p_makhoa)
-- nếu trùng dữ liệu sẵn có, để tránh lỗi PRIMARY KEY/FOREIGN KEY.
DECLARE
    v_mabn   QLBV.BENHNHAN.MABN%TYPE;
    v_makhoa QLBV.NHANVIEN.MAKHOA%TYPE;
BEGIN
    SELECT MABN
    INTO v_mabn
    FROM QLBV.BENHNHAN
    WHERE ROWNUM = 1;

    SELECT MAKHOA
    INTO v_makhoa
    FROM QLBV.NHANVIEN
    WHERE MANV = USER;

    QLBV.sp_KhoiTaoHSBAKhancap(
        p_mahsba => 'HS_DEMO09_BS',
        p_mabn   => v_mabn,
        p_mabs   => USER,
        p_makhoa => v_makhoa
    );
END;
/

ROLLBACK;


-- ============================================================
-- [TC4: Điều phối viên thực thi function tính tổng chi phí điều trị]
-- Kết nối: NV0001 (hoặc NV0002) @ XEPDB1
-- Policy kích hoạt: AuditSucDPVExecFunc
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
DECLARE
    v_total  NUMBER;
    v_mahsba QLBV.HSBA.MAHSBA%TYPE;
BEGIN
    SELECT MAHSBA
    INTO v_mahsba
    FROM QLBV.HSBA
    WHERE ROWNUM = 1;

    v_total := QLBV.fn_TinhTongChiPhiDieuTri(
        p_mahsba => v_mahsba
    );

    DBMS_OUTPUT.PUT_LINE('Tong dich vu: ' || v_total);
END;
/

-- ============================================================
-- [TC4.5: Điều phối viên cập nhật phân công Khoa/Bác sĩ điều trị trên HSBA]
-- Kết nối: NV0001 (hoặc NV0002) @ XEPDB1
-- Policy kích hoạt: AuditDPVUpdateHSBA
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
UPDATE QLBV.HSBA
SET    MAKHOA = MAKHOA
WHERE  ROWNUM = 1;

ROLLBACK;


-- ============================================================
-- [TC5: Kỹ thuật viên cập nhật kết quả dịch vụ trong HSBA_DV]
-- Kết nối: KTV001 (hoặc KTV002) @ XEPDB1
-- Policy kích hoạt: AuditSucKTVUpdateDV
-- FGA kích hoạt thêm: AuditKTVUpdateKetQua (audit_column = KETQUA)
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
UPDATE QLBV.VW_KTV_XemHSBADV
SET    KETQUA = KETQUA
WHERE  ROWNUM = 1;

ROLLBACK;


-- ============================================================
-- [TC6: Bác sĩ cập nhật đơn thuốc thuộc HSBA mình điều trị] 
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditSucBSUpdateDT
-- FGA kích hoạt thêm: AuditSuaDonThuoc, Unified AuditDonThuocUpdate
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
UPDATE QLBV.DONTHUOC
SET    LIEUDUNG = LIEUDUNG
WHERE  MAHSBA IN (SELECT MAHSBA FROM QLBV.HSBA WHERE MABS = USER)
AND    ROWNUM = 1;

ROLLBACK;


-- ============================================================
-- [TC7: Bệnh nhân xem thông tin cá nhân qua view được cấp quyền]
-- Kết nối: BN000001 (hoặc BN000002) @ XEPDB1
-- Policy kích hoạt: AuditBonusBNSelectInfo
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
SELECT *
FROM   QLBV.VW_BenhNhan_Xemthongtin
WHERE  ROWNUM <= 5;


-- ============================================================
-- [3.3.a+: Unified Audit ghi nhận INSERT/UPDATE ĐƠNTHUỐC, không dùng cột trạng thái]
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: Unified AuditDonThuocInsert
-- Kết quả mong đợi: THÀNH CÔNG
-- (Điều chỉnh tên cột PK/NGAYDT/TENTHUOC/LIEUDUNG theo đúng 02.sql
--  nếu cấu trúc bảng DONTHUOC khác với giả định bên dưới.)
-- ============================================================
INSERT INTO QLBV.DONTHUOC (MAHSBA, NGAYDT, TENTHUOC, LIEUDUNG)
VALUES (
    (SELECT MAHSBA FROM QLBV.HSBA WHERE MABS = USER AND ROWNUM = 1),
    SYSDATE,
    N'Thuoc demo audit 09',
    N'Theo chi dinh - lieu mau demo'
);

ROLLBACK;


-- ============================================================
-- [TBx: Bác sĩ cố tình thực thi function tính tổng chi phí điều trị]
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditFailBSExecFunc
-- Kết quả mong đợi: THẤT BẠI (không có quyền EXECUTE)
-- ============================================================
DECLARE
    v_total NUMBER;
BEGIN
    v_total := QLBV.fn_TinhTongChiPhiDieuTri('HS_DEMO');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] EXEC fn_TinhTongChiPhiDieuTri: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB1: Bác sĩ cố cập nhật/xóa thông tin nhân viên]
-- Kết nối: BS0001 AuditFailBSUpdateNV(hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditFailBSUpdateNV
-- Kết quả mong đợi: THẤT BẠI (không có quyền UPDATE/DELETE NHANVIEN)
-- ============================================================
BEGIN
    UPDATE QLBV.NHANVIEN
    SET    COSO = COSO
    WHERE  ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] UPDATE NHANVIEN: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/

BEGIN
    DELETE FROM QLBV.NHANVIEN WHERE ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] DELETE NHANVIEN: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB2: Bệnh nhân cố xóa hồ sơ bệnh án]
-- Kết nối: BN000001 (hoặc BN000002) @ XEPDB1
-- Policy kích hoạt: AuditFailBNDeleteHSBA
-- Kết quả mong đợi: THẤT BẠI (không có quyền DELETE HSBA)
-- ============================================================
BEGIN
    DELETE FROM QLBV.HSBA WHERE ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] DELETE HSBA: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB3: Kỹ thuật viên cố xóa đơn thuốc]
-- Kết nối: KTV001 (hoặc KTV002) @ XEPDB1
-- Policy kích hoạt: AuditFailKTVDeleteDT
-- Kết quả mong đợi: THẤT BẠI (không có quyền DELETE DONTHUOC)
-- ============================================================
BEGIN
    DELETE FROM QLBV.DONTHUOC WHERE ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] DELETE DONTHUOC: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB4: Điều phối viên cố xóa hồ sơ bệnh án]
-- Kết nối: NV0001 (hoặc NV0002) @ XEPDB1
-- Policy kích hoạt: AuditFailDPVDeleteHSBA
-- Kết quả mong đợi: THẤT BẠI (không có quyền DELETE HSBA)
-- ============================================================
BEGIN
    DELETE FROM QLBV.HSBA WHERE ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] DELETE HSBA: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB5: Bệnh nhân cố cập nhật kết quả dịch vụ HSBA_DV]
-- Kết nối: BN000001 (hoặc BN000002) @ XEPDB1
-- Policy kích hoạt: AuditFailBNUpdateDV
-- Kết quả mong đợi: THẤT BẠI (không có quyền UPDATE HSBA_DV)
-- ============================================================
BEGIN
    UPDATE QLBV.HSBA_DV
    SET    KETQUA = KETQUA
    WHERE  ROWNUM = 1;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] UPDATE HSBA_DV: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB6: Kỹ thuật viên cố xem bảng BENHNHAN trực tiếp]
-- Kết nối: KTV001 (hoặc KTV002) @ XEPDB1
-- Policy kích hoạt: AuditFailKTVSelectBN
-- Kết quả mong đợi: THẤT BẠI (không có quyền SELECT trực tiếp BENHNHAN)
-- ============================================================
BEGIN
    FOR r IN (SELECT * FROM QLBV.BENHNHAN WHERE ROWNUM = 1) LOOP
        NULL;
    END LOOP;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] SELECT BENHNHAN: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [TB7: Bệnh nhân cố thực thi stored procedure dành cho bác sĩ]
-- Kết nối: BN000001 (hoặc BN000002) @ XEPDB1
-- Policy kích hoạt: AuditBonusBNExecProc
-- Kết quả mong đợi: THẤT BẠI (không có quyền EXECUTE - chỉ
--                    ROLE_BACSI mới được grant execute proc này)
-- ============================================================
BEGIN
    QLBV.sp_KhoiTaoHSBAKhancap(
        p_mahsba => 'HS_DEMO09_BN_FAIL',
        p_mabn   => USER,
        p_mabs   => 'BS0001',
        p_makhoa => 'K001'
    );
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] EXEC sp_KhoiTaoHSBAKhancap: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [3.3.b: BS update CHANDOAN/DIEUTRI/KETLUAN hợp pháp (FGA)]
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditBSUpdateHSBA (FGA)
-- Điều kiện: chỉ ghi vết khi MABS = session_user (đúng bác sĩ
--            điều trị hồ sơ), tức UPDATE hợp pháp.
-- Kết quả mong đợi: THÀNH CÔNG
-- ============================================================
UPDATE QLBV.HSBA
SET    KETLUAN = KETLUAN
WHERE  MABS = USER
AND    ROWNUM = 1;

ROLLBACK;


-- ============================================================
-- [3.3.c: UPDATE CHANDOAN/DIEUTRI/KETLUAN bất hợp pháp (Unified Audit - whenever not successful)
-- Không dùng FGA vì VPD chặn trước, FGA không kích hoạt được] 
-- Kết nối: BS0001 (hoặc BS0002) @ XEPDB1
-- Policy kích hoạt: AuditIllegalUpdateHSBA (Unified, whenever not successful)
-- Bối cảnh: VPD (03.sql/04.sql) chỉ cho BS thấy/sửa hồ sơ do
--           chính mình điều trị (MABS = session_user). Cố tình
--           UPDATE 1 hồ sơ KHÔNG thuộc về mình sẽ bị VPD chặn
--           và phát sinh lỗi (vd ORA-28115 nếu policy dạng
--           UPDATE_CHECK, hoặc 0 dòng nếu policy chỉ lọc SELECT
--           - tùy cấu hình VPD ở 03.sql/04.sql).
-- Kết quả mong đợi: THẤT BẠI
-- ============================================================
BEGIN
    UPDATE QLBV.HSBA
    SET    CHANDOAN = CHANDOAN
    WHERE  MABS != USER
    AND    ROWNUM = 1;
    
    IF SQL%ROWCOUNT = 0 THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi - VPD chan] UPDATE HSBA: Cap nhat 0 dong do VPD an du lieu.');
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi - VPD chan] UPDATE HSBA: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [3.3.d: INSERT/UPDATE/DELETE bất hợp pháp trên HSBA_DV (Unified Audit - whenever not successful)]
-- Kết nối: KTV001 (hoặc KTV002) @ XEPDB1
-- Policy kích hoạt: AuditIllegalHSBADV (Unified, whenever not successful)
-- Bối cảnh: KTV không thuộc khoa quản lý hồ sơ, hoặc thao tác
--           ngoài phạm vi dịch vụ được phân công, bị VPD/role
--           chặn khi INSERT/UPDATE/DELETE HSBA_DV.
-- Kết quả mong đợi: THẤT BẠI
-- ============================================================
BEGIN
    DELETE FROM QLBV.VW_KTV_XemHSBADV WHERE ROWNUM = 1;
    
    IF SQL%ROWCOUNT = 0 THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi - VPD chan] DELETE VW_KTV_XemHSBADV: Cap nhat 0 dong.');
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] DELETE VW_KTV_XemHSBADV: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/

BEGIN
    INSERT INTO QLBV.VW_KTV_XemHSBADV (MAHSBA, KETQUA)
    VALUES ('HS_KHONG_TON_TAI_HOAC_NGOAI_PHAM_VI', N'Demo loi VPD');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[Mong doi loi] INSERT VW_KTV_XemHSBADV: (Loi ' || SQLCODE || ' - dung ky vong) ' || SQLERRM);
END;
/


-- ============================================================
-- [09-VERIFY] Kết nối: SYSDBA - XEPDB1
-- Mục đích: Kiểm tra nhanh các bản ghi vừa sinh ra ở các section
--           trên đã được ghi vào audit trail hay chưa.
--           (Tham khảo đầy đủ hơn ở mục 3.4 của 06.sql)
-- ============================================================

-- Toàn bộ log liên quan QLBV, mới nhất trước (nếu ae chạy nhiều lần trong khoảng thời gian ngắn, 
--                                              bỏ cái comment ở AND)
SELECT event_timestamp, dbusername, action_name,
       object_schema, object_name, return_code,
       unified_audit_policies, fga_policy_name
FROM   unified_audit_trail
WHERE  object_schema = 'QLBV'
--AND    event_timestamp >= SYSTIMESTAMP - INTERVAL '1' HOUR
ORDER  BY event_timestamp DESC
FETCH FIRST 30 ROWS ONLY;

-- Riêng các lần đăng nhập thất bại vừa thực hiện ở [09-LOGON-01] (nếu ae chạy nhiều lần trong khoảng thời gian 
--                                                                  ngắn, bỏ cái comment ở AND)
SELECT event_timestamp, dbusername, return_code
FROM   unified_audit_trail
WHERE  action_name = 'LOGON'
AND    event_timestamp >= SYSTIMESTAMP - INTERVAL '1' HOUR
ORDER  BY event_timestamp DESC
FETCH FIRST 30 ROWS ONLY;

-- Riêng FGA (đơn thuốc, hồ sơ bệnh án hợp pháp, kết quả KTV).
SELECT event_timestamp, dbusername, fga_policy_name,
       object_schema, object_name, sql_text, return_code
FROM   unified_audit_trail
WHERE  fga_policy_name IN (
           'AUDITSUADONTHUOC',
           'AUDITBSUPDATEHSBA',
           'AUDITKTVUPDATEKETQUA'
       )
AND    event_timestamp >= SYSTIMESTAMP - INTERVAL '1' HOUR
ORDER  BY event_timestamp DESC
FETCH FIRST 30 ROWS ONLY;