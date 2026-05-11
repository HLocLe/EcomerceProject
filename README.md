# HappyBox - Nền Tảng Thương Mại Điện Tử Bán Hộp Quà Tặng

Một nền tảng thương mại điện tử hiện đại được xây dựng để quản lý và bán các hộp quà tặng có thể tùy chỉnh với tích hợp quản lý kho hàng, xử lý đơn hàng và thanh toán.

## Giới Thiệu Dự Án

HappyBox được xây dựng theo nguyên tắc Clean Architecture với sự phân tách rõ ràng của các lớp chính:
- Lớp Miền (Domain Layer): Các thực thể kinh doanh cốt lõi và quy tắc
- Lớp Ứng Dụng (Application Layer): Logic kinh doanh, DTOs, mapping và dịch vụ
- Lớp Cơ Sở Hạ Tầng (Infrastructure Layer): Lưu trữ dữ liệu, cấu hình EF Core, migration và tích hợp bên ngoài

## Công Nghệ Sử Dụng

- .NET 8: Framework hỗ trợ dài hạn mới nhất
- Entity Framework Core 8: ORM để truy cập dữ liệu
- SQL Server: Cơ sở dữ liệu quan hệ
- AutoMapper: Mapping đối tượng-đối tượng
- BCrypt.NET: Mã hóa mật khẩu
- Google.Apis.Auth: Xác thực Google OAuth
- JWT (JSON Web Tokens): Xác thực không trạng thái
- Redis: Bộ nhớ đệm phân tán cho refresh tokens
- Docker: Containerization

## Kiến Trúc và Cấu Trúc Dự Án

```
EcomerceProject/
├── Domain/                          # Các thực thể cốt lõi và giao diện
│   ├── Entities/                    # Các mô hình miền
│   ├── Enums/                       # Các liệt kê
│   ├── Constants/                   # Các hằng số (RoleIds, v.v.)
│   └── IUnitOfWork/                 # Giao diện UnitOfWork
│
├── Application/                     # Logic kinh doanh và DTOs
│   ├── DTOs/
│   │   ├── Request/                 # DTOs đầu vào
│   │   └── Response/                # DTOs đầu ra
│   ├── IService/                    # Giao diện dịch vụ
│   ├── Service/                     # Triển khai dịch vụ
│   ├── Mappings/                    # Hồ sơ AutoMapper
│   └── Application.csproj
│
├── Infrastructure/                  # Dữ liệu và dịch vụ bên ngoài
│   ├── Data/                        # DbContext
│   ├── Configurations/              # FluentAPI và Seeder
│   ├── Migrations/                  # EF Core migrations
│   ├── Repositories/                # Mẫu kho lưu trữ chung
│   ├── Services/                    # Triển khai dịch vụ bên ngoài
│   ├── UnitOfWork/                  # Triển khai UnitOfWork
│   └── Infrastructure.csproj
│
└── EcomerceProject/                 # Lớp API (Controllers)
    ├── Controllers/                 # Điểm cuối API REST
    ├── Properties/                  # Cài đặt ứng dụng
    └── Program.cs                   # Cấu hình khởi động
```

## Sơ Đồ Cơ Sở Dữ Liệu

Dự án bao gồm 14 bảng chính được tổ chức theo nhóm chức năng:

Xác thực và Phân quyền:
- Roles: Các vai trò người dùng (Admin, Staff, Customer, Guest)
- Users: Tài khoản người dùng với hỗ trợ B2B

Sản phẩm và Danh mục:
- Categories: Danh mục sản phẩm với hỗ trợ phân cấp
- Products: Danh mục sản phẩm
- Images: Hình ảnh sản phẩm/hộp quà tặng
- GiftBoxes: Hộp quà tặng có thể tùy chỉnh
- GiftBoxComponentConfig: Mẫu thành phần hộp quà tặng
- BoxComponents: Thành phần trong hộp quà tặng (quan hệ N-N)

Đơn hàng và Kho hàng:
- Inventory: Theo dõi kho hàng
- InventoryTransactions: Nhật ký chuyển động kho hàng
- Orders: Đơn hàng khách hàng
- OrderDetails: Các mục dòng trong đơn hàng
- OrderHistories: Theo dõi trạng thái đơn hàng

Thanh toán và Chiết khấu:
- Payments: Bản ghi thanh toán
- PaymentHistories: Nhật ký giao dịch thanh toán
- Vouchers: Phiếu giảm giá/coupon

## Các Tính Năng Chính

Xác thực và Phân quyền:
- Đăng nhập Google OAuth với JWT tokens
- Tích hợp đăng nhập Facebook
- Đăng ký dựa trên email
- Kiểm soát truy cập dựa trên vai trò (RBAC)
- Đặt lại mật khẩu với xác minh OTP
- Quản lý refresh token thông qua Redis

Quản lý Sản phẩm:
- Phân cấp danh mục (quan hệ cha-con)
- Danh mục sản phẩm với theo dõi SKU
- Quản lý hình ảnh (nhiều hình ảnh trên mỗi sản phẩm)
- Theo dõi kho hàng sản phẩm

Cấu hình Hộp Quà Tặng:
- Mẫu hộp quà tặng được cấu hình sẵn (GiftBoxComponentConfig)
- Tạo hộp quà tặng có thể tùy chỉnh
- Thành phần hộp với quản lý số lượng
- Quan hệ N-N giữa Sản phẩm và Hộp quà tặng

Quản lý Đơn Hàng:
- Tạo và theo dõi đơn hàng
- Quản lý trạng thái đơn hàng (Đang chờ, Đang xử lý, Đã gửi, Đã giao, Đã hủy)
- Lịch sử đơn hàng với dấu thời gian
- Các mục dòng cho Sản phẩm và Hộp quà tặng

Hệ thống Kho hàng:
- Theo dõi kho hàng theo thời gian thực
- Giao dịch kho hàng (Nhập, Bán, Trả lại, Hư hỏng, Chuyển)
- Cảnh báo mức kho hàng thấp
- Độc lập chi nhánh (kho hàng đơn)

Xử lý Thanh toán:
- Nhiều phương thức thanh toán (COD, MOMO, VN_PAY)
- Theo dõi trạng thái thanh toán (CHỜ, ĐÃ HOÀN TẤT, THẤT BẠI, HOÀN TIỀN)
- Nhật ký lịch sử thanh toán
- Theo dõi tham chiếu giao dịch

Chiết khấu và Khuyến mãi:
- Quản lý voucher/coupon
- Chiết khấu phần trăm và số tiền cố định
- Yêu cầu giá trị đơn hàng tối thiểu
- Giới hạn chiết khấu tối đa
- Giới hạn sử dụng và kích hoạt dựa trên ngày

## Hướng Dẫn Bắt Đầu

Yêu cầu Tiên Quyết:
- .NET 8 SDK
- SQL Server 2019 trở lên
- Visual Studio 2022 hoặc VS Code
- Git

Cài đặt:

1. Sao chép repository:
   ```bash
   git clone https://github.com/HLocLe/EcomerceProject.git
   cd EcomerceProject
   ```

2. Khôi phục các phụ thuộc:
   ```bash
   dotnet restore
   ```

3. Cấu hình kết nối cơ sở dữ liệu:
   - Chỉnh sửa appsettings.json trong dự án EcomerceProject
   - Cập nhật ConnectionStrings:DefaultConnection
   
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=YOUR_SERVER;Database=EcomerceDb;Trusted_Connection=true;"
   }
   ```

4. Áp dụng migrations:
   ```bash
   dotnet ef database update -p Infrastructure -s EcomerceProject
   ```

5. Cấu hình dịch vụ bên ngoài (trong appsettings.json):
   ```json
   {
     "Google": {
       "ClientId": "your-google-client-id"
     },
     "Facebook": {
       "AppId": "your-facebook-app-id",
       "AppSecret": "your-facebook-app-secret"
     },
     "EmailSettings": {
       "SmtpServer": "smtp.gmail.com",
       "Port": 587,
       "Username": "your-email",
       "Password": "your-password"
     }
   }
   ```

6. Chạy ứng dụng:
   ```bash
   dotnet run --project EcomerceProject
   ```

   API sẽ khả dụng tại: https://localhost:5001

## Các Điểm Cuối API (Tổng Quan)

Xác thực:
- POST /api/auth/register: Đăng ký người dùng mới
- POST /api/auth/login: Đăng nhập với email/mật khẩu
- POST /api/auth/google-login: Đăng nhập Google OAuth
- POST /api/auth/facebook-login: Đăng nhập Facebook OAuth
- POST /api/auth/refresh-token: Làm mới JWT token
- POST /api/auth/forgot-password: Yêu cầu đặt lại mật khẩu
- POST /api/auth/reset-password: Đặt lại mật khẩu với OTP

Sản phẩm:
- GET /api/products: Liệt kê tất cả sản phẩm
- GET /api/products/{id}: Lấy chi tiết sản phẩm
- POST /api/products: Tạo sản phẩm (Admin)
- PUT /api/products/{id}: Cập nhật sản phẩm (Admin)
- DELETE /api/products/{id}: Xóa sản phẩm (Admin)

Danh mục:
- GET /api/categories: Liệt kê danh mục
- GET /api/categories/{id}: Lấy chi tiết danh mục
- POST /api/categories: Tạo danh mục (Admin)
- PUT /api/categories/{id}: Cập nhật danh mục (Admin)
- DELETE /api/categories/{id}: Xóa danh mục (Admin)

Đơn hàng:
- GET /api/orders: Liệt kê đơn hàng của người dùng
- GET /api/orders/{id}: Lấy chi tiết đơn hàng
- POST /api/orders: Tạo đơn hàng mới
- PUT /api/orders/{id}/status: Cập nhật trạng thái đơn hàng

Kho hàng:
- GET /api/inventory: Lấy mức kho hàng
- POST /api/inventory/transactions: Ghi lại giao dịch kho hàng

## Quản Lý Migrations

Tạo migration mới:
```bash
dotnet ef migrations add <MigrationName> -p Infrastructure -s EcomerceProject
```

Áp dụng migrations:
```bash
dotnet ef database update -p Infrastructure -s EcomerceProject
```

Quay lại migration trước đó:
```bash
dotnet ef database update <PreviousMigrationName> -p Infrastructure -s EcomerceProject
```

Xóa cơ sở dữ liệu:
```bash
dotnet ef database drop --force -p Infrastructure -s EcomerceProject
```

## Các Tính Năng Bảo Mật

- Xác thực không trạng thái dựa trên JWT
- Mã hóa mật khẩu với BCrypt
- Đặt lại mật khẩu dựa trên OTP
- Phân quyền dựa trên vai trò
- Thực thi HTTPS
- Cấu hình CORS
- Tích hợp OAuth 2.0 (Google, Facebook)
- Xoay refresh token với Redis

## Cấu Hình

Cấu trúc appsettings.json:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=EcomerceDb;..."
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-min-32-chars",
    "Issuer": "EcomerceProject",
    "Audience": "EcomerceUsers",
    "ExpirationMinutes": 30
  },
  "Google": {
    "ClientId": "..."
  },
  "Facebook": {
    "AppId": "..."
  },
  "EmailSettings": {
    "SmtpServer": "...",
    "Port": 587,
    "Username": "...",
    "Password": "..."
  }
}
```

## Kiểm Thử

Chạy các kiểm tra đơn vị:
```bash
dotnet test
```

## Hướng Dẫn Phát Triển

Kiểu Mã:
- Tuân theo các quy ước đặt tên C# (PascalCase cho các thành viên công khai)
- Sử dụng async/await cho các hoạt động I/O
- Giữ các phương thức tập trung và nhỏ
- Sử dụng tên biến có ý nghĩa

Quy trình Git:
1. Tạo nhánh tính năng: git checkout -b feature/feature-name
2. Commit thay đổi: git commit -am 'Add feature'
3. Đẩy đến nhánh: git push origin feature/feature-name
4. Tạo Pull Request

Thay đổi Cơ Sở Dữ Liệu:
- Luôn tạo migrations cho các thay đổi lược đồ
- Sử dụng tên migrations có ý nghĩa
- Bao gồm các bản cập nhật seeder nếu cần thiết

## Đóng Góp

1. Fork repository
2. Tạo nhánh tính năng của bạn
3. Commit các thay đổi của bạn
4. Đẩy đến nhánh
5. Tạo Pull Request

## Tác Giả

Loc - Phát triển ban đầu

## Hỗ Trợ

Để nhận hỗ trợ, vui lòng mở issue trên GitHub hoặc liên hệ với nhóm phát triển.

---

Cập nhật Lần Cuối: 11 Tháng 5 Năm 2026
