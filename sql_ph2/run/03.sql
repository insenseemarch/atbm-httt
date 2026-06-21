-- CONNECTION: QLBV - XEPDB1

-- Yêu cầu 1:
-- Câu 1:
-- TC#1: Tạo tài khoản cho Nhân viên và Bệnh Nhân
-- Nhân viên 
/* begin 
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
*/
-- Bệnh nhân
/*
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
*/
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


-- TC#5 Bệnh nhân:
-- Xoá role nếu tồn tại
/*
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
*/
-- Gán role cho user 
begin
    for nv in (select MANV from NhanVien where VAITRO = N'Kỹ thuật viên') loop
        execute immediate 'grant ROLE_KTV to ' || nv.MANV;
    end loop;
    /* for bn in (select MABN from BenhNhan) loop
        execute immediate 'grant ROLE_BENHNHAN to ' || bn.MABN;
    end loop; */
end;
/

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
    for r in (select CAPBAC from NhanVien where MANV = v_user) loop
        if r.CAPBAC = N'Ban Giám đốc' then
            return '1=1';
        end if;
    end loop;
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
    if v_user in ('SYS', 'SYSTEM', 'ADMIN', 'APP_ADMIN', 'QLBV') then
        return '1=1';
    end if;
    
    for r in (select CAPBAC from NhanVien where MANV = v_user) loop
        if r.CAPBAC = N'Ban Giám đốc' then
            return '1=1'; 
        end if;
    end loop;

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
    
    for r in (select CAPBAC from NhanVien where MANV = v_user) loop
        if r.CAPBAC = N'Ban Giám đốc' then
            return '1=1'; 
        end if;
    end loop;

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

-- Cho phép mọi người đọc thông báo (tùy nhãn OLS của mỗi người sẽ thấy dòng khác nhau)
grant select on THONGBAO to public;

-- Tạo Role Giám Đốc và cấp full quyền xem
begin execute immediate 'drop role ROLE_GIAMDOC'; exception when others then null; end;
/
create role ROLE_GIAMDOC;
grant select on NhanVien to ROLE_GIAMDOC;
grant select on BenhNhan to ROLE_GIAMDOC;
grant select on HSBA     to ROLE_GIAMDOC;
grant select on HSBA_DV  to ROLE_GIAMDOC;
grant select on DonThuoc to ROLE_GIAMDOC;

-- Gán Role Giám đốc cho những ai có Cấp bậc là Ban Giám đốc
begin
    for nv in (select MANV from NhanVien where CAPBAC = N'Ban Giám đốc') loop
        execute immediate 'grant ROLE_GIAMDOC to ' || nv.MANV;
    end loop;
end;
/

commit

