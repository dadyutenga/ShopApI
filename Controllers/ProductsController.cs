using Microsoft.AspNetCore.Mvc;
using ShopApI.DTOs;
using ShopApI.Services;

namespace ShopApI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ILogger<ProductsController> _logger;
    // private readonly IProductService _productService;

    public ProductsController(ILogger<ProductsController> logger)
    {
        _logger = logger;
        // _productService = productService;
    }

    // GET: api/products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
    {
        _logger.LogInformation("Getting all products");
        // var products = await _productService.GetAllProductsAsync();
        // return Ok(products);
        return Ok(new List<ProductDto>());
    }

    // GET: api/products/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        _logger.LogInformation("Getting product with id: {Id}", id);
        // var product = await _productService.GetProductByIdAsync(id);
        // if (product == null)
        //     return NotFound();
        // return Ok(product);
        return NotFound();
    }

    // POST: api/products
    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(CreateProductDto createProductDto)
    {
        _logger.LogInformation("Creating new product");
        // var product = await _productService.CreateProductAsync(createProductDto);
        // return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        return Ok();
    }

    // PUT: api/products/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, UpdateProductDto updateProductDto)
    {
        _logger.LogInformation("Updating product with id: {Id}", id);
        // var result = await _productService.UpdateProductAsync(id, updateProductDto);
        // if (!result)
        //     return NotFound();
        // return NoContent();
        return NoContent();
    }

    // DELETE: api/products/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        _logger.LogInformation("Deleting product with id: {Id}", id);
        // var result = await _productService.DeleteProductAsync(id);
        // if (!result)
        //     return NotFound();
        // return NoContent();
        return NoContent();
    }
}
