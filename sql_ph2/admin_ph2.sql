-- SCRIPT TẠO SCHEMA CSDL ORACLE - PHÂN HỆ 2 ĐỒ ÁN ATBM
-- ============================================================
-- BƯỚC 2: Chạy file ADMIN_PH2.SQL này SAU sys_PH2.sql (Phần 1)
--          và TRƯỚC khi chạy tiếp sys_PH2.sql (Phần 3 — Yêu cầu 3).
--
--   Thứ tự chạy đầy đủ:
--     [1] sys_PH2.sql   (Lines   1–220: Tạo QLBV user, cấp quyền, khởi tạo OLS)
--     [2] admin_ph2.sql (File này: Tạo bảng, data, roles, VPD, OLS apply)
--     [3] sys_PH2.sql   (Lines 221+: FGA + Unified Audit + Standard Audit)
-- ============================================================

-- ============================================================
-- [WINFORM-GỘPCODE-1] Schema prefix → ĐÃ GỘP VÀO SubSystem2Form.cs
--   Tất cả bảng dưới đây thuộc schema QLBV.
--   WinForms trước đây dùng APP_ADMIN.TABLE_NAME — đã sửa toàn bộ
--   107 vị trí sang QLBV.TABLE_NAME để VPD và OLS hoạt động đúng.
-- ============================================================

-- Xóa các bảng cũ nếu có

BEGIN EXECUTE IMMEDIATE 'DROP TABLE DONTHUOC   CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE HSBA_DV    CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE HSBA       CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE NHANVIEN   CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE BENHNHAN   CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE THONGBAO   CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE KHOA       CASCADE CONSTRAINTS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_DONTHUOC_ID'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_THONGBAO_ID'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_DONTHUOC_ID'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_THONGBAO_ID'; EXCEPTION WHEN OTHERS THEN NULL; END;
/

-- Yêu cầu 1:
-- Câu 1:
-- TC#1: Tạo tài khoản cho Nhân viên và Bệnh Nhân
-- Nhân viên 
begin 
    for nv in (select MANV from NHANVIEN) loop
        -- xoá user nếu đã tồn tại
        begin 
            execute immediate 'drop user ' || nv.MANV || ' cascade'; 
        exception 
            when others then null; 
        end;
        
        -- tạo user mới 
        begin
            execute immediate 'create user ' || nv.MANV || ' identified by nv123';
            execute immediate 'grant create session to ' || nv.MANV;
        exception 
            when others then null; 
        end;
    end loop;
end;
/
-- Bệnh nhân
begin
    for bn in (select MABN from BENHNHAN) loop
        -- xoá bệnh nhân nếu đã tồn tại
        begin 
            execute immediate 'drop user ' || bn.MABN || ' cascade';
        exception 
            when others then null; 
        end;
        
        -- tạo user mới
        begin
            execute immediate 'create user ' || bn.MABN || ' identified by bn123';
            execute immediate 'grant create session to ' || bn.MABN;
        exception 
            when others then null; 
        end;
    end loop;
end;
/

-- ============================================================
-- [WINFORM-GỘPCODE-2] ROLE_KTV + VW_KTV_XemHSBADV → ĐÃ GỘP (một phần)
--   • Tên cột thực trong HSBA_DV là LOAIDV và NGAYDV (PK composite).
--     WinForms KTV đã sửa: MADV→LOAIDV, NGAY→NGAYDV trong double-click
--     UPDATE KETQUA và BS delete HSBA_DV đã thêm NGAYDV vào WHERE.
--   ⚠ Chưa gộp: WinForms vẫn query QLBV.HSBA_DV trực tiếp, chưa dùng
--     view VW_KTV_XemHSBADV — VPD fn_vpdHSBADV tự lọc thay thế.
-- ============================================================

-- Câu 2: Chính sách bảo mật liên quan vai trò và "Kỹ thuật viên" và "Bệnh nhân"
-- TC#4: Kỹ thuật viên
-- Xóa role nếu đã tồn tại:
begin execute immediate 'drop role ROLE_KTV';exception when others then null; end;
/
-- Tạo role Kỹ thuật viên
create role ROLE_KTV;
-- KTV chỉ được xem dòng trong HDBA_DV do mình thực hiện
create or replace view VW_KTV_XemHSBADV as
select *
from HSBA_DV
where MAKTV = sys_context ('userenv', 'session_user');
-- TC#5: Nhân viên xem thông tin của chính mình và update trừ các trường cấm
create or replace view VW_KTV_Xemthongtin as
select *
from NhanVien
where MANV = sys_context ('userenv', 'session_user');
-- Cấp quyền trên View cho Role
grant select on VW_KTV_XemHSBADV to ROLE_KTV;
grant select on VW_KTV_Xemthongtin to ROLE_KTV;
-- KTV update KETQUA trong HSBA_DV do mình thực hiện
grant update (KETQUA) on VW_KTV_XemHSBADV to ROLE_KTV;
grant update (QUEQUAN, SODT) ON VW_KTV_Xemthongtin TO ROLE_KTV;

-- ============================================================
-- [WINFORM-GỘPCODE-3] ROLE_BENHNHAN + VW_BenhNhan_Xemthongtin → ĐÃ GỘP (một phần)
--   • WinForms BN query QLBV.BENHNHAN — VPD fn_vpdBenhNhan tự lọc
--     chỉ trả về hàng của BN đang đăng nhập (không cần WHERE).
--   • UPDATE BENHNHAN không cần WHERE vì VPD tự giới hạn đúng hàng.
--   ⚠ Chưa gộp: WinForms chưa dùng trực tiếp VW_BenhNhan_Xemthongtin.
-- ============================================================

-- TC#5 Bệnh nhân:
-- Xoá role nếu tồn tại
begin execute immediate 'drop role ROLE_BENHNHAN'; exception when others then null; end;
/
-- Tạo role bệnh nhân
create role ROLE_BENHNHAN;
-- Bệnh nhân xem thông tin của chính mình 
create or replace view VW_BenhNhan_Xemthongtin as
select *
from BenhNhan
where MABN = sys_context ('userenv', 'session_user');
-- Cấp quyền trên View cho Role
grant select on VW_BenhNhan_Xemthongtin to ROLE_BENHNHAN;
grant update (SONHA, TENDUONG, QUANHUYEN, TINHTP, TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC) ON VW_BenhNhan_Xemthongtin TO ROLE_BENHNHAN;

-- Gán role cho user 
begin
    for nv in (select MANV from NhanVien where VAITRO = N'Kỹ thuật viên') loop
        execute immediate 'grant ROLE_KTV to ' || nv.MANV;
    end loop;
    for bn in (select MABN from BenhNhan) loop
        execute immediate 'grant ROLE_BENHNHAN to ' || bn.MABN;
    end loop;
end;
/

-- ============================================================
-- [WINFORM-GỘPCODE-4] ROLE_DPV + ROLE_BACSI → ĐÃ GỘP
--   • Grant SELECT/INSERT/UPDATE/DELETE trên bảng QLBV.* cho phép
--     WinForms DPV/BS thực hiện đúng các thao tác trong SubSystem2Form.cs.
--   • BS chỉ UPDATE (CHANDOAN, DIEUTRI, KETLUAN) — WinForms EditRowForm
--     đã giới hạn đúng các cột này khi sửa HSBA.
--   ⚠ Chưa gộp: BS insert HSBA_DV (grant select,insert,delete on HSBA_DV)
--     thiếu MAKTV NOT NULL — WinForms insert không truyền MAKTV → ORA-01400.
--     Cần sửa schema MAKTV thành nullable hoặc thêm field MAKTV vào form BS.
-- ============================================================

-- Câu 3: Chính sách bảo mật liên quan "Điều phối viên" và "Bác sĩ/ Y sĩ" dùng VPD
-- TC#2: Điều phối viên
-- Xoá role nếu đã tồn tại
begin execute immediate 'drop role ROLE_DPV'; exception when others then null; end;
/
-- Tạo role
create role ROLE_DPV;
-- DPV xem, thêm, sửa bệnh nhân
grant select, insert, update on BenhNhan to ROLE_DPV;
-- DPV thêm, cập nhật (MAKHOA, MABS) trong HSBA
grant select, insert on HSBA to ROLE_DPV;
grant update (MAKHOA, MABS) on HSBA to ROLE_DPV;
-- DPV cập nhật MAKTV trong HSBA_DV
grant select on HSBA_DV to ROLE_DPV;
grant update (MAKTV) on HSBA_DV to ROLE_DPV;
-- TC#5: DPV xem và sửa thông tin cá nhân
grant select on NhanVien to ROLE_DPV;
grant update (QUEQUAN, SODT) on NhanVien to ROLE_DPV;

-- TC#3: Bác sĩ/Y sĩ
-- Xoá role nếu đã tồn tại
begin execute immediate 'drop role ROLE_BACSI'; exception when others then null; end;
/
-- Tạo role
create role ROLE_BACSI;
-- BS thêm, xóa dòng HSBA_DV mình phụ trách
grant select, insert, delete on HSBA_DV to ROLE_BACSI;
-- BS cập nhật (CHANDOAN, DIEUTRI, KETLUAN) trong HSBA mình phụ trách
grant select on HSBA to ROLE_BACSI;
grant update (CHANDOAN, DIEUTRI, KETLUAN) on HSBA to ROLE_BACSI;
-- BS xem, cập nhật TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC BENHNHAN do mình phụ trách
grant select on BenhNhan to ROLE_BACSI;
grant update (TIENSUBENH, TIENSUBENHGD, DIUNGTHUOC) on BenhNhan to ROLE_BACSI;
-- BS thêm, xoá, cập nhật DONTHUOC do mình điều trị
grant select, insert, update, delete on DonThuoc to ROLE_BACSI;
-- TC#5: BS xem và sửa thông tin cá nhân 
grant select on NhanVien to ROLE_BACSI;
grant update (QUEQUAN, SODT) on NhanVien to ROLE_BACSI;

-- ============================================================
-- [WINFORM-GỘPCODE-5] VPD Functions → ĐÃ GỘP (tự động ở tầng DB)
--   WinForms không code VPD — các function dưới đây hoạt động ở tầng DB.
--   Khi WinForms query QLBV.TABLE_NAME, Oracle tự gọi hàm tương ứng:
--     • fn_vpdNhanVien  → NV chỉ thấy dòng của chính mình
--     • fn_vpdHSBA      → BS chỉ thấy HSBA mình phụ trách; DPV thấy tất cả
--     • fn_vpdHSBADV    → BS/KTV/DPV lọc theo vai trò
--     • fn_vpdBenhNhan  → BS chỉ thấy BN đang điều trị; BN thấy chính mình
--     • fn_vpdDonThuoc  → BS chỉ thấy đơn thuốc HSBA mình phụ trách
--   Điều kiện tiên quyết: schema QLBV đã đúng trong WinForms (đã sửa).
-- ============================================================

-- Viết hàm policy cho VPD
-- TC#5 Tất cả nhân viên chỉ thấy dòng của chính mình
create or replace function fn_vpdNhanVien(
    p_schema in varchar2,
    p_table in varchar2
) return varchar2 as
    v_user varchar2(100);
begin
    v_user := sys_context ('userenv', 'session_user');
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN', 'QLBV') then
        return '1=1';
    end if;
    return 'MANV = sys_context(''userenv'', ''session_user'')';
end;
/

-- BS chỉ thấy HSBA mình phụ trách, DPV: thấy tất cả (1=1)
create or replace function fn_vpdHSBA(
    p_schema in varchar2,
    p_table in varchar2
) return varchar2 as
    v_vaitro nvarchar2(50);
    v_user   varchar2(100);
begin
    v_user := sys_context ('userenv', 'session_user');
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN') then
        return '1=1';
    end if;
    select VAITRO into v_vaitro
    from NhanVien
    where MANV = v_user;
    if v_vaitro = N'Bác sĩ/Y sĩ' then
        return 'MABS = sys_context(''userenv'', ''session_user'')';
    elsif v_vaitro = N'Điều phối viên' then
        return '1=1';
    else
        return '1=0';
    end if;
exception
    when no_data_found then
        return '1=0';
end;
/

-- BS chỉ thấy HSBA_DV liên quan HSBA mình phụ trách
create or replace function fn_vpdHSBADV(
    p_schema in varchar2,
    p_table in varchar2
) return varchar2 as
    v_vaitro nvarchar2(50);
    v_user   varchar2(100);
begin
    v_user := sys_context ('userenv', 'session_user');
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN', 'QLBV') then
        return '1=1';
    end if;
    select VAITRO into v_vaitro
    from NhanVien
    where MANV = v_user;
    if v_vaitro = N'Bác sĩ/Y sĩ' then
        return 'MAHSBA in (select MAHSBA from HSBA where MABS = sys_context(''userenv'', ''session_user''))';
    elsif v_vaitro = N'Kỹ thuật viên' then
        return 'MAKTV = sys_context(''userenv'', ''session_user'')';
    elsif v_vaitro = N'Điều phối viên' then
        return '1=1';
    else
        return '1=0';
    end if;
exception
    when no_data_found then
        return '1=0';
end;
/

-- BS chỉ thấy BN liên quan HSBA mình phụ trách, bệnh nhân chỉ thấy chính mình
create or replace function fn_vpdBenhNhan(
    p_schema in varchar2,
    p_table in varchar2
) return varchar2 as
    v_vaitro nvarchar2(50);
    v_user   varchar2(100);
begin
    v_user := sys_context ('userenv', 'session_user');
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN','QLBV') then
        return '1=1';
    end if;
    select VAITRO into v_vaitro
    from NhanVien
    where MANV = v_user;
    if v_vaitro = N'Bác sĩ/Y sĩ' then
        return 'MABN in (select MABN from HSBA where MABS = sys_context(''userenv'', ''session_user''))';
    elsif v_vaitro = N'Điều phối viên' then
        return '1=1';
    else
        return '1=0';
    end if;
exception
    when no_data_found then
        return 'MABN = sys_context(''userenv'', ''session_user'')';
end;
/

-- BS chỉ thấy DONTHUOC liên quan HSBA mình phụ trách
create or replace function fn_vpdDonThuoc(
    p_schema in varchar2,
    p_table in varchar2
) return varchar2 as
    v_vaitro nvarchar2(50);
    v_user   varchar2(100);
begin
    v_user := sys_context ('userenv', 'session_user');
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN', 'QLBV') then
        return '1=1';
    end if;
    select VAITRO into v_vaitro
    from NhanVien
    where MANV = v_user;
    if v_vaitro = N'Bác sĩ/Y sĩ' then
        return 'MAHSBA in (select MAHSBA from HSBA where MABS = sys_context(''userenv'', ''session_user''))';
    else
        return '1=0';
    end if;
exception
    when no_data_found then
        return '1=0';
end;
/

-- Áp dụng policy cho các bảng
begin
    -- Xoá policy cũ bằng vòng lặp
    for p in (select object_name, policy_name from user_policies where object_name in ('NHANVIEN', 'HSBA', 'HSBA_DV', 'BENHNHAN', 'DONTHUOC')) loop
        dbms_rls.drop_policy(user, p.object_name, p.policy_name);
    end loop;

    -- BẢNG NHANVIEN
    dbms_rls.add_policy(object_schema => user, object_name => 'NHANVIEN', policy_name => 'VPD_NV_SEL', function_schema => user, policy_function => 'fn_vpdNhanVien', statement_types => 'SELECT');
    dbms_rls.add_policy(object_schema => user, object_name => 'NHANVIEN', policy_name => 'VPD_NV_EDIT', function_schema => user, policy_function => 'fn_vpdNhanVien', statement_types => 'UPDATE', update_check => TRUE);

    -- BẢNG HSBA
    dbms_rls.add_policy(object_schema => user, object_name => 'HSBA', policy_name => 'VPD_HSBA_SEL', function_schema => user, policy_function => 'fn_vpdHSBA', statement_types => 'SELECT');
    dbms_rls.add_policy(object_schema => user, object_name => 'HSBA', policy_name => 'VPD_HSBA_EDIT', function_schema => user, policy_function => 'fn_vpdHSBA', statement_types => 'INSERT, UPDATE, DELETE', update_check => TRUE);

    -- BẢNG HSBA_DV
    dbms_rls.add_policy(object_schema => user, object_name => 'HSBA_DV', policy_name => 'VPD_HSBADV_SEL', function_schema => user, policy_function => 'fn_vpdHSBADV', statement_types => 'SELECT');
    dbms_rls.add_policy(object_schema => user, object_name => 'HSBA_DV', policy_name => 'VPD_HSBADV_EDIT', function_schema => user, policy_function => 'fn_vpdHSBADV', statement_types => 'INSERT, UPDATE, DELETE', update_check => TRUE);

    -- BẢNG BENHNHAN
    dbms_rls.add_policy(object_schema => user, object_name => 'BENHNHAN', policy_name => 'VPD_BN_SEL', function_schema => user, policy_function => 'fn_vpdBenhNhan', statement_types => 'SELECT');
    dbms_rls.add_policy(object_schema => user, object_name => 'BENHNHAN', policy_name => 'VPD_BN_EDIT', function_schema => user, policy_function => 'fn_vpdBenhNhan', statement_types => 'UPDATE', update_check => TRUE);

    -- BẢNG DONTHUOC
    dbms_rls.add_policy(object_schema => user, object_name => 'DONTHUOC', policy_name => 'VPD_DT_SEL', function_schema => user, policy_function => 'fn_vpdDonThuoc', statement_types => 'SELECT');
    dbms_rls.add_policy(object_schema => user, object_name => 'DONTHUOC', policy_name => 'VPD_DT_EDIT', function_schema => user, policy_function => 'fn_vpdDonThuoc', statement_types => 'INSERT, UPDATE, DELETE', update_check => TRUE);
end;
/

-- Gán role cho user Điều phối viên và Bác sĩ
begin
    for nv in (select MANV from NhanVien where VAITRO = N'Điều phối viên') loop
        execute immediate 'grant ROLE_DPV to ' || nv.MANV;
    end loop;
    for nv in (select MANV from NhanVien where VAITRO = N'Bác sĩ/Y sĩ') loop
        execute immediate 'grant ROLE_BACSI to ' || nv.MANV;
    end loop;
end;
/


-- ============================================================
-- [WINFORM-GỘPCODE-6] OLS — Cập nhật CAPBAC/COSO/MAKHOA (u1–u8) → CHƯA GỘP đầy đủ
--   Dữ liệu u1–u8 là nền tảng để sys_PH2.sql gán nhãn OLS cho từng user.
--   ⚠ Chưa gộp vào WinForms:
--     - WinForms detect Giám đốc qua ROLE_GIAMDOC hoặc username GD*,
--       nhưng SQL không tạo ROLE_GIAMDOC — Giám đốc được phân biệt
--       qua CAPBAC = 'Ban Giám đốc' (NV0001 ở dưới).
--     - Cần sửa WinForms: query CAPBAC từ QLBV.NHANVIEN sau login
--       thay vì detect theo tên role/username.
-- ============================================================

-- Yêu cầu 2: OLS:
-- Cập nhật dữ liệu mẫu để test 
-- tương ứng mô tả u1-u8 trong đề
grant select on THONGBAO to public;

-- u1: Giám đốc đọc toàn bộ
update NHANVIEN set CAPBAC = N'Ban Giám đốc',   MAKHOA = null,  COSO = N'Hồ Chí Minh' 
where MANV = 'NV0001';

-- u2: Lãnh đạo Khoa tim mạch tại HCM
update NHANVIEN set CAPBAC = N'Lãnh đạo khoa', MAKHOA = 'K003', COSO = N'Hồ Chí Minh' 
where MANV = 'NV0002';

-- u3: Lãnh đạo Khoa thần kinh tại Hà Nội
update NHANVIEN set CAPBAC = N'Lãnh đạo khoa', MAKHOA = 'K002', COSO = N'Hà Nội'       
where MANV = 'NV0003';

-- u4: Nhân viên Khoa thần kinh tại HCM
update NHANVIEN set CAPBAC = N'Nhân viên', MAKHOA = 'K002', COSO = N'Hồ Chí Minh' 
where MANV = 'NV0004';

-- u5: Nhân viên Khoa tim mạch tại HCM
update NHANVIEN set CAPBAC = N'Nhân viên', MAKHOA = 'K003', COSO = N'Hồ Chí Minh' 
where MANV = 'NV0005';

-- u6: Lãnh đạo phòng đọc thông báo Khoa tim mạch tại HCM
update NHANVIEN set CAPBAC = N'Lãnh đạo phòng', MAKHOA = 'K003', COSO = N'Hồ Chí Minh' 
where MANV = 'NV0006';

-- u7: Lãnh đạo phòng đọc toàn bộ thông báo cấp lãnh đạo phòng
update NHANVIEN set CAPBAC = N'Lãnh đạo phòng', MAKHOA = null,   COSO = null             
where MANV = 'NV0007';

-- u8: Nhân viên Khoa tiêu hóa tại Hà Nội
update NHANVIEN set CAPBAC = N'Nhân viên', MAKHOA = 'K001', COSO = N'Hà Nội'
where MANV = 'NV0008';

commit;

-- ============================================================
-- [WINFORM-GỘPCODE-7] INSERT THONGBAO với OLS_COL → CHƯA GỘP vào WinForms
--   WinForms GuiThongBao() chỉ INSERT (NOIDUNG, NGAYGIO, DIADIEM),
--   không truyền OLS_COL. Oracle sẽ tự gán OLS_COL = row_label của
--   user đang kết nối NẾU OLS đã được setup đầy đủ theo sys_PH2.sql.
--   ⚠ Nếu OLS chưa setup hoặc user chưa được set_user_labels,
--     INSERT có thể thất bại hoặc bị gán nhãn sai.
--   Dữ liệu mẫu t1–t7 dưới đây minh họa đầy đủ các tổ hợp nhãn
--   Level:Compartment:Group — WinForms không tái tạo logic này.
-- ============================================================

-- Qua sys chạy ròi mới lại 
-- Insert dữ liệu mẫu 

truncate table THONGBAO;

-- t1: toàn bộ nhân viên
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t1] Thông báo họp toàn thể nhân viên bệnh viện',
    systimestamp, N'Hội trường trung tâm',
    char_to_label('OLS_QLBV_POLICY', 'NV'));

-- t2: toàn bộ Ban giám đốc
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t2] Thông báo họp Ban Giám đốc',
    systimestamp - interval '1' hour, N'Phòng họp A1',
    char_to_label('OLS_QLBV_POLICY', 'BGD'));

-- t3: các lãnh đạo khoa (không giới hạn khoa, không giới hạn cơ sở)
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t3] Thông báo họp Lãnh đạo các khoa',
    systimestamp - interval '2' hour, N'Phòng họp B2',
    char_to_label('OLS_QLBV_POLICY', 'LDK'));

-- t4: lãnh đạo Khoa tiêu hóa (không giới hạn cơ sở)
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t4] Họp khẩn Lãnh đạo Khoa Tiêu hóa',
    systimestamp - interval '3' hour, N'Khoa Tiêu hóa - Tầng 3',
    char_to_label('OLS_QLBV_POLICY', 'LDK:TH'));

-- t5: nhân viên Khoa tiêu hóa tại Hồ Chí Minh
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t5] Họp nhân viên Khoa Tiêu hóa cơ sở Hồ Chí Minh',
    systimestamp - interval '4' hour, N'CS Hồ Chí Minh - Phòng C3',
    char_to_label('OLS_QLBV_POLICY', 'NV:TH:HCM'));

-- t6: nhân viên Khoa tiêu hóa tại Hà Nội
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t6] Họp nhân viên Khoa Tiêu hóa cơ sở Hà Nội',
    systimestamp - interval '5' hour, N'CS Hà Nội - Phòng H2',
    char_to_label('OLS_QLBV_POLICY', 'NV:TH:HN'));

-- t7: lãnh đạo Khoa tiêu hóa VÀ Khoa thần kinh tại Hải Phòng
insert into THONGBAO (NOIDUNG, NGAYGIO, DIADIEM, OLS_COL) values (
    N'[t7] Họp liên khoa Tiêu hóa - Thần kinh tại Hải Phòng',
    systimestamp - interval '6' hour, N'CS Hải Phòng - Phòng HP1',
    char_to_label('OLS_QLBV_POLICY', 'LDK:TH,TK:HP'));

commit;

-- Kiểm tra
select ID, NOIDUNG, DIADIEM, LABEL_TO_CHAR(OLS_COL) as NHAN_OLS
from THONGBAO;

-- ============================================================
-- [WINFORM-GỘPCODE-8] Flashback Recovery → CHƯA GỘP vào WinForms
--   Đây là demo kỹ thuật Flashback để khôi phục dữ liệu bị sửa nhầm.
--   WinForms không có tính năng này — thực hiện thủ công qua SQL Developer
--   hoặc công cụ DBA khi cần phục hồi. Không cần port vào WinForms.
-- ============================================================

-- Câu 4: Phương pháp khôi phục nhật ký dựa vào audit  
-- Lệnh 1.1: Xem CCCD gốc ban đầu
select CCCD from QLBV.BENHNHAN where MABN = 'BN000001';
-- -> Kết quả trả về: '970000000001'

-- Lệnh 1.2: Lấy mốc thời gian hiện tại của Database
select to_char(sysdate, 'YYYY-MM-DD HH24:MI:SS') from dual;
-- -> Kết quả trả về ví dụ: '2026-05-28 09:15:00'

update QLBV.BENHNHAN set CCCD = '999999999999' where MABN = 'BN000001';
commit;

select CCCD from QLBV.BENHNHAN where MABN = 'BN000001';
-- -> Kết quả lúc này: '999999999999' (Dữ liệu đã bị hỏng hoàn toàn)

-- Bước: Khôi phục trực tiếp dòng dữ liệu bị sửa nhầm từ quá khứ
update QLBV.BENHNHAN
set (CCCD, NGAYSINH) = (
  select CCCD, NGAYSINH
  from QLBV.BENHNHAN as of timestamp to_timestamp('2026-05-28 02:14:07', 'YYYY-MM-DD HH24:MI:SS')
  where MABN = 'BN000001'
)
where MABN = 'BN000001';

commit;
-- ktra
select CCCD from QLBV.BENHNHAN where MABN = 'BN000001';


