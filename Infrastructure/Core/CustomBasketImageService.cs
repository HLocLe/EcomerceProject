using Application.DTOs.Request.Chatbot;
using Application.DTOs.Response.Chatbot;
using Application.Service.Chatbot;
using Domain.Entities;
using Domain.IUnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Infrastructure.Core
{
    public class CustomBasketImageService : ICustomBasketImageService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IUnitOfWork _unitOfWork;

        public CustomBasketImageService(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration,
            IWebHostEnvironment webHostEnvironment,
            IUnitOfWork unitOfWork)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
            _unitOfWork = unitOfWork;
        }

        public async Task<string> GenerateCustomBasketAsync(CreateCustomBasketRequest request,Guid userId)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.BasketImageUrl))
                throw new ArgumentException("BasketImageUrl là bắt buộc.", nameof(request));

            var productsWithImage = request.Products?
                .Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl))
                .ToList() ?? new List<CustomBasketProductItem>();

            if (productsWithImage.Count == 0)
                throw new ArgumentException("Cần ít nhất một sản phẩm có ImageUrl.", nameof(request));

            var basket = request.BasketImageUrl;
            var listProduct = productsWithImage.Select(p => p.ImageUrl!).ToList();
            var totalQuantity = productsWithImage.Sum(p => p.Quantity);
            var quantityLines = string.Join(". ", productsWithImage.Select((p, i) => $"Ảnh sản phẩm {i + 1}: số lượng {p.Quantity}"));
        
            var textPrompt = $@"
            Hãy tạo một hình ảnh giỏ quà Tết thực tế với yêu cầu sau:
            - Sử dụng cái giỏ trong ảnh đầu tiên làm vật chứa
            - Các ảnh tiếp theo là ảnh từng loại sản phẩm. Số lượng mỗi loại: {quantityLines}
            - Tổng cộng đặt {totalQuantity} sản phẩm vào bên trong giỏ theo đúng số lượng từng loại
            - Sắp xếp các sản phẩm một cách thẩm mỹ, cân đối như một giỏ quà Tết thực sự
            - Đảm bảo các sản phẩm trông tự nhiên, không bị biến dạng
            ";

            var contentList = new List<object>
            {
                new { type = "text", text = textPrompt }
            };

            if (!string.IsNullOrWhiteSpace(basket))
            {
                contentList.Add(new
                {
                    type = "image_url",
                    image_url = new { url = basket }
                });
            }

            foreach (var productUrl in listProduct)
            {
                contentList.Add(new
                {
                    type = "image_url",
                    image_url = new { url = productUrl }
                });
            }

           var openRouterRequest = new
            {
                model = "google/gemini-2.5-flash-image",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentList
                    }
                }
            };

            var response = await PostToOpenRouter(openRouterRequest);
            var base64Image = response.GeneratedImageUrl;

            if (string.IsNullOrWhiteSpace(base64Image))
                throw new Exception("OpenRouter did not return a valid image.");

            // Parse Base64 to byte[]
            var base64Data = base64Image;
            if (base64Data.StartsWith("data:image/"))
            {
                var commaIndex = base64Data.IndexOf(',');
                if (commaIndex > 0)
                    base64Data = base64Data.Substring(commaIndex + 1);
            }

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to decode Base64 image.", ex);
            }

          
            var webRootPath = _webHostEnvironment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
                throw new InvalidOperationException("WebRootPath is not configured. Ensure wwwroot folder exists.");

            var tempFolder = Path.Combine(webRootPath, "images", "custom-baskets", "temp");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            var fileName = $"preview_{Guid.NewGuid()}.png";
            var filePath = Path.Combine(tempFolder, fileName);

            await File.WriteAllBytesAsync(filePath, imageBytes);
  
            return $"/images/custom-baskets/temp/{fileName}";
        }

        public async Task<string> GenerateExclusiveDetailsAsync(GenerateExclusiveDetailsRequest request, Guid userId)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.RelativeImagePath))
                throw new ArgumentException("relativeImagePath là bắt buộc.", nameof(request.RelativeImagePath));

            if (string.IsNullOrWhiteSpace(request.UserPrompt))
                throw new ArgumentException("userPrompt là bắt buộc.", nameof(request.UserPrompt));

           
            var fullImageUrl = request.RelativeImagePath;
            if (!Uri.IsWellFormedUriString(request.RelativeImagePath, UriKind.Absolute))
            {
                var baseUrl = _configuration["AppSettings:PublicServerUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidOperationException("AppSettings:PublicServerUrl is not configured.");

           
                fullImageUrl = $"{baseUrl.TrimEnd('/')}{request.RelativeImagePath}";
            }

            var contentList = new List<object>
            {
                new { type = "text", text = request.UserPrompt },
                new
                {
                    type = "image_url",
                    image_url = new { url = fullImageUrl }
                }
            };

            var openRouterRequest = new
            {
                model = "google/gemini-2.5-flash-image",
                messages = new[]
                {
                    new { role = "user", content = contentList }
                }
            };

            var response = await PostToOpenRouter(openRouterRequest);
            var base64Image = response.GeneratedImageUrl;

            if (string.IsNullOrWhiteSpace(base64Image))
                throw new Exception("OpenRouter did not return a valid image.");

          
            var base64Data = base64Image;
            if (base64Data.StartsWith("data:image/"))
            {
                var commaIndex = base64Data.IndexOf(',');
                if (commaIndex > 0)
                    base64Data = base64Data.Substring(commaIndex + 1);
            }

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to decode Base64 image.", ex);
            }

       
            var webRootPath = _webHostEnvironment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
                throw new InvalidOperationException("WebRootPath is not configured. Ensure wwwroot folder exists.");

            var tempFolder = Path.Combine(webRootPath, "images", "custom-baskets", "temp");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            var fileName = $"exclusive_detail_{Guid.NewGuid()}.png";
            var filePath = Path.Combine(tempFolder, fileName);

            await File.WriteAllBytesAsync(filePath, imageBytes);

          
            return $"/images/custom-baskets/temp/{fileName}";
        }

        public async Task<Guid> ConfirmCustomBasketAsync(ConfirmCustomBasketRequest request, Guid userId)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.PreviewImageUrl))
                throw new ArgumentException("PreviewImageUrl là bắt buộc.", nameof(request));

            var previewImageUrl = request.PreviewImageUrl.Trim();
            var isAbsolutePreviewUrl =
                Uri.TryCreate(previewImageUrl, UriKind.Absolute, out var previewUri) &&
                (previewUri.Scheme == Uri.UriSchemeHttp || previewUri.Scheme == Uri.UriSchemeHttps);

            var previewPath = isAbsolutePreviewUrl
                ? previewUri!.AbsolutePath
                : previewImageUrl;

            var isTempPreviewPath = previewPath.StartsWith(
                "/images/custom-baskets/temp/",
                StringComparison.OrdinalIgnoreCase);

            if (!isTempPreviewPath && !isAbsolutePreviewUrl)
                throw new ArgumentException("PreviewImageUrl không hợp lệ. Chỉ chấp nhận full URL hoặc URL từ thư mục temp.", nameof(request));

            if (request.Products == null || request.Products.Count == 0)
                throw new ArgumentException("Cần ít nhất một sản phẩm.", nameof(request));

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId);
            if (user == null)
                throw new Exception("Không tìm thấy người dùng.");

            var webRootPath = _webHostEnvironment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
                throw new InvalidOperationException("WebRootPath is not configured. Ensure wwwroot folder exists.");

            var giftBoxId = Guid.NewGuid();
            string finalImageUrl;

            if (isTempPreviewPath)
            {
                var tempFileName = Path.GetFileName(previewPath);
                var tempFilePath = Path.Combine(webRootPath, "images", "custom-baskets", "temp", tempFileName);

                if (!File.Exists(tempFilePath))
                    throw new Exception("File ảnh preview không tồn tại hoặc đã bị xóa. Vui lòng tạo lại.");

                var finalFolder = Path.Combine(webRootPath, "images", "custom-baskets", "final");
                if (!Directory.Exists(finalFolder))
                    Directory.CreateDirectory(finalFolder);

                var finalFileName = $"giftbox_{giftBoxId}.png";
                var finalFilePath = Path.Combine(finalFolder, finalFileName);

                File.Move(tempFilePath, finalFilePath);
                finalImageUrl = $"/images/custom-baskets/final/{finalFileName}";
            }
            else
            {
                finalImageUrl = previewImageUrl;
            }

            var productIds = request.Products.Select(p => p.ProductId).Distinct().ToList();
            var products = new List<Product>();
            foreach (var productId in productIds)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(productId);
                if (product != null)
                    products.Add(product);
            }

            var descriptionLines = request.Products
                .Where(p => products.Any(prod => prod.Id == p.ProductId))
                .Select(p =>
                {
                    var productName = products.First(prod => prod.Id == p.ProductId).Name;
                    return $"{productName} số lượng {p.Quantity}";
                });
            var description = string.Join(", ", descriptionLines);

           
            decimal basePrice = 0;
            foreach (var item in request.Products)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                    basePrice += product.Price * item.Quantity;
            }

            
            var giftBox = new GiftBox
            {
                Id = giftBoxId,
                Code = $"CUSTOM_{giftBoxId.ToString().Substring(0, 8).ToUpper()}",
                Name = $"Hộp quà custom của {user.Username}",
                Description = description,
                BasePrice = basePrice,
                IsActive = true,
                IsCustom = true,
                IsDraft = true,
                CategoryId = null,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.GiftBoxRepository.AddAsync(giftBox);

            var componentLines = request.Products
                .Where(p => products.Any(pr => pr.Id == p.ProductId))
                .GroupBy(p => p.ProductId)
                .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)));

            foreach (var line in componentLines)
            {
                var boxComponent = new BoxComponent
                {
                    GiftBoxId = giftBoxId,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<BoxComponent>().AddAsync(boxComponent);
            }

            var image = new Image
            {
                Id = Guid.NewGuid(),
                Url = finalImageUrl,
                IsMain = true,
                SortOrder = 1,
                GiftBoxId = giftBoxId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Image>().AddAsync(image);

            await _unitOfWork.SaveChangesAsync();

            return giftBoxId;
        }

       
        private async Task<OpenRouterResponse> PostToOpenRouter(object requestBody)
        {
            var client = _httpClientFactory.CreateClient();

            var apiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenRouter:ApiKey is not configured.");
            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var referer = _configuration["OpenRouter:Referer"] ?? "https://happybox.vn";
            client.DefaultRequestHeaders.Add("HTTP-Referer", referer);
            client.DefaultRequestHeaders.Add("X-Title", "HappyBox-Custom-Basket");

            var httpResponse = await client.PostAsJsonAsync(
                "https://openrouter.ai/api/v1/chat/completions",
                requestBody);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var body = await httpResponse.Content.ReadAsStringAsync();
                throw new Exception($"OpenRouter API call failed: {(int)httpResponse.StatusCode} - {body}");
            }

            await using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
            var response = await JsonSerializer.DeserializeAsync<OpenRouterResponse>(responseStream);

            if (response == null)
            {
                throw new Exception("OpenRouter API returned an empty response.");
            }

            return response;
        }
    }
}

