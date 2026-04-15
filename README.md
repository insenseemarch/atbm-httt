# atbm-httt

Phân hệ 1: ứng dụng WinForms quản trị Oracle cho bệnh viện.

## 1. Tổng quan luồng chạy

Bộ script chuẩn để khởi tạo hệ thống nằm trong thư mục [sql](sql):

1. [01_setup_app_admin.sql](sql/01_setup_app_admin.sql)
2. [02_schema_objects.sql](sql/02_schema_objects.sql)
3. [03_views_procedures.sql](sql/03_views_procedures.sql)
4. [04_mock_data.sql](sql/04_mock_data.sql)

Thứ tự chạy là bắt buộc. Script trong thư mục [procedure](procedure) là các bản sao/phiên bản cũ để tham khảo, không phải luồng chạy chính.

## 2. Điều kiện trước khi chạy

- Oracle Database đã được cài và đang chạy.
- Bạn có quyền đăng nhập bằng tài khoản có quyền tạo user/role, tốt nhất là `SYS` hoặc `SYSTEM` với quyền `SYSDBA`.
- SQL Developer, SQL*Plus hoặc công cụ tương đương để chạy script.
- Visual Studio để chạy WinForms.

## 3. Chạy SQL từ đầu đến cuối

### Bước 1: Tạo user `APP_ADMIN`

Đăng nhập bằng kết nối quản trị Oracle, ví dụ:
- User: `SYS`
- Role: `SYSDBA`
- Host/Port/Service: theo máy của bạn

Sau đó chạy file:

- [sql/01_setup_app_admin.sql](sql/01_setup_app_admin.sql)

Script này sẽ:
- tạo user `app_admin` với mật khẩu mặc định `123456`
- cấp các quyền cần thiết cho `app_admin`
- cấp quyền đọc các view hệ thống để màn WinForms lấy dữ liệu

### Bước 2: Chuyển sang kết nối `APP_ADMIN`

Sau khi chạy xong bước 1, ngắt kết nối `SYS`/`SYSTEM` và đăng nhập lại bằng:
- User: `APP_ADMIN`
- Password: `123456`

Các script tiếp theo phải chạy bằng `APP_ADMIN`.

Lưu ý: nếu PDB chưa mở, hãy chạy trước:

```sql
ALTER PLUGGABLE DATABASE PDBQLBV OPEN;
```

### Bước 3: Tạo schema objects

Chạy file:

- [sql/02_schema_objects.sql](sql/02_schema_objects.sql)

Script này tạo:
- các bảng quản lý người dùng, role, object, privilege
- các bảng grant cho user/role/cột
- các ràng buộc, index, comment
- dữ liệu khởi tạo rỗng / cấu trúc nền

### Bước 4: Tạo views và procedures

Chạy file:

- [sql/03_views_procedures.sql](sql/03_views_procedures.sql)

Script này tạo:
- các view hiển thị danh sách user, role, quyền hệ thống, quyền object, quyền cột
- các stored procedure để tạo/xóa user, role
- các stored procedure để cấp/thu hồi quyền hệ thống, quyền object, quyền cột

### Bước 5: Nạp dữ liệu mẫu

Chạy file:

- [sql/04_mock_data.sql](sql/04_mock_data.sql)

Script này sẽ:
- xóa dữ liệu cũ trong các bảng mock
- tạo các user mẫu như `ADMIN`, `BACSI`, `KTV`, `BN`, `DPV`
- tạo các role mẫu
- nạp object/cột mẫu
- nạp dữ liệu grant mẫu để test màn hình WinForms

### Lưu ý quan trọng khi chạy SQL

- `01_setup_app_admin.sql` chỉ nên chạy ở kết nối quản trị `SYS`/`SYSTEM`.
- `02_schema_objects.sql`, `03_views_procedures.sql`, `04_mock_data.sql` phải chạy ở kết nối `APP_ADMIN`.
- Nếu chạy lại nhiều lần, `02_schema_objects.sql` và `04_mock_data.sql` đã có logic dọn dữ liệu/bảng cũ để hỗ trợ nạp lại.
- `03_views_procedures.sql` dùng `CREATE OR REPLACE`, nên có thể chạy lại để cập nhật view/procedure.

## 4. Chạy WinForms

### Bước 1: Mở solution

Mở file solution:

- [winform/PhanHe1/PhanHe1.sln](winform/PhanHe1/PhanHe1.sln)

### Bước 2: Kiểm tra project startup

Đảm bảo project `PhanHe1` là startup project.

### Bước 3: Kiểm tra kết nối Oracle

Khi chạy app, màn hình đăng nhập sẽ hỏi:
- Host
- Port
- Service Name
- Tài khoản
- Mật khẩu

Giá trị mặc định hiện tại trong form login là:
- Host: `localhost`
- Port: `1521`
- Service Name: `PDBQLBV`
- Tài khoản: `app_admin`

Mật khẩu mặc định của `app_admin` sau khi chạy script là:
- `123456`

Nếu database của bạn nằm ở máy khác hoặc service khác, nhập lại đúng thông tin ở màn hình đăng nhập trước khi bấm `Đăng nhập`.

### Bước 4: Chạy ứng dụng

Chọn `Debug > Start` hoặc nhấn `F5`.

Nếu đăng nhập thành công, app sẽ mở màn hình quản trị chính.

## 5. Cách đổi connection string

### A. Đổi kết nối khi đăng nhập WinForms

WinForms hiện lấy thông tin kết nối từ màn hình đăng nhập, không hard-code một connection cố định trong logic chính.

Nếu muốn đổi giá trị mặc định hiển thị trên form login, sửa file:

- [winform/PhanHe1/LoginForm.cs](winform/PhanHe1/LoginForm.cs)

Các dòng cần đổi:

- `txtHost` mặc định `localhost`
- `txtPort` mặc định `1521`
- `txtService` mặc định `PDBQLBV`
- `txtUser` mặc định `app_admin`

Nếu database của bạn là:
- host khác
- port khác
- service name khác

thì sửa các giá trị này để người chạy sau không phải nhập lại mỗi lần.

### B. Đổi connection string trong App.config

File:

- [winform/PhanHe1/App.config](winform/PhanHe1/App.config)

Trong đó có connection string mặc định:

```xml
<add name="OracleDbContext"
     connectionString="User Id=app_admin;Password=123456;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=PDBQLBV)))"
     providerName="Oracle.ManagedDataAccess.Client" />
```

Nếu muốn đồng bộ cấu hình mặc định với máy khác, đổi:
- `HOST=localhost`
- `PORT=1521`
- `SERVICE_NAME=PDBQLBV`
- `User Id=app_admin`
- `Password=123456`

Ngoài ra trong phần Oracle client alias cũng có cấu hình:

```xml
<dataSource alias="PDBQLBV_Local" descriptor="(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=PDBQLBV)))" />
```

Nếu service đổi, nên sửa luôn cả phần này để đồng bộ.

### C. Công thức connection string Oracle

Nếu cần tự ghép lại, format đang dùng là:

```text
User Id=<username>;Password=<password>;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=<host>)(PORT=<port>))(CONNECT_DATA=(SERVICE_NAME=<service_name>)));
```

Ví dụ:

```text
User Id=app_admin;Password=123456;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=PDBQLBV)));
```

## 6. Tài khoản test nhanh

Sau khi chạy xong bộ script, có thể dùng:

- User đăng nhập app: `app_admin`
- Password: `123456`

Các user/role mock sẽ có sẵn trong dữ liệu để test màn hình quản trị.

## 7. Ghi chú kỹ thuật

- Project target .NET Framework 4.7.2.
- Dùng `Oracle.ManagedDataAccess`.
- Luồng logout đã được cấu hình để quay về màn hình login.
- Nếu không thấy dữ liệu sau khi nạp mock data, kiểm tra lại user đang kết nối có đúng là `APP_ADMIN` hay không.

## 8. Checklist chạy lại từ đầu

1. Chạy [sql/01_setup_app_admin.sql](sql/01_setup_app_admin.sql) bằng `SYS`/`SYSTEM`.
2. Đăng nhập lại bằng `APP_ADMIN`.
3. Chạy [sql/02_schema_objects.sql](sql/02_schema_objects.sql).
4. Chạy [sql/03_views_procedures.sql](sql/03_views_procedures.sql).
5. Chạy [sql/04_mock_data.sql](sql/04_mock_data.sql).
6. Mở [winform/PhanHe1/PhanHe1.sln](winform/PhanHe1/PhanHe1.sln).
7. Start project `PhanHe1`.
8. Đăng nhập bằng `APP_ADMIN` / `123456`.
