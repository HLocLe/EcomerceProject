using Application.DTOs.Request.GiftBox;
using Application.DTOs.Response.GiftBox;
using AutoMapper;
using Domain.Entities;
using Domain.IUnitOfWork;

namespace Application.Service.GiftBox
{
    using GiftBoxEntity = Domain.Entities.GiftBox;
    using CategoryEntity = Domain.Entities.Category;
    using GiftBoxComponentConfigEntity = Domain.Entities.GiftBoxComponentConfig;
    using ImageEntity = Domain.Entities.Image;
    using InventoryTransactionEntity = Domain.Entities.InventoryTransaction;

    public class GiftBoxService : IGiftBoxService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GiftBoxService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GiftBoxResponse>> GetAllGiftBoxesAsync()
        {
            var giftBoxes = await _unitOfWork.GiftBoxRepository.FindAsync(
                filter: g => !g.IsDeleted,
                includeProperties: "Category,ComponentConfig,BoxComponents.Product,Images"
            );

            return _mapper.Map<IEnumerable<GiftBoxResponse>>(giftBoxes);
        }

        public async Task<GiftBoxResponse?> GetGiftBoxByIdAsync(Guid id)
        {
            var giftBox = await _unitOfWork.GiftBoxRepository.GetFirstOrDefaultAsync(
                filter: g => g.Id == id && !g.IsDeleted,
                includeProperties: "Category,ComponentConfig,BoxComponents.Product,Images"
            );

            if (giftBox == null)
                return null;

            return _mapper.Map<GiftBoxResponse>(giftBox);
        }

        public async Task<GiftBoxResponse?> GetGiftBoxByCodeAsync(string code)
        {
            var giftBox = await _unitOfWork.GiftBoxRepository.GetByCodeAsync(code);

            if (giftBox == null)
                return null;

            return _mapper.Map<GiftBoxResponse>(giftBox);
        }

        public async Task<IEnumerable<GiftBoxResponse>> GetGiftBoxesByCategoryAsync(Guid categoryId)
        {
            var giftBoxes = await _unitOfWork.GiftBoxRepository.GetByCategoryAsync(categoryId);
            return _mapper.Map<IEnumerable<GiftBoxResponse>>(giftBoxes);
        }

        public async Task<IEnumerable<GiftBoxResponse>> GetActiveGiftBoxesAsync()
        {
            var giftBoxes = await _unitOfWork.GiftBoxRepository.GetActiveGiftBoxesAsync();
            return _mapper.Map<IEnumerable<GiftBoxResponse>>(giftBoxes);
        }

        public async Task<IEnumerable<GiftBoxResponse>> GetGiftBoxesByUserIdAsync(Guid userId)
        {
            var giftBoxes = await _unitOfWork.GiftBoxRepository.GetGiftBoxesByUserIdAsync(userId);
            return _mapper.Map<IEnumerable<GiftBoxResponse>>(giftBoxes);
        }

        public async Task<GiftBoxResponse> CreateGiftBoxAsync(CreateGiftBoxRequest request)
        {
            // Check if Code already exists
            if (await _unitOfWork.GiftBoxRepository.CodeExistsAsync(request.Code))
            {
                throw new InvalidOperationException($"GiftBox with Code '{request.Code}' already exists.");
            }

            // Check if Category exists
            var category = await _unitOfWork.Repository<CategoryEntity>().GetByIdAsync(request.CategoryId);
            if (category == null || category.IsDeleted)
            {
                throw new InvalidOperationException($"Category with ID '{request.CategoryId}' not found.");
            }

            // Validate GiftBoxComponentConfig only when client sends a config id
            if (request.GiftBoxComponentConfigId.HasValue)
            {
                var componentConfig = await _unitOfWork.Repository<GiftBoxComponentConfigEntity>().GetByIdAsync(request.GiftBoxComponentConfigId.Value);
                if (componentConfig == null || componentConfig.IsDeleted)
                {
                    throw new InvalidOperationException($"GiftBoxComponentConfig with ID '{request.GiftBoxComponentConfigId}' not found.");
                }
            }

            // Start transaction
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // 1. Create GiftBox
                var giftBox = _mapper.Map<GiftBoxEntity>(request);
                giftBox.CreatedAt = DateTime.UtcNow;

                await _unitOfWork.GiftBoxRepository.AddAsync(giftBox);
                await _unitOfWork.SaveChangesAsync();

                // 2. Process BoxComponents and update Inventory
                if (request.Items != null && request.Items.Any())
                {
                    foreach (var item in request.Items)
                    {
                        // Validate Product exists and is active
                        var product = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                            filter: p => p.Id == item.ProductId && !p.IsDeleted,
                            includeProperties: "Inventories"
                        );

                        if (product == null || !product.IsActive)
                        {
                            throw new InvalidOperationException($"Product with ID '{item.ProductId}' not found or is inactive.");
                        }

                        // Check and update Inventory
                        var inventory = product.Inventories.FirstOrDefault();
                        if (inventory == null)
                        {
                            throw new InvalidOperationException($"No inventory found for Product '{product.Name}' (ID: {product.Id}).");
                        }

                        if (inventory.Quantity < item.Quantity)
                        {
                            throw new InvalidOperationException($"Insufficient stock for Product '{product.Name}'. Available: {inventory.Quantity}, Required: {item.Quantity}.");
                        }

                        // Deduct inventory
                        inventory.Quantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.UtcNow;

                        // Update inventory status based on quantity
                        if (inventory.Quantity == 0)
                        {
                            inventory.Status = Domain.Enums.InventoryStatus.OutOfStock;
                        }
                        else if (inventory.Quantity <= inventory.MinStockLevel)
                        {
                            inventory.Status = Domain.Enums.InventoryStatus.LowStock;
                        }

                        _unitOfWork.Repository<Inventory>().Update(inventory);

                        // Create BoxComponent
                        var boxComponent = new BoxComponent
                        {
                            GiftBoxId = giftBox.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<BoxComponent>().AddAsync(boxComponent);

                        // Create InventoryTransaction for CREATE operation (+quantity)
                        var transaction = new InventoryTransactionEntity
                        {
                            InventoryId = inventory.Id,
                            QuantityChange = -item.Quantity, // Negative because we deducted
                            TransactionType = "Transfer",
                            ReferenceId = giftBox.Id.ToString(),
                            Note = $"GiftBox '{giftBox.Code}' created - Product used",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<InventoryTransactionEntity>().AddAsync(transaction);
                    }
                }

                // 3. Add Images if provided
                if (request.ImageUrls != null && request.ImageUrls.Any())
                {
                    var uniqueImageUrls = GetDistinctImageUrls(request.ImageUrls);
                    int sortOrder = 0;
                    foreach (var imageUrl in uniqueImageUrls)
                    {
                        var image = new ImageEntity
                        {
                            Url = imageUrl,
                            IsMain = sortOrder == 0, // First image is main
                            SortOrder = sortOrder,
                            GiftBoxId = giftBox.Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<ImageEntity>().AddAsync(image);
                        sortOrder++;
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // Reload with related entities for response
                var createdGiftBox = await _unitOfWork.GiftBoxRepository.GetFirstOrDefaultAsync(
                    filter: g => g.Id == giftBox.Id,
                    includeProperties: "Category,ComponentConfig,BoxComponents.Product,Images"
                );

                return _mapper.Map<GiftBoxResponse>(createdGiftBox);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<GiftBoxResponse?> UpdateGiftBoxAsync(Guid id, UpdateGiftBoxRequest request)
        {
            var giftBox = await _unitOfWork.GiftBoxRepository.GetFirstOrDefaultAsync(
                filter: g => g.Id == id && !g.IsDeleted,
                includeProperties: "BoxComponents,Images"
            );

            if (giftBox == null)
                return null;

            // Check if Code already exists for another GiftBox
            if (await _unitOfWork.GiftBoxRepository.CodeExistsAsync(request.Code, id))
            {
                throw new InvalidOperationException($"GiftBox with Code '{request.Code}' already exists.");
            }

            // Check if Category exists
            var category = await _unitOfWork.Repository<CategoryEntity>().GetByIdAsync(request.CategoryId);
            if (category == null || category.IsDeleted)
            {
                throw new InvalidOperationException($"Category with ID '{request.CategoryId}' not found.");
            }

            // Validate GiftBoxComponentConfig only when client sends a config id
            if (request.GiftBoxComponentConfigId.HasValue)
            {
                var componentConfig = await _unitOfWork.Repository<GiftBoxComponentConfigEntity>().GetByIdAsync(request.GiftBoxComponentConfigId.Value);
                if (componentConfig == null || componentConfig.IsDeleted)
                {
                    throw new InvalidOperationException($"GiftBoxComponentConfig with ID '{request.GiftBoxComponentConfigId}' not found.");
                }
            }
            else
            {
                giftBox.GiftBoxComponentConfigId = null;
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Update basic info
                _mapper.Map(request, giftBox);
                giftBox.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.GiftBoxRepository.Update(giftBox);

                // Update BoxComponents if provided (null = không thay ??i, empty = xóa h?t)
                if (request.Items != null)
                {
                    // Hoàn tr? inventory t? các BoxComponent c?
                    var existingComponents = giftBox.BoxComponents.Where(bc => !bc.IsDeleted).ToList();
                    foreach (var oldComponent in existingComponents)
                    {
                        var inventory = (await _unitOfWork.Repository<Inventory>().FindAsync(
                            filter: inv => inv.ProductId == oldComponent.ProductId && !inv.IsDeleted
                        )).FirstOrDefault();

                        if (inventory != null)
                        {
                            inventory.Quantity += oldComponent.Quantity;
                            inventory.LastUpdated = DateTime.UtcNow;
                            
                            // Update status
                            if (inventory.Quantity > inventory.MinStockLevel)
                                inventory.Status = Domain.Enums.InventoryStatus.InStock;
                            else if (inventory.Quantity > 0)
                                inventory.Status = Domain.Enums.InventoryStatus.LowStock;

                            _unitOfWork.Repository<Inventory>().Update(inventory);

                            // Create InventoryTransaction for returning old quantity
                            var returnTransaction = new InventoryTransactionEntity
                            {
                                InventoryId = inventory.Id,
                                QuantityChange = +oldComponent.Quantity, // Positive because we returned
                                TransactionType = "Return",
                                ReferenceId = giftBox.Id.ToString(),
                                Note = $"GiftBox '{giftBox.Code}' updated - Product returned",
                                CreatedBy = "System",
                                CreatedAt = DateTime.UtcNow
                            };

                            await _unitOfWork.Repository<InventoryTransactionEntity>().AddAsync(returnTransaction);
                        }

                        // Soft delete old component
                        oldComponent.IsDeleted = true;
                        oldComponent.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Repository<BoxComponent>().Update(oldComponent);
                    }

                    // Thêm BoxComponents m?i
                    foreach (var item in request.Items)
                    {
                        var product = await _unitOfWork.ProductRepository.GetFirstOrDefaultAsync(
                            filter: p => p.Id == item.ProductId && !p.IsDeleted,
                            includeProperties: "Inventories"
                        );

                        if (product == null || !product.IsActive)
                        {
                            throw new InvalidOperationException($"Product with ID '{item.ProductId}' not found or is inactive.");
                        }

                        var inventory = product.Inventories.FirstOrDefault();
                        if (inventory == null)
                        {
                            throw new InvalidOperationException($"No inventory found for Product '{product.Name}' (ID: {product.Id}).");
                        }

                        if (inventory.Quantity < item.Quantity)
                        {
                            throw new InvalidOperationException($"Insufficient stock for Product '{product.Name}'. Available: {inventory.Quantity}, Required: {item.Quantity}.");
                        }

                        // Tr? inventory
                        inventory.Quantity -= item.Quantity;
                        inventory.LastUpdated = DateTime.UtcNow;

                        if (inventory.Quantity == 0)
                            inventory.Status = Domain.Enums.InventoryStatus.OutOfStock;
                        else if (inventory.Quantity <= inventory.MinStockLevel)
                            inventory.Status = Domain.Enums.InventoryStatus.LowStock;

                        _unitOfWork.Repository<Inventory>().Update(inventory);

                        // T?o BoxComponent m?i
                        var boxComponent = new BoxComponent
                        {
                            GiftBoxId = giftBox.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<BoxComponent>().AddAsync(boxComponent);

                        // Create InventoryTransaction for deducting new quantity
                        var deductTransaction = new InventoryTransactionEntity
                        {
                            InventoryId = inventory.Id,
                            QuantityChange = -item.Quantity, // Negative because we deducted
                            TransactionType = "Transfer",
                            ReferenceId = giftBox.Id.ToString(),
                            Note = $"GiftBox '{giftBox.Code}' updated - Product used",
                            CreatedBy = "System",
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<InventoryTransactionEntity>().AddAsync(deductTransaction);
                    }
                }

                // Update Images if provided (null = không thay ??i, empty = xóa h?t)
                if (request.ImageUrls != null)
                {
                    var uniqueImageUrls = GetDistinctImageUrls(request.ImageUrls);

                    // Soft delete old images
                    var existingImages = giftBox.Images.Where(img => !img.IsDeleted).ToList();
                    foreach (var oldImage in existingImages)
                    {
                        oldImage.IsDeleted = true;
                        oldImage.UpdatedAt = DateTime.UtcNow;
                        _unitOfWork.Repository<ImageEntity>().Update(oldImage);
                    }

                    // Add new images
                    int sortOrder = 0;
                    foreach (var imageUrl in uniqueImageUrls)
                    {
                        var image = new ImageEntity
                        {
                            Url = imageUrl,
                            IsMain = sortOrder == 0,
                            SortOrder = sortOrder,
                            GiftBoxId = giftBox.Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<ImageEntity>().AddAsync(image);
                        sortOrder++;
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // Reload with related entities for response
                var updatedGiftBox = await _unitOfWork.GiftBoxRepository.GetFirstOrDefaultAsync(
                    filter: g => g.Id == id,
                    includeProperties: "Category,ComponentConfig,BoxComponents.Product,Images"
                );

                return _mapper.Map<GiftBoxResponse>(updatedGiftBox);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<bool> DeleteGiftBoxAsync(Guid id)
        {
            var giftBox = await _unitOfWork.GiftBoxRepository.GetFirstOrDefaultAsync(
                filter: g => g.Id == id && !g.IsDeleted
            );

            if (giftBox == null)
                return false;

            // Soft delete
            giftBox.IsDeleted = true;
            giftBox.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.GiftBoxRepository.Update(giftBox);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private static IReadOnlyList<string> GetDistinctImageUrls(IEnumerable<string> imageUrls)
        {
            var normalizedImageUrls = new List<string>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imageUrl in imageUrls)
            {
                var normalizedUrl = imageUrl?.Trim();

                if (string.IsNullOrWhiteSpace(normalizedUrl))
                {
                    throw new InvalidOperationException("ImageUrls cannot contain empty values.");
                }

                if (!seenUrls.Add(normalizedUrl))
                {
                    duplicateUrls.Add(normalizedUrl);
                    continue;
                }

                normalizedImageUrls.Add(normalizedUrl);
            }

            if (duplicateUrls.Any())
            {
                throw new InvalidOperationException(
                    $"ImageUrls contains duplicate values: {string.Join(", ", duplicateUrls)}."
                );
            }

            return normalizedImageUrls;
        }
    }
}
