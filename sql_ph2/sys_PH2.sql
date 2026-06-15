-- ============================================================
-- MAPPING GIỮA sys_PH2.sql VÀ SubSystem2Form.cs (WinForms)
--   ✅ Đã gộp : §3.4 Audit SQL query, mock data 10 policies (§3.2+§3.3),
--               schema prefix QLBV (toàn bộ file)
--   ⚠ Chưa gộp: OLS setup (§Yêu cầu 2), AuditSession UI, View/SP/FN UI
-- ============================================================

-- ============================================================
-- [WINFORM-GỘPCODE-9] Tạo user QLBV + cấp quyền → CHƯA GỘP vào WinForms
--   Đây là tác vụ DB admin chạy 1 lần bởi SYS/SYSTEM.
--   WinForms kết nối vào Oracle với user của từng vai trò (DPV01, BS0001…)
--   — không tạo user QLBV từ WinForms.
-- ============================================================

-- Tạo user admin bệnh viện
begin execute immediate 'drop user QLBV cascade';  exception when others then null; end;
/
create user QLBV identified by 123;
-- Cấp quyền Quản trị viên
grant dba to QLBV;
-- Cấp quyền cho VPD (Yêu cầu 1) 
grant execute on DBMS_RLS to QLBV;
-- Cấp quyền cho kiểm tra FGA và Unified Audit
grant execute on DBMS_FGA to QLBV;
grant audit_admin to QLBV;
-- Cấp quyền đọc từ điển dữ liệu (để WinForm truy vấn danh sách User/Role/Privilege không bị lỗi)
grant select any dictionary to QLBV;

-- Yêu cầu 2: OLS
-- Cấp role quản trị tối đa cho OLS
grant LBAC_DBA to QLBV;

grant execute on LBACSYS.SA_SYSDBA to QLBV;
grant execute on LBACSYS.LBAC_POLICY_ADMIN to QLBV;
grant execute on LBACSYS.SA_USER_ADMIN to QLBV;
grant execute on LBACSYS.SA_LABEL_ADMIN to QLBV;
grant execute on LBACSYS.SA_COMPONENTS to QLBV;

-- Kiểm tra OLS đã cài chưa
select value from V$OPTION where parameter = 'Oracle Label Security';
-- Kiểm tra schema LBACSYS có tồn tại không
select username from dba_users where username = 'LBACSYS';
-- Kiểm tra các package OLS có tồn tại không
select object_name, object_type, status 
from dba_objects 
where owner = 'LBACSYS'
order by object_name;

-- Xóa policy cũ hoàn toàn
begin
    lbacsys.sa_sysdba.drop_policy(
        policy_name => 'OLS_QLBV_POLICY',
        drop_column => true
    );
exception 
    when others then null;
end;
/

-- Xóa role còn sót nếu có
begin execute immediate 'drop role OLS_QLBV_POLICY_DBA'; exception when others then null; end;
/
begin execute immediate 'drop role OLS_QLBV_POLICY_COMP_MGR'; exception when others then null; end;
/

-- ============================================================
-- [WINFORM-GỘPCODE-10] OLS Policy + Label Components → CHƯA GỘP vào WinForms
--   Toàn bộ phần này (create_policy, create_level, create_compartment,
--   create_group, create_label) là cấu hình DB thuần túy.
--   WinForms hưởng lợi tự động:
--     • SELECT THONGBAO: Oracle lọc theo max_read_label của user.
--     • INSERT THONGBAO: Oracle gán OLS_COL = row_label của user.
--   WinForms không gọi bất kỳ SA_* API nào — phụ thuộc hoàn toàn
--   vào việc DB đã chạy đủ phần này trước khi dùng WinForms.
-- ============================================================

-- Tạo policy
begin
    sa_sysdba.create_policy(
        policy_name => 'OLS_QLBV_POLICY',
        column_name => 'OLS_COL'
    );
end;
/

-- Tạo thành phần nhãn (level, compartment, group)
begin
    -- LEVEL: tách LDP và LDK riêng
    sa_components.create_level('OLS_QLBV_POLICY', 10, 'NV',  'NhanVien');
    sa_components.create_level('OLS_QLBV_POLICY', 20, 'LDP', 'LanhDaoPhong');
    sa_components.create_level('OLS_QLBV_POLICY', 30, 'LDK', 'LanhDaoKhoa');
    sa_components.create_level('OLS_QLBV_POLICY', 40, 'BGD', 'BanGiamDoc');

    -- COMPARTMENT: khoa
    sa_components.create_compartment('OLS_QLBV_POLICY', 1, 'TH', 'TieuHoa');
    sa_components.create_compartment('OLS_QLBV_POLICY', 2, 'TK', 'ThanKinh');
    sa_components.create_compartment('OLS_QLBV_POLICY', 3, 'TM', 'TimMach');

    -- GROUP: cơ sở
    sa_components.create_group('OLS_QLBV_POLICY', 10, 'HCM', 'HoChiMinh', null);
    sa_components.create_group('OLS_QLBV_POLICY', 20, 'HP',  'HaiPhong',  null);
    sa_components.create_group('OLS_QLBV_POLICY', 30, 'HN',  'HaNoi',     null);
end;
/

begin
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50001, 'NV');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50002, 'LDP'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50003, 'LDK'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50004, 'BGD'); exception when others then null; end;

    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50005, 'LDK:TH');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50006, 'LDK:TK');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50007, 'LDK:TM');  exception when others then null; end;

    -- Thêm các nhãn LDK đầy đủ theo cơ sở (để gán cho bác sĩ như BS0001, BS0002, BS0003...)
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50020, 'LDK:TH:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50021, 'LDK:TK:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50022, 'LDK:TM:HP');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50023, 'LDK:TH:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50024, 'LDK:TK:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50025, 'LDK:TM:HN');  exception when others then null; end;

    -- Thêm các nhãn LDP đầy đủ theo cơ sở (để gán cho các Lãnh đạo phòng như NV0001, NV0002, NV0003...)
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50008, 'LDP:TM:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50009, 'LDP:TK:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50026, 'LDP:TH:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50027, 'LDP:TK:HP');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50028, 'LDP:TM:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50010, 'LDP:TH,TK,TM:HCM,HP,HN'); exception when others then null; end;

    -- Thêm các nhãn NV đầy đủ theo cơ sở (cho kỹ thuật viên KTV và nhân viên thường)
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50011, 'NV:TK:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50012, 'NV:TM:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50013, 'NV:TH:HCM'); exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50014, 'NV:TH:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50015, 'NV:TH:HP');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50016, 'NV:TK:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50017, 'NV:TM:HN');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50029, 'NV:TK:HP');  exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50030, 'NV:TM:HP');  exception when others then null; end;

    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50018, 'LDK:TH,TK:HP');          exception when others then null; end;
    begin lbacsys.sa_label_admin.create_label('OLS_QLBV_POLICY', 50019, 'BGD:TH,TK,TM:HCM,HP,HN'); exception when others then null; end;
end;
/
-- Chạy tới đây dừng ròi qua bên admin chạy
-- Apply policy lên bảng THONGBAO
begin
    lbacsys.lbac_policy_admin.apply_table_policy(
        policy_name   => 'OLS_QLBV_POLICY',
        schema_name   => 'QLBV',
        table_name    => 'THONGBAO',
        table_options => 'READ_CONTROL, WRITE_CONTROL'
    );
end;
/


-- Kiểm tra
select policy_name, column_name from dba_sa_policies;
select * from dba_sa_levels    where policy_name = 'OLS_QLBV_POLICY';
select * from dba_sa_compartments where policy_name = 'OLS_QLBV_POLICY';
select * from dba_sa_groups    where policy_name = 'OLS_QLBV_POLICY';

begin
    lbacsys.sa_user_admin.set_user_labels(
        policy_name     => 'OLS_QLBV_POLICY',
        user_name       => 'QLBV', 
        max_read_label  => 'BGD:TH,TK,TM:HCM,HP,HN',
        max_write_label => 'BGD:TH,TK,TM:HCM,HP,HN',
        min_write_label => null,
        def_label       => 'BGD:TH,TK,TM:HCM,HP,HN',
        row_label       => 'BGD:TH,TK,TM:HCM,HP,HN'
    );
    lbacsys.sa_user_admin.set_user_privs(
        policy_name => 'OLS_QLBV_POLICY',
        user_name   => 'QLBV', 
        privileges  => 'FULL'
    );
end;
/

-- Gán nhãn cho admin với quyền cao nhất để insert được tất cả nhãn
begin
    lbacsys.sa_user_admin.set_user_labels(
        policy_name     => 'OLS_QLBV_POLICY',
        user_name       => 'QLBV', 
        max_read_label  => 'BGD:TH,TK,TM:HCM,HP,HN',
        max_write_label => 'BGD:TH,TK,TM:HCM,HP,HN',
        min_write_label => null,
        def_label       => 'BGD:TH,TK,TM:HCM,HP,HN',
        row_label       => 'BGD:TH,TK,TM:HCM,HP,HN'
    );
    -- cấp quyền writeup cho admin để insert nhãn thấp hơn def_label
    lbacsys.sa_user_admin.set_user_privs(
        policy_name => 'OLS_QLBV_POLICY',
        user_name   => 'QLBV', 
        privileges  => 'FULL'
    );
end;
/

-- ============================================================
-- [WINFORM-GỘPCODE-11] set_user_labels toàn bộ NHANVIEN → CHƯA GỘP vào WinForms
--   Logic gán nhãn dựa trên CAPBAC/MAKHOA/COSO → chuỗi label OLS.
--   WinForms không đọc/ghi nhãn OLS trực tiếp — chỉ INSERT THONGBAO
--   thông thường, Oracle tự gán OLS_COL theo row_label của user.
--   ⚠ Phải chạy đoạn này TRƯỚC khi test WinForms GuiThongBao(),
--     nếu không INSERT THONGBAO sẽ bị gán nhãn sai hoặc thất bại.
-- ============================================================

-- Gán nhãn OLS tự động cho toàn bộ nhân viên (chạy sau khi admin_ph2.sql đã tạo/insert xong NHANVIEN)
-- chạy SAU KHI admin_ph2.sql đã chạy

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

-- ============================================================
-- [WINFORM-GỘPCODE-12] AuditSession — LOGON thất bại → CHƯA GỘP vào WinForms
--   Policy này ghi nhật ký mỗi lần đăng nhập Oracle thất bại.
--   WinForms chưa có tab/UI hiển thị log LOGON thất bại.
--   ⚠ Cần bổ sung: Tab "Đăng nhập thất bại" trong Audit UI với query:
--     SELECT TO_CHAR(EVENT_TIMESTAMP,'DD/MM/YYYY HH24:MI:SS'),
--            DBUSERNAME, ACTION_NAME, RETURN_CODE
--     FROM UNIFIED_AUDIT_TRAIL
--     WHERE ACTION_NAME = 'LOGON' AND RETURN_CODE != 0
--     ORDER BY EVENT_TIMESTAMP DESC
-- ============================================================

-- Yêu cầu 3: 
-- 3.1: Kích hoạt kiểm toán hệ thống 
begin execute immediate 'noaudit policy AuditSession'; exception when others then null; end;
/
begin execute immediate 'drop audit policy AuditSession'; exception when others then null; end;
/
create audit policy AuditSession
actions logon;
audit policy AuditSession whenever not successful;

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

-- Dọn dẹp FGA cũ
begin dbms_fga.drop_policy('QLBV', 'HSBA',    'AuditBSUpdateHSBA');        exception when others then null; end;
/

begin dbms_fga.drop_policy('QLBV', 'DONTHUOC','AuditSuaDonThuoc');         exception when others then null; end;
/
begin dbms_fga.drop_policy('QLBV', 'HSBA_DV', 'AuditKTVUpdateKetQua');     exception when others then null; end;
/


-- ============================================================
-- [WINFORM-GỘPCODE-13] VW_BaoCaoDieuTri + sp_KhoiTaoHSBAKhancap
--                     + fn_TinhTongChiPhiDieuTri → CHƯA GỘP vào WinForms
--   3 đối tượng này được tạo để phục vụ audit policies tương ứng:
--     • AuditBSSelectBaoCao  → BS SELECT VW_BaoCaoDieuTri
--     • AuditBSExecCapCuu    → BS EXECUTE sp_KhoiTaoHSBAKhancap
--     • AuditNVCalcFee       → DPV/NV EXECUTE fn_TinhTongChiPhiDieuTri
--   ⚠ WinForms chưa có UI gọi 3 đối tượng này → các audit policy trên
--     không bao giờ kích hoạt khi chạy WinForms. Cần thêm:
--     - Tab "Báo cáo điều trị" cho BS: SELECT * FROM QLBV.VW_BaoCaoDieuTri
--     - Nút "Tạo HSBA cấp cứu" cho BS: gọi sp_KhoiTaoHSBAKhancap
--     - Nút "Tính chi phí" cho DPV: SELECT QLBV.fn_TinhTongChiPhiDieuTri(:mahsba) FROM DUAL
-- ============================================================

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


-- ============================================================
-- [WINFORM-GỘPCODE-14] Standard Audit — 5 ngữ cảnh → ĐÃ GỘP (mock data)
--   Mock data trong GetMockAuditData() của SubSystem2Form.cs phản ánh
--   đúng 5 policy dưới đây: tên policy, user, action, object khớp hoàn toàn.
--   Khi Oracle DB đã chạy đủ file này, WinForms Audit UI sẽ hiện dữ liệu thật
--   thay vì mock data (query dùng WHERE OBJECT_SCHEMA='QLBV').
-- ============================================================

-- 3.2: Standard Audit — 5 ngữ cảnh

-- Ngữ cảnh 1: NV update BENHNHAN thành công
create audit policy AuditNVUpdateBN
actions update on QLBV.BENHNHAN
when 'sys_context(''userenv'',''session_user'') in (''NV0001'',''NV0007'')'
evaluate per session;
audit policy AuditNVUpdateBN whenever successful;

-- Ngữ cảnh 2: BS cố update/delete NHANVIEN thất bại
create audit policy AuditBSUpdateNVFail
actions update on QLBV.NHANVIEN, delete on QLBV.NHANVIEN
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditBSUpdateNVFail whenever not successful;

-- Ngữ cảnh 3: BS thực thi stored procedure sp_KhoiTaoHSBAKhancap
create audit policy AuditBSExecCapCuu
actions execute on QLBV.sp_KhoiTaoHSBAKhancap
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditBSExecCapCuu;

-- Ngữ cảnh 4: BS select view VW_BaoCaoDieuTri thành công
create audit policy AuditBSSelectBaoCao
actions select on QLBV.VW_BaoCaoDieuTri
when 'sys_context(''userenv'',''session_user'') in (''BS0001'',''BS0002'')'
evaluate per session;
audit policy AuditBSSelectBaoCao whenever successful;

-- Ngữ cảnh 5: NV thực thi function fn_TinhTongChiPhiDieuTri
create audit policy AuditNVCalcFee
actions execute on QLBV.fn_TinhTongChiPhiDieuTri
when 'sys_context(''userenv'',''session_user'') in (''NV0001'',''NV0002'')'
evaluate per session;
audit policy AuditNVCalcFee;


-- ============================================================
-- [WINFORM-GỘPCODE-15] FGA Policies (3 policy) → ĐÃ GỘP (SQL query + mock data)
--   • WinForms Audit SQL đã thêm: OR FGA_POLICY_NAME IS NOT NULL
--     → lấy cả Standard Audit lẫn FGA trong cùng 1 truy vấn.
--   • Cột "POLICY" = NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES)
--     → phân biệt rõ nguồn gốc mỗi audit entry trên UI.
--   • Mock data phản ánh đúng 3 FGA: AuditSuaDonThuoc,
--     AuditBSUpdateHSBA, AuditKTVUpdateKetQua.
--   ⚠ AuditSuaDonThuoc chỉ trigger khi TRANGTHAI='DA_TAO_XONG' —
--     WinForms DonThuocAddForm đã fix ComboBox chỉ cho 2 giá trị hợp lệ.
-- ============================================================

-- 3.3: FGA / Unified Audit cho 4 tình huống đề yêu cầu

-- 3.3.a: UPDATE DONTHUOC sau khi DA_TAO_XONG (FGA)
begin
    dbms_fga.add_policy(
        object_schema   => 'QLBV',
        object_name     => 'DONTHUOC',
        policy_name     => 'AuditSuaDonThuoc',
        audit_column    => 'MAHSBA,NGAYDT,TENTHUOC,LIEUDUNG',
        audit_condition => 'TRANGTHAI = ''DA_TAO_XONG''',
        statement_types => 'UPDATE'
    );
end;
/

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

-- ============================================================
-- [WINFORM-GỘPCODE-16] Unified Audit (whenever not successful) → ĐÃ GỘP (mock data)
--   AuditIllegalUpdateHSBA và AuditIllegalHSBADV ghi nhật ký các hành vi
--   UPDATE/INSERT/DELETE trái phép bị VPD chặn trước khi thực thi.
--   Mock data trong GetMockAuditData() có đủ 2 policy này với đúng tên,
--   đúng user (BN, KTV, DPV cố thao tác trái quyền), đúng action.
-- ============================================================

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


-- ============================================================
-- [WINFORM-GỘPCODE-17] Query đọc nhật ký kiểm toán → ĐÃ GỘP vào WinForms
--   WinForms LoadAuditData() dùng query kết hợp Standard + FGA:
--     WHERE (OBJECT_SCHEMA = 'QLBV' OR FGA_POLICY_NAME IS NOT NULL)
--       AND DBUSERNAME NOT IN ('SYS', 'SYSTEM')
--   Thêm cột POLICY = NVL(FGA_POLICY_NAME, UNIFIED_AUDIT_POLICIES).
--   Tăng limit từ 100 lên 200 dòng mới nhất.
--   Query gốc bên dưới là cơ sở để xây dựng query trong WinForms.
-- ============================================================

-- 3.4: Query đọc nhật ký kiểm toán

-- Standard / Unified Audit
select unified_audit_policies, dbusername, action_name,
       object_schema, object_name, return_code, event_timestamp
from unified_audit_trail
where object_schema = 'QLBV'
order by event_timestamp desc;

-- Test 
-- Chạy câu lệnh này dưới quyền SYS hoặc QLBV để xem log FGA:
SELECT 
    event_timestamp, 
    dbusername, 
    fga_policy_name, 
    sql_text
FROM unified_audit_trail
WHERE fga_policy_name IS NOT NULL
ORDER BY event_timestamp DESC;




