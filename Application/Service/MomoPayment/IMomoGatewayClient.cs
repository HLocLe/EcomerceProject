using Application.DTOs.Request.MomoPayment;
using Application.DTOs.Response.MomoPayment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Service.MomoPayment
{
    public interface IMomoGatewayClient
    {
        Task<MomoCreateGatewayResultDto> CreatePaymentAsync(MomoCreateGatewayRequestDto request);
        Task<MomoCreateGatewayResultDto> CreateMobilePaymentAsync(MomoCreateGatewayRequestDto request);
        Task<MomoQueryGatewayResultDto> QueryPaymentAsync(string momoOrderId);
        bool ValidateSignature(MomoIpnRequest request);
        bool IsValidPartnerCode(string partnerCode);
    }
}
