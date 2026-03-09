using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotTwo.Models
{
    public class WorkOrderModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        public int? ProductionLineId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EstimatedEndDate { get; set; }

        [Required]
        [RegularExpression("Pending|InProgress|Completed|Cancelled",
            ErrorMessage = "Status must be Pending, InProgress, Completed or Cancelled")]
        public string Status { get; set; } = "Pending";

        [ForeignKey("ProductId")]
        public ProductModel? Product { get; set; }

        [ForeignKey("ProductionLineId")]
        public ProductionLineModel? ProductionLine { get; set; }
    }
}
