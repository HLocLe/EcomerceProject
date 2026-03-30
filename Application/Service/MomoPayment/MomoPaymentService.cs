using System.Text.Json;
using Application.DTOs.Request.MomoPayment;
using Application.DTOs.Response.MomoPayment;
using Domain.Entities;
using Domain.Enums;
using Domain.IUnitOfWork;
using OrderEntity = Domain.Entities.Order;

namespace Application.Service.MomoPayment
{
    /// <summary>
    /// Service nghiệp vụ cho luồng thanh toán MoMo.
    ///
    /// FLOW CHÍNH:
    /// 1. FE tạo Order trước bằng /api/orders với PaymentMethod = "Online"
    /// 2. FE gọi /api/Payment/momo/create để tạo link thanh toán từ OrderId
    /// 3. User thanh toán trên MoMo
    /// 4. Hệ thống đồng bộ kết quả qua:
    ///    - IPN từ MoMo (luồng chính)
    ///    - Query status (luồng fallback / sync lại)
    /// 5. Khi thanh toán thành công:
    ///    - Payment.Status -> Success
    ///    - Order.CurrentStatus -> Confirmed
    ///    - Gom Product/GiftBox trong OrderDetails
    ///    - Trừ tồn kho trong Inventory
    ///    - Ghi lịch sử vào InventoryTransaction
    ///    - Ghi PaymentHistory / OrderHistory
    /// </summary>
    public class MomoPaymentService : IMomoPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMomoGatewayClient _momoGatewayClient;

        public MomoPaymentService(
            IUnitOfWork unitOfWork,
            IMomoGatewayClient momoGatewayClient)
        {
            _unitOfWork = unitOfWork;
            _momoGatewayClient = momoGatewayClient;
        }

        /// <summary>
        /// Tạo link thanh toán MoMo cho một order đã tồn tại.
        ///
        /// Lưu ý:
        /// - Order phải thuộc về đúng user đang đăng nhập.
        /// - Chỉ cho phép order online đang ở trạng thái Pending.
        /// - PaymentMethod "Online" sẽ được chuẩn hóa sang "MOMO"
        ///   để các bước query/IPN phía sau tìm đúng record.
        /// </summary>
        public async Task<MomoPaymentResponse> CreatePaymentAsync(Guid orderId, Guid currentUserId, string? orderInfo = null)
        {
            // Load order kèm Payments + OrderDetails để đủ dữ liệu xử lý
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                o => o.Id == orderId && o.UserId == currentUserId && !o.IsDeleted,
                includeProperties: "Payments,OrderDetails");

            if (order == null)
                throw new UnauthorizedAccessException("Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.");

            // Chỉ đơn online đang chờ thanh toán mới được tạo link MoMo
            if (!IsOnlineOrder(order))
                throw new Exception("Chỉ đơn hàng online đang chờ thanh toán mới có thể tạo link MoMo.");

            // Tìm payment thuộc luồng online:
            // - order mới tạo từ /api/orders có thể đang là "Online"
            // - sau khi tạo link MoMo sẽ được normalize thành "MOMO"
            var payment = order.Payments
                .Where(x => !x.IsDeleted && IsOnlinePaymentMethod(x.PaymentMethod))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (payment == null)
                throw new Exception("Đơn hàng này chưa có bản ghi Payment cho hình thức thanh toán online.");

            // Không cho tạo link lại nếu đơn đã thanh toán xong
            if (string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Đơn hàng này đã thanh toán thành công rồi.");

            // Chuẩn hóa method sang MOMO để query status / IPN luôn tìm đúng cùng 1 record
            payment.PaymentMethod = "MOMO";

            // TransactionReference là mã ổn định dùng để:
            // - gửi sang MoMo
            // - query status
            // - đối chiếu IPN
            if (string.IsNullOrWhiteSpace(payment.TransactionReference))
            {
                payment.TransactionReference = $"MOMO_{payment.Id:N}";
            }

            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            MomoCreateGatewayResultDto gatewayResult;

            try
            {
                // Gọi adapter Infrastructure để tạo link thanh toán với MoMo
                gatewayResult = await _momoGatewayClient.CreatePaymentAsync(new MomoCreateGatewayRequestDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    TransactionReference = payment.TransactionReference!,
                    Amount = payment.Amount,
                    OrderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                // Nếu lỗi exception ở tầng gateway -> mark failed để trace lại được
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    ex.ToString(),
                    "Gọi MoMo create payment bị exception.");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception("Không gọi được MoMo để tạo link thanh toán.", ex);
            }

            // HTTP lỗi từ phía MoMo
            if (!gatewayResult.IsSuccessStatusCode)
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    gatewayResult.RawResponse,
                    $"Gọi API create MoMo thất bại. HTTP {gatewayResult.HttpStatusCode}");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception($"MoMo trả lỗi HTTP {gatewayResult.HttpStatusCode}: {gatewayResult.RawResponse}");
            }

            // HTTP thành công nhưng business result code của MoMo báo lỗi
            if (gatewayResult.ResultCode != 0)
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    gatewayResult.RawResponse,
                    $"MoMo tạo link thất bại. ResultCode = {gatewayResult.ResultCode}, Message = {gatewayResult.Message}");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception($"MoMo tạo link thất bại: {gatewayResult.Message} ({gatewayResult.ResultCode})");
            }

            // Tạo link thành công -> payment chuyển sang chờ người dùng thanh toán
            payment.Status = "WaitingForPayment";
            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            await AddPaymentHistoryAsync(
                payment.Id,
                payment.Status,
                gatewayResult.RawResponse,
                "Tạo link thanh toán MoMo thành công. Đang chờ người dùng thanh toán.");

            await _unitOfWork.SaveChangesAsync();

            // Trả link cho FE redirect user sang MoMo
            return new MomoPaymentResponse
            {
                OrderId = order.Id,
                MomoOrderId = payment.TransactionReference!,
                RequestId = payment.TransactionReference!,
                Amount = payment.Amount,
                ResultCode = gatewayResult.ResultCode,
                Message = gatewayResult.Message,
                PayUrl = gatewayResult.PayUrl,
                Deeplink = gatewayResult.Deeplink,
                QrCodeUrl = gatewayResult.QrCodeUrl,
                LocalPaymentStatus = payment.Status
            };
        }

        public async Task<MomoMobilePaymentResponse> CreateMobilePaymentAsync(Guid orderId, Guid currentUserId, string? orderInfo = null)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                o => o.Id == orderId && o.UserId == currentUserId && !o.IsDeleted,
                includeProperties: "Payments,OrderDetails");

            if (order == null)
                throw new UnauthorizedAccessException("Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.");

            if (!IsOnlineOrder(order))
                throw new Exception("Chỉ đơn hàng online đang chờ thanh toán mới có thể tạo link MoMo.");

            var payment = order.Payments
                .Where(x => !x.IsDeleted && IsOnlinePaymentMethod(x.PaymentMethod))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (payment == null)
                throw new Exception("Đơn hàng này chưa có bản ghi Payment cho hình thức thanh toán online.");

            if (string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Đơn hàng này đã thanh toán thành công rồi.");

            payment.PaymentMethod = "MOMO";

            if (string.IsNullOrWhiteSpace(payment.TransactionReference))
            {
                payment.TransactionReference = $"MOMO_{payment.Id:N}";
            }

            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            MomoCreateGatewayResultDto gatewayResult;

            try
            {
                gatewayResult = await _momoGatewayClient.CreateMobilePaymentAsync(new MomoCreateGatewayRequestDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    TransactionReference = payment.TransactionReference!,
                    Amount = payment.Amount,
                    OrderInfo = orderInfo
                });
            }
            catch (Exception ex)
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    ex.ToString(),
                    "Gọi MoMo create mobile payment bị exception.");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception("Không gọi được MoMo để tạo link thanh toán mobile.", ex);
            }

            if (!gatewayResult.IsSuccessStatusCode)
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    gatewayResult.RawResponse,
                    $"Gọi API create mobile MoMo thất bại. HTTP {gatewayResult.HttpStatusCode}");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception($"MoMo trả lỗi HTTP {gatewayResult.HttpStatusCode}: {gatewayResult.RawResponse}");
            }

            if (gatewayResult.ResultCode != 0)
            {
                payment.Status = "Failed";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    gatewayResult.RawResponse,
                    $"MoMo tạo link mobile thất bại. ResultCode = {gatewayResult.ResultCode}, Message = {gatewayResult.Message}");

                await _unitOfWork.SaveChangesAsync();

                throw new Exception($"MoMo tạo link mobile thất bại: {gatewayResult.Message} ({gatewayResult.ResultCode})");
            }

            payment.Status = "WaitingForPayment";
            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            await AddPaymentHistoryAsync(
                payment.Id,
                payment.Status,
                gatewayResult.RawResponse,
                "Tạo link thanh toán MoMo cho mobile thành công. Đang chờ người dùng thanh toán.");

            await _unitOfWork.SaveChangesAsync();

            return new MomoMobilePaymentResponse
            {
                OrderId = order.Id,
                MomoOrderId = payment.TransactionReference!,
                RequestId = payment.TransactionReference!,
                Amount = payment.Amount,
                ResultCode = gatewayResult.ResultCode,
                Message = gatewayResult.Message,
                PayUrl = gatewayResult.PayUrl,
                Deeplink = gatewayResult.Deeplink,
                QrCodeUrl = gatewayResult.QrCodeUrl,
                LocalPaymentStatus = payment.Status,
                RedirectUrlUsed = gatewayResult.RedirectUrlUsed ?? string.Empty
            };
        }

        /// <summary>
        /// Query trạng thái thanh toán từ MoMo.
        ///
        /// Dùng khi:
        /// - FE muốn sync lại trạng thái sau khi user quay về từ MoMo
        /// - IPN bị chậm / mạng lag / cần kiểm tra lại thủ công
        ///
        /// Nếu query ra success thì sẽ hoàn tất luôn luồng:
        /// đổi status + trừ kho + ghi lịch sử.
        /// </summary>
        public async Task<MomoPaymentStatusResponse> QueryPaymentStatusAsync(Guid orderId, Guid currentUserId)
        {
            var order = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                o => o.Id == orderId && o.UserId == currentUserId && !o.IsDeleted,
                includeProperties: "Payments,OrderDetails");

            if (order == null)
                throw new UnauthorizedAccessException("Không tìm thấy đơn hàng hoặc bạn không có quyền truy cập.");

            var payment = order.Payments
                .Where(x => !x.IsDeleted && IsOnlinePaymentMethod(x.PaymentMethod))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (payment == null)
                throw new Exception("Đơn hàng này chưa có payment online/MOMO.");

            if (string.IsNullOrWhiteSpace(payment.TransactionReference))
                throw new Exception("Payment online chưa có TransactionReference để query.");

            var gatewayResult = await _momoGatewayClient.QueryPaymentAsync(payment.TransactionReference);

            if (!gatewayResult.IsSuccessStatusCode)
            {
                throw new Exception($"MoMo query thất bại. HTTP {gatewayResult.HttpStatusCode}: {gatewayResult.RawResponse}");
            }

            // Nếu query báo thành công và local DB chưa success
            // => hoàn tất toàn bộ flow thanh toán tại đây
            if (gatewayResult.ResultCode == 0 && !string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
            {
                await MarkPaymentSuccessAsync(
                    order,
                    payment,
                    gatewayResult.RawResponse,
                    gatewayResult.TransId,
                    "Đồng bộ query MoMo báo thanh toán thành công.");
            }
            else
            {
                // Không success thì chỉ ghi history để trace
                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    gatewayResult.RawResponse,
                    $"Query trạng thái MoMo: {gatewayResult.Message} ({gatewayResult.ResultCode})");

                await _unitOfWork.SaveChangesAsync();
            }

            return new MomoPaymentStatusResponse
            {
                OrderId = order.Id,
                MomoOrderId = payment.TransactionReference,
                RequestId = payment.TransactionReference,
                Amount = payment.Amount,
                TransId = gatewayResult.TransId,
                ResultCode = gatewayResult.ResultCode,
                Message = gatewayResult.Message,
                LocalPaymentStatus = payment.Status
            };
        }

        /// <summary>
        /// Xử lý IPN từ MoMo.
        ///
        /// Đây là luồng chính để backend xác nhận giao dịch.
        /// Nếu IPN báo success thì sẽ:
        /// - trừ kho
        /// - update payment/order
        /// - ghi lịch sử
        /// </summary>
        public async Task HandleIpnAsync(MomoIpnRequest request)
        {
            // Chặn IPN giả / sai signature
            if (!_momoGatewayClient.ValidateSignature(request))
                return;

            // Chặn IPN không đúng partner code cấu hình
            if (!_momoGatewayClient.IsValidPartnerCode(request.PartnerCode))
                return;

            var payment = await _unitOfWork.Repository<Payment>().GetFirstOrDefaultAsync(
                p => !p.IsDeleted
                     && IsOnlinePaymentMethod(p.PaymentMethod)
                     && p.TransactionReference == request.OrderId,
                includeProperties: "Order,Order.OrderDetails");

            if (payment == null)
                return;

            // Kiểm tra amount từ MoMo có khớp local DB không
            var expectedAmount = ToLongAmount(payment.Amount);
            if (expectedAmount != request.Amount)
                return;

            var rawIpn = JsonSerializer.Serialize(request);

            // Thanh toán thành công
            if (request.ResultCode == 0)
            {
                await MarkPaymentSuccessAsync(
                    payment.Order,
                    payment,
                    rawIpn,
                    request.TransId,
                    "MoMo IPN báo thanh toán thành công.");
                return;
            }

            // 9000: giao dịch được authorize, chưa hẳn hoàn tất
            if (request.ResultCode == 9000)
            {
                payment.Status = "WaitingForPayment";
                payment.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Payment>().Update(payment);

                await AddPaymentHistoryAsync(
                    payment.Id,
                    payment.Status,
                    rawIpn,
                    $"MoMo IPN báo giao dịch được cấp quyền (9000). TransId = {request.TransId}");

                await _unitOfWork.SaveChangesAsync();
                return;
            }

            // Các trường hợp còn lại xem như thất bại
            payment.Status = "Failed";
            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            await AddPaymentHistoryAsync(
                payment.Id,
                payment.Status,
                rawIpn,
                $"MoMo IPN báo thanh toán thất bại. ResultCode = {request.ResultCode}, Message = {request.Message}, TransId = {request.TransId}");

            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Hoàn tất thanh toán thành công.
        ///
        /// Thứ tự:
        /// 1. Chống xử lý trùng nếu payment đã Success
        /// 2. Trừ kho cho order online
        /// 3. Update payment sang Success
        /// 4. Update order sang Confirmed
        /// 5. Ghi lịch sử payment/order
        /// </summary>
        private async Task MarkPaymentSuccessAsync(
            OrderEntity order,
            Payment payment,
            string rawResponse,
            long? transId,
            string note)
        {
            if (string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
                return;

            // Chỉ trừ kho sau khi online payment thành công
            await DeductInventoryForPaidOrderAsync(order);

            payment.Status = "Success";
            payment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Payment>().Update(payment);

            await AddPaymentHistoryAsync(
                payment.Id,
                payment.Status,
                rawResponse,
                $"{note} TransId = {transId}");

            // Đơn online lúc mới tạo sẽ là Pending
            // Khi thanh toán xong thì chuyển sang Confirmed
            if (order.CurrentStatus == OrderStatus.Pending)
            {
                order.CurrentStatus = OrderStatus.Confirmed;
                order.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.OrderRepository.Update(order);

                await AddOrderHistoryAsync(
                    order.Id,
                    OrderStatus.Confirmed,
                    "Hệ thống tự động xác nhận đơn hàng sau khi MoMo báo thanh toán thành công.");
            }

            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Trừ tồn kho cho order online đã thanh toán thành công.
        ///
        /// Có chống xử lý trùng:
        /// - Nếu đã tồn tại InventoryTransaction loại Sale với ReferenceId = OrderId
        ///   thì bỏ qua, tránh trừ kho 2 lần khi IPN/query bị gọi lặp.
        ///
        /// Có hỗ trợ:
        /// - Product thường
        /// - GiftBox (bung thành BoxComponent để cộng dồn số lượng Product thật cần xuất)
        /// </summary>
        private async Task DeductInventoryForPaidOrderAsync(OrderEntity order)
        {
            var orderToProcess = order;

            // Đảm bảo có đủ OrderDetails để xử lý
            if (orderToProcess.OrderDetails == null || !orderToProcess.OrderDetails.Any())
            {
                orderToProcess = await _unitOfWork.OrderRepository.GetFirstOrDefaultAsync(
                    o => o.Id == order.Id && !o.IsDeleted,
                    includeProperties: "OrderDetails")
                    ?? throw new Exception("Không tìm thấy OrderDetails để trừ kho sau thanh toán.");
            }

            var inventoryTxRepo = _unitOfWork.Repository<InventoryTransaction>();

            // Chống trừ kho 2 lần cho cùng 1 order
            var existedTx = await inventoryTxRepo.GetFirstOrDefaultAsync(
                x => x.ReferenceId == order.Id.ToString() && x.TransactionType == "Sale");

            var alreadyDeducted = existedTx != null;

            if (alreadyDeducted)
                return;

            // Dictionary dùng để gom tổng số lượng Product thật cần xuất kho
            var requiredProducts = new Dictionary<Guid, int>();

            foreach (var detail in orderToProcess.OrderDetails)
            {
                // Case 1: OrderDetail là Product thường
                if (detail.ProductId.HasValue)
                {
                    var productId = detail.ProductId.Value;

                    if (requiredProducts.ContainsKey(productId))
                        requiredProducts[productId] += detail.Quantity;
                    else
                        requiredProducts[productId] = detail.Quantity;
                }
                // Case 2: OrderDetail là GiftBox
                // => Bung ra BoxComponent để tính số lượng Product gốc cần trừ
                else if (detail.GiftBoxId.HasValue)
                {
                    var components = await _unitOfWork.Repository<BoxComponent>()
                        .FindAsync(c => c.GiftBoxId == detail.GiftBoxId.Value);

                    foreach (var component in components)
                    {
                        var productId = component.ProductId;
                        var totalNeeded = detail.Quantity * component.Quantity;

                        if (requiredProducts.ContainsKey(productId))
                            requiredProducts[productId] += totalNeeded;
                        else
                            requiredProducts[productId] = totalNeeded;
                    }
                }
            }

            var inventoryRepo = _unitOfWork.Repository<Inventory>();

            foreach (var item in requiredProducts)
            {
                var productId = item.Key;
                var totalNeeded = item.Value;

                var inventory = await inventoryRepo.GetFirstOrDefaultAsync(i => i.ProductId == productId);

                // Dù lúc tạo order online đã validate tồn kho,
                // tại thời điểm thanh toán thành công vẫn phải check lại
                // vì tồn kho có thể đã đổi do đơn khác.
                if (inventory == null || inventory.Quantity < totalNeeded)
                {
                    var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId);
                    var productName = product?.Name ?? productId.ToString();

                    throw new Exception(
                        $"Sản phẩm '{productName}' không đủ tồn kho để hoàn tất thanh toán. " +
                        $"Cần: {totalNeeded}, Hiện có: {inventory?.Quantity ?? 0}");
                }

                // Thực hiện trừ kho
                inventory.Quantity -= totalNeeded;
                inventory.LastUpdated = DateTime.UtcNow;

                // Cập nhật trạng thái tồn kho sau khi trừ
                if (inventory.Quantity == 0)
                    inventory.Status = InventoryStatus.OutOfStock;
                else if (inventory.Quantity <= inventory.MinStockLevel)
                    inventory.Status = InventoryStatus.LowStock;
                else
                    inventory.Status = InventoryStatus.InStock;

                inventoryRepo.Update(inventory);

                // Ghi lịch sử xuất kho
                await inventoryTxRepo.AddAsync(new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    InventoryId = inventory.Id,
                    QuantityChange = -totalNeeded,
                    TransactionType = "Sale",
                    ReferenceId = order.Id.ToString(),
                    Note = "Xuất kho sau khi thanh toán online thành công",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Ghi lịch sử thay đổi trạng thái payment để dễ audit / đối soát.
        /// </summary>
        private async Task AddPaymentHistoryAsync(Guid paymentId, string status, string? rawResponse, string note)
        {
            var history = new PaymentHistory
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId,
                Status = status,
                RawResponse = rawResponse,
                Note = note,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<PaymentHistory>().AddAsync(history);
        }

        /// <summary>
        /// Ghi lịch sử thay đổi trạng thái order.
        /// </summary>
        private async Task AddOrderHistoryAsync(Guid orderId, OrderStatus status, string note)
        {
            var history = new OrderHistory
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = status,
                Note = note,
                ChangedBy = "System",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<OrderHistory>().AddAsync(history);
        }

        /// <summary>
        /// Xác định payment method có thuộc luồng online không.
        ///
        /// Hỗ trợ cả:
        /// - "Online" (lúc order mới tạo)
        /// - "MOMO"   (sau khi normalize ở bước create payment)
        /// </summary>
        private static bool IsOnlinePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return false;

            return paymentMethod.Equals("MOMO", StringComparison.OrdinalIgnoreCase)
                || paymentMethod.Equals("Online", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Với flow hiện tại:
        /// - Order online vừa tạo sẽ là Pending
        /// - COD sẽ Confirmed ngay
        ///
        /// Nên nếu order đang Pending thì xem như đủ điều kiện tạo link MoMo.
        /// </summary>
        private static bool IsOnlineOrder(OrderEntity order)
        {
            return order.CurrentStatus == OrderStatus.Pending;
        }

        /// <summary>
        /// Chuyển decimal amount trong DB về số nguyên long để so sánh/gửi sang MoMo.
        /// </summary>
        private static long ToLongAmount(decimal amount)
        {
            return Convert.ToInt64(Math.Round(amount, 0, MidpointRounding.AwayFromZero));
        }


    }
}