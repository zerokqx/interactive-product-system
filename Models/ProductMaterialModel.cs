using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotTwo.Models
{
  public class ProductMaterialModel
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int MaterialId { get; set; }

    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Количество должно быть больше 0")]
    public decimal QuantityNeeded { get; set; }

    [ForeignKey("ProductId")]
    required public ProductModel Product { get; set; }

    [ForeignKey("MaterialId")]
    required public MaterialModel Material { get; set; }
  }
}
