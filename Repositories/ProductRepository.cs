using ShopApI.Models;
// using ShopApI.Data;

namespace ShopApI.Repositories;

public class ProductRepository : IProductRepository
{
    // private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(ILogger<ProductRepository> logger)
    {
        // _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        _logger.LogInformation("Getting all products from repository");
        // return await _context.Products.ToListAsync();
        return await Task.FromResult(new List<Product>());
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting product {Id} from repository", id);
        // return await _context.Products.FindAsync(id);
        return await Task.FromResult<Product?>(null);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        _logger.LogInformation("Creating product in repository");
        // _context.Products.Add(product);
        // await _context.SaveChangesAsync();
        // return product;
        return await Task.FromResult(product);
    }

    public async Task UpdateAsync(Product product)
    {
        _logger.LogInformation("Updating product {Id} in repository", product.Id);
        // _context.Products.Update(product);
        // await _context.SaveChangesAsync();
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting product {Id} from repository", id);
        // var product = await _context.Products.FindAsync(id);
        // if (product != null)
        // {
        //     _context.Products.Remove(product);
        //     await _context.SaveChangesAsync();
        // }
        await Task.CompletedTask;
    }
}
