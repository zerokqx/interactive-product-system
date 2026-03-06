using DotTwo.Models;
using Microsoft.EntityFrameworkCore;
namespace DotTwo.Data;

public class AppDbContext : DbContext
{
  public AppDbContext(DbContextOptions<AppDbContext> options)
      : base(options)
  {

  }

  public DbSet<ProductModel> Products { get; set; }
  public DbSet<ProductionLineModel> ProductionLines { get; set; }
  public DbSet<MaterialModel> Materials { get; set; }
  public DbSet<ProductMaterialModel> ProductMaterials { get; set; }
}
