# HappyBox - Nen tang Thuong mai dien tu Hop qua tang

## Tong quan du an

HappyBox la mot nen tang thuong mai dien tu (e-commerce) backend duoc xay dung bang ASP.NET Core 8, chuyen ve quan ly va ban cac hop qua tang (gift box) tuy chinh. He thong ho tro day du cac tinh nang tu quan ly san pham, tao hop qua tuy chinh, gio hang, xu ly don hang, quan ly ton kho, thanh toan (COD, MoMo, VNPay), quan ly voucher/ma giam gia, chatbot AI tu van san pham, den xac thuc nguoi dung (email, Google OAuth, Facebook OAuth).

Du an theo kien truc **Clean Architecture** voi 4 project trong mot solution:

| Project | Vai tro |
|---------|---------|
| `Domain/` | Cac thuc the (entity), enum, interface repository |
| `Application/` | Logic nghiep vu, DTO, interface va implementation cua service |
| `Infrastructure/` | EF Core, repository, tich hop dich vu ben ngoai (mail, MoMo, chatbot, Redis) |
| `PRN2322/` | Tang API (ASP.NET Core Web API) - controller, cau hinh khoi dong |

Phan frontend duoc trien khai rieng biet tai `https://tet-den-roi.vercel.app`.

---

## Cong nghe su dung

| Thanh phan | Chi tiet |
|------------|----------|
| Ngon ngu | C# 12 |
| Framework | .NET 8 / ASP.NET Core 8 (Web API) |
| Kien truc | Clean Architecture (Domain - Application - Infrastructure - API) |
| ORM | Entity Framework Core 8 (Npgsql provider) |
| Co so du lieu | PostgreSQL |
| Bo nho dem | Redis (StackExchange.Redis) |
| Quan ly package | NuGet (tu dong qua `dotnet restore`) |
| Build tool | dotnet CLI / MSBuild |
| Container hoa | Docker + Docker Compose (v3.8) |
| Xac thuc | JWT Bearer, Google OAuth, Facebook OAuth, BCrypt ma hoa mat khau |
| Tai lieu API | Swagger / Swashbuckle |
| Anh xa doi tuong | AutoMapper 16 |
| Email | MailKit + SendGrid |
| Thanh toan | MoMo Gateway (moi truong test) |
| AI / Chatbot | OpenRouter API (Gemini model), Google GenAI |

---

## Cau truc thu muc du an

```
PRN2322/
|-- Domain/                          # Tang mien (core)
|   |-- Entities/                    # Cac thuc the domain
|   |-- Enums/                       # Cac enum (UserRole, OrderStatus, InventoryStatus)
|   |-- Constants/                   # Hang so (RoleIds)
|   +-- IUnitOfWork/                 # Interface UnitOfWork
|
|-- Application/                     # Tang ung dung (logic nghiep vu)
|   |-- DTOs/
|   |   |-- Request/                 # DTO dau vao
|   |   +-- Response/                # DTO dau ra
|   |-- Service/                     # Interface va implementation cua cac service
|   |-- Mappings/                    # AutoMapper profile
|   +-- Application.csproj
|
|-- Infrastructure/                  # Tang ha tang
|   |-- Data/                        # DbContext (AppDbContext)
|   |-- Configurations/              # Fluent API va Seeder
|   |-- Migrations/                  # EF Core migration
|   |-- Repositories/                # Generic Repository pattern
|   |-- Services/                    # Implementation dich vu ben ngoai
|   |-- UnitOfWork/                  # Implementation UnitOfWork
|   +-- Infrastructure.csproj
|
+-- PRN2322/                         # Tang API
    |-- Controllers/                 # Cac REST API endpoint
    |-- Properties/                  # launchSettings.json
    +-- Program.cs                   # Cau hinh khoi dong ung dung
```

---

## Co so du lieu - Schema

### Thuc the co so (BaseEntity)

Tat ca thuc the deu ke thua tu `BaseEntity` voi cac truong: `Id` (Guid), `CreatedAt`, `UpdatedAt`, `IsDeleted`.

### Danh sach thuc the (18 bang)

#### Danh tinh va nguoi dung

| Thuc the | Mo ta | Cac truong chinh |
|----------|-------|-----------------|
| **Role** | Vai tro nguoi dung | RoleName (Admin, Staff, Customer, Guest), Description |
| **User** | Tai khoan nguoi dung | Username, PasswordHash, FullName, Email, Phone, Address, IsActive, RoleId, TaxCode, CompanyName |

#### San pham va danh muc

| Thuc the | Mo ta | Cac truong chinh |
|----------|-------|-----------------|
| **Category** | Danh muc san pham (ho tro phan cap cha-con) | Name, ParentId |
| **Product** | San pham trong catalog | SKU, Name, Description, Price, IsActive, CategoryId |
| **Image** | Hinh anh san pham/hop qua | Url, IsMain, SortOrder, ProductId, GiftBoxId |
| **GiftBox** | Hop qua tang tuy chinh | Code, Name, Description, BasePrice, IsActive, IsCustom, IsDraft, CategoryId, GiftBoxComponentConfigId, UserId |
| **GiftBoxComponentConfig** | Cau hinh mau cho hop qua | Name, Description, Price, Category, IsActive |
| **BoxComponent** | Thanh phan trong hop qua (quan he N-N) | GiftBoxId, ProductId, Quantity |

#### Don hang va ton kho

| Thuc the | Mo ta | Cac truong chinh |
|----------|-------|-----------------|
| **Cart** | Gio hang | UserId |
| **CartItem** | San pham trong gio hang | CartId, ProductId, GiftBoxId, Quantity, Price |
| **Order** | Don hang | OrderNumber, UserId, CartId, VoucherId, TotalAmount, DiscountAmount, ShippingFee, ShippingAddress, ShippingPhone, FinalAmount, CurrentStatus, Note |
| **OrderDetail** | Chi tiet dong don hang | OrderId, ProductId, GiftBoxId, Quantity, UnitPrice |
| **OrderHistory** | Lich su trang thai don hang | OrderId, Status, Note, ChangedBy |
| **Inventory** | Theo doi ton kho | ProductId, Quantity, MinStockLevel, Status, LastUpdated |
| **InventoryTransaction** | Nhat ky xuat nhap kho | InventoryId, QuantityChange, TransactionType, ReferenceId, Note, CreatedBy |

#### Thanh toan va khuyen mai

| Thuc the | Mo ta | Cac truong chinh |
|----------|-------|-----------------|
| **Payment** | Ban ghi thanh toan | OrderId, PaymentMethod, Amount, Status, TransactionReference |
| **PaymentHistory** | Nhat ky giao dich thanh toan | PaymentId, Status, RawResponse, Note |
| **Voucher** | Ma giam gia / khuyen mai | Code, Description, DiscountType, Value, MinOrderValue, MaxDiscountAmount, StartDate, EndDate, UsageLimit, IsActive |

### Enum

| Enum | Gia tri |
|------|---------|
| **UserRole** | Admin, Staff, Customer, Guest |
| **OrderStatus** | Pending, Confirmed, Processing, Shipping, Delivered, Cancelled, Returned |
| **InventoryStatus** | InStock, LowStock, OutOfStock, Inactive |

---

## Cac tinh nang chinh

### 1. Xac thuc va phan quyen

- Dang ky tai khoan bang email
- Dang nhap bang email/mat khau
- Dang nhap bang Google OAuth
- Dang nhap bang Facebook OAuth
- Xac thuc JWT (JSON Web Token) khong trang thai (stateless)
- Quan ly refresh token qua Redis
- Quen mat khau voi xac minh OTP qua email
- Doi mat khau
- Phan quyen theo vai tro (RBAC): Admin, Staff, Customer, Guest

### 2. Quan ly san pham

- Phan cap danh muc (quan he cha-con)
- Catalog san pham voi ma SKU
- Quan ly hinh anh (nhieu hinh cho moi san pham)
- Theo doi ton kho san pham

### 3. Cau hinh hop qua tang

- Mau hop qua tang duoc cau hinh san (GiftBoxComponentConfig)
- Tao hop qua tang tuy chinh
- Quan ly thanh phan trong hop voi so luong
- Quan he N-N giua San pham va Hop qua

### 4. Gio hang

- Them san pham hoac hop qua vao gio hang
- Cap nhat so luong
- Xoa san pham khoi gio hang
- Dem so luong trong gio
- Thanh toan (checkout) voi dia chi giao hang va voucher

### 5. Quan ly don hang

- Tao va theo doi don hang
- Quan ly trang thai don hang (Pending, Confirmed, Processing, Shipping, Delivered, Cancelled, Returned)
- Lich su don hang voi moc thoi gian
- Chi tiet dong don hang cho San pham va Hop qua
- Xoa don hang

### 6. He thong ton kho

- Theo doi ton kho theo thoi gian thuc
- Giao dich xuat nhap kho (Import, Sale, Return, Damage, Transfer)
- Canh bao muc ton kho thap
- Quan ly trang thai ton kho (InStock, LowStock, OutOfStock, Inactive)

### 7. Xu ly thanh toan

- Nhieu phuong thuc thanh toan: COD (thanh toan khi nhan hang), MoMo
- Theo doi trang thai thanh toan (Pending, Completed, Failed, Refunded)
- Nhat ky lich su thanh toan
- Theo doi ma giao dich (Transaction Reference)
- Ho tro thanh toan MoMo tren web va mobile (deeplink, QR code)
- Xu ly IPN (Instant Payment Notification) tu MoMo

### 8. Khuyen mai va giam gia

- Quan ly voucher/ma giam gia
- Ho tro giam gia theo phan tram va so tien co dinh
- Yeu cau gia tri don hang toi thieu
- Gioi han giam gia toi da
- Gioi han so luot su dung va kich hoat theo thoi gian

### 9. Chatbot AI

- Tu van san pham qua chatbot (su dung OpenRouter API voi model Gemini)
- Tim kiem va goi y san pham thong minh
- Tao hinh anh hop qua tuy chinh bang AI (Google GenAI)
- Tao chi tiet doc quyen cho hop qua tuy chinh

### 10. Bao cao va Thong ke (Dashboard)

- Tong quan doanh thu, don hang, san pham ban chay, khach hang tot nhat
- Xu huong ban hang theo thoi gian
- Bieu do trang thai don hang
- Don hang gan day
- San pham ban chay nhat
- Bao cao doanh thu theo khoang thoi gian
- Chi tiet doanh thu theo ngay
- Xuat bao cao doanh thu ra file CSV

---

## Danh sach API Endpoint

### Xac thuc (AuthController - `api/auth`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/auth/register` | Dang ky tai khoan moi | Khong |
| POST | `/api/auth/login` | Dang nhap bang email/mat khau | Khong |
| POST | `/api/auth/google-login` | Dang nhap bang Google OAuth | Khong |
| POST | `/api/auth/refresh-token` | Lam moi JWT token | Khong |
| POST | `/api/auth/forgot-password` | Yeu cau dat lai mat khau | Khong |
| POST | `/api/auth/reset-password` | Dat lai mat khau voi OTP | Khong |
| GET | `/api/auth/profile` | Lay thong tin ca nhan | Co |

### Doi mat khau (ChangePasswordController - `api/changepassword`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/changepassword` | Doi mat khau | Co |

### San pham (ProductController - `api/product`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/product` | Lay danh sach san pham | Khong |
| GET | `/api/product/{id}` | Lay chi tiet san pham | Khong |
| POST | `/api/product` | Tao san pham moi | Khong (*) |
| PUT | `/api/product?id={id}` | Cap nhat san pham | Khong (*) |
| DELETE | `/api/product/{id}` | Xoa san pham | Khong (*) |

### Danh muc (CategoryController - `api/category`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/category/GetAllCategories` | Lay tat ca danh muc | Khong |
| GET | `/api/category/GetCategoryById/{id}` | Lay chi tiet danh muc | Khong |
| POST | `/api/category/CreateCategory` | Tao danh muc moi | Khong (*) |
| PUT | `/api/category/UpdateCategory/{id}` | Cap nhat danh muc | Khong (*) |
| DELETE | `/api/category/DeleteCategory/{id}` | Xoa danh muc | Khong (*) |

### Hop qua tang (GiftBoxController - `api/giftbox`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/giftbox` | Lay tat ca hop qua | Khong |
| GET | `/api/giftbox/active` | Lay cac hop qua dang hoat dong | Khong |
| GET | `/api/giftbox/user` | Lay hop qua cua nguoi dung hien tai | Co |
| GET | `/api/giftbox/{id}` | Lay chi tiet hop qua | Khong |
| GET | `/api/giftbox/code/{code}` | Lay hop qua theo ma code | Khong |
| GET | `/api/giftbox/category/{categoryId}` | Lay hop qua theo danh muc | Khong |
| POST | `/api/giftbox` | Tao hop qua moi | Khong (*) |
| PUT | `/api/giftbox/{id}` | Cap nhat hop qua | Khong (*) |
| DELETE | `/api/giftbox/{id}` | Xoa hop qua | Khong (*) |

### Cau hinh thanh phan hop qua (GiftBoxComponentConfigController - `api/giftboxcomponentconfig`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/giftboxcomponentconfig` | Lay tat ca cau hinh | Khong |
| GET | `/api/giftboxcomponentconfig/active` | Lay cau hinh dang hoat dong | Khong |
| GET | `/api/giftboxcomponentconfig/{id}` | Lay chi tiet cau hinh | Khong |
| GET | `/api/giftboxcomponentconfig/category/{category}` | Lay cau hinh theo danh muc | Khong |
| POST | `/api/giftboxcomponentconfig` | Tao cau hinh moi | Khong (*) |
| PUT | `/api/giftboxcomponentconfig/{id}` | Cap nhat cau hinh | Khong (*) |
| DELETE | `/api/giftboxcomponentconfig/{id}` | Xoa cau hinh | Khong (*) |

### Hinh anh (ImageController - `api/images`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/images` | Lay tat ca hinh anh | Khong |
| GET | `/api/images/products/{productId}` | Lay hinh anh theo san pham | Khong |
| POST | `/api/images` | Them hinh anh moi | Khong |
| PUT | `/api/images/{id}` | Cap nhat hinh anh | Khong |
| DELETE | `/api/images/{id}` | Xoa hinh anh | Khong |

### Gio hang (CartController - `api/cart`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/cart` | Lay gio hang hien tai | Co |
| GET | `/api/cart/count` | Dem so luong trong gio | Co |
| POST | `/api/cart/items` | Them san pham vao gio | Co |
| PUT | `/api/cart/items/{cartItemId}` | Cap nhat so luong san pham | Co |
| DELETE | `/api/cart/items/{cartItemId}` | Xoa mot san pham khoi gio | Co |
| DELETE | `/api/cart/items` | Xoa nhieu san pham khoi gio (body: danh sach ID) | Co |
| DELETE | `/api/cart` | Xoa toan bo gio hang | Co |
| POST | `/api/cart/checkout` | Thanh toan gio hang | Co |

### Don hang (OrderController - `api/orders`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/orders` | Lay tat ca don hang | Khong |
| GET | `/api/orders/{id}` | Lay chi tiet don hang | Khong |
| POST | `/api/orders` | Tao don hang moi | Khong |
| PATCH | `/api/orders/{id}/status` | Cap nhat trang thai don hang | Khong |
| GET | `/api/orders/user/{userId}` | Lay don hang theo nguoi dung | Khong |
| DELETE | `/api/orders/{id}` | Xoa don hang | Khong |

### Thanh toan MoMo (PaymentController - `api/payment`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/payment/momo/create-order` | Tao don thanh toan MoMo | Co |
| POST | `/api/payment/momo/create` | Tao thanh toan MoMo (web) | Co |
| POST | `/api/payment/momo/create-mobile` | Tao thanh toan MoMo (mobile) | Co |
| GET | `/api/payment/momo/orders/{orderId}/status` | Kiem tra trang thai thanh toan | Co |
| POST | `/api/payment/momo/ipn` | Xu ly IPN tu MoMo | Khong (callback) |

### Ton kho (InventoriesController - `api/inventories`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/inventories` | Lay tat ca ton kho | Khong |
| GET | `/api/inventories/{id}` | Lay chi tiet ton kho | Khong |
| GET | `/api/inventories/product/{productId}` | Lay ton kho theo san pham | Khong |
| GET | `/api/inventories/status/{status}` | Lay ton kho theo trang thai | Khong |
| POST | `/api/inventories` | Tao ban ghi ton kho | Khong |
| PUT | `/api/inventories/{id}` | Cap nhat ton kho | Khong |
| DELETE | `/api/inventories/{id}` | Xoa ban ghi ton kho | Khong |
| PATCH | `/api/inventories/{id}/quantity` | Cap nhat so luong ton kho | Khong |

### Giao dich ton kho (InventoryTransactionsController - `api/inventorytransactions`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/inventorytransactions` | Lay tat ca giao dich | Khong |
| GET | `/api/inventorytransactions/{id}` | Lay chi tiet giao dich | Khong |
| GET | `/api/inventorytransactions/byInventory/{inventoryId}` | Lay giao dich theo ton kho | Khong |
| GET | `/api/inventorytransactions/byType/{transactionType}` | Lay giao dich theo loai | Khong |
| GET | `/api/inventorytransactions/byReference/{referenceId}` | Lay giao dich theo ma tham chieu | Khong |
| POST | `/api/inventorytransactions` | Tao giao dich moi | Khong |
| PUT | `/api/inventorytransactions/{id}` | Cap nhat giao dich | Khong |
| DELETE | `/api/inventorytransactions/{id}` | Xoa giao dich | Khong |

### Voucher / Ma giam gia (VoucherController - `api/vouchers`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/vouchers` | Lay tat ca voucher | Khong |
| GET | `/api/vouchers/{id}` | Lay chi tiet voucher | Khong |
| POST | `/api/vouchers` | Tao voucher moi | Khong |
| PUT | `/api/vouchers/{id}` | Cap nhat voucher | Khong |
| DELETE | `/api/vouchers/{id}` | Xoa voucher | Khong |

### Nguoi dung (UserController - `api/user`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/user` | Tao nguoi dung moi | Khong |
| GET | `/api/user` | Lay tat ca nguoi dung | Khong |
| GET | `/api/user/{id}` | Lay chi tiet nguoi dung | Khong |
| PUT | `/api/user/{id}` | Cap nhat nguoi dung | Khong |
| DELETE | `/api/user/{id}` | Xoa nguoi dung | Khong |

### Chatbot AI (ChatbotController - `api/chatbot`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/chatbot/chat` | Gui tin nhan cho chatbot AI | Khong |

### Hop qua tuy chinh (CustomBasketController - `api/custom-baskets`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| POST | `/api/custom-baskets/generate-image` | Tao hinh anh hop qua bang AI | Co |
| POST | `/api/custom-baskets/confirm` | Xac nhan hop qua tuy chinh | Co |
| POST | `/api/custom-baskets/generate-exclusive-details` | Tao chi tiet doc quyen | Co |

### Thong ke (DashboardController - `api/dashboards`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/dashboards/summary` | Tong quan thong ke (doanh thu, don hang, san pham/khach hang top) | Khong |
| GET | `/api/dashboards/sales-trend` | Xu huong ban hang | Khong |
| GET | `/api/dashboards/order-status` | Bieu do trang thai don hang | Khong |
| GET | `/api/dashboards/recent-orders` | Cac don hang gan day | Khong |
| GET | `/api/dashboards/best-sellers` | San pham ban chay nhat | Khong |

### Bao cao (ReportController - `api/reports`)

| Phuong thuc | Duong dan | Mo ta | Xac thuc |
|-------------|-----------|-------|----------|
| GET | `/api/reports/revenue` | Bao cao doanh thu theo khoang thoi gian | Khong |
| GET | `/api/reports/revenue/day-details` | Chi tiet doanh thu theo ngay | Khong |
| GET | `/api/reports/revenue/export` | Xuat bao cao doanh thu ra CSV | Khong |

(*) Ghi chu: Mot so endpoint ghi/xoa hien tai chua bat xac thuc (Authorize bi comment trong ma nguon). Trong moi truong san xuat can bat lai.

---

## Cac goi NuGet su dung

### PRN2322 (tang API)

| Goi | Phien ban | Muc dich |
|-----|-----------|----------|
| AutoMapper | 16.0.0 | Anh xa doi tuong tu dong |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.0 | Xac thuc JWT |
| Microsoft.EntityFrameworkCore.Design | 8.0.0 | Ho tro thiet ke EF Core (migration) |
| Swashbuckle.AspNetCore | 6.6.2 | Tai lieu API Swagger |

### Application (tang ung dung)

| Goi | Phien ban | Muc dich |
|-----|-----------|----------|
| AutoMapper | 16.0.0 | Anh xa doi tuong tu dong |
| BCrypt.Net-Next | 4.1.0 | Ma hoa mat khau |
| Microsoft.Extensions.Caching.Abstractions | 8.0.0 | Interface bo nho dem |

### Infrastructure (tang ha tang)

| Goi | Phien ban | Muc dich |
|-----|-----------|----------|
| AutoMapper | 16.0.0 | Anh xa doi tuong tu dong |
| Google.Apis.Auth | 1.73.0 | Xac thuc Google OAuth |
| Google.GenAI | 1.2.0 | Tao noi dung AI (hinh anh hop qua) |
| MailKit | 4.15.0 | Gui email (SMTP) |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.0 | Xac thuc JWT |
| Microsoft.EntityFrameworkCore | 8.0.0 | ORM Entity Framework Core |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.0 | Provider PostgreSQL cho EF Core |
| Microsoft.EntityFrameworkCore.Tools | 8.0.0 | Cong cu EF Core (migration CLI) |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.1 | Bo nho dem Redis |
| SendGrid | 9.29.3 | Gui email qua SendGrid |
| System.IdentityModel.Tokens.Jwt | 8.15.0 | Xu ly JWT token |

### Domain (tang mien)

Khong co goi NuGet truc tiep.

---

## Cau hinh Dependency Injection

### Web Services (`PRN2322/DependencyInjection.cs`)

- **CORS** - Chinh sach "AllowFrontend":
  - Origin duoc phep: `http://localhost:5173`, `http://localhost:3000`, `https://tet-den-roi.vercel.app`
  - Cho phep moi Header va Method
  - Cho phep Credentials
- **Swagger** voi ho tro JWT Bearer security scheme

### Infrastructure Services (`Infrastructure/DependencyInjection.cs`)

- **DbContext**: Dang ky PostgreSQL qua Npgsql
- **HttpClient**: Cho goi API ben ngoai
- **MoMo**: `MomoApiOptions`, `IMomoGatewayClient`, `IMomoPaymentService`
- **Redis**: Distributed cache voi prefix `HappyBox_`
- **Repository**: `IGenericRepository<>` -> `GenericRepository<>`, `IUnitOfWork` -> `UnitOfWork`
- **Cac Service nghiep vu** (Scoped):
  - ITokenService, IAuthService, IMailService
  - ICategoryService, IProductService, IGiftBoxService
  - IGiftBoxComponentConfigService, ICartService, IImageService
  - IOrderService, IVoucherService, IUserService
  - IInventoryService, IInventoryTransactionService
  - IDashboardService, IReportService
  - IChatbotService, ICustomBasketImageService
  - IChangePasswordService, IMomoPaymentService
  - IPasswordHasher
- **Hosted Service**: `TempFileCleanupService` - doc dep file tam trong `wwwroot/images/custom-baskets/temp`
- **AutoMapper**: Tu `MappingProfile` (Application) va `InfrastructureProfile` (Infrastructure)
- **Authentication**: JWT Bearer voi validation tu cau hinh `Jwt` section

---

## Bao mat

- Xac thuc JWT Bearer khong trang thai (stateless)
- Ma hoa mat khau bang BCrypt
- Dat lai mat khau qua OTP
- Phan quyen theo vai tro (Role-based Authorization)
- HTTPS enforcement
- Cau hinh CORS
- Tich hop OAuth 2.0 (Google, Facebook)
- Luan chuyen Refresh Token voi Redis
- Swagger co ho tro nhap JWT token de test API

---

## Yeu cau he thong

### Bat buoc

- .NET 8 SDK
- PostgreSQL (phien ban 12 tro len)
- Redis

### Tuy chon (cho cac tinh nang nang cao)

- Tai khoan SMTP Gmail (gui email OTP, dat lai mat khau)
- OpenRouter API Key (chatbot AI tu van san pham)
- Google OAuth Client ID (dang nhap Google)
- Facebook App ID/Secret (dang nhap Facebook)
- MoMo Payment API credentials (thanh toan MoMo)
- Google GenAI API Key (tao hinh anh hop qua AI)
- SendGrid API Key (gui email qua SendGrid)

---

## Huong dan cai dat va chay

### 1. Clone repository

```bash
git clone https://github.com/loclhse/PRN232.git
cd PRN2322
```

### 2. Cai dat .NET 8 SDK

Tai va cai dat .NET 8 SDK tu: https://dotnet.microsoft.com/download/dotnet/8.0

Kiem tra phien ban:
```bash
dotnet --version
```

### 3. Cai dat va khoi dong PostgreSQL

Cai dat PostgreSQL va tao co so du lieu:
```bash
sudo -u postgres psql -c "ALTER USER postgres PASSWORD '1234567890';"
sudo -u postgres psql -c "CREATE DATABASE \"HappyBoxDb\";"
```

### 4. Cai dat va khoi dong Redis

```bash
sudo service redis-server start
```

Kiem tra Redis:
```bash
redis-cli ping
# Ket qua mong doi: PONG
```

### 5. Cau hinh ket noi co so du lieu

Chinh sua file `PRN2322/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=HappyBoxDb;Username=postgres;Password=1234567890",
    "Redis": "localhost:6379"
  }
}
```

### 6. Cau hinh dich vu ben ngoai (tuy chon)

Chinh sua `PRN2322/appsettings.json` de cau hinh cac dich vu:

```json
{
  "Jwt": {
    "Key": "chuoi-bi-mat-it-nhat-32-ky-tu",
    "Issuer": "PRN2322",
    "Audience": "PRN2322",
    "ExpiryInMinutes": 10
  },
  "Google": {
    "ClientId": "google-client-id-cua-ban"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "email-cua-ban@gmail.com",
    "SmtpPassword": "mat-khau-ung-dung",
    "FromEmail": "email-cua-ban@gmail.com",
    "FromName": "HappyBox",
    "SendGridKey": "sendgrid-api-key"
  },
  "OpenRouter": {
    "ApiKey": "openrouter-api-key",
    "Referer": "http://localhost:3000"
  },
  "MomoAPI": {
    "ApiUrl": "https://test-payment.momo.vn/v2/gateway/api/create",
    "PartnerCode": "ma-doi-tac",
    "AccessKey": "access-key",
    "SecretKey": "secret-key",
    "RedirectUrl": "url-chuyen-huong",
    "MobileRedirectUrl": "url-chuyen-huong-mobile",
    "IpnUrl": "url-ipn"
  },
  "GoogleAI": {
    "ApiKey": "google-genai-api-key",
    "ProjectId": "project-id",
    "Location": "asia-southeast1"
  }
}
```

### 7. Khoi phuc cac goi NuGet

```bash
dotnet restore
```

### 8. Chay ung dung

```bash
dotnet run --project PRN2322
```

Ung dung se tu dong chay migration khi khoi dong. API se co san tai: `http://localhost:5142`

Giao dien Swagger UI: `http://localhost:5142/swagger`

### Chay bang Docker Compose

```bash
docker-compose up
```

Luu y: Can co PostgreSQL chay rieng biet vi `docker-compose.yml` khong bao gom dich vu PostgreSQL.

---

## Quan ly Migration co so du lieu

### Tao migration moi

```bash
dotnet ef migrations add <TenMigration> -p Infrastructure -s PRN2322
```

### Ap dung migration

```bash
dotnet ef database update -p Infrastructure -s PRN2322
```

### Quay lai migration truoc do

```bash
dotnet ef database update <TenMigrationTruoc> -p Infrastructure -s PRN2322
```

### Xoa co so du lieu

```bash
dotnet ef database drop --force -p Infrastructure -s PRN2322
```

---

## Chay kiem thu

```bash
dotnet test
```

Ghi chu: Hien tai du an chua co project test rieng biet. Lenh tren se khong tim thay test nao de chay.

---

## Cau hinh CORS

Chinh sach CORS "AllowFrontend" cho phep cac origin sau:

| Origin | Moi truong |
|--------|-----------|
| `http://localhost:5173` | Frontend dev (Vite) |
| `http://localhost:3000` | Frontend dev (React/Next.js) |
| `https://tet-den-roi.vercel.app` | Frontend production (Vercel) |

Cau hinh:
- Cho phep moi Header
- Cho phep moi Method (GET, POST, PUT, DELETE, PATCH, v.v.)
- Cho phep gui Credentials (cookie, authorization header)

---

## Pipeline xu ly request (Program.cs)

Thu tu middleware:

1. `UseSwagger` / `UseSwaggerUI` - Tai lieu API (bat o moi moi truong)
2. `UseHttpsRedirection` - Chuyen huong HTTPS
3. `UseStaticFiles` - Phuc vu file tinh (wwwroot/)
4. `UseCors("AllowFrontend")` - CORS (dat truoc Authentication)
5. `UseAuthentication` - Xac thuc JWT
6. `UseAuthorization` - Phan quyen
7. `MapControllers` - Dinh tuyen den controller

Khi khoi dong, ung dung tu dong kiem tra va chay cac migration chua ap dung (pending migrations).

---

## Quy tac phat trien

### Phong cach code

- Tuan thu quy tac dat ten C# (PascalCase cho thanh vien public)
- Su dung async/await cho cac thao tac I/O
- Giu phuong thuc ngan gon va tap trung
- Dat ten bien co y nghia

### Quy trinh Git

1. Tao nhanh tinh nang: `git checkout -b feature/ten-tinh-nang`
2. Commit thay doi: `git commit -am 'Mo ta thay doi'`
3. Day len nhanh: `git push origin feature/ten-tinh-nang`
4. Tao Pull Request

### Thay doi co so du lieu

- Luon tao migration cho cac thay doi schema
- Dat ten migration co y nghia
- Cap nhat seeder neu can thiet

---

## Giay phep

Du an nay duoc cap phep theo Giay phep MIT.

## Tac gia

**Loc** - Phat trien ban dau

## Ho tro

De duoc ho tro, mo issue tren GitHub hoac lien he voi doi phat trien.
