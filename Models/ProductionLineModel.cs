using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotTwo.Models
{
    public class ProductionLineModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Production Line Name")]
        public required string Name { get; set; }

        [Required]
        [RegularExpression("Active|Stopped", ErrorMessage = "Status must be Active or Stopped")]
        public required string Status { get; set; }

        [Range(0.5, 2.0)]
        public float EfficiencyFactor { get; set; }

        public int? CurrentWorkOrderId { get; set; }

        [ForeignKey("CurrentWorkOrderId")]
        public WorkOrderModel? CurrentWorkOrder { get; set; }
    }
}
