using DotTwo.Data;
using DotTwo.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotTwo.Controllers
{

  [ApiController]
  [Route("api/[controller]")]
  public class MaterialsController(AppDbContext db) : ControllerBase
  {



    public class UpdateStockRequest
    {
      public int Amount { get; set; }
    }

    [HttpGet]
    public IActionResult Index()
    {
      return Ok(db.Materials.ToList());
    }
    [HttpPost]
    public IActionResult Create(MaterialModel material)
    {
      db.Materials.Add(material);
      db.SaveChanges();
      return Ok(material);


    }

    [HttpPut("{id}/stock")]
    public IActionResult Edit(int id, UpdateStockRequest body)
    {
      var existing = db.Materials.Find(id);
      if (existing == null) return NotFound();
      existing.MinimalStock = body.Amount;
      db.SaveChanges();
      return Ok();
    }

  }
}
