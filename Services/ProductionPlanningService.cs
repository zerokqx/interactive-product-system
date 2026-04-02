using DotTwo.Data;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Services;

public class ProductionPlanningService(AppDbContext db)
{
  public sealed record MaterialRequirementResult(
      int MaterialId,
      string MaterialName,
      decimal RequiredQuantity,
      decimal AvailableQuantity,
      string UnitOfMeasure
  );

  public async Task<List<MaterialRequirementResult>> GetMaterialShortagesAsync(int productId, int quantity)
  {
    var requiredMaterials = await db.ProductMaterials
        .Where(pm => pm.ProductId == productId)
        .Include(pm => pm.Material)
        .ToListAsync();

    return requiredMaterials
        .Select(pm => new MaterialRequirementResult(
            pm.MaterialId,
            pm.Material.Name,
            pm.QuantityNeeded * quantity,
            pm.Material.Quantity,
            pm.Material.UnitOfMeasure
        ))
        .Where(x => x.AvailableQuantity < x.RequiredQuantity)
        .ToList();
  }

  public async Task ConsumeMaterialsAsync(int productId, int quantity)
  {
    var requiredMaterials = await db.ProductMaterials
        .Where(pm => pm.ProductId == productId)
        .Include(pm => pm.Material)
        .ToListAsync();

    foreach (var item in requiredMaterials)
    {
      item.Material.Quantity -= item.QuantityNeeded * quantity;
    }
  }

  public double CalculateProductionMinutes(int quantity, int productionTimePerUnit, float efficiencyFactor)
  {
    var normalizedEfficiency = efficiencyFactor <= 0 ? 1.0f : efficiencyFactor;
    return quantity * productionTimePerUnit / normalizedEfficiency;
  }

  public int CalculateProgressPercent(DateTime startDate, DateTime estimatedEndDate, string status)
  {
    if (status == "Completed")
    {
      return 100;
    }

    if (status == "Cancelled" || status == "Pending")
    {
      return 0;
    }

    var totalSeconds = (estimatedEndDate - startDate).TotalSeconds;
    if (totalSeconds <= 0)
    {
      return 0;
    }

    var elapsedSeconds = (DateTime.UtcNow - startDate).TotalSeconds;
    var ratio = Math.Clamp(elapsedSeconds / totalSeconds, 0, 1);
    return (int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero);
  }
}
