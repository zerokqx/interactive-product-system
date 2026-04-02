using DotTwo.Data;
using DotTwo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers;

[ApiController]
[Route("api/calculate")]
public class CalculateController(AppDbContext db, ProductionPlanningService planningService) : ControllerBase
{
  public class CalculateProductionRequest
  {
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int? LineId { get; set; }
  }

  [HttpPost("production")]
  public async Task<IActionResult> Production(CalculateProductionRequest request)
  {
    if (request.Quantity <= 0)
    {
      return BadRequest("Quantity must be greater than 0");
    }

    var product = await db.Products.FindAsync(request.ProductId);
    if (product == null)
    {
      return NotFound($"Product with id={request.ProductId} was not found");
    }

    var efficiencyFactor = 1.0f;
    if (request.LineId.HasValue)
    {
      var line = await db.ProductionLines.FirstOrDefaultAsync(l => l.Id == request.LineId.Value);
      if (line == null)
      {
        return NotFound($"Production line with id={request.LineId.Value} was not found");
      }

      efficiencyFactor = line.EfficiencyFactor;
    }

    var minutes = planningService.CalculateProductionMinutes(
        request.Quantity,
        product.ProductionTimePerUnit,
        efficiencyFactor
    );

    return Ok(new
    {
      request.ProductId,
      request.Quantity,
      EfficiencyFactor = efficiencyFactor,
      ProductionTimeMinutes = Math.Ceiling(minutes)
    });
  }
}
