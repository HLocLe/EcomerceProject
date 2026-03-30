using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Response.MomoPayment
{
    public class MomoMobilePaymentResponse
    {
        public Guid OrderId { get; set; }
        public string MomoOrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PayUrl { get; set; }
        public string? Deeplink { get; set; }
        public string? QrCodeUrl { get; set; }
        public string LocalPaymentStatus { get; set; } = string.Empty;

        // Cho dễ debug bên mobile / swagger
        public string RedirectUrlUsed { get; set; } = string.Empty;
    }

}
