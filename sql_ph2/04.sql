-- CONNECTION: SYSDBA - XEPDB1

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