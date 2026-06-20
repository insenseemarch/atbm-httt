-- CONNECTION: QLBV - XEPDB1

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

-- Tạo bảng

-- Bảng Khoa (vì ở yêu cầu 2 (OLS) khoa là chiều nhãn (Tiêu hóa / Thần kinh / Tim mạch)

CREATE TABLE KHOA (
    MAKHOA  VARCHAR2(20)   NOT NULL,
    TENKHOA NVARCHAR2(100) NOT NULL,
    CONSTRAINT PK_KHOA PRIMARY KEY (MAKHOA)
);

-- Bệnh nhân
CREATE TABLE BENHNHAN (
    MABN            VARCHAR2(20)    NOT NULL,
    TENBN           NVARCHAR2(100)  NOT NULL,
    PHAI            NVARCHAR2(10),
    NGAYSINH        DATE,
    CCCD            VARCHAR2(20),
    SONHA           NVARCHAR2(50),
    TENDUONG        NVARCHAR2(100),
    QUANHUYEN       NVARCHAR2(100),
    TINHTP          NVARCHAR2(100),
    TIENSUBENH      NVARCHAR2(1000),
    TIENSUBENHGD    NVARCHAR2(1000),
    DIUNGTHUOC      NVARCHAR2(500),
    CONSTRAINT PK_BENHNHAN PRIMARY KEY (MABN)
);


-- Nhân viên
CREATE TABLE NHANVIEN (
    MANV        VARCHAR2(20)    NOT NULL,
    HOTEN       NVARCHAR2(100)  NOT NULL,
    PHAI        NVARCHAR2(10),
    NGAYSINH    DATE,
    CMND        VARCHAR2(20),
    QUEQUAN     NVARCHAR2(300),
    SODT        VARCHAR2(20),
    VAITRO      NVARCHAR2(50)   NOT NULL,
    MAKHOA      VARCHAR2(20),
    -- Thêm cho YC2 (OLS)  
    CAPBAC      NVARCHAR2(50),
    COSO        NVARCHAR2(50),
    CONSTRAINT PK_NHANVIEN PRIMARY KEY (MANV),
    CONSTRAINT FK_NV_KHOA FOREIGN KEY (MAKHOA) REFERENCES KHOA(MAKHOA),
    CONSTRAINT CHK_VAITRO CHECK (
        VAITRO IN (
            N'Điều phối viên',
            N'Bác sĩ/Y sĩ',
            N'Kỹ thuật viên',
            N'Bệnh nhân'
        )
    ),
    CONSTRAINT CHK_CAPBAC CHECK (
        CAPBAC IS NULL OR CAPBAC IN (
            N'Ban Giám đốc',
            N'Lãnh đạo khoa',
            N'Lãnh đạo phòng',
            N'Nhân viên'
        )
    ),
    CONSTRAINT CHK_COSO CHECK (
        COSO IS NULL OR COSO IN ('Hồ Chí Minh', 'Hải Phòng', 'Hà Nội')
    )
);


-- Hồ sơ bệnh án

CREATE TABLE HSBA (
    MAHSBA      VARCHAR2(20)    NOT NULL,
    MABN        VARCHAR2(20)    NOT NULL,
    NGAY        DATE            NOT NULL,
    CHANDOAN    NVARCHAR2(1000),
    DIEUTRI     NVARCHAR2(1000),
    MABS        VARCHAR2(20),
    MAKHOA      VARCHAR2(20)    NOT NULL,
    KETLUAN     NVARCHAR2(1000),
    CONSTRAINT PK_HSBA      PRIMARY KEY (MAHSBA),
    CONSTRAINT FK_HSBA_BN   FOREIGN KEY (MABN)   REFERENCES BENHNHAN(MABN),
    CONSTRAINT FK_HSBA_BS   FOREIGN KEY (MABS)   REFERENCES NHANVIEN(MANV),
    CONSTRAINT FK_HSBA_KHOA FOREIGN KEY (MAKHOA) REFERENCES KHOA(MAKHOA)
);


-- Hồ sơ bệnh án - Dịch vụ
CREATE TABLE HSBA_DV (
    MAHSBA    VARCHAR2(20)    NOT NULL,
    LOAIDV    NVARCHAR2(100)  NOT NULL,
    NGAYDV    DATE            NOT NULL,
    MAKTV     VARCHAR2(20),
    KETQUA    NVARCHAR2(2000),
    CONSTRAINT PK_HSBA_DV     PRIMARY KEY (MAHSBA, LOAIDV, NGAYDV),
    CONSTRAINT FK_HSBADV_HSBA FOREIGN KEY (MAHSBA) REFERENCES HSBA(MAHSBA),
    CONSTRAINT FK_HSBADV_KTV  FOREIGN KEY (MAKTV)  REFERENCES NHANVIEN(MANV),
    -- Chưa gán KTV thì chưa được nhập kết quả
    CONSTRAINT CHK_HSBADV_KQ_KTV CHECK (
        MAKTV IS NOT NULL OR KETQUA IS NULL
    )
);


-- Đơn thuốc
CREATE TABLE DONTHUOC (
    MAHSBA      VARCHAR2(20)    NOT NULL,
    NGAYDT      DATE            NOT NULL,
    TENTHUOC    NVARCHAR2(200)  NOT NULL,
    LIEUDUNG    NVARCHAR2(500),
    CONSTRAINT PK_DONTHUOC      PRIMARY KEY (MAHSBA, NGAYDT, TENTHUOC),
    CONSTRAINT FK_DT_HSBA       FOREIGN KEY (MAHSBA) REFERENCES HSBA(MAHSBA)
);


-- Thông báo (PK: ID tự động tăng vì OLS gắn nhãn theo từng hàng)
CREATE TABLE THONGBAO (
    ID       NUMBER          GENERATED ALWAYS AS IDENTITY,
    NOIDUNG  NVARCHAR2(2000),
    NGAYGIO  TIMESTAMP,
    DIADIEM  NVARCHAR2(300),
    CONSTRAINT PK_THONGBAO    PRIMARY KEY (ID)
);

CREATE INDEX IDX_HSBA_MABN      ON HSBA(MABN);
CREATE INDEX IDX_HSBA_MABS      ON HSBA(MABS);
CREATE INDEX IDX_HSBADV_MAKTV   ON HSBA_DV(MAKTV);


COMMIT;

-- ============================================================================
-- RUN TAT CA FILE DATA THEO DUNG THU TU KHOA NGOAI
-- ============================================================================

PROMPT === Start loading KHOA/NHANVIEN/THONGBAO ===
@../data_ph2/insert_khoa.sql
@../data_ph2/insert_nhanvien.sql
@../data_ph2/insert_thongbao.sql

PROMPT === Start loading BENHNHAN (10 parts) ===
@../data_ph2/insert_benhnhan_part_01.sql
@../data_ph2/insert_benhnhan_part_02.sql
@../data_ph2/insert_benhnhan_part_03.sql
@../data_ph2/insert_benhnhan_part_04.sql
@../data_ph2/insert_benhnhan_part_05.sql
@../data_ph2/insert_benhnhan_part_06.sql
@../data_ph2/insert_benhnhan_part_07.sql
@../data_ph2/insert_benhnhan_part_08.sql
@../data_ph2/insert_benhnhan_part_09.sql
@../data_ph2/insert_benhnhan_part_10.sql

PROMPT === Start loading HSBA (10 parts) ===
@../data_ph2/insert_hsba_part_01.sql
@../data_ph2/insert_hsba_part_02.sql
@../data_ph2/insert_hsba_part_03.sql
@../data_ph2/insert_hsba_part_04.sql
@../data_ph2/insert_hsba_part_05.sql
@../data_ph2/insert_hsba_part_06.sql
@../data_ph2/insert_hsba_part_07.sql
@../data_ph2/insert_hsba_part_08.sql
@../data_ph2/insert_hsba_part_09.sql
@../data_ph2/insert_hsba_part_10.sql

PROMPT === Start loading HSBA_DV (10 parts) ===
@../data_ph2/insert_hsba_dv_part_01.sql
@../data_ph2/insert_hsba_dv_part_02.sql
@../data_ph2/insert_hsba_dv_part_03.sql
@../data_ph2/insert_hsba_dv_part_04.sql
@../data_ph2/insert_hsba_dv_part_05.sql
@../data_ph2/insert_hsba_dv_part_06.sql
@../data_ph2/insert_hsba_dv_part_07.sql
@../data_ph2/insert_hsba_dv_part_08.sql
@../data_ph2/insert_hsba_dv_part_09.sql
@../data_ph2/insert_hsba_dv_part_10.sql

PROMPT === Start loading DONTHUOC (10 parts) ===
@../data_ph2/insert_donthuoc_part_01.sql
@../data_ph2/insert_donthuoc_part_02.sql
@../data_ph2/insert_donthuoc_part_03.sql
@../data_ph2/insert_donthuoc_part_04.sql
@../data_ph2/insert_donthuoc_part_05.sql
@../data_ph2/insert_donthuoc_part_06.sql
@../data_ph2/insert_donthuoc_part_07.sql
@../data_ph2/insert_donthuoc_part_08.sql
@../data_ph2/insert_donthuoc_part_09.sql
@../data_ph2/insert_donthuoc_part_10.sql

PROMPT === Done ===
COMMIT;

