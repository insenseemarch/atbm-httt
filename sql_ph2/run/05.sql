-- CONNECTION: QLBV - XEPDB1
-- =================================================
-- = NOTE: Nhớ DISCONNECT xong CONNECT lại nhé !!! =
-- =================================================

truncate table THONGBAO;

SELECT *
FROM DBA_SA_USER_LABELS
WHERE USER_NAME='QLBV';
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
/*
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
*/
