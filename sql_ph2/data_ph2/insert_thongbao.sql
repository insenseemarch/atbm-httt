-- ============================================================================
-- INSERT THONGBAO (7 thong bao OLS)
-- ============================================================================

INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t1] Thông báo họp toàn thể nhân viên bệnh viện', SYSTIMESTAMP - INTERVAL '0' HOUR, N'Hội trường trung tâm');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t2] Thông báo họp Ban Giám đốc', SYSTIMESTAMP - INTERVAL '1' HOUR, N'Phòng họp A1');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t3] Thông báo họp Lãnh đạo các khoa', SYSTIMESTAMP - INTERVAL '2' HOUR, N'Phòng họp B2');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t4] Họp khẩn Lãnh đạo Khoa Tiêu hóa', SYSTIMESTAMP - INTERVAL '3' HOUR, N'Khoa Tiêu hóa - Tầng 3');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t5] Họp nhân viên Khoa Tiêu hóa cơ sở Hồ Chí Minh', SYSTIMESTAMP - INTERVAL '4' HOUR, N'CS Hồ Chí Minh - Phòng C3');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t6] Họp nhân viên Khoa Tiêu hóa cơ sở Hà Nội', SYSTIMESTAMP - INTERVAL '5' HOUR, N'CS Hà Nội - Phòng H2');
INSERT INTO THONGBAO (NOIDUNG, NGAYGIO, DIADIEM) VALUES (N'[t7] Họp liên khoa Tiêu hóa - Thần kinh tại Hải Phòng', SYSTIMESTAMP - INTERVAL '6' HOUR, N'CS Hải Phòng - Phòng HP1');

COMMIT;
