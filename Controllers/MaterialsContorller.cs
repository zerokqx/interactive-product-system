using DotTwo.Data;
using DotTwo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers
{

  [ApiController]
  [Route("api/[controller]")]
  public class MaterialsController(AppDbContext db) : ControllerBase
  {

    public class CreateMaterialRequest
    {
      public required string Name { get; set; }
      public decimal Quantity { get; set; }
      public required string Unit { get; set; }
      public decimal MinStock { get; set; }
    }

    public class UpdateStockRequest
    {
      public int Amount { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool low_stock = false)
    {
      var query = db.Materials.AsQueryable();

      if (low_stock)
      {
        query = query.Where(m => m.Quantity <= m.MinimalStock);
      }

      return Ok(await query.ToListAsync());
    }
    [HttpPost]
    public async Task<IActionResult> Create(CreateMaterialRequest material)
    {
      var entity = new MaterialModel
      {
        Name = material.Name,
        Quantity = material.Quantity,
        UnitOfMeasure = material.Unit,
        MinimalStock = material.MinStock
      };

      db.Materials.Add(entity);
      await db.SaveChangesAsync();
      return CreatedAtAction(nameof(Index), new { id = entity.Id }, entity);


    }

    [HttpPut("{id}/stock")]
    public async Task<IActionResult> Edit(int id, UpdateStockRequest body)
    {
      var existing = await db.Materials.FindAsync(id);
      if (existing == null) return NotFound();
      existing.Quantity = body.Amount;
      await db.SaveChangesAsync();
      return Ok(existing);
    }

  }
}
