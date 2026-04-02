using DotTwo.Data;
using DotTwo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers
{

  [ApiController]
  [Route("api/[controller]")]
  public class ProductsController(AppDbContext db) : ControllerBase
  {
    public class ProductMaterialInput
    {
      public int MaterialId { get; set; }
      public decimal QuantityNeeded { get; set; }
    }

    public class CreateProductRequest
    {
      public required string Name { get; set; }
      public int ProdTime { get; set; }
      public required string Category { get; set; }
      public string Description { get; set; } = "";
      public int MinimalStock { get; set; }
      public string SpecFirst { get; set; } = "";
    }


    [HttpGet]
    public async Task<IActionResult> Index(string? category)
    {
      var query = db.Products.AsQueryable();

      if (!string.IsNullOrWhiteSpace(category))
      {
        query = query.Where(p => p.Category == category);
      }

      return Ok(await query.ToListAsync());
    }
    [HttpGet("{id}/materials")]
    public async Task<IActionResult> Edit(int id)
    {

      var productExists = await db.Products.AnyAsync(p => p.Id == id);
      if (!productExists)
        return NotFound($"Продукт с id={id} не найден");

      var materials = await db.ProductMaterials
          .Where(pm => pm.ProductId == id)
          .Include(pm => pm.Material)
          .Select(pm => new
          {
            pm.Id,
            pm.ProductId,
            pm.MaterialId,
            pm.QuantityNeeded,
            MaterialName = pm.Material.Name
          })
          .ToListAsync();

      return Ok(materials);

    }



    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
      var product = db.Products.Find(id);

      if (product == null)
        return NotFound();

      return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest product)
    {
      var entity = new ProductModel
      {
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        MinimalStock = product.MinimalStock,
        ProductionTimePerUnit = product.ProdTime,
        Specifications = new ProductSpecifications
        {
          SpecFirtst = product.SpecFirst
        }
      };

      db.Products.Add(entity);
      await db.SaveChangesAsync();

      return CreatedAtAction(
          nameof(GetById),
          new { id = entity.Id },
          entity
      );
    }

    [HttpPost("{id}/materials")]
    public async Task<IActionResult> SetMaterials(int id, List<ProductMaterialInput> materials)
    {
      var product = await db.Products.FindAsync(id);
      if (product == null)
      {
        return NotFound($"Product with id={id} was not found");
      }

      var materialIds = materials.Select(m => m.MaterialId).Distinct().ToList();
      var existingMaterialIds = await db.Materials
          .Where(m => materialIds.Contains(m.Id))
          .Select(m => m.Id)
          .ToListAsync();

      var missingMaterialIds = materialIds.Except(existingMaterialIds).ToList();
      if (missingMaterialIds.Count > 0)
      {
        return BadRequest(new
        {
          Message = "Some materials were not found",
          MissingMaterialIds = missingMaterialIds
        });
      }

      var existingLinks = await db.ProductMaterials
          .Where(pm => pm.ProductId == id)
          .ToListAsync();

      db.ProductMaterials.RemoveRange(existingLinks);

      var allMaterials = await db.Materials
          .Where(m => materialIds.Contains(m.Id))
          .ToDictionaryAsync(m => m.Id);

      var newLinks = materials
          .Where(m => m.QuantityNeeded > 0)
          .Select(m => new ProductMaterialModel
          {
            ProductId = id,
            MaterialId = m.MaterialId,
            QuantityNeeded = m.QuantityNeeded,
            Product = product,
            Material = allMaterials[m.MaterialId]
          })
          .ToList();

      db.ProductMaterials.AddRange(newLinks);
      await db.SaveChangesAsync();

      return Ok(newLinks.Select(link => new
      {
        link.ProductId,
        link.MaterialId,
        link.QuantityNeeded
      }));
    }
  }
}
