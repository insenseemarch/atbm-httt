-- connection: sysdba và qlbv - xepdb1
-- ============================================================
-- 07.sql - yêu cầu 4: sao lưu và phục hồi bằng oracle data pump
-- ============================================================
-- file này chạy sau các file trong thư mục run:
--   01.sql: chạy bằng sysdba để tạo qlbv và cấu hình ols.
--   02.sql: chạy bằng qlbv để tạo bảng và dữ liệu mẫu.
--   03.sql: chạy bằng qlbv để tạo user, role và vpd.
--   04.sql: chạy bằng sysdba để áp dụng ols.
--   05.sql: chạy bằng qlbv để chèn thông báo và demo flashback cơ bản.
--   06.sql: chạy bằng sysdba để tạo audit policy và đọc audit log.
--
-- lưu ý quan trọng:
--   1. phần tạo directory object phải chạy bằng sysdba.
--   2. phần tạo bảng lịch sử và kiểm tra dữ liệu chạy bằng qlbv.
--   3. expdp và impdp không chạy trực tiếp trong sql developer worksheet.
--      hãy sao chép lệnh ở mục [07-datapump-cmd] và chạy trong cmd hoặc powershell.
--   4. thư mục d:\oracle_backup phải tồn tại trên máy đang chạy oracle database.
-- ============================================================

set serveroutput on
set linesize 260
set pagesize 100

-- ============================================================
-- [07-sysdba-01] chạy bằng role: sysdba - xepdb1
-- mục đích: tạo oracle directory object cho data pump.
-- nếu đang kết nối qlbv, hãy đổi sang connection sysdba trước khi chạy mục này.
-- ============================================================

-- tạo directory object trỏ đến thư mục vật lý dùng để lưu file .dmp và .log.
-- nếu máy của bạn dùng đường dẫn khác, sửa 'C:\oracle_backup' cho đúng môi trường.
-- Lưu ý: 
--      - nên ghi vào ổ đĩa C để dễ demo, bởi vì oracle được cấp quyền ghi trên ổ đĩa C.
--      - tạo folder bên ngoài trước rồi dán PATH vào dưới đây.
create or replace directory backup_dir as 'C:\oracle_backup';

-- cấp quyền cho schema nghiệp vụ qlbv để export/import data pump.
grant read, write on directory backup_dir to qlbv;

-- cấp quyền cho system trong trường hợp muốn import/restore bằng system.
grant read, write on directory backup_dir to system;

-- kiểm tra directory object và quyền vừa cấp.

select directory_name, directory_path
from dba_directories
where directory_name = 'BACKUP_DIR';

select grantee, privilege
from dba_tab_privs
where table_name = 'BACKUP_DIR'
order by grantee, privilege;

-- ============================================================
-- [07-qlbv-01] chạy bằng user: qlbv - xepdb1
-- mục đích: tạo bảng lịch sử backup/restore trong schema qlbv.
-- nếu vừa chạy phần sysdba ở trên, hãy đổi sang connection qlbv rồi chạy từ đây.
-- ============================================================

-- hai bảng lịch sử này không ảnh hưởng đến các bảng nghiệp vụ chính.
-- script có thể chạy lại nhiều lần vì đã xử lý drop nếu bảng tồn tại.
begin
    execute immediate 'drop table qlbv.restore_history cascade constraints';
exception
    when others then
        if sqlcode != -942 then
            raise;
        end if;
end;
/

begin
    execute immediate 'drop table qlbv.backup_history cascade constraints';
exception
    when others then
        if sqlcode != -942 then
            raise;
        end if;
end;
/

create table qlbv.backup_history (
    backup_id    number generated always as identity primary key,
    backup_name  varchar2(255) not null,
    backup_type  varchar2(30)  not null,
    backup_time  timestamp     default systimestamp not null,
    backup_path  varchar2(500),
    object_scope varchar2(1000),
    note         nvarchar2(1000),
    constraint chk_backup_history_type check (
        backup_type in ('schema', 'tables', 'manual', 'auto')
    )
);

comment on table qlbv.backup_history is 'lịch sử sao lưu data pump của schema qlbv';
comment on column qlbv.backup_history.backup_name is 'tên file dump hoặc tên đợt backup';
comment on column qlbv.backup_history.backup_type is 'loại backup: schema, tables, manual, auto';
comment on column qlbv.backup_history.object_scope is 'phạm vi backup: qlbv hoặc danh sách bảng quan trọng';

create table qlbv.restore_history (
    restore_id        number generated always as identity primary key,
    restore_time      timestamp default systimestamp not null,
    backup_name       varchar2(255) not null,
    restore_object    varchar2(1000),
    incident_time     timestamp,
    incident_user     varchar2(128),
    incident_action   varchar2(80),
    audit_object_name varchar2(128),
    note              nvarchar2(1000)
);

comment on table qlbv.restore_history is 'lịch sử phục hồi dữ liệu dựa trên dump và audit log';
comment on column qlbv.restore_history.incident_time is 'thời điểm sự cố xác định từ audit log';
comment on column qlbv.restore_history.incident_user is 'user thực hiện thao tác gây sự cố';
comment on column qlbv.restore_history.incident_action is 'hành động audit, ví dụ update/delete/drop table';
comment on column qlbv.restore_history.audit_object_name is 'object bị tác động theo audit log';

insert into qlbv.backup_history (
    backup_name, backup_type, backup_path, object_scope, note
) values (
    'qlbv_schema_yyyymmdd_hh24mi.dmp',
    'schema',
    'd:\oracle_backup',
    'qlbv',
    n'mẫu ghi lịch sử backup schema bằng data pump'
);

commit;

select * from qlbv.backup_history order by backup_time desc;
select * from qlbv.restore_history order by restore_time desc;

-- ============================================================
-- [07-qlbv-02] chạy bằng user: qlbv - xepdb1
-- mục đích: kiểm tra các bảng nghiệp vụ cần backup/restore.
-- các bảng này được tạo trong run/02.sql và được phân quyền/vpd trong run/03.sql.
-- ============================================================

select table_name
from user_tables
where table_name in (
    'KHOA',
    'BENHNHAN',
    'NHANVIEN',
    'HSBA',
    'HSBA_DV',
    'DONTHUOC',
    'THONGBAO'
)
order by table_name;

-- nhóm bảng ưu tiên backup/restore khi có sự cố nghiệp vụ:
--   benhnhan: thông tin bệnh nhân.
--   hsba: hồ sơ bệnh án.
--   hsba_dv: dịch vụ và kết quả cận lâm sàng.
--   donthuoc: đơn thuốc, đang được audit trong run/06.sql.
select 'benhnhan' as table_name, count(*) as row_count from qlbv.benhnhan
union all
select 'hsba', count(*) from qlbv.hsba
union all
select 'hsba_dv', count(*) from qlbv.hsba_dv
union all
select 'donthuoc', count(*) from qlbv.donthuoc;

-- ============================================================
-- [07-datapump-cmd] chạy bằng cmd hoặc powershell, không phải sql role
-- mục đích: backup chủ động bằng expdp và restore bằng impdp.
-- kết nối theo môi trường run: qlbv/123@localhost:1521/xepdb1.
-- sao chép từng lệnh dưới đây ra command prompt hoặc powershell để chạy.
-- ============================================================

/* CHẠY TRONG CMP:
rem 1. backup toàn bộ schema qlbv:
expdp qlbv/123@localhost:1521/xepdb1 schemas=qlbv directory=backup_dir dumpfile=qlbv_schema_%date:~-4%%date:~4,2%%date:~7,2%.dmp logfile=qlbv_schema_export.log

rem 2. backup các bảng quan trọng:
expdp qlbv/123@localhost:1521/xepdb1 tables=qlbv.benhnhan,qlbv.hsba,qlbv.hsba_dv,qlbv.donthuoc directory=backup_dir dumpfile=qlbv_important_tables.dmp logfile=qlbv_important_tables_export.log

rem 3. restore toàn schema qlbv.
rem cảnh báo: table_exists_action=replace có thể ghi đè dữ liệu hiện tại.
Note: qlbv_schema_%date:~-4%%date:~4,2%%date:~7,2%: file này được lưu theo ngày tháng hiện tại, phải để đúng tên file nhé: <ten_file_dump_can_restore>
impdp qlbv/123@localhost:1521/xepdb1 schemas=qlbv directory=backup_dir dumpfile=<ten_file_dump_can_restore>.dmp logfile=qlbv_schema_import.log table_exists_action=replace

rem 4. restore một bảng hoặc nhóm bảng sau khi xác định sự cố từ audit log.
rem sửa tables=... theo object_name đọc được ở mục [07-audit-01].
impdp qlbv/123@localhost:1521/xepdb1 tables=qlbv.donthuoc directory=backup_dir dumpfile=<ten_file_dump_can_restore>.dmp logfile=qlbv_table_import.log table_exists_action=replace
*/

-- sau khi chạy expdp thành công, có thể ghi lịch sử backup thủ công.
-- chạy bằng user: qlbv.
/*
insert into qlbv.backup_history (
    backup_name, backup_type, backup_path, object_scope, note
) values (
    'qlbv_important_tables.dmp',
    'tables',
    'd:\oracle_backup',
    'benhnhan,hsba,hsba_dv,donthuoc',
    n'backup các bảng quan trọng bằng expdp'
);
commit;
*/

-- ============================================================
-- [07-audit-01] chạy bằng sysdba hoặc qlbv có quyền select any dictionary
-- mục đích: đọc audit log từ run/06.sql để xác định sự cố trước khi restore.
-- ============================================================

-- các thao tác nguy hiểm gần nhất trên schema qlbv.
-- return_code = 0 nghĩa là thao tác thành công; return_code <> 0 là thất bại.
select
    event_timestamp,
    dbusername,
    action_name,
    object_schema,
    object_name,
    return_code,
    unified_audit_policies,
    fga_policy_name,
    sql_text
from unified_audit_trail
where object_schema = 'QLBV'
  and object_name in ('BENHNHAN', 'HSBA', 'HSBA_DV', 'DONTHUOC', 'NHANVIEN')
order by event_timestamp desc
fetch first 100 rows only;

-- log riêng của các policy audit đã tạo trong run/06.sql.
select
    event_timestamp,
    dbusername,
    action_name,
    object_schema,
    object_name,
    return_code,
    unified_audit_policies,
    fga_policy_name,
    sql_text
from unified_audit_trail
where unified_audit_policies in (
    'AUDITSUCDPVUPDATEBN',
    'AUDITSUCBSUPDATEDT',
    'AUDITDONTHUOCINSERT',
    'AUDITDONTHUOCUPDATE',
    'AUDITFAILBSUPDATENV',
    'AUDITFAILBNDELETEHSBA',
    'AUDITFAILKTVDELETEDT',
    'AUDITFAILDPVDELETEHSBA',
    'AUDITFAILBNUPDATEDV',
    'AUDITFAILKTVSELECTBN',
    'AUDITILLEGALUPDATEHSBA',
    'AUDITILLEGALHSBADV'
)
or fga_policy_name in (
    'AUDITSUADONTHUOC',
    'AUDITBSUPDATEHSBA',
    'AUDITKTVUPDATEKETQUA'
)
order by event_timestamp desc
fetch first 100 rows only;

-- fga trail riêng nếu môi trường ghi log fga vào dba_fga_audit_trail.

select
    timestamp,
    db_user,
    object_schema,
    object_name,
    policy_name,
    statement_type,
    sql_text
from dba_fga_audit_trail
where object_schema = 'QLBV'
  and policy_name in (
      'AUDITSUADONTHUOC',
      'AUDITBSUPDATEHSBA',
      'AUDITKTVUPDATEKETQUA'
  )
order by timestamp desc;

-- ============================================================
-- [07-restore-history] chạy bằng user: qlbv - xepdb1
-- mục đích: ghi nhận kết quả restore sau khi đã chạy impdp thành công.
-- sửa các giá trị mẫu theo audit log thực tế.
-- ============================================================

/*
insert into qlbv.restore_history (
    backup_name,
    restore_object,
    incident_time,
    incident_user,
    incident_action,
    audit_object_name,
    note
) values (
    '<ten_file_dump_can_restore>.dmp',
    'qlbv.donthuoc',
    to_timestamp('2026-06-17 10:15:00', 'yyyy-mm-dd hh24:mi:ss'),
    'bs0001',
    'update',
    'donthuoc',
    n'restore bảng donthuoc sau khi audit log ghi nhận cập nhật sai'
);
commit;
*/

select * from qlbv.restore_history order by restore_time desc;

-- ============================================================
-- [07-flashback-demo] chạy bằng user: qlbv - xepdb1
-- mục đích: demo flashback table như phương án bổ sung.
-- mặc định chỉ để lệnh mẫu, không tự động sửa hoặc xóa dữ liệu thật.
-- ============================================================

-- lấy mốc thời gian trước khi thao tác lỗi.
select to_char(systimestamp, 'yyyy-mm-dd hh24:mi:ss') as before_incident_time
from dual;

-- xem một dòng donthuoc có thể dùng để demo.
select *
from qlbv.donthuoc
where rownum = 1;

/*
-- cảnh báo: chỉ bỏ comment khi chắc chắn đang demo trên dữ liệu mẫu.
alter table qlbv.donthuoc enable row movement;

update qlbv.donthuoc
set lieudung = n'dữ liệu bị sửa nhầm trong demo flashback'
where rownum = 1;
commit;

flashback table qlbv.donthuoc
to timestamp to_timestamp('yyyy-mm-dd hh24:mi:ss', 'yyyy-mm-dd hh24:mi:ss');

select * from qlbv.donthuoc where rownum = 1;
*/

-- ============================================================
-- [07-demo-flow] tóm tắt kịch bản demo
-- ============================================================
-- 1. sysdba chạy [07-sysdba-01] để tạo backup_dir.
-- 2. qlbv chạy [07-qlbv-01] để tạo backup_history và restore_history.
-- 3. chạy expdp ngoài cmd/powershell để backup schema hoặc các bảng quan trọng.
-- 4. tạo sự cố demo, ví dụ update sai qlbv.donthuoc.lieudung.
-- 5. sysdba hoặc qlbv đọc [07-audit-01] để lấy user, thời điểm, object và sql text.
-- 6. chọn file dump trước thời điểm sự cố.
-- 7. chạy impdp ngoài cmd/powershell để restore bảng bị ảnh hưởng.
-- 8. qlbv ghi kết quả vào restore_history.
-- ============================================================
