using ShopApI.DTOs;

namespace ShopApI.Services;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<ProductDto> CreateProductAsync(CreateProductDto createProductDto);
    Task<bool> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
    Task<bool> DeleteProductAsync(int id);
}
