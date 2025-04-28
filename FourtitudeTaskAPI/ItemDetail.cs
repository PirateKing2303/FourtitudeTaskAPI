using System.ComponentModel.DataAnnotations;

namespace FourtitudeTaskAPI
{
    public class ItemDetail
    {
        [Required(ErrorMessage = "PartnerItemRef is required.")]
        [StringLength(50, ErrorMessage = "PartnerItemRef cannot exceed 50 characters.")]
        public required string PartnerItemRef { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public required string Name { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, 5, ErrorMessage = "Quantity must be between 1 and 5.")]
        public required int Qty { get; set; }

        [Required(ErrorMessage = "UnitPrice is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "UnitPrice must be a positive value.")]
        public required long UnitPrice { get; set; } // Amount in cents
    }
}
