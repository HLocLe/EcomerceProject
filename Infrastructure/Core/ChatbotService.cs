using Application.DTOs.Request.Chatbot;
using Application.DTOs.Response.Chatbot;
using Application.Service.Chatbot;
using Domain.Entities;
using Domain.IUnitOfWork;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.Core
{
    public class ChatbotService : IChatbotService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;

        public ChatbotService(IConfiguration configuration, IUnitOfWork unitOfWork, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ChatMessageResponse> GetChatResponseAsync(string userMessage)
        {
            var apiKey = _configuration["OpenRouter:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                return new ChatMessageResponse
                {
                    Response = "Xin lỗi, chức năng chatbot chưa được cấu hình. Vui lòng thêm OpenRouter:ApiKey vào cấu hình."
                };
            }


            var criteria = await ExtractKeywordsAsync(userMessage);


            var products = new List<Product>();
            if (criteria.NeedProductSearch)
            {
                products = await SearchProductsAsync(criteria);
            }


            var response = await GenerateConsultationAsync(userMessage, products, criteria.NeedProductSearch);

            return new ChatMessageResponse
            {
                Response = response,
                ProductSuggestions =
                    (criteria.NeedProductSearch && products.Count > 0)
                        ? products.Select(p => new ProductSuggestion
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Description = p.Description ?? string.Empty,
                            Price = p.Price,
                            ImageUrl = p.Images?.FirstOrDefault(i => i.IsMain)?.Url
                        }).ToList()
                        : null
            };
        }

        private async Task<string> PostToOpenRouterAsync(string systemPrompt, string userContent, float temperature = 0.7f)
        {
            var client = _httpClientFactory.CreateClient();
            var apiKey = _configuration["OpenRouter:ApiKey"];

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("HTTP-Referer", _configuration["OpenRouter:Referer"] ?? "https://happybox.vn");
            client.DefaultRequestHeaders.Add("X-Title", "HappyBox-Chatbot");

            var requestBody = new
            {
                model = "google/gemini-2.5-flash-lite",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userContent }
                },
                temperature = temperature
            };

            var response = await client.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenRouter API call failed: {(int)response.StatusCode} - {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }

        private async Task<ProductSearchCriteria> ExtractKeywordsAsync(string userMessage)
        {
            var systemPrompt = """
Bạn là AI chuyên phân tích yêu cầu của khách hàng về sản phẩm quà tặng.

Nhiệm vụ: Đọc tin nhắn của khách hàng và trích xuất thông tin sau dưới dạng JSON,
đồng thời xác định xem user có đang muốn tìm/mua hỏi về sản phẩm hay không.

Luật ý định:
- needProductSearch = true nếu user có ý định hỏi về sản phẩm, tìm sản phẩm, mua hàng, hỏi giá, hỏi “có bán không”, gợi ý quà tặng,...
- needProductSearch = false nếu user chỉ chào hỏi/chat thông thường (ví dụ: "hi", "hello", "chào bạn", "bạn khỏe không"...), hoặc không hỏi về sản phẩm.

{
  "needProductSearch": true/false,
  "products": ["danh sách sản phẩm/đồ vật khách hàng nhắc đến: gấu bông, kẹo, socola, rượu, hoa..."],
  "attributes": ["các thuộc tính: màu sắc, kích thước, chất liệu"],
  "occasion": "dịp tặng quà (sinh nhật, tết, valentine...)",
  "recipient": "người nhận (bạn nữ, bố mẹ, đồng nghiệp...)",
  "minPrice": "giá thấp nhất (số hoặc null). Ví dụ: 'từ 200k' -> 200000",
  "maxPrice": "giá cao nhất (số hoặc null). Ví dụ: 'dưới 500k' -> 500000, 'tầm 1 triệu đổ lại' -> 1000000",
  "sortPrice": "hướng sắp xếp: 'asc' (nếu khách muốn rẻ nhất/giá thấp nhất), 'desc' (nếu khách muốn đắt nhất/sang trọng nhất/tốt nhất trong tầm giá) hoặc null",
  "origin": ["xuất xứ/nguồn gốc: tách từng địa danh hoặc quốc gia thành phần tử riêng, viết thường. VD: 'bang cleveland Mỹ' -> ['cleveland','mỹ'], 'Nhật Bản' -> ['nhật bản'], 'Pháp, Ý' -> ['pháp','ý']"],
  "description": "chỉ dùng cho mô tả phong cách/cảm xúc trừu tượng (vd: 'ấm áp, sang trọng'). Để rỗng nếu không có."
}

Luật description (quan trọng):
- Hệ thống tìm sản phẩm qua products, attributes, minPrice, maxPrice, sortPrice, origin — KHÔNG dùng description để lọc.
- Mọi từ khóa có thể xuất hiện trong tên/mô tả sản phẩm (hộp quà, gấu bông, socola, màu hồng, v.v.) phải đưa vào products hoặc attributes, không gom vào description.
- Chỉ hỏi giá/ngân sách/rẻ nhất/đắt nhất/dưới Xk mà không mô tả phong cách trừu tượng → description = "" (không nhét lại cả câu hỏi vào description).

Nếu needProductSearch = false thì hãy để:
- products = []
- attributes = []
- occasion = ""
- recipient = ""
- minPrice = null
- maxPrice = null
- sortPrice = null
- origin = []
- description = ""

Ví dụ:
Input: "hi"
Output:
{"needProductSearch":false,"products":[],"attributes":[],"occasion":"","recipient":"","minPrice":null,"maxPrice":null,"sortPrice":null,"origin":[],"description":""}

Input: "Tôi muốn tìm hộp quà có gấu bông màu hồng và socola cho bạn nữ, giá khoảng 500k"
Output: 
{"needProductSearch":true,"products":["hộp quà","gấu bông","socola"],"attributes":["màu hồng"],"occasion":"","recipient":"bạn nữ","minPrice":null,"maxPrice":500000,"sortPrice":"desc","origin":[],"description":""}

Input: "tôi chỉ có 500k thì mua món nào"
Output:
{"needProductSearch":true,"products":[],"attributes":[],"occasion":"","recipient":"","minPrice":null,"maxPrice":500000,"sortPrice":"desc","origin":[],"description":""}

Input: "tìm cho tôi món nào rẻ nhất dưới 200000"
Output:
{"needProductSearch":true,"products":[],"attributes":[],"occasion":"","recipient":"","minPrice":null,"maxPrice":200000,"sortPrice":"asc","origin":[],"description":""}

Input: "quà gì đó ấm áp và sang trọng"
Output:
{"needProductSearch":true,"products":[],"attributes":[],"occasion":"","recipient":"","minPrice":null,"maxPrice":null,"sortPrice":null,"origin":[],"description":"quà tặng phong cách ấm áp, sang trọng"}

Chỉ trả về JSON hợp lệ, không thêm văn bản nào khác.
""";

            try
            {
                var jsonText = await PostToOpenRouterAsync(systemPrompt, userMessage, 0.1f);

                if (string.IsNullOrEmpty(jsonText))
                    return new ProductSearchCriteria();

                if (jsonText.StartsWith("```"))
                {
                    var start = jsonText.IndexOf('{');
                    var end = jsonText.LastIndexOf('}') + 1;
                    if (start >= 0 && end > start)
                        jsonText = jsonText[start..end];
                }

                var criteria = JsonSerializer.Deserialize<ProductSearchCriteria>(jsonText);
                return criteria ?? new ProductSearchCriteria();
            }
            catch
            {
                return new ProductSearchCriteria();
            }
        }

        private async Task<List<Product>> SearchProductsAsync(ProductSearchCriteria criteria)
        {
            var allProducts = await _unitOfWork.ProductRepository.FindAsync(
                filter: null,
                includeProperties: "Category,Images",
                tracked: false);

            var query = allProducts.Where(p => p.IsActive);

            if (criteria.Products.Count > 0)
            {
                var keywords = criteria.Products
                    .Select(k => k.Trim().ToLowerInvariant())
                    .Where(k => !string.IsNullOrEmpty(k))
                    .ToList();

                if (keywords.Count > 0)
                {
                    query = query.Where(p =>
                        keywords.Any(kw =>
                            (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(kw)));
                }
            }

            if (criteria.Attributes.Count > 0)
            {
                var attrs = criteria.Attributes
                    .Select(a => a.Trim().ToLowerInvariant())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                if (attrs.Count > 0)
                {
                    query = query.Where(p =>
                        attrs.Any(attr =>
                            (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(attr)));
                }
            }

            if (criteria.MinPrice.HasValue)
            {
                query = query.Where(p => p.Price >= criteria.MinPrice.Value);
            }

            if (criteria.MaxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= criteria.MaxPrice.Value);
            }

            if (criteria.Origin.Count > 0)
            {
                var originTerms = criteria.Origin
                    .Select(o => o.Trim().ToLowerInvariant())
                    .Where(o => !string.IsNullOrEmpty(o))
                    .ToList();

                if (originTerms.Count > 0)
                {
                    query = query.Where(p =>
                        originTerms.Any(term =>
                            (p.Name + " " + (p.Description ?? "")).ToLowerInvariant().Contains(term)));
                }
            }

            if (criteria.SortPrice == "asc")
            {
                query = query.OrderBy(p => p.Price);
            }
            else if (criteria.SortPrice == "desc")
            {
                query = query.OrderByDescending(p => p.Price);
            }

            return query.Take(5).ToList();
        }

        private async Task<string> GenerateConsultationAsync(
            string userMessage,
            List<Product> products,
            bool needProductSearch)
        {
            var productsContext = (needProductSearch && products.Count > 0)
                ? string.Join("\n", products.Select((p, i) =>
                    $"- [{i + 1}] {p.Name} | Giá: {p.Price:N0} VNĐ | Mô tả: {(p.Description?.Length > 200 ? p.Description[..200] + "..." : p.Description ?? "")}"))
                : string.Empty;

            var systemPrompt = $"""
Bạn là nhân viên tư vấn thân thiện của HappyBox - cửa hàng chuyên về hộp quà tặng và sản phẩm quà tặng.

Nhiệm vụ:
- Trong cuộc gọi này, needProductSearch = {needProductSearch}.
- Nếu needProductSearch = false: trả lời như một nhân viên tư vấn/chat bình thường, hỏi thêm nhu cầu của khách hàng, KHÔNG gợi ý sản phẩm.
- Nếu needProductSearch = true: dựa trên yêu cầu của khách hàng và danh sách sản phẩm tìm được, tư vấn thân thiện, chuyên nghiệp.

Quy tắc:
- Trả lời bằng tiếng Việt, thân thiện.
- Nếu needProductSearch = true và có danh sách sản phẩm: giới thiệu 1-3 sản phẩm nổi bật nhất và giải thích vì sao phù hợp.
- Nếu needProductSearch = true nhưng không có sản phẩm: gợi ý khách hàng thử mô tả khác hoặc liên hệ hotline.
- Tuyệt đối không đưa “Danh sách sản phẩm” hay tên sản phẩm cụ thể nếu needProductSearch = false.
- Giữ câu trả lời ngắn gọn, dễ đọc (2-4 câu).
""";

            var userContent = needProductSearch
                ? (products.Count > 0
                    ? $"""
Yêu cầu khách hàng: "{userMessage}"

Danh sách sản phẩm tìm được:
{productsContext}

Hãy tư vấn cho khách hàng.
"""
                    : $"""
Yêu cầu khách hàng: "{userMessage}"

Không tìm thấy sản phẩm phù hợp trong kho.

Hãy tư vấn cho khách hàng (gợi ý thử mô tả khác hoặc liên hệ hotline nếu phù hợp).
""")
                : $"""
Khách hàng: "{userMessage}"

Hãy trả lời thân thiện, tự nhiên và hỏi thêm nhu cầu nếu cần.
""";

            try
            {
                return await PostToOpenRouterAsync(systemPrompt, userContent);
            }
            catch (Exception ex)
            {
                return $"Xin lỗi, đã có lỗi xảy ra: {ex.Message}";
            }
        }
    }
}