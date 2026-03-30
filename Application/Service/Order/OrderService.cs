using Application.DTOs.Request.Order;
using Application.DTOs.Response.Order;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.IUnitOfWork;

namespace Application.Service.Order
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public OrderService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OrderResponse?> GetOrderByIdAsync(Guid id)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                filter: o => o.Id == id && !o.IsDeleted,
                includeProperties: "OrderDetails,OrderHistories,Payments" // Gọi ra các bảng con
            );
            return _mapper.Map<OrderResponse>(order);
        }

        public async Task<IEnumerable<OrderResponse>> GetAllOrdersAsync()
        {
            var orders = await _unitOfWork.OrderRepository.FindAsync(
                filter: o => !o.IsDeleted,
                includeProperties: "OrderDetails,OrderHistories,Payments" // Gọi ra các bảng con
            );
            return _mapper.Map<IEnumerable<OrderResponse>>(orders);
        }

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
        {
            // AutoMapper sẽ map các thông tin cơ bản từ Request sang Order
            var order = _mapper.Map<Domain.Entities.Order>(request);

            if (order.Id == Guid.Empty) order.Id = Guid.NewGuid();
            order.CreatedAt = DateTime.UtcNow;

            // =======================================================
            // KHỐI 1: TÍNH TOÁN TIỀN (BẢO MẬT: LẤY GIÁ TỪ DATABASE)
            // =======================================================

            // 1. Lấy danh sách ID của tất cả sản phẩm và hộp quà mà khách đặt
            var productIds = request.OrderDetails.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).ToList();
            var giftBoxIds = request.OrderDetails.Where(x => x.GiftBoxId.HasValue).Select(x => x.GiftBoxId!.Value).ToList();

            // 2. Gom thành các câu query để lấy thông tin từ DB (Tối ưu hiệu năng)
            var productsInDb = productIds.Any() 
                ? await _unitOfWork.ProductRepository.FindAsync(p => productIds.Contains(p.Id))
                : new List<Domain.Entities.Product>();
                
            var giftBoxesInDb = giftBoxIds.Any()
                ? await _unitOfWork.GiftBoxRepository.FindAsync(g => giftBoxIds.Contains(g.Id))
                : new List<Domain.Entities.GiftBox>();

            order.TotalAmount = 0; // Khởi tạo tổng tiền hàng

            // 3. Duyệt qua từng dòng chi tiết đơn hàng để gán giá chuẩn
            foreach (var detail in order.OrderDetails)
            {
                if (detail.ProductId.HasValue)
                {
                    var product = productsInDb.FirstOrDefault(p => p.Id == detail.ProductId);
                    if (product == null)
                        throw new Exception($"Sản phẩm với ID {detail.ProductId} không tồn tại hoặc đã bị xóa!");

                    detail.UnitPrice = product.Price;
                }
                else if (detail.GiftBoxId.HasValue)
                {
                    var giftBox = giftBoxesInDb.FirstOrDefault(g => g.Id == detail.GiftBoxId);
                    if (giftBox == null)
                        throw new Exception($"Hộp quà với ID {detail.GiftBoxId} không tồn tại hoặc đã bị xóa!");

                    detail.UnitPrice = giftBox.BasePrice;
                }
                else
                {
                    throw new Exception("Mỗi dòng chi tiết đơn hàng phải có ProductId hoặc GiftBoxId!");
                }

                // Cộng dồn vào tổng tiền hàng của đơn
                order.TotalAmount += (detail.Quantity * detail.UnitPrice);
            }

            order.DiscountAmount = 0; // Khởi tạo tiền giảm giá

            // =======================================================
            // KHỐI 2: TỰ ĐỘNG TÍNH PHÍ SHIP Ở BACKEND
            // =======================================================
            // Logic: Tổng tiền hàng >= 500k thì Freeship (0đ), ngược lại thu 30k
            decimal backendShippingFee = order.TotalAmount >= 500000 ? 0 : 30000;

            // =======================================================
            // KHỐI 3: XỬ LÝ VOUCHER (KIỂM TRA CHẶT CHẼ)
            // =======================================================
            if (request.VoucherId.HasValue)
            {
                var repoVoucher = _unitOfWork.Repository<Domain.Entities.Voucher>();
                var voucher = await repoVoucher.GetByIdAsync(request.VoucherId.Value);

                // Các lớp phòng thủ kiểm tra tính hợp lệ của Voucher
                if (voucher == null)
                    throw new Exception("Voucher không tồn tại!");
                if (!voucher.IsActive || voucher.EndDate < DateTime.UtcNow)
                    throw new Exception("Voucher đã hết hạn hoặc bị vô hiệu hóa!");
                if (order.TotalAmount < voucher.MinOrderValue)
                    throw new Exception($"Đơn hàng chưa đạt giá trị tối thiểu ({voucher.MinOrderValue}đ) để áp dụng voucher này!");
                if (voucher.UsageLimit <= 0)
                    throw new Exception("Voucher này đã hết lượt sử dụng!");

                // Tính toán số tiền được giảm
                if (voucher.DiscountType == "PERCENT")
                {
                    decimal calculatedDiscount = order.TotalAmount * (voucher.Value / 100);
                    // Dùng Math.Min để không bao giờ giảm vượt quá số tiền tối đa (MaxDiscountAmount)
                    order.DiscountAmount = voucher.MaxDiscountAmount.HasValue
                        ? Math.Min(calculatedDiscount, voucher.MaxDiscountAmount.Value)
                        : calculatedDiscount;
                }
                else
                {
                    // Giảm tiền mặt trực tiếp
                    order.DiscountAmount = voucher.Value;
                }

                // Trừ đi 1 lượt sử dụng của Voucher và đánh dấu cần Update
                voucher.UsageLimit -= 1;
                repoVoucher.Update(voucher);
            }

            // =======================================================
            // KHỐI 4: TỔNG KẾT TIỀN & RẼ NHÁNH TRẠNG THÁI
            // =======================================================

            // Tính tiền khách phải trả cuối cùng
            order.FinalAmount = order.TotalAmount - order.DiscountAmount + backendShippingFee;

            // [LUỒNG TỰ ĐỘNG]:
            // COD    -> tự động Confirmed và trừ kho ngay
            // Online -> Pending chờ thanh toán, chỉ kiểm tra tồn kho chứ chưa trừ
            order.CurrentStatus = request.PaymentMethod == "COD"
                ? OrderStatus.Confirmed
                : OrderStatus.Pending;

            // Chỉ COD mới được trừ kho ngay tại bước tạo đơn
            var shouldDeductInventoryNow = string.Equals(request.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase);

            // =======================================================
            // BÓC TÁCH GIỎ QUÀ VÀ KIỂM TRA / TRỪ TỒN KHO
            // =======================================================
            var requiredProducts = new Dictionary<Guid, int>();

            // 4.1 Bóc tách toàn bộ chi tiết đơn hàng ra thành Product gốc
            foreach (var detail in request.OrderDetails)
            {
                if (detail.ProductId.HasValue)
                {
                    var pId = detail.ProductId.Value;
                    if (requiredProducts.ContainsKey(pId))
                        requiredProducts[pId] += detail.Quantity;
                    else
                        requiredProducts[pId] = detail.Quantity;
                }
                else if (detail.GiftBoxId.HasValue)
                {
                    var components = await _unitOfWork.Repository<Domain.Entities.BoxComponent>()
                        .FindAsync(c => c.GiftBoxId == detail.GiftBoxId.Value);

                    foreach (var component in components)
                    {
                        var pId = component.ProductId;
                        var totalNeeded = detail.Quantity * component.Quantity;

                        if (requiredProducts.ContainsKey(pId))
                            requiredProducts[pId] += totalNeeded;
                        else
                            requiredProducts[pId] = totalNeeded;
                    }
                }
            }

            var inventoryRepo = _unitOfWork.Repository<Domain.Entities.Inventory>();
            var inventoryTransactionRepo = _unitOfWork.Repository<Domain.Entities.InventoryTransaction>();

            // 4.2 Với mọi đơn: luôn kiểm tra tồn kho trước
            // COD    -> kiểm tra xong thì trừ luôn
            // Online -> chỉ kiểm tra, chưa trừ
            foreach (var item in requiredProducts)
            {
                var pId = item.Key;
                var totalNeeded = item.Value;

                var inventory = await inventoryRepo.GetFirstOrDefaultAsync(i => i.ProductId == pId);

                if (inventory == null || inventory.Quantity < totalNeeded)
                {
                    var product = await _unitOfWork.ProductRepository.GetByIdAsync(pId);
                    var productName = product?.Name ?? pId.ToString();
                    throw new Exception($"Sản phẩm '{productName}' không đủ số lượng trong kho. Cần: {totalNeeded}, Hiện có: {inventory?.Quantity ?? 0}");
                }

                // Online: dừng ở mức validate tồn kho, chưa trừ
                if (!shouldDeductInventoryNow)
                {
                    continue;
                }

                // COD: trừ kho ngay
                inventory.Quantity -= totalNeeded;
                inventory.LastUpdated = DateTime.UtcNow;

                if (inventory.Quantity == 0)
                    inventory.Status = InventoryStatus.OutOfStock;
                else if (inventory.Quantity <= inventory.MinStockLevel)
                    inventory.Status = InventoryStatus.LowStock;
                else
                    inventory.Status = InventoryStatus.InStock;

                inventoryRepo.Update(inventory);

                // Ghi lịch sử biến động kho chỉ cho COD
                var transaction = new Domain.Entities.InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    InventoryId = inventory.Id,
                    QuantityChange = -totalNeeded,
                    TransactionType = "Sale",
                    ReferenceId = order.Id.ToString(),
                    Note = "Xuất kho ngay khi tạo đơn COD",
                    CreatedAt = DateTime.UtcNow
                };
                await inventoryTransactionRepo.AddAsync(transaction);
            }


            // Đưa Order vào danh sách chờ thêm mới
            await _unitOfWork.OrderRepository.AddAsync(order);

            // =======================================================
            // KHỐI 5: LƯU VẾT LỊCH SỬ VÀ TẠO GIAO DỊCH THANH TOÁN
            // =======================================================

            // Khởi tạo History lần đầu ứng với trạng thái tự động
            var history = new OrderHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = order.CurrentStatus,
                Note = request.PaymentMethod == "COD" ? "Hệ thống tự động xác nhận đơn COD" : "Khởi tạo đơn hàng chờ thanh toán online",
                ChangedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<OrderHistory>().AddAsync(history);

            // Tạo bản ghi lưu giao dịch tiền bạc (PAYMENT)
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PaymentMethod = request.PaymentMethod,
                Status = request.PaymentMethod == "COD" ? "Pending" : "WaitingForPayment",
                Amount = order.FinalAmount,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Payment>().AddAsync(payment);

            // =======================================================
            // KHỐI 6: COMMIT LƯU DATABASE & TRẢ VỀ RESPONSE
            // =======================================================

            // Lưu tất cả mọi thứ (Order, OrderDetails, History, Payment, Voucher update) 
            // trong 1 Transaction an toàn duy nhất
            await _unitOfWork.SaveChangesAsync();

            // Ánh xạ ra Response để trả về cho Frontend
            var response = _mapper.Map<OrderResponse>(order);

            // Ghi đè lại 2 trường này để FE hiển thị đúng dữ liệu thực tế đã xử lý
            response.PaymentMethod = request.PaymentMethod;
            response.ShippingFee = backendShippingFee;

            return response;
        }

        public async Task<OrderResponse?> UpdateOrderStatusAsync(Guid id, OrderStatus newStatus)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                filter: o => o.Id == id && !o.IsDeleted,
                includeProperties: "OrderDetails,OrderHistories,Payments"
            );

            if (order == null) return null;

            // Nếu trạng thái không thay đổi thì không làm gì cả
            if (order.CurrentStatus == newStatus)
                return _mapper.Map<OrderResponse>(order);

            order.CurrentStatus = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            string historyNote = $"Hệ thống cập nhật trạng thái thành: {newStatus}";

            // Lấy thông tin Payment của đơn hàng này để cập nhật trạng thái tiền bạc
            var paymentRepo = _unitOfWork.Repository<Domain.Entities.Payment>();
            var paymentList = await paymentRepo.FindAsync(p => p.OrderId == order.Id);
            var payment = paymentList.FirstOrDefault();

            // ========================================================
            // XỬ LÝ LOGIC CHO TỪNG NHÁNH TRẠNG THÁI
            // ========================================================
            switch (newStatus)
            {
                case OrderStatus.Shipping:
                    historyNote = $"Bắt đầu giao hàng (Mã đơn: {order.OrderNumber}).";
                    break;

                case OrderStatus.Delivered:
                    if (payment != null && payment.Status != "Success")
                    {
                        payment.Status = "Success";
                        paymentRepo.Update(payment);
                    }
                    historyNote = "Giao hàng thành công. Đã thu tiền.";
                    break;

                case OrderStatus.Cancelled:
                    if (payment != null)
                    {
                        payment.Status = payment.PaymentMethod == "COD" ? "Cancelled" : "Failed";
                        paymentRepo.Update(payment);
                    }

                    // Hoàn trả lại 1 lượt dùng Voucher cho hệ thống
                    if (order.VoucherId.HasValue)
                    {
                        var voucherRepo = _unitOfWork.Repository<Domain.Entities.Voucher>();
                        var voucher = await voucherRepo.GetByIdAsync(order.VoucherId.Value);
                        if (voucher != null)
                        {
                            voucher.UsageLimit += 1;
                            voucherRepo.Update(voucher);
                        }
                    }
                    historyNote = "Đơn hàng đã bị hủy. Đã hoàn trả voucher và tồn kho.";
                    break;

                case OrderStatus.Returned:
                    if (payment != null)
                    {
                        payment.Status = "Failed";
                        paymentRepo.Update(payment);
                    }
                    historyNote = "Khách boom hàng / Trả hàng. Giao thất bại. Đã hoàn trả tồn kho.";
                    break;

                case OrderStatus.Confirmed:
                case OrderStatus.Processing:
                    break;
            }

            // ========================================================
            // BƯỚC MỚI: HOÀN TRẢ TỒN KHO NẾU HỦY HOẶC TRẢ HÀNG
            // ========================================================
            if (newStatus == OrderStatus.Returned)
            {
                var productsToRestock = new Dictionary<Guid, int>();

                // 1. Bóc tách sản phẩm (Giống hệt logic trừ kho lúc Create Order)
                foreach (var detail in order.OrderDetails)
                {
                    if (detail.ProductId.HasValue)
                    {
                        var pId = detail.ProductId.Value;
                        if (productsToRestock.ContainsKey(pId)) productsToRestock[pId] += detail.Quantity;
                        else productsToRestock[pId] = detail.Quantity;
                    }
                    else if (detail.GiftBoxId.HasValue)
                    {
                        var components = await _unitOfWork.Repository<Domain.Entities.BoxComponent>()
                            .FindAsync(c => c.GiftBoxId == detail.GiftBoxId.Value);

                        foreach (var component in components)
                        {
                            var pId = component.ProductId;
                            var totalToRestock = detail.Quantity * component.Quantity;

                            if (productsToRestock.ContainsKey(pId)) productsToRestock[pId] += totalToRestock;
                            else productsToRestock[pId] = totalToRestock;
                        }
                    }
                }

                var inventoryRepo = _unitOfWork.Repository<Domain.Entities.Inventory>();
                var inventoryTxRepo = _unitOfWork.Repository<Domain.Entities.InventoryTransaction>();

                // 2. Cập nhật lại số lượng vào Database
                foreach (var item in productsToRestock)
                {
                    var pId = item.Key;
                    var qtyToRestock = item.Value;

                    var inventory = await inventoryRepo.GetFirstOrDefaultAsync(i => i.ProductId == pId);

                    if (inventory != null)
                    {
                        // CỘNG lại tồn kho
                        inventory.Quantity += qtyToRestock;
                        inventoryRepo.Update(inventory);

                        // Ghi lại lịch sử (TransactionType là Return/Restock)
                        var transaction = new Domain.Entities.InventoryTransaction
                        {
                            Id = Guid.NewGuid(),
                            InventoryId = inventory.Id,
                            QuantityChange = qtyToRestock, // Số Dương vì là nhập kho
                            TransactionType = "Return",
                            ReferenceId = order.Id.ToString(),
                            Note = newStatus == OrderStatus.Returned
                                   ? "Hoàn trả kho do khách trả hàng/boom hàng"
                                   : "Hoàn trả kho do đơn hàng bị hủy",
                            CreatedAt = DateTime.UtcNow
                        };
                        await inventoryTxRepo.AddAsync(transaction);
                    }
                }
            }
            // ========================================================

            // Tự động sinh OrderHistory mới để lưu vết
            var history = new Domain.Entities.OrderHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = newStatus,
                Note = historyNote,
                ChangedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Domain.Entities.OrderHistory>().AddAsync(history);

            _unitOfWork.OrderRepository.Update(order);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<OrderResponse>(order);
        }

        public async Task<bool> DeleteOrderAsync(Guid id)
        {
            var order = await _unitOfWork.OrderRepository.GetByIdAsync(id);
            if (order == null || order.IsDeleted) return false;

            order.IsDeleted = true;
            _unitOfWork.OrderRepository.Update(order);
            return await _unitOfWork.SaveChangesAsync() > 0;
        }

        public async Task<List<OrderResponse>> GetOrdersByUserIdAsync(Guid userId)
        {
            // Truy vấn Database lấy danh sách đơn hàng của User
            var orders = await _unitOfWork.OrderRepository.FindAsync(
                filter: o => o.UserId == userId && !o.IsDeleted,
                orderBy: q => q.OrderByDescending(o => o.CreatedAt), // Đơn mới nhất lên đầu
                includeProperties: "OrderDetails,OrderHistories,Payments"      // Lấy kèm chi tiết và lịch sử
            );

            // Map sang Response DTO và trả về dạng List
            return _mapper.Map<List<OrderResponse>>(orders.ToList());
        }
    }
}
