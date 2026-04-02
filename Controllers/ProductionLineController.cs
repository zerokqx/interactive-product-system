using DotTwo.Data;
using DotTwo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers
{
  [ApiController]
  [Route("api/lines")]
  public class ProductionLineController(AppDbContext db) : ControllerBase
  {
    public class UpdateLineRequest
    {
      public string? Status { get; set; }
      public float? EfficiencyFactor { get; set; }
    }

    public class UpdateLineStatusRequest
    {
      public required string Status { get; set; }
    }

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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateLineRequest request)
    {
      var line = await db.ProductionLines.FindAsync(id);
      if (line == null)
      {
        return NotFound();
      }

      if (!string.IsNullOrWhiteSpace(request.Status))
      {
        if (!new[] { "Active", "Stopped" }.Contains(request.Status))
        {
          return BadRequest("Status must be Active or Stopped");
        }

        line.Status = request.Status;
        if (request.Status == "Stopped")
        {
          line.CurrentWorkOrderId = null;
        }
      }

      if (request.EfficiencyFactor.HasValue)
      {
        if (request.EfficiencyFactor < 0.5f || request.EfficiencyFactor > 2.0f)
        {
          return BadRequest("EfficiencyFactor must be between 0.5 and 2.0");
        }

        line.EfficiencyFactor = request.EfficiencyFactor.Value;
      }

      await db.SaveChangesAsync();
      return Ok(line);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateLineStatusRequest request)
    {
      if (!new[] { "Active", "Stopped" }.Contains(request.Status))
      {
        return BadRequest("Status must be Active or Stopped");
      }

      var line = await db.ProductionLines.FindAsync(id);
      if (line == null)
      {
        return NotFound();
      }

      line.Status = request.Status;

      if (request.Status == "Stopped")
      {
        line.CurrentWorkOrderId = null;
      }

      await db.SaveChangesAsync();
      return Ok(line);
    }

    [HttpGet("{id}/schedule")]
    public async Task<IActionResult> Schedule(int id)
    {
      var line = await db.ProductionLines.FindAsync(id);
      if (line == null)
      {
        return NotFound();
      }

      var schedule = await db.WorkOrders
          .Where(w => w.ProductionLineId == id)
          .Include(w => w.Product)
          .OrderBy(w => w.StartDate)
          .Select(w => new
          {
            w.Id,
            w.Status,
            w.Quantity,
            w.StartDate,
            w.EstimatedEndDate,
            ProductName = w.Product!.Name
          })
          .ToListAsync();

      return Ok(new
      {
        line.Id,
        line.Name,
        line.Status,
        line.EfficiencyFactor,
        Orders = schedule
      });
    }



  }
}
