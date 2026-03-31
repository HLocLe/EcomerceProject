using Application.DTOs.Request.Product;
using Application.DTOs.Response.Product;
using AutoMapper;
using Domain.IUnitOfWork;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.Service.Product
{
    using CategoryEntity = Domain.Entities.Category;

    public class ProductService : IProductService
    {
        private const string AllProductsCacheKey = "products:all";
        private static readonly TimeSpan AllProductsCacheTtl = TimeSpan.FromMinutes(30);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;

        public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IDistributedCache cache)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<IEnumerable<ProductResponse>> GetAllProductsAsync()
        {
            try
            {
                var cached = await _cache.GetStringAsync(AllProductsCacheKey);
                if (!string.IsNullOrEmpty(cached))
                {
                    var list = JsonSerializer.Deserialize<List<ProductResponse>>(cached);
                    if (list != null)
                        return list;
                }
            }
            catch
            {
                // Redis lỗi hoặc payload cũ không đọc được — lấy từ DB
            }

            var products = await _unitOfWork.ProductRepository.FindAsync(
                filter: p => !p.IsDeleted,
                includeProperties: "Category,Images,Inventories"
            );

            var mapped = _mapper.Map<List<ProductResponse>>(products);

            try
            {
                var opts = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = AllProductsCacheTtl
                };
                await _cache.SetStringAsync(
                    AllProductsCacheKey,
                    JsonSerializer.Serialize(mapped),
                    opts);
            }
            catch
            {
                // Ghi cache lỗi vẫn trả dữ liệu từ DB
            }

            return mapped;
        }

        public async Task<ProductResponse?> GetProductByIdAsync(Guid id)
        {
            var product = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.Id == id && !p.IsDeleted,
                includeProperties: "Category,Images,Inventories"
            );

            if (product == null)
                return null;

            return _mapper.Map<ProductResponse>(product);
        }

        public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request)
        {
            // 1. Check if SKU already exists 
            var existingProduct = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.SKU == request.SKU && !p.IsDeleted
            );

            if (existingProduct != null)
                throw new InvalidOperationException($"Product with SKU '{request.SKU}' already exists.");

            // 2. Check if Category exists 
            var categoryRepo = _unitOfWork.Repository<Domain.Entities.Category>();
            var category = await categoryRepo.GetByIdAsync(request.CategoryId);

            if (category == null || category.IsDeleted)
                throw new InvalidOperationException($"Category with ID '{request.CategoryId}' not found.");

            // 3. Mapping data
            var product = _mapper.Map<Domain.Entities.Product>(request);
            product.Id = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;

            // 4. Handle Images 
            product.Images = new List<Domain.Entities.Image>();
            if (request.ImageUrls != null && request.ImageUrls.Any())
            {
                int sortOrder = 1; // Khởi tạo thứ tự ảnh
                foreach (var url in request.ImageUrls)
                {
                    product.Images.Add(new Domain.Entities.Image
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Url = url,
                        // Nếu là ảnh đầu tiên (sortOrder == 1) thì IsMain = true, còn lại là false
                        IsMain = (sortOrder == 1),
                        SortOrder = sortOrder++, // Gán xong thì tăng sortOrder lên 1
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // 5. save to database
            await _unitOfWork.ProductRepository.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            await InvalidateAllProductsCacheAsync();

            // 6. Reload with Category, Images for response 
            var createdProduct = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.Id == product.Id,
                includeProperties: "Category,Images,Inventories"
            );

            return _mapper.Map<ProductResponse>(createdProduct);
        }

        public async Task<ProductResponse?> UpdateProductAsync(Guid id, UpdateProductRequest request)
        {
            var product = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.Id == id && !p.IsDeleted
            );

            if (product == null)
                return null;

            // Check if SKU already exists for another product
            var existingProduct = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.SKU == request.SKU && p.Id != id && !p.IsDeleted
            );

            if (existingProduct != null)
                throw new InvalidOperationException($"Product with SKU '{request.SKU}' already exists.");

            // Check if Category exists
            var category = await _unitOfWork.Repository<CategoryEntity>().GetByIdAsync(request.CategoryId);
            if (category == null || category.IsDeleted)
                throw new InvalidOperationException($"Category with ID '{request.CategoryId}' not found.");

            _mapper.Map(request, product);
            product.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ProductRepository.Update(product);
            await _unitOfWork.SaveChangesAsync();

            await InvalidateAllProductsCacheAsync();

            // Reload with Category for response
            var updatedProduct = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.Id == id,
                includeProperties: "Category,Images,Inventories"
            );

            return _mapper.Map<ProductResponse>(updatedProduct);
        }

        public async Task<bool> DeleteProductAsync(Guid id)
        {
            var product = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                filter: p => p.Id == id && !p.IsDeleted
            );

            if (product == null)
                return false;

            // Soft delete
            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ProductRepository.Update(product);
            await _unitOfWork.SaveChangesAsync();

            await InvalidateAllProductsCacheAsync();

            return true;
        }

        private async Task InvalidateAllProductsCacheAsync()
        {
            try
            {
                await _cache.RemoveAsync(AllProductsCacheKey);
            }
            catch
            {
                // bỏ qua nếu Redis không sẵn sàng
            }
        }
    }
}
