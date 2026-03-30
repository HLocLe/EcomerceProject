using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Response.MomoPayment
{
    public class MomoCreateGatewayResultDto
    {
        public bool IsSuccessStatusCode { get; set; }
        public int HttpStatusCode { get; set; }
        public string RawResponse { get; set; } = string.Empty;

        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;

        public string? PayUrl { get; set; }
        public string? Deeplink { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? RedirectUrlUsed { get; set; }
    }
}
