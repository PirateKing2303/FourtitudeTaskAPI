using System.ComponentModel.DataAnnotations;

namespace FourtitudeTaskAPI
{
    public class SubmitTrxMessageRequest
    {
        [Required(ErrorMessage = "PartnerKey is required.")]
        [StringLength(50)]
        public required string PartnerKey { get; set; }

        [Required(ErrorMessage = "PartnerRefNo is required.")]
        [StringLength(50)]
        public required string PartnerRefNo { get; set; }

        [Required(ErrorMessage = "PartnerPassword is required.")]
        [StringLength(50)]
        public required string PartnerPassword { get; set; }

        [Required(ErrorMessage = "TotalAmount is required.")]
        [Range(1, long.MaxValue, ErrorMessage = "TotalAmount must be a positive value.")]
        public required long TotalAmount { get; set; } // Amount in cents

        public List<ItemDetail> Items { get; set; } = new(); // Optional, default empty list

        [Required(ErrorMessage = "Timestamp is required.")]
        public required string Timestamp { get; set; } // ISO8601 format string, e.g., "2024-08-15T02:11:22.0000000Z"

        [Required(ErrorMessage = "Sig is required.")]
        public required string Sig { get; set; } // Signature generated based on specific logic
    }

    public class SubmitTrxMessageResponse()
    {
        [Required]
        public int Result { get; set; } = 1; // Defaults to 1 - Success

        [Range(1, long.MaxValue, ErrorMessage = "TotalAmount must be a positive value.")]
        public long TotalAmount { get; set; } // Amount in cents

        [Range(1, long.MaxValue, ErrorMessage = "TotalDiscount must be a positive value.")]
        public long TotalDiscount { get; set; } // Amount in cents

        [Range(1, long.MaxValue, ErrorMessage = "FinalAmount must be a positive value.")]
        public long FinalAmount { get; set; } // Amount in cents
    }

    public class SubmitTrxMessageErrorResponse(string message)
    {
        [Required]
        public int Result { get; set; } = 0; // Defaults to 0 - Error

        public string ResultMessage { get; set; } = message;
    }
}
