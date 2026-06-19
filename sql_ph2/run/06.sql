-- CONNECTION: SYSDBA - XEPDB1

declare
    v_label varchar2(200);
    v_level varchar2(10);
    v_comp  varchar2(10);
    v_grp   varchar2(10);
begin
    for nv in (select MANV, CAPBAC, MAKHOA, COSO from QLBV.NHANVIEN) loop
        case nv.CAPBAC
            when N'Ban Giám đốc'   then v_level := 'BGD';
            when N'Lãnh đạo khoa'  then v_level := 'LDK';
            when N'Lãnh đạo phòng' then v_level := 'LDP';
            else v_level := 'NV';
        end case;

        case nv.MAKHOA
            when 'K001' then v_comp := 'TH';
            when 'K002' then v_comp := 'TK';
            when 'K003' then v_comp := 'TM';
            else v_comp := null;
        end case;

        case nv.COSO
            when N'Hồ Chí Minh' then v_grp := 'HCM';
            when N'Hải Phòng'   then v_grp := 'HP';
            when N'Hà Nội'      then v_grp := 'HN';
            else v_grp := null;
        end case;

        if nv.CAPBAC = N'Ban Giám đốc' then
            v_label := 'BGD:TH,TK,TM:HCM,HP,HN';
        elsif nv.CAPBAC = N'Lãnh đạo phòng' and nv.MAKHOA is null then
            -- u7: LDP toàn bộ không giới hạn
            v_label := 'LDP:TH,TK,TM:HCM,HP,HN';
        elsif v_comp is not null and v_grp is not null then
            v_label := v_level || ':' || v_comp || ':' || v_grp;
        elsif v_comp is not null then
            v_label := v_level || ':' || v_comp;
        elsif v_grp is not null then
            v_label := v_level || '::' || v_grp;
        else
            v_label := v_level;
        end if;

        begin
            lbacsys.sa_user_admin.set_user_labels(
                policy_name    => 'OLS_QLBV_POLICY',
                user_name      => nv.MANV,
                max_read_label => v_label,
                def_label      => v_label,
                row_label      => v_label
            );
        exception when others then null;
        end;
    end loop;
end;
/

-- Yêu cầu 3: 
-- Dọn dẹp TẤT CẢ policy cũ TRƯỚC

begin
    for r in (
        select distinct policy_name 
        from audit_unified_policies 
        where upper(policy_name) like 'AUDIT%'
    ) loop
        begin execute immediate 'noaudit policy ' || r.policy_name; 
        exception when others then null; end;
        begin execute immediate 'drop audit policy '  || r.policy_name; 
        exception when others then null; end;
    end loop;
exception when others then null;
end;
/

-- 3.1: Kích hoạt kiểm toán hệ thống
-- Theo dõi các lần đăng nhập thất bại vào CSDL
create audit policy AuditSession
actions logon;
audit policy AuditSession whenever not successful;

-- Dọn dẹp FGA cũ
begin dbms_fga.drop_policy('QLBV', 'HSBA',    'AuditBSUpdateHSBA');        exception when others then null; end;
/

begin dbms_fga.drop_policy('QLBV', 'DONTHUOC','AuditSuaDonThuoc');         exception when others then null; end;
/
begin dbms_fga.drop_policy('QLBV', 'HSBA_DV', 'AuditKTVUpdateKetQua');     exception when others then null; end;
/

-- Khởi tạo các đối tượng nghiệp vụ mẫu phục vụ kiểm toán

create or replace view QLBV.VW_BaoCaoDieuTri as
select h.MAHSBA, h.MABN, b.TENBN, h.NGAY, h.CHANDOAN, h.DIEUTRI, h.KETLUAN
from QLBV.HSBA h
join QLBV.BENHNHAN b on h.MABN = b.MABN;
/

create or replace procedure QLBV.sp_KhoiTaoHSBAKhancap(
    p_mahsba in varchar2,
    p_mabn in varchar2,
    p_mabs in varchar2,
    p_makhoa in varchar2
) as
begin
    insert into QLBV.HSBA (MAHSBA, MABN, NGAY, CHANDOAN, DIEUTRI, MABS, MAKHOA, KETLUAN)
    values (p_mahsba, p_mabn, sysdate, N'Cấp cứu khẩn cấp', N'Theo phác đồ khẩn cấp', p_mabs, p_makhoa, N'Đang theo dõi đặc biệt');
end;
/

create or replace function QLBV.fn_TinhTongChiPhiDieuTri(
    p_mahsba in varchar2
) return number as
    v_total number := 0;
begin
    select nvl(count(*), 0) into v_total
    from QLBV.HSBA_DV
    where MAHSBA = p_mahsba;
    return v_total;
end;
/

-- Cấp quyền truy cập trên các đối tượng mẫu cho các role tương ứng
grant select on QLBV.VW_BaoCaoDieuTri to ROLE_BACSI;
grant execute on QLBV.sp_KhoiTaoHSBAKhancap to ROLE_BACSI;
grant execute on QLBV.fn_TinhTongChiPhiDieuTri to ROLE_DPV;

-- 3.2: Standard Audit

-- Thành công 1: Điều phối viên cập nhật thông tin bệnh nhân
create audit policy AuditSucDPVUpdateBN
actions update on QLBV.BENHNHAN
when 'sys_context(''userenv'',''session_user'') in (''NV0001'',''NV0007'')'
evaluate per session;
audit policy AuditSucDPVUpdateBN whenever successful;

-- Thành công 2: Bác sĩ xem view báo cáo điều trị
create audit policy AuditSucBSSelectView
actions select on QLBV.VW_BaoCaoDieuTri
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditSucBSSelectView whenever successful;

-- Thành công 3: Bác sĩ thực thi stored procedure khởi tạo HSBA khẩn cấp
create audit policy AuditSucBSExecProc
actions execute on QLBV.sp_KhoiTaoHSBAKhancap
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditSucBSExecProc whenever successful;

-- Thất bại 4: Bác sĩ cố tình thực thi function tính tổng chi phí điều trị nhưng thất bại (vượt quyền)
create audit policy AuditFailBSExecFunc
actions execute on QLBV.fn_TinhTongChiPhiDieuTri
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditFailBSExecFunc whenever not successful;

-- Thành công 4.5: Điều phối viên cập nhật phân công Khoa/Bác sĩ điều trị trên HSBA
create audit policy AuditDPVUpdateHSBA
actions update on QLBV.HSBA
when 'sys_context(''userenv'',''session_user'') in (''NV0001'',''NV0002'')'
evaluate per session;
audit policy AuditDPVUpdateHSBA whenever successful;

-- Thành công 5: Kỹ thuật viên cập nhật kết quả dịch vụ trong HSBA_DV
create audit policy AuditSucKTVUpdateDV
actions update on QLBV.HSBA_DV
when 'sys_context(''userenv'',''session_user'') in (''KTV001'',''KTV002'')'
evaluate per session;
audit policy AuditSucKTVUpdateDV whenever successful;

-- Thành công 6: Bác sĩ cập nhật đơn thuốc thuộc HSBA mình điều trị
create audit policy AuditSucBSUpdateDT
actions update on QLBV.DONTHUOC
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditSucBSUpdateDT whenever successful;

-- Bonus thành công: Bệnh nhân xem thông tin cá nhân qua view được cấp quyền
create audit policy AuditBonusBNSelectInfo
actions select on QLBV.VW_BenhNhan_Xemthongtin
when 'sys_context(''userenv'',''session_user'') in (''BN000001'',''BN000002'')'
evaluate per session;
audit policy AuditBonusBNSelectInfo whenever successful;

-- Thất bại 1: Bác sĩ cố cập nhật/xóa thông tin nhân viên
create audit policy AuditFailBSUpdateNV
actions update on QLBV.NHANVIEN, delete on QLBV.NHANVIEN
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditFailBSUpdateNV whenever not successful;

-- Thất bại 2: Bệnh nhân cố xóa hồ sơ bệnh án
create audit policy AuditFailBNDeleteHSBA
actions delete on QLBV.HSBA
when 'sys_context(''userenv'',''session_user'') in (''BN000001'',''BN000002'')'
evaluate per session;
audit policy AuditFailBNDeleteHSBA whenever not successful;

-- Thất bại 3: Kỹ thuật viên cố xóa đơn thuốc
create audit policy AuditFailKTVDeleteDT
actions delete on QLBV.DONTHUOC
when 'sys_context(''userenv'',''session_user'') in (''KTV001'',''KTV002'')'
evaluate per session;
audit policy AuditFailKTVDeleteDT whenever not successful;

-- Thất bại 4: Điều phối viên cố xóa hồ sơ bệnh án
create audit policy AuditFailDPVDeleteHSBA
actions delete on QLBV.HSBA
when 'sys_context(''userenv'',''session_user'') in (''NV0001'',''NV0002'')'
evaluate per session;
audit policy AuditFailDPVDeleteHSBA whenever not successful;

-- Thất bại 5: Bệnh nhân cố cập nhật kết quả dịch vụ HSBA_DV
create audit policy AuditFailBNUpdateDV
actions update on QLBV.HSBA_DV
when 'sys_context(''userenv'',''session_user'') in (''BN000001'',''BN000002'')'
evaluate per session;
audit policy AuditFailBNUpdateDV whenever not successful;

-- Thất bại 6: Kỹ thuật viên cố xem bảng BENHNHAN trực tiếp
create audit policy AuditFailKTVSelectBN
actions select on QLBV.BENHNHAN
when 'sys_context(''userenv'',''session_user'') in (''KTV001'',''KTV002'')'
evaluate per session;
audit policy AuditFailKTVSelectBN whenever not successful;

-- Bonus thất bại: Bệnh nhân cố thực thi stored procedure dành cho bác sĩ
create audit policy AuditBonusBNExecProc
actions execute on QLBV.sp_KhoiTaoHSBAKhancap
when 'sys_context(''userenv'',''session_user'') in (''BN000001'',''BN000002'')'
evaluate per session;
audit policy AuditBonusBNExecProc whenever not successful;

-- 3.3: FGA / Unified Audit cho 4 tình huống đề yêu cầu

-- 3.3.a: UPDATE DONTHUOC sau khi DA_TAO_XONG (FGA)
begin
    dbms_fga.add_policy(
        object_schema   => 'QLBV',
        object_name     => 'DONTHUOC',
        policy_name     => 'AuditSuaDonThuoc',
        audit_column    => 'MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG',
        audit_condition => NULL,
        statement_types => 'UPDATE'
    );
end;
/

-- 3.3.a+: Unified Audit ghi nhận INSERT/UPDATE ĐƠNTHUỐC, không dùng cột trạng thái
create audit policy AuditDonThuocInsert
actions insert on QLBV.DONTHUOC;
audit policy AuditDonThuocInsert whenever successful;

create audit policy AuditDonThuocUpdate
actions update on QLBV.DONTHUOC;
audit policy AuditDonThuocUpdate whenever successful;

-- 3.3.b: BS update CHANDOAN/DIEUTRI/KETLUAN hợp pháp (FGA)
begin
    dbms_fga.add_policy(
        object_schema   => 'QLBV',
        object_name     => 'HSBA',
        policy_name     => 'AuditBSUpdateHSBA',
        audit_column    => 'CHANDOAN,DIEUTRI,KETLUAN',
        audit_condition => 'MABS = SYS_CONTEXT(''USERENV'', ''SESSION_USER'')',
        statement_types => 'UPDATE'
    );
end;
/

-- 3.3.c: UPDATE CHANDOAN/DIEUTRI/KETLUAN bất hợp pháp (Unified Audit - whenever not successful)
-- Không dùng FGA vì VPD chặn trước, FGA không kích hoạt được
create audit policy AuditIllegalUpdateHSBA
actions update on QLBV.HSBA;
audit policy AuditIllegalUpdateHSBA whenever not successful;

-- 3.3.d: INSERT/UPDATE/DELETE bất hợp pháp trên HSBA_DV (Unified Audit - whenever not successful)
create audit policy AuditIllegalHSBADV
actions insert on QLBV.HSBA_DV,
        update on QLBV.HSBA_DV,
        delete on QLBV.HSBA_DV;
audit policy AuditIllegalHSBADV whenever not successful;

-- TC#4: KTV update KETQUA ghi vết (FGA)
begin
    dbms_fga.add_policy(
        object_schema   => 'QLBV',
        object_name     => 'HSBA_DV',
        policy_name     => 'AuditKTVUpdateKetQua',
        audit_column    => 'KETQUA',
        audit_condition => '1=1',
        statement_types => 'UPDATE'
    );
end;
/


-- 3.4: Query đọc nhật ký kiểm toán

-- 3.4.1: Xem audit hệ thống (logon thất bại)
select event_timestamp, dbusername, return_code, object_schema, object_name
from unified_audit_trail
where action_name = 'LOGON'
order by event_timestamp desc;

-- 3.4.2: Xem Standard Audit theo từng ngữ cảnh
select event_timestamp, dbusername, action_name, object_schema, object_name,
       return_code, unified_audit_policies
from unified_audit_trail
where unified_audit_policies in (
    'AUDITSUCDPVUPDATEBN',
    'AUDITSUCBSSELECTVIEW',
    'AUDITSUCBSEXECPROC',
    'AUDITFAILBSEXECFUNC',
    'AUDITDPVUPDATEHSBA',
    'AUDITSUCKTVUPDATEDV',
    'AUDITSUCBSUPDATEDT',
    'AUDITDONTHUOCINSERT',
    'AUDITDONTHUOCUPDATE',
    'AUDITBONUSBNSELECTINFO',
    'AUDITFAILBSUPDATENV',
    'AUDITFAILBNDELETEHSBA',
    'AUDITFAILKTVDELETEDT',
    'AUDITFAILDPVDELETEHSBA',
    'AUDITFAILBNUPDATEDV',
    'AUDITFAILKTVSELECTBN',
    'AUDITBONUSBNEXECPROC'
)
order by event_timestamp desc;

-- 3.4.3: Xem FGA / Unified Audit theo đối tượng
select event_timestamp, dbusername, fga_policy_name, object_schema, object_name,
       sql_text, return_code
from unified_audit_trail
where fga_policy_name in (
    'AUDITSUADONTHUOC',
    'AUDITBSUPDATEHSBA',
    'AUDITKTVUPDATEKETQUA'
)
order by event_timestamp desc;

-- 3.4.4: Xem Unified Audit cho INSERT/UPDATE ĐƠNTHUỐC
select event_timestamp, dbusername, action_name, object_schema, object_name,
       return_code, unified_audit_policies, sql_text
from unified_audit_trail
where unified_audit_policies in (
    'AUDITDONTHUOCINSERT',
    'AUDITDONTHUOCUPDATE'
)
order by event_timestamp desc;

-- 3.4.5: Xem toàn bộ bản ghi liên quan QLBV
select unified_audit_policies, dbusername, action_name,
       object_schema, object_name, return_code, event_timestamp
from unified_audit_trail
where object_schema = 'QLBV'
order by event_timestamp desc;
