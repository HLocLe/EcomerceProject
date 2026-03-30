using Application.DTOs.Request.MomoPayment;
using Application.DTOs.Response.MomoPayment;
using Application.Service.MomoPayment;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Core.Momo
{
    public class MomoGatewayClient : IMomoGatewayClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MomoApiOptions _options;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public MomoGatewayClient(
            IHttpClientFactory httpClientFactory,
            IOptions<MomoApiOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public Task<MomoCreateGatewayResultDto> CreatePaymentAsync(MomoCreateGatewayRequestDto request)
        {
            return CreatePaymentInternalAsync(request, _options.RedirectUrl);
        }

        public Task<MomoCreateGatewayResultDto> CreateMobilePaymentAsync(MomoCreateGatewayRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(_options.MobileRedirectUrl))
                throw new Exception("Cấu hình MomoAPI:MobileRedirectUrl chưa được thiết lập.");

            return CreatePaymentInternalAsync(request, _options.MobileRedirectUrl);
        }

        private async Task<MomoCreateGatewayResultDto> CreatePaymentInternalAsync(
    MomoCreateGatewayRequestDto request,
    string redirectUrl)
        {
            ValidateConfig(redirectUrl);

            var amount = ToLongAmount(request.Amount);
            if (amount < 1000)
                throw new Exception("MoMo yêu cầu số tiền thanh toán tối thiểu là 1000 VND.");

            var finalOrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo)
                ? $"Thanh toán đơn hàng {request.OrderNumber}"
                : request.OrderInfo;

            finalOrderInfo = finalOrderInfo.Replace("&", " ");

            var extraData = BuildExtraData(request.OrderId, request.OrderNumber);

            var rawSignature =
                $"accessKey={_options.AccessKey}&amount={amount}&extraData={extraData}" +
                $"&ipnUrl={_options.IpnUrl}&orderId={request.TransactionReference}&orderInfo={finalOrderInfo}" +
                $"&partnerCode={_options.PartnerCode}&redirectUrl={redirectUrl}" +
                $"&requestId={request.TransactionReference}&requestType=captureWallet";

            var signature = ComputeHmacSha256(rawSignature, _options.SecretKey);

            var payload = new MomoCreateGatewayRequest
            {
                PartnerCode = _options.PartnerCode,
                RequestType = "captureWallet",
                IpnUrl = _options.IpnUrl,
                RedirectUrl = redirectUrl,
                OrderId = request.TransactionReference,
                Amount = amount,
                OrderInfo = finalOrderInfo,
                RequestId = request.TransactionReference,
                ExtraData = extraData,
                Lang = "vi",
                Signature = signature
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var httpResponse = await client.PostAsJsonAsync(_options.ApiUrl, payload);
            var rawResponse = await httpResponse.Content.ReadAsStringAsync();

            var momoResponse = JsonSerializer.Deserialize<MomoCreateGatewayResponse>(rawResponse, JsonOptions);

            return new MomoCreateGatewayResultDto
            {
                IsSuccessStatusCode = httpResponse.IsSuccessStatusCode,
                HttpStatusCode = (int)httpResponse.StatusCode,
                RawResponse = rawResponse,
                ResultCode = momoResponse?.ResultCode ?? -1,
                Message = momoResponse?.Message ?? string.Empty,
                PayUrl = momoResponse?.PayUrl,
                Deeplink = momoResponse?.Deeplink,
                QrCodeUrl = momoResponse?.QrCodeUrl,
                RedirectUrlUsed = redirectUrl
            };
        }

        public async Task<MomoQueryGatewayResultDto> QueryPaymentAsync(string momoOrderId)
        {
            ValidateConfig();

            var requestId = Guid.NewGuid().ToString("N");

            var rawSignature =
                $"accessKey={_options.AccessKey}&orderId={momoOrderId}&partnerCode={_options.PartnerCode}" +
                $"&requestId={requestId}";

            var signature = ComputeHmacSha256(rawSignature, _options.SecretKey);

            var payload = new MomoQueryGatewayRequest
            {
                PartnerCode = _options.PartnerCode,
                RequestId = requestId,
                OrderId = momoOrderId,
                Lang = "vi",
                Signature = signature
            };

            var queryUrl = BuildQueryUrl(_options.ApiUrl);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var httpResponse = await client.PostAsJsonAsync(queryUrl, payload);
            var rawResponse = await httpResponse.Content.ReadAsStringAsync();

            var momoResponse = JsonSerializer.Deserialize<MomoQueryGatewayResponse>(rawResponse, JsonOptions);

            return new MomoQueryGatewayResultDto
            {
                IsSuccessStatusCode = httpResponse.IsSuccessStatusCode,
                HttpStatusCode = (int)httpResponse.StatusCode,
                RawResponse = rawResponse,
                ResultCode = momoResponse?.ResultCode ?? -1,
                Message = momoResponse?.Message ?? string.Empty,
                TransId = momoResponse?.TransId
            };
        }

        public bool ValidateSignature(MomoIpnRequest request)
        {
            ValidateConfig();

            var rawSignature =
                $"accessKey={_options.AccessKey}&amount={request.Amount}&extraData={request.ExtraData}" +
                $"&message={request.Message}&orderId={request.OrderId}&orderInfo={request.OrderInfo}" +
                $"&orderType={request.OrderType}&partnerCode={request.PartnerCode}&payType={request.PayType}" +
                $"&requestId={request.RequestId}&responseTime={request.ResponseTime}" +
                $"&resultCode={request.ResultCode}&transId={request.TransId}";

            var computed = ComputeHmacSha256(rawSignature, _options.SecretKey);
            return string.Equals(computed, request.Signature, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValidPartnerCode(string partnerCode)
        {
            ValidateConfig();
            return string.Equals(partnerCode, _options.PartnerCode, StringComparison.OrdinalIgnoreCase);
        }

        private void ValidateConfig()
        {
            if (string.IsNullOrWhiteSpace(_options.ApiUrl) ||
                string.IsNullOrWhiteSpace(_options.PartnerCode) ||
                string.IsNullOrWhiteSpace(_options.AccessKey) ||
                string.IsNullOrWhiteSpace(_options.SecretKey) ||
                string.IsNullOrWhiteSpace(_options.IpnUrl))
            {
                throw new Exception("Cấu hình MomoAPI trong appsettings.json chưa đầy đủ.");
            }
        }

        private void ValidateConfig(string redirectUrl)
        {
            ValidateConfig();

            if (string.IsNullOrWhiteSpace(redirectUrl))
            {
                throw new Exception("RedirectUrl của MoMo chưa được cấu hình.");
            }
        }

        private static long ToLongAmount(decimal amount)
        {
            return Convert.ToInt64(Math.Round(amount, 0, MidpointRounding.AwayFromZero));
        }

        private static string BuildExtraData(Guid orderId, string orderNumber)
        {
            var payload = new
            {
                orderId,
                orderNumber
            };

            var json = JsonSerializer.Serialize(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        private static string ComputeHmacSha256(string rawData, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var dataBytes = Encoding.UTF8.GetBytes(rawData);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string BuildQueryUrl(string createUrl)
        {
            if (createUrl.EndsWith("/create", StringComparison.OrdinalIgnoreCase))
            {
                return createUrl[..^"/create".Length] + "/query";
            }

            return createUrl.Replace("create", "query", StringComparison.OrdinalIgnoreCase);
        }

        private class MomoCreateGatewayRequest
        {
            public string PartnerCode { get; set; } = string.Empty;
            public string RequestType { get; set; } = string.Empty;
            public string IpnUrl { get; set; } = string.Empty;
            public string RedirectUrl { get; set; } = string.Empty;
            public string OrderId { get; set; } = string.Empty;
            public long Amount { get; set; }
            public string OrderInfo { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
            public string ExtraData { get; set; } = string.Empty;
            public string Lang { get; set; } = "vi";
            public string Signature { get; set; } = string.Empty;
        }

        private class MomoCreateGatewayResponse
        {
            public string? PartnerCode { get; set; }
            public string? OrderId { get; set; }
            public string? RequestId { get; set; }
            public long Amount { get; set; }
            public long ResponseTime { get; set; }
            public string? Message { get; set; }
            public int ResultCode { get; set; }
            public string? PayUrl { get; set; }
            public string? Deeplink { get; set; }
            public string? QrCodeUrl { get; set; }
        }

        private class MomoQueryGatewayRequest
        {
            public string PartnerCode { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
            public string OrderId { get; set; } = string.Empty;
            public string Lang { get; set; } = "vi";
            public string Signature { get; set; } = string.Empty;
        }

        private class MomoQueryGatewayResponse
        {
            public string? PartnerCode { get; set; }
            public string? RequestId { get; set; }
            public string? OrderId { get; set; }
            public string? ExtraData { get; set; }
            public long Amount { get; set; }
            public long? TransId { get; set; }
            public string? PayType { get; set; }
            public int ResultCode { get; set; }
            public string? Message { get; set; }
            public long ResponseTime { get; set; }
        }
    }
}