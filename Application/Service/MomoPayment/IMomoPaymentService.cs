using Application.DTOs.Request.MomoPayment;
using Application.DTOs.Response.MomoPayment;

namespace Application.Service.MomoPayment
{
    public interface IMomoPaymentService
    {
        Task<MomoPaymentResponse> CreatePaymentAsync(Guid orderId, Guid currentUserId, string? orderInfo = null);
        Task<MomoMobilePaymentResponse> CreateMobilePaymentAsync(Guid orderId, Guid currentUserId, string? orderInfo = null);
        Task<MomoPaymentStatusResponse> QueryPaymentStatusAsync(Guid orderId, Guid currentUserId);
        Task HandleIpnAsync(MomoIpnRequest request);
    }
}