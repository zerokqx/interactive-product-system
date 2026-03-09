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


    [HttpGet]
    public IActionResult Index(int category)
    {
      return Ok(category);
    }
    [HttpGet("{id}/materials")]
    public IActionResult Edit(int id)
    {

      var productExists = db.Products.Any(p => p.Id == id);
      if (!productExists)
        return NotFound($"Продукт с id={id} не найден");

      var materials = db.ProductMaterials
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
          .ToList();

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
    public IActionResult Create(ProductModel product)
    {
      db.Products.Add(product);
      db.SaveChanges();

      return CreatedAtAction(
          nameof(GetById),
          new { id = product.Id },
          product
      );
    }
  }
}
