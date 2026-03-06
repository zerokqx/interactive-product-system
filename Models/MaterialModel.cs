using System.ComponentModel.DataAnnotations;

namespace DotTwo.Models
{
    public class MaterialModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите название материала")]
        [StringLength(100)]
        required public string Name { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Количество не может быть отрицательным")]
        public decimal Quantity { get; set; }

        [Required]
        [StringLength(20)]
        required public string UnitOfMeasure { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Минимальный запас не может быть отрицательным")]
        public decimal MinimalStock { get; set; }
    }
}
