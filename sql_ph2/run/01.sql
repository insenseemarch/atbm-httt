-- CONNECTION: SYSDBA - XEPDB1

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
exec LBACSYS.CONFIGURE_OLS;
exec LBACSYS.OLS_ENFORCEMENT.ENABLE_OLS;
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