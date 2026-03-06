using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DotTwo.Models
{

  [Owned]
  public class ProductSpecifications

  {
    required public string SpecFirtst { get; set; } = "";
  }
  public class ProductModel
  {
    public int Id {get;set;}
    [Required]
    required public string Name { get; set; }
    public string Description { get; set; } = "";
    required public ProductSpecifications Specifications { get; set; }
    required public string Category { get; set; }
    required public int MinimalStock { get; set; }
    required public int ProductionTimePerUnit { get; set; }
  }

}
