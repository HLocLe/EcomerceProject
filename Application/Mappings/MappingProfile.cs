using Application.DTOs.Request;
using Application.DTOs.Request.Category;
using Application.DTOs.Request.GiftBox;
using Application.DTOs.Request.GiftBoxComponentConfig;
using Application.DTOs.Request.Image;
using Application.DTOs.Request.Inventory;
using Application.DTOs.Request.InventoryTransaction;
using Application.DTOs.Request.Order;
using Application.DTOs.Request.Product;
using Application.DTOs.Request.Register;
using Application.DTOs.Request.Voucher;
using Application.DTOs.Request.User;
using Application.DTOs.Response;
using Application.DTOs.Response.Auth;
using Application.DTOs.Response.Cart;
using Application.DTOs.Response.GiftBox;
using Application.DTOs.Response.GiftBoxComponentConfig;
using Application.DTOs.Response.Image;
using Application.DTOs.Response.Inventory;
using Application.DTOs.Response.InventoryTransaction;
using Application.DTOs.Response.Order;
using Application.DTOs.Response.Product;
using Application.DTOs.Response.Voucher;
using AutoMapper;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;

namespace Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User Mapping (DTO <-> Entity)
            CreateMap<RegisterRequest, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => RoleIds.Customer))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
            
            CreateMap<User, UserResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName.ToString()));

            // Product Mapping (DTO <-> Entity)
            CreateMap<CreateProductRequest, Product>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.BoxComponents, opt => opt.Ignore())
                .ForMember(dest => dest.Inventories, opt => opt.Ignore())
                .ForMember(dest => dest.OrderDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<UpdateProductRequest, Product>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.BoxComponents, opt => opt.Ignore())
                .ForMember(dest => dest.Inventories, opt => opt.Ignore())
                .ForMember(dest => dest.OrderDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<Product, ProductResponse>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images == null ? null : src.Images.OrderBy(i => i.SortOrder)))
                .ForMember(dest => dest.Inventories, opt => opt.MapFrom(src => src.Inventories == null ? null : src.Inventories));

            // Category Mapping (DTO <-> Entity)
            CreateMap<CreateCategoryRequest, Category>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
                .ForMember(dest => dest.SubCategories, opt => opt.Ignore())
                .ForMember(dest => dest.Products, opt => opt.Ignore())
                .ForMember(dest => dest.GiftBoxes, opt => opt.Ignore());

            CreateMap<UpdateCategoryRequest, Category>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.ParentCategory, opt => opt.Ignore())
                .ForMember(dest => dest.SubCategories, opt => opt.Ignore())
                .ForMember(dest => dest.Products, opt => opt.Ignore())
                .ForMember(dest => dest.GiftBoxes, opt => opt.Ignore());

            CreateMap<Category, CategoryResponse>()
                .ForMember(dest => dest.ParentName, opt => opt.MapFrom(src => src.ParentCategory != null ? src.ParentCategory.Name : null));

            // Image Mapping (DTO <-> Entity)
            CreateMap<CreateImageRequest, Image>();
            CreateMap<Image, ImageResponse>();
            // Mapping cho Update Image
            CreateMap<UpdateImageRequest, Domain.Entities.Image>();

            // Inventory Mapping (DTO <-> Entity)
            CreateMap<CreateInventoryRequest, Domain.Entities.Inventory>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.LastUpdated, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.Transactions, opt => opt.Ignore());

            CreateMap<UpdateInventoryRequest, Domain.Entities.Inventory>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.LastUpdated, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.Transactions, opt => opt.Ignore());

            CreateMap<Domain.Entities.Inventory, InventoryResponse>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null));

            // =====================================
            // InventoryTransaction Mapping
            // =====================================
            CreateMap<CreateInventoryTransactionRequest, Domain.Entities.InventoryTransaction>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Inventory, opt => opt.Ignore());

            CreateMap<UpdateInventoryTransactionRequest, Domain.Entities.InventoryTransaction>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.InventoryId, opt => opt.Ignore())
                .ForMember(dest => dest.Inventory, opt => opt.Ignore());

            CreateMap<Domain.Entities.InventoryTransaction, InventoryTransactionResponse>()
                .ForMember(dest => dest.InventoryProductName, opt => opt.MapFrom(src => src.Inventory != null && src.Inventory.Product != null ? src.Inventory.Product.Name : null));

            // =====================================
            // Order Mapping
            // =====================================
            CreateMap<CreateOrderDetailRequest, OrderDetail>();

            CreateMap<OrderDetail, OrderDetailResponse>()
                // Backend tự tính TotalPrice cho từng món
                .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.Quantity * src.UnitPrice));

            CreateMap<OrderHistory, OrderHistoryResponse>();

            CreateMap<Order, OrderResponse>()
                .ForMember(
                    dest => dest.PaymentMethod,
                    opt => opt.MapFrom(src =>
                        src.Payments == null
                            ? string.Empty
                            : src.Payments
                                .Where(p => !p.IsDeleted)
                                .OrderByDescending(p => p.CreatedAt)
                                .Select(p => p.PaymentMethod)
                                .FirstOrDefault() ?? string.Empty
                    )
                )
                .ForMember(dest => dest.OrderDetails, opt => opt.MapFrom(src => src.OrderDetails))
                .ForMember(dest => dest.OrderHistories, opt => opt.MapFrom(src => src.OrderHistories));

            CreateMap<CreateOrderRequest, Order>()
                .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src => "ORD-" + DateTime.Now.Ticks.ToString().Substring(10)))
                .ForMember(dest => dest.CurrentStatus, opt => opt.MapFrom(src => OrderStatus.Pending))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // =====================================
            // Voucher Mapping
            // =====================================
            CreateMap<CreateVoucherRequest, Domain.Entities.Voucher>()
                 .ForMember(dest => dest.Id, opt => opt.Ignore())
                 .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                 .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                 // Đổi bool IsPercentage thành chuỗi lưu DB
                 .ForMember(dest => dest.DiscountType, opt => opt.MapFrom(src => src.IsPercentage ? "PERCENT" : "AMOUNT"));

            CreateMap<UpdateVoucherRequest, Domain.Entities.Voucher>()
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.DiscountType, opt => opt.MapFrom(src => src.IsPercentage ? "PERCENT" : "AMOUNT"));

            CreateMap<Domain.Entities.Voucher, VoucherResponse>()
                // Đổi chuỗi DB ngược lại thành bool cho FE
                .ForMember(dest => dest.IsPercentage, opt => opt.MapFrom(src => src.DiscountType == "PERCENT"));

            // =====================================
            // GiftBox Mapping
            // =====================================
            CreateMap<CreateGiftBoxRequest, GiftBox>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.ComponentConfig, opt => opt.Ignore())
                .ForMember(dest => dest.BoxComponents, opt => opt.Ignore())
                .ForMember(dest => dest.OrderDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<UpdateGiftBoxRequest, GiftBox>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Category, opt => opt.Ignore())
                .ForMember(dest => dest.ComponentConfig, opt => opt.Ignore())
                .ForMember(dest => dest.BoxComponents, opt => opt.Ignore())
                .ForMember(dest => dest.OrderDetails, opt => opt.Ignore())
                .ForMember(dest => dest.Images, opt => opt.Ignore());

            CreateMap<GiftBox, GiftBoxResponse>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
                .ForMember(dest => dest.ComponentConfigName, opt => opt.MapFrom(src => src.ComponentConfig != null ? src.ComponentConfig.Name : null))
                .ForMember(dest => dest.IsCustom, opt => opt.MapFrom(src => src.IsCustom))
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.UserId))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src =>
                    src.Images == null
                        ? null
                        : src.Images
                            .Where(i => !i.IsDeleted)
                            .GroupBy(i => i.Id)
                            .Select(g => g.First())
                            .OrderBy(i => i.SortOrder)
                ))
                .ForMember(dest => dest.BoxComponents, opt => opt.MapFrom(src =>
                    src.BoxComponents == null
                        ? null
                        : src.BoxComponents
                            .Where(bc => !bc.IsDeleted)
                            .GroupBy(bc => bc.Id)
                            .Select(g => g.First())
                ));

            CreateMap<BoxComponent, BoxComponentResponse>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ProductSKU, opt => opt.MapFrom(src => src.Product != null ? src.Product.SKU : null))
                .ForMember(dest => dest.ProductPrice, opt => opt.MapFrom(src => src.Product != null ? src.Product.Price : 0));

            // =====================================
            // GiftBoxComponentConfig Mapping
            // =====================================
            CreateMap<CreateGiftBoxComponentConfigRequest, GiftBoxComponentConfig>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.GiftBox, opt => opt.Ignore());

            CreateMap<UpdateGiftBoxComponentConfigRequest, GiftBoxComponentConfig>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.GiftBox, opt => opt.Ignore());

            CreateMap<GiftBoxComponentConfig, GiftBoxComponentConfigResponse>()
                .ForMember(dest => dest.GiftBoxId, opt => opt.MapFrom(src => src.GiftBox != null ? src.GiftBox.Id : (Guid?)null))
                .ForMember(dest => dest.GiftBoxName, opt => opt.MapFrom(src => src.GiftBox != null ? src.GiftBox.Name : null))
                .ForMember(dest => dest.GiftBoxCode, opt => opt.MapFrom(src => src.GiftBox != null ? src.GiftBox.Code : null));

            // =====================================
            // Cart Mapping
            // =====================================
            CreateMap<Cart, CartResponse>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : string.Empty))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items.Where(i => !i.IsDeleted)));

            CreateMap<CartItem, CartItemResponse>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ProductSKU, opt => opt.MapFrom(src => src.Product != null ? src.Product.SKU : null))
                .ForMember(dest => dest.ProductImageUrl, opt => opt.MapFrom(src => 
                    src.Product != null && src.Product.Images != null 
                        ? src.Product.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() 
                        : null))
                .ForMember(dest => dest.GiftBoxName, opt => opt.MapFrom(src => src.GiftBox != null ? src.GiftBox.Name : null))
                .ForMember(dest => dest.GiftBoxCode, opt => opt.MapFrom(src => src.GiftBox != null ? src.GiftBox.Code : null))
                .ForMember(dest => dest.GiftBoxImageUrl, opt => opt.MapFrom(src => 
                    src.GiftBox != null && src.GiftBox.Images != null 
                        ? src.GiftBox.Images.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() 
                        : null))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.Price));
        }
    }
}
