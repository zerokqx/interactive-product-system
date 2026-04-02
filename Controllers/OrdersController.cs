using DotTwo.Data;
using DotTwo.Models;
using DotTwo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(AppDbContext db, ProductionPlanningService planningService) : ControllerBase
{
  public class CreateOrderRequest
  {
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int? LineId { get; set; }
  }

  public class UpdateProgressRequest
  {
    public int Percent { get; set; }
  }

  public class UpdateOrderStatusRequest
  {
    public required string Status { get; set; }
    public int? LineId { get; set; }
  }

  public class UpdateOrderScheduleRequest
  {
    public DateTime StartDate { get; set; }
    public int? LineId { get; set; }
  }

  [HttpGet]
  public async Task<IActionResult> Index(string? status, string? date)
  {
    var query = db.WorkOrders
        .Include(w => w.Product)
        .Include(w => w.ProductionLine)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
    {
      query = status.ToLower() switch
      {
        "active" => query.Where(w => w.Status == "Pending" || w.Status == "InProgress"),
        _ => query.Where(w => w.Status == status)
      };
    }

    if (string.Equals(date, "today", StringComparison.OrdinalIgnoreCase))
    {
      var today = DateTime.UtcNow.Date;
      var tomorrow = today.AddDays(1);
      query = query.Where(w => w.StartDate >= today && w.StartDate < tomorrow);
    }

    var orders = await query
        .OrderBy(w => w.StartDate)
        .ToListAsync();

    return Ok(orders.Select(w => new
    {
      w.Id,
      w.ProductId,
      ProductName = w.Product!.Name,
      w.ProductionLineId,
      ProductionLineName = w.ProductionLine != null ? w.ProductionLine.Name : null,
      w.Quantity,
      w.StartDate,
      w.EstimatedEndDate,
      w.Status,
      ProgressPercent = planningService.CalculateProgressPercent(w.StartDate, w.EstimatedEndDate, w.Status)
    }));
  }

  [HttpPost]
  public async Task<IActionResult> Create(CreateOrderRequest request)
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

    ProductionLineModel? line = null;
    if (request.LineId.HasValue)
    {
      line = await db.ProductionLines.FindAsync(request.LineId.Value);
      if (line == null)
      {
        return NotFound($"Production line with id={request.LineId.Value} was not found");
      }

      if (line.Status != "Active" || line.CurrentWorkOrderId != null)
      {
        return BadRequest("Production line is not available");
      }
    }

    var shortages = await planningService.GetMaterialShortagesAsync(request.ProductId, request.Quantity);
    if (shortages.Count > 0)
    {
      return BadRequest(new
      {
        Message = "Insufficient materials for order creation",
        Shortages = shortages
      });
    }

    var startDate = DateTime.UtcNow;
    var productionMinutes = planningService.CalculateProductionMinutes(
        request.Quantity,
        product.ProductionTimePerUnit,
        line?.EfficiencyFactor ?? 1.0f
    );

    var order = new WorkOrderModel
    {
      ProductId = product.Id,
      ProductionLineId = line?.Id,
      Quantity = request.Quantity,
      StartDate = startDate,
      EstimatedEndDate = startDate.AddMinutes(Math.Ceiling(productionMinutes)),
      Status = line is null ? "Pending" : "InProgress"
    };

    db.WorkOrders.Add(order);
    await planningService.ConsumeMaterialsAsync(request.ProductId, request.Quantity);
    await db.SaveChangesAsync();

    if (line != null)
    {
      line.CurrentWorkOrderId = order.Id;
      await db.SaveChangesAsync();
    }

    return CreatedAtAction(nameof(Details), new { id = order.Id }, new
    {
      order.Id,
      order.ProductId,
      order.ProductionLineId,
      order.Quantity,
      order.StartDate,
      order.EstimatedEndDate,
      order.Status
    });
  }

  [HttpPut("{id}/progress")]
  public async Task<IActionResult> UpdateProgress(int id, UpdateProgressRequest request)
  {
    if (request.Percent < 0 || request.Percent > 100)
    {
      return BadRequest("Percent must be between 0 and 100");
    }

    var order = await db.WorkOrders
        .Include(w => w.ProductionLine)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (order == null)
    {
      return NotFound();
    }

    if (request.Percent == 0)
    {
      order.Status = "Pending";
    }
    else if (request.Percent < 100)
    {
      order.Status = "InProgress";
    }
    else
    {
      order.Status = "Completed";
      if (order.ProductionLine != null)
      {
        order.ProductionLine.CurrentWorkOrderId = null;
      }
    }

    await db.SaveChangesAsync();

    return Ok(new
    {
      order.Id,
      request.Percent,
      order.Status,
      ProgressPercent = planningService.CalculateProgressPercent(order.StartDate, order.EstimatedEndDate, order.Status)
    });
  }

  [HttpPut("{id}/status")]
  public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusRequest request)
  {
    if (!new[] { "Pending", "InProgress", "Completed", "Cancelled" }.Contains(request.Status))
    {
      return BadRequest("Unsupported status");
    }

    var order = await db.WorkOrders
        .Include(w => w.Product)
        .Include(w => w.ProductionLine)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (order == null)
    {
      return NotFound();
    }

    if (request.Status == "InProgress")
    {
      if (!request.LineId.HasValue)
      {
        return BadRequest("LineId is required to start the order");
      }

      var line = await db.ProductionLines.FindAsync(request.LineId.Value);
      if (line == null)
      {
        return NotFound($"Production line with id={request.LineId.Value} was not found");
      }

      if (line.Status != "Active" || (line.CurrentWorkOrderId != null && line.CurrentWorkOrderId != order.Id))
      {
        return BadRequest("Production line is not available");
      }

      if (order.ProductionLine != null && order.ProductionLine.Id != line.Id)
      {
        order.ProductionLine.CurrentWorkOrderId = null;
      }

      order.ProductionLineId = line.Id;
      order.Status = "InProgress";
      line.CurrentWorkOrderId = order.Id;

      var minutes = planningService.CalculateProductionMinutes(
          order.Quantity,
          order.Product!.ProductionTimePerUnit,
          line.EfficiencyFactor
      );
      order.EstimatedEndDate = order.StartDate.AddMinutes(Math.Ceiling(minutes));
    }
    else
    {
      order.Status = request.Status;
      if ((request.Status == "Completed" || request.Status == "Cancelled") && order.ProductionLine != null)
      {
        order.ProductionLine.CurrentWorkOrderId = null;
      }
    }

    await db.SaveChangesAsync();

    return Ok(new
    {
      order.Id,
      order.Status,
      order.ProductionLineId
    });
  }

  [HttpPut("{id}/schedule")]
  public async Task<IActionResult> UpdateSchedule(int id, UpdateOrderScheduleRequest request)
  {
    var order = await db.WorkOrders
        .Include(w => w.Product)
        .Include(w => w.ProductionLine)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (order == null)
    {
      return NotFound();
    }

    float efficiencyFactor = order.ProductionLine?.EfficiencyFactor ?? 1.0f;
    if (request.LineId.HasValue)
    {
      var line = await db.ProductionLines.FindAsync(request.LineId.Value);
      if (line == null)
      {
        return NotFound($"Production line with id={request.LineId.Value} was not found");
      }

      order.ProductionLineId = line.Id;
      efficiencyFactor = line.EfficiencyFactor;
    }

    order.StartDate = request.StartDate;
    var minutes = planningService.CalculateProductionMinutes(
        order.Quantity,
        order.Product!.ProductionTimePerUnit,
        efficiencyFactor
    );
    order.EstimatedEndDate = order.StartDate.AddMinutes(Math.Ceiling(minutes));

    await db.SaveChangesAsync();

    return Ok(new
    {
      order.Id,
      order.StartDate,
      order.EstimatedEndDate,
      order.ProductionLineId
    });
  }

  [HttpGet("{id}/details")]
  public async Task<IActionResult> Details(int id)
  {
    var order = await db.WorkOrders
        .Include(w => w.Product)
        .Include(w => w.ProductionLine)
        .FirstOrDefaultAsync(w => w.Id == id);

    if (order == null)
    {
      return NotFound();
    }

    var materials = await db.ProductMaterials
        .Where(pm => pm.ProductId == order.ProductId)
        .Include(pm => pm.Material)
        .Select(pm => new
        {
          pm.MaterialId,
          MaterialName = pm.Material.Name,
          RequiredQuantity = pm.QuantityNeeded * order.Quantity,
          UnitOfMeasure = pm.Material.UnitOfMeasure
        })
        .ToListAsync();

    return Ok(new
    {
      order.Id,
      order.Quantity,
      order.StartDate,
      order.EstimatedEndDate,
      order.Status,
      ProgressPercent = planningService.CalculateProgressPercent(order.StartDate, order.EstimatedEndDate, order.Status),
      Product = new
      {
        order.Product!.Id,
        order.Product.Name,
        order.Product.Category,
        order.Product.ProductionTimePerUnit
      },
      ProductionLine = order.ProductionLine == null ? null : new
      {
        order.ProductionLine.Id,
        order.ProductionLine.Name,
        order.ProductionLine.Status,
        order.ProductionLine.EfficiencyFactor
      },
      Materials = materials
    });
  }
}
