using DotTwo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers
{
  [ApiController]
  [Route("api/lines")]
  public class ProductionLineController(AppDbContext db) : ControllerBase
  {

    [HttpGet]
    public async Task<IActionResult> Index(bool available = false)
    {
      var query = db.ProductionLines.AsQueryable();

      if (available)
      {
        query = query.Where(p => p.Status == "Active" && p.CurrentWorkOrderId == null);
      }

      var lines = await query.ToListAsync();

      return Ok(lines);
    }




  }
}
