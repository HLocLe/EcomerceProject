using Application.DTOs.Request.Order;
using Application.DTOs.Request.MomoPayment;
using Application.DTOs.Response;
using Application.DTOs.Response.MomoPayment;
using Application.Service.Order;
using Application.Service.MomoPayment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PRN2322.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IMomoPaymentService _momoPaymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IOrderService orderService,
            IMomoPaymentService momoPaymentService,
            ILogger<PaymentController> logger)
        {
            _orderService = orderService;
            _momoPaymentService = momoPaymentService;
            _logger = logger;
        }

        // Tạo order + tạo link MoMo luôn
        [Authorize]
        [HttpPost("momo/create-order")]
        public async Task<ActionResult<ApiResponse<MomoPaymentResponse>>> CreateOrderAndMomoPayment([FromBody] CreateOrderRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                request.PaymentMethod = "MOMO";

                var createdOrder = await _orderService.CreateOrderAsync(request);
                var momoResponse = await _momoPaymentService.CreatePaymentAsync(createdOrder.Id, currentUserId, request.Note);

                return Ok(ApiResponse<MomoPaymentResponse>.SuccessResponse(
                    momoResponse,
                    "Tạo đơn hàng và link thanh toán MoMo thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<MomoPaymentResponse>.FailureResponse(
                    "Bạn chưa đăng nhập hoặc token không hợp lệ.",
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<MomoPaymentResponse>.FailureResponse(
                    "Tạo thanh toán MoMo thất bại.",
                    new List<string> { ex.Message }));
            }
        }

        // Dùng khi order đã tồn tại rồi, giờ mới muốn tạo lại link MoMo
        [Authorize]
        [HttpPost("momo/create")]
        public async Task<ActionResult<ApiResponse<MomoPaymentResponse>>> CreateMomoPayment([FromBody] CreateMomoPaymentRequest request)
        {
            var currentUserId = GetCurrentUserId();

            var result = await _momoPaymentService.CreatePaymentAsync(
                request.OrderId,
                currentUserId,
                request.OrderInfo);

            return Ok(ApiResponse<MomoPaymentResponse>.SuccessResponse(
                result,
                "Tạo link thanh toán MoMo thành công."));
        }

        [Authorize]
        [HttpPost("momo/create-mobile")]
        public async Task<ActionResult<ApiResponse<MomoMobilePaymentResponse>>> CreateMomoMobilePayment([FromBody] CreateMomoMobilePaymentRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var result = await _momoPaymentService.CreateMobilePaymentAsync(
                    request.OrderId,
                    currentUserId,
                    request.OrderInfo);

                return Ok(ApiResponse<MomoMobilePaymentResponse>.SuccessResponse(
                    result,
                    "Tạo link thanh toán MoMo cho mobile thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<MomoMobilePaymentResponse>.FailureResponse(
                    "Bạn chưa đăng nhập hoặc token không hợp lệ.",
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<MomoMobilePaymentResponse>.FailureResponse(
                    "Tạo thanh toán MoMo cho mobile thất bại.",
                    new List<string> { ex.Message }));
            }
        }

        // FE có thể gọi endpoint này sau khi user quay về từ MoMo để sync lại trạng thái
        [Authorize]
        [HttpGet("momo/orders/{orderId:guid}/status")]
        public async Task<ActionResult<ApiResponse<MomoPaymentStatusResponse>>> GetMomoPaymentStatus(Guid orderId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var result = await _momoPaymentService.QueryPaymentStatusAsync(orderId, currentUserId);

                return Ok(ApiResponse<MomoPaymentStatusResponse>.SuccessResponse(
                    result,
                    "Lấy trạng thái thanh toán MoMo thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<MomoPaymentStatusResponse>.FailureResponse(
                    "Bạn chưa đăng nhập hoặc không có quyền truy cập đơn hàng này.",
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<MomoPaymentStatusResponse>.FailureResponse(
                    "Lấy trạng thái thanh toán MoMo thất bại.",
                    new List<string> { ex.Message }));
            }
        }

        // Endpoint này MoMo sẽ gọi trực tiếp, không dùng JWT
        [AllowAnonymous]
        [HttpPost("momo/ipn")]
        public async Task<IActionResult> MomoIpn([FromBody] MomoIpnRequest request)
        {
            try
            {
                await _momoPaymentService.HandleIpnAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tại endpoint IPN MoMo");
            }

            // Theo khuyến nghị của MoMo: trả 204 No Content
            return NoContent();
        }

    private Guid GetCurrentUserId()
        {
            var userIdClaim =
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value ??
                User.FindFirst("userId")?.Value ??
                User.FindFirst("UserId")?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
                throw new UnauthorizedAccessException("Không tìm thấy UserId trong JWT.");

            if (!Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("UserId trong JWT không hợp lệ.");

            return userId;
        }
    } 
}

