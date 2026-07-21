using System;

namespace eComm_ms.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object that provides complete order details with related product, status, and user information.
    /// This is used to enhance order visibility on the UI by embedding all necessary reference data.
    /// </summary>
    public class OrderDetailsDto
    {
        // Order Information
        public Int64 OrderId { get; set; }
        public Int64 UserId { get; set; }
        public Int64 ProductId { get; set; }
        public Int64 StatusId { get; set; }
        public Int64 LastUpdatedByUserId { get; set; }
        public Int64 PaymentMode { get; set; }
        public string OrderedFor { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;

        // Timestamps
        public string LastUpdatedOn { get; set; } = string.Empty;
        public string AddedOn { get; set; } = string.Empty;
        public string OrderedOn { get; set; } = string.Empty;
        public string PackagedOn { get; set; } = string.Empty;
        public string DeliveredOn { get; set; } = string.Empty;
        public string CancelledOn { get; set; } = string.Empty;
        public string CancellationPaidOn { get; set; } = string.Empty;

        // Product Details
        public ProductDetailsDto? Product { get; set; }

        // Status Details
        public StatusDetailsDto? Status { get; set; }

        // User Details
        public UserDetailsDto? User { get; set; }

        // Updated By User Details
        public UserDetailsDto? UpdatedByUser { get; set; }
    }
}