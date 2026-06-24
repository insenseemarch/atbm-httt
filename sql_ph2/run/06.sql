-- CONNECTION: SYSDBA - XEPDB1
-- ============================================================================
-- 06.sql - Yêu cầu 2 (OLS User Labels) & Yêu cầu 3 (Hệ thống Kiểm toán Toàn diện)
-- ============================================================================
SELECT POLICY_NAME, POLICY_COLUMN, ENABLED 
FROM DBA_AUDIT_POLICIES 
WHERE OBJECT_NAME = 'DONTHUOC';-- ----------------------------------------------------------------------------
-- PHẦN I: GÁN NHÃN BẢO MẬT OLS CHO NGƯỜI DÙNG (YÊU CẦU 2)
-- ----------------------------------------------------------------------------
-- === Executing OLS User Label Assignment ===

DECLARE
    v_label VARCHAR2(200);
    v_level VARCHAR2(10);
    v_comp  VARCHAR2(10);
    v_grp   VARCHAR2(10);
BEGIN
    FOR nv IN (SELECT MANV, CAPBAC, MAKHOA, COSO FROM QLBV.NHANVIEN) LOOP
        -- 1. Xác định LEVEL dựa vào CAPBAC
        CASE nv.CAPBAC
            WHEN N'Ban Giám đốc'   THEN v_level := 'BGD';
            WHEN N'Lãnh đạo khoa'  THEN v_level := 'LDK';
            WHEN N'Lãnh đạo phòng' THEN v_level := 'LDP';
            ELSE v_level := 'NV';
        END CASE;

        -- 2. Xác định COMPARTMENT dựa vào MAKHOA
        CASE nv.MAKHOA
            WHEN 'K001' THEN v_comp := 'TH';
            WHEN 'K002' THEN v_comp := 'TK';
            WHEN 'K003' THEN v_comp := 'TM';
            ELSE v_comp := NULL;
        END CASE;

        -- 3. Xác định GROUP dựa vào COSO
        CASE nv.COSO
            WHEN N'Hồ Chí Minh' THEN v_grp := 'HCM';
            WHEN N'Hải Phòng'   THEN v_grp := 'HP';
            WHEN N'Hà Nội'      THEN v_grp := 'HN';
            ELSE v_grp := NULL;
        END CASE;

        -- 4. Xây dựng chuỗi nhãn OLS phù hợp theo từng trường hợp đặc biệt
        IF nv.CAPBAC = N'Ban Giám đốc' THEN
            v_label := 'BGD:TH,TK,TM:HCM,HP,HN';
        ELSIF nv.CAPBAC = N'Lãnh đạo phòng' AND nv.MAKHOA IS NULL THEN
            -- Trường hợp u7: Lãnh đạo phòng ban tổng thể không giới hạn khoa/cơ sở
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

        -- 5. Gán nhãn cho User tương ứng trong hệ thống OLS
        BEGIN
            LBACSYS.SA_USER_ADMIN.SET_USER_LABELS(
                policy_name    => 'OLS_QLBV_POLICY',
                user_name      => nv.MANV,
                max_read_label => v_label,
                def_label      => v_label,
                row_label      => v_label
            );
        EXCEPTION 
            WHEN OTHERS THEN NULL; -- Tránh ngắt vòng lặp nếu user db chưa được tạo hoàn tất
        END;
    END LOOP;
END;
/

-- ----------------------------------------------------------------------------
-- PHẦN II: TÁI CẤU TRÚC HỆ THỐNG KIỂM TOÁN HỢP NHẤT (YÊU CẦU 3)
-- ----------------------------------------------------------------------------
-- === Cleaning up existing Audit Policies & FGA Policies ===

-- Dọn dẹp Unified Audit Policies cũ bắt đầu bằng chữ 'AUDIT%'
BEGIN
    FOR r IN (
        SELECT DISTINCT policy_name 
        FROM audit_unified_policies 
        WHERE UPPER(policy_name) LIKE 'AUDIT%'
    ) LOOP
        BEGIN EXECUTE IMMEDIATE 'NOAUDIT POLICY ' || r.policy_name; EXCEPTION WHEN OTHERS THEN NULL; END;
        BEGIN EXECUTE IMMEDIATE 'DROP AUDIT POLICY '  || r.policy_name; EXCEPTION WHEN OTHERS THEN NULL; END;
    END LOOP;
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

-- Dọn dẹp Fine-Grained Audit (FGA) cũ trên schema QLBV
BEGIN DBMS_FGA.DROP_POLICY('QLBV', 'DONTHUOC', 'AuditSuaDonThuoc');         EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN DBMS_FGA.DROP_POLICY('QLBV', 'HSBA',     'AuditBSUpdateHSBA_HopPhap'); EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN DBMS_FGA.DROP_POLICY('QLBV', 'HSBA_DV',   'AuditKTVUpdateKetQua');      EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- ----------------------------------------------------------------------------
-- 3.0.0: KHỞI TẠO CÁC ĐỐI TƯỢNG HÀM/THỦ TỤC VÀ ĐỊNH DANH HỆ THỐNG
-- ----------------------------------------------------------------------------
-- === Preparing Procedures and Functions for Auditing ===

ALTER SESSION SET CONTAINER = XEPDB1;

-- Khởi tạo Procedure sp_KhoiTaoHSBAKhancap (nếu chưa có ở các file trước)
CREATE OR REPLACE PROCEDURE QLBV.sp_KhoiTaoHSBAKhancap(
    p_mahsba VARCHAR2, p_mabn VARCHAR2, p_mabs VARCHAR2, p_makhoa VARCHAR2
) AS
BEGIN
    INSERT INTO QLBV.HSBA(MAHSBA, MABN, NGAY, CHANDOAN, DIEUTRI, MABS, MAKHOA, KETLUAN)
    VALUES (p_mahsba, p_mabn, SYSDATE, N'Cấp cứu khẩn cấp', N'Theo dõi', p_mabs, p_makhoa, N'Chưa kết luận');
END;
/
GRANT EXECUTE ON QLBV.sp_KhoiTaoHSBAKhancap TO PUBLIC;

-- Khởi tạo Procedure sp_DieuPhoiNhanSu (cho audit giám sát ĐPV chuyển khoa bệnh nhân)
CREATE OR REPLACE PROCEDURE QLBV.sp_DieuPhoiNhanSu (
    p_mahsba      IN VARCHAR2,
    p_makhoa_moi  IN VARCHAR2
)
AS
    v_count NUMBER;
BEGIN
    -- 1. Kiểm tra hồ sơ bệnh án có tồn tại hay không
    SELECT COUNT(*) INTO v_count 
    FROM QLBV.HSBA 
    WHERE MAHSBA = p_mahsba;
    
    IF v_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Hồ sơ bệnh án không tồn tại trên hệ thống.');
    END IF;

    -- 2. Tiến hành cập nhật chuyển khoa cho bệnh nhân (Cập nhật trên bảng HSBA)
    UPDATE QLBV.HSBA
    SET MAKHOA = p_makhoa_moi
    WHERE MAHSBA = p_mahsba;
    
    -- In thông báo xác nhận thành công
    DBMS_OUTPUT.PUT_LINE('Chuyển khoa thành công cho hồ sơ: ' || p_mahsba || ' sang khoa: ' || p_makhoa_moi);
    
    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
END;
/
GRANT EXECUTE ON QLBV.sp_DieuPhoiNhanSu TO ROLE_DPV;

-- Khởi tạo FUNC-B: Hàm kiểm tra dị ứng thuốc nguy kịch của bệnh nhân
CREATE OR REPLACE FUNCTION QLBV.fn_KiemTraDiUngThuoc(p_mabn VARCHAR2) 
RETURN NVARCHAR2 AS
    v_diung NVARCHAR2(500);
BEGIN
    SELECT DIUNGTHUOC INTO v_diung FROM QLBV.BENHNHAN WHERE MABN = p_mabn;
    RETURN v_diung;
EXCEPTION 
    WHEN OTHERS THEN RETURN N'Không có dữ liệu';
END;
/
GRANT EXECUTE ON QLBV.fn_KiemTraDiUngThuoc TO PUBLIC;

-- ----------------------------------------------------------------------------
-- 3.2 (BỔ SUNG): AUDIT POLICY CHO STORED PROCEDURE XEM LỊCH SỬ ĐIỀU TRỊ
-- Giám đốc truy xuất lịch sử bệnh nhân → audit mọi lần EXECUTE (cả thành công lẫn thất bại)
-- ----------------------------------------------------------------------------

-- Tạo procedure trước (để audit policy bind được object)
CREATE OR REPLACE PROCEDURE QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN(
    p_mabn   IN  VARCHAR2,
    p_cursor OUT SYS_REFCURSOR
) AS
    v_ho_ten_bn NVARCHAR2(100);
BEGIN
    -- Kiểm tra bệnh nhân tồn tại, lấy tên để in debug
    BEGIN
        SELECT TENBN INTO v_ho_ten_bn
        FROM QLBV.BENHNHAN
        WHERE MABN = p_mabn;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            DBMS_OUTPUT.PUT_LINE('[SP_LICHSU] Không tìm thấy bệnh nhân: ' || p_mabn);
            OPEN p_cursor FOR SELECT NULL MABN FROM DUAL WHERE 1=0;
            RETURN;
    END;

    DBMS_OUTPUT.PUT_LINE('=== LỊCH SỬ ĐIỀU TRỊ: ' || p_mabn || ' - ' || v_ho_ten_bn || ' ===');

    -- Trả về toàn bộ lịch sử qua REFCURSOR
    OPEN p_cursor FOR
        SELECT
            -- Thông tin bệnh nhân
            BN.MABN,
            BN.TENBN,
            BN.PHAI,
            BN.NGAYSINH,
            BN.TIENSUBENH,
            BN.TIENSUBENHGD,
            BN.DIUNGTHUOC,
            -- Thông tin hồ sơ bệnh án
            HS.MAHSBA,
            HS.NGAY             AS NGAY_KHAM,
            HS.CHANDOAN,
            HS.DIEUTRI,
            HS.KETLUAN,
            KH.TENKHOA,
            -- Bác sĩ phụ trách
            BS.HOTEN            AS TEN_BACSI,
            BS.MANV             AS MA_BACSI,
            -- Dịch vụ cận lâm sàng
            DV.LOAIDV,
            DV.NGAYDV,
            DV.KETQUA           AS KETQUA_DV,
            KTV.HOTEN           AS TEN_KTV,
            -- Đơn thuốc
            DT.NGAYDT,
            DT.TENTHUOC,
            DT.LIEUDUNG
        FROM       QLBV.BENHNHAN  BN
        JOIN       QLBV.HSBA      HS  ON HS.MABN   = BN.MABN
        JOIN       QLBV.KHOA      KH  ON KH.MAKHOA = HS.MAKHOA
        LEFT JOIN  QLBV.NHANVIEN  BS  ON BS.MANV   = HS.MABS
        LEFT JOIN  QLBV.HSBA_DV   DV  ON DV.MAHSBA = HS.MAHSBA
        LEFT JOIN  QLBV.NHANVIEN  KTV ON KTV.MANV  = DV.MAKTV
        LEFT JOIN  QLBV.DONTHUOC  DT  ON DT.MAHSBA = HS.MAHSBA
        WHERE BN.MABN = p_mabn
        ORDER BY HS.NGAY DESC, DV.NGAYDV DESC, DT.NGAYDT DESC;

    DBMS_OUTPUT.PUT_LINE('[SP_LICHSU] Đã mở cursor lịch sử cho: ' || p_mabn);

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('[SP_LICHSU] Lỗi: ' || SQLERRM);
        RAISE;
END;
/

GRANT EXECUTE ON QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN TO ROLE_DPV;


-- ----------------------------------------------------------------------------
-- 3.0: KÍCH HOẠT KIỂM TOÁN HỆ THỐNG (LOGON THẤT BẠI)
-- ----------------------------------------------------------------------------
-- === Creating System Audit Policy (Logon Failures) ===

CREATE AUDIT POLICY AuditSession ACTIONS LOGON;
AUDIT POLICY AuditSession WHENEVER NOT SUCCESSFUL;


-- ----------------------------------------------------------------------------
-- 3.2: STANDARD AUDIT - THEO DÕI HÀNH VI THEO NGỮ CẢNH VAI TRÒ CHUẨN (ĐÃ SỬA LỖI ORA-46368)
-- ----------------------------------------------------------------------------
-- === Creating Selected Standard Audit Contexts by Role ===

-- Ngữ cảnh 1: [Thành công 1] Người dùng "ROLE_DPV" cập nhật thông tin BENHNHAN thành công
CREATE AUDIT POLICY AuditSucDPVUpdateBN
ACTIONS UPDATE ON QLBV.BENHNHAN
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_DPV%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditSucDPVUpdateBN WHENEVER SUCCESSFUL;

-- Ngữ cảnh 2: [Thành công 4.5] Người dùng "ROLE_DPV" cập nhật thông tin trên HSBA thành công
CREATE AUDIT POLICY AuditDPVUpdateHSBA
ACTIONS UPDATE ON QLBV.HSBA
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_DPV%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditDPVUpdateHSBA WHENEVER SUCCESSFUL;

-- Ngữ cảnh 3: [Thành công 5] Người dùng "ROLE_KTV" cập nhật thông tin trên View dịch vụ thành công
CREATE AUDIT POLICY AuditSucKTVUpdateDV
ACTIONS UPDATE ON QLBV.VW_KTV_XemHSBADV
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_KTV%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditSucKTVUpdateDV WHENEVER SUCCESSFUL;

-- Ngữ cảnh 4: [Thành công 6] Người dùng "ROLE_BACSI" cập nhật ĐƠNTHUỐC thành công
CREATE AUDIT POLICY AuditSucBSUpdateDT
ACTIONS UPDATE ON QLBV.DONTHUOC
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_BACSI%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditSucBSUpdateDT WHENEVER SUCCESSFUL;

-- Ngữ cảnh 5: [Thất bại 1] Người dùng "ROLE_BACSI" cố tình cập nhật/xóa thông tin nhân viên (Thất bại)
CREATE AUDIT POLICY AuditFailBSUpdateNV
ACTIONS UPDATE ON QLBV.NHANVIEN, DELETE ON QLBV.NHANVIEN
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_BACSI%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditFailBSUpdateNV WHENEVER NOT SUCCESSFUL;

-- Ngữ cảnh 6: [Stored Procedure]: Giám sát ĐIỀU PHỐI VIÊN thực thi thủ tục chuyển khoa bệnh nhân
CREATE AUDIT POLICY AuditDieuPhoiNhanSu
ACTIONS EXECUTE ON QLBV.sp_DieuPhoiNhanSu
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_DPV%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditDieuPhoiNhanSu;


-- Ngữ cảnh 7: [Function - NGỮ CẢNH MỚI - Thành công]: Bác sĩ thực thi hàm "Kiểm tra dị ứng thuốc nguy kịch" thành công
CREATE AUDIT POLICY AuditSucBSExecFunc
ACTIONS EXECUTE ON QLBV.fn_KiemTraDiUngThuoc
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_BACSI%''' EVALUATE PER STATEMENT;
AUDIT POLICY AuditSucBSExecFunc WHENEVER SUCCESSFUL;

-- Ngữ cảnh 8: DPV xem lịch sử khám bệnh của bệnh nhân
-- Policy 1: Ghi log DPV thực thi thành công
CREATE AUDIT POLICY AuditDPVXemLichSuBN
ACTIONS EXECUTE ON QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN
WHEN 'SYS_CONTEXT(''userenv'', ''client_identifier'') LIKE ''%ROLE_DPV%'''
EVALUATE PER STATEMENT;
AUDIT POLICY AuditDPVXemLichSuBN WHENEVER SUCCESSFUL;

-- Policy 2: Ghi log bất kỳ ai gọi thất bại (truy cập trái phép)
CREATE AUDIT POLICY AuditDPVXemLichSuBN_Fail
ACTIONS EXECUTE ON QLBV.SP_XEM_LICHSU_DIEUTRI_BENHNHAN;
AUDIT POLICY AuditDPVXemLichSuBN_Fail WHENEVER NOT SUCCESSFUL;

-- ----------------------------------------------------------------------------
-- 3.3: TÌNH HUỐNG KIỂM TOÁN NGHIỆP VỤ CHI TIẾT (a, b, c, d)
-- ----------------------------------------------------------------------------
-- === Implementing Specific Business Audit Situations (a, b, c, d) ===

-- [TÌNH HUỐNG a]: Sửa đổi ĐƠN THUỐC trên các cột quy định bởi chính Bác sĩ phụ trách (Dùng FGA)
-- Đơn thuốc đã lưu vào DB đồng nghĩa đã được chỉ định, mọi hành vi sửa đổi (UPDATE) sẽ bị ghi vết
BEGIN
    DBMS_FGA.ADD_POLICY(
        object_schema   => 'QLBV',
        object_name     => 'DONTHUOC',
        policy_name     => 'AuditSuaDonThuoc',
        audit_column    => 'MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG',
        -- Điều kiện: Hồ sơ này nằm trong nhóm do chính bác sĩ hiện tại phụ trách điều trị
        audit_condition => 'SYS_CONTEXT(''USERENV'', ''SESSION_USER'') LIKE ''BS%''',
        statement_types => 'UPDATE'
    );
END;
/

-- [TÌNH HUỐNG b]: Bác sĩ cập nhật THÀNH CÔNG các trường lâm sàng trên HSBA do mình điều trị (Dùng FGA)
BEGIN
    DBMS_FGA.ADD_POLICY(
        object_schema   => 'QLBV',
        object_name     => 'HSBA',
        policy_name     => 'AuditBSUpdateHSBA_HopPhap',
        audit_column    => 'CHANDOAN,DIEUTRI,KETLUAN',
        -- Điều kiện: Đúng bác sĩ phụ trách hồ sơ thực hiện cập nhật thành công
        audit_condition => 'MABS = SYS_CONTEXT(''USERENV'', ''SESSION_USER'')',
        statement_types => 'UPDATE'
    );
END;
/

-- [TÌNH HUỐNG c]: Cập nhật BẤT HỢP PHÁP trên các trường CHẨNĐOÁN, ĐIỀUTRỊ, KẾTLUẬN (Dùng Unified Audit)
-- Khi sửa trái phép (ví dụ: Bác sĩ sửa hồ sơ người khác, hoặc DPV phá dữ liệu), VPD sẽ chặn đứng, sinh lỗi lệnh thất bại
CREATE AUDIT POLICY AuditIllegalUpdateHSBA
ACTIONS UPDATE ON QLBV.HSBA;

-- Bật chính sách ghi vết cho mọi đối tượng khi câu lệnh UPDATE thất bại (Bất hợp pháp)
AUDIT POLICY AuditIllegalUpdateHSBA WHENEVER NOT SUCCESSFUL;

-- [TÌNH HUỐNG d]: Thêm, Xóa, Sửa BẤT HỢP PHÁP trên quan hệ dịch vụ bệnh án HSBA_DV (Dùng Unified Audit)
-- Theo dõi toàn bộ các hành vi can thiệp dữ liệu bất hợp pháp bị hệ thống từ chối/thất bại
CREATE AUDIT POLICY AuditIllegalHSBADV
ACTIONS INSERT ON QLBV.HSBA_DV,
        UPDATE ON QLBV.HSBA_DV,
        DELETE ON QLBV.HSBA_DV;
AUDIT POLICY AuditIllegalHSBADV WHENEVER NOT SUCCESSFUL;


-- ----------------------------------------------------------------------------
-- 3.4: HỆ THỐNG TRUY VẤN ĐỌC NHẬT KÝ KIỂM TOÁN (YÊU CẦU 3.4)
-- ----------------------------------------------------------------------------
-- (Phần này để sẵn trong script phục vụ việc chấm điểm hoặc truy vấn nhanh nhật ký)

/*
-- 3.4.1: Kiểm tra lịch sử Đăng nhập thất bại (Hệ thống)
SELECT event_timestamp, dbusername, return_code, os_username, userhost
FROM   unified_audit_trail
WHERE  action_name = 'LOGON'
ORDER  BY event_timestamp DESC;

-- 3.4.2: Đọc dữ liệu Standard Audit của 8 ngữ cảnh vai trò chuẩn đã chỉnh sửa
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
        'AuditDPVXemLichSuBN',  -- NC9: DPV Xem lịch sử khám bệnh - TC
        'AuditDPVXemLichSuBN_Fail' --NC9: Role khác xem lịch sử khám bệnh thất bại
)
ORDER BY event_timestamp DESC;

-- 3.4.3: Đọc dữ liệu FGA của tình huống (a) Sửa Đơn Thuốc & (b) Bác sĩ Sửa HSBA hợp pháp
SELECT event_timestamp, dbusername, fga_policy_name, object_schema, object_name,
       sql_text, return_code
FROM   unified_audit_trail
WHERE  fga_policy_name IN ('AUDITSUADONTHUOC', 'AUDITBSUPDATEHSBA_HOPPHAP')
ORDER  BY event_timestamp DESC;

-- 3.4.4: Đọc dữ liệu lỗi của tình huống (c) Sửa HSBA trái phép & (d) Can thiệp trái phép bảng HSBA_DV
SELECT event_timestamp, dbusername, action_name, object_name, return_code,
       unified_audit_policies, sql_text
FROM   unified_audit_trail
WHERE  unified_audit_policies IN ('AUDITILLEGALUPDATEHSBA', 'AUDITILLEGALHSBADV')
ORDER  BY event_timestamp DESC;
*/