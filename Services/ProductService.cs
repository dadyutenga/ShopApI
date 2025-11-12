using ShopApI.DTOs;
using ShopApI.Models;
using ShopApI.Repositories;

namespace ShopApI.Services;

public class ProductService : IProductService
{
    // private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(ILogger<ProductService> logger)
    {
        // _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        _logger.LogInformation("Getting all products from service");
        // var products = await _productRepository.GetAllAsync();
        // return products.Select(MapToDto);
        return await Task.FromResult(new List<ProductDto>());
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        _logger.LogInformation("Getting product {Id} from service", id);
        // var product = await _productRepository.GetByIdAsync(id);
        // return product != null ? MapToDto(product) : null;
        return await Task.FromResult<ProductDto?>(null);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto)
    {
        _logger.LogInformation("Creating product from service");
        // var product = new Product
        // {
        //     Name = createProductDto.Name,
        //     Description = createProductDto.Description,
        //     Price = createProductDto.Price,
        //     Stock = createProductDto.Stock,
        //     Category = createProductDto.Category,
        //     CreatedAt = DateTime.UtcNow
        // };
        // var createdProduct = await _productRepository.CreateAsync(product);
        // return MapToDto(createdProduct);
        return await Task.FromResult(new ProductDto());
    }

    public async Task<bool> UpdateProductAsync(int id, UpdateProductDto updateProductDto)
    {
        _logger.LogInformation("Updating product {Id} from service", id);
        // var product = await _productRepository.GetByIdAsync(id);
        // if (product == null)
        //     return false;
        
        // product.Name = updateProductDto.Name;
        // product.Description = updateProductDto.Description;
        // product.Price = updateProductDto.Price;
        // product.Stock = updateProductDto.Stock;
        // product.Category = updateProductDto.Category;
        // product.UpdatedAt = DateTime.UtcNow;
        
        // await _productRepository.UpdateAsync(product);
        // return true;
        return await Task.FromResult(false);
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        _logger.LogInformation("Deleting product {Id} from service", id);
        // var product = await _productRepository.GetByIdAsync(id);
        // if (product == null)
        //     return false;
        
        // await _productRepository.DeleteAsync(id);
        // return true;
        return await Task.FromResult(false);
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            Category = product.Category
        };
    }
}
