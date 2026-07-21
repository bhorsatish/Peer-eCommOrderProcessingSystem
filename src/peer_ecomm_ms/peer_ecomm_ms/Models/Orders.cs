using SQLite;

namespace eComm_ms.Models
{
    public class Orders
    {
        [PrimaryKey]
        public Int64 Id { get; set; }
        public Int64 UserId { get; set; }
        public Int64 ProductId { get; set; }
        public Int64 StatusId { get; set; }
        public Int64 LastUpdatedByUserId { get; set; }
        public Int64 PaymentMode { get; set; }
        public string LastUpdatedOn { get; set; } = string.Empty;
        public string AddedOn { get; set; } = string.Empty;
        public string OrderedOn { get; set; } = string.Empty;
        public string PackagedOn { get; set; } = string.Empty;
        public string DeliveredOn { get; set; } = string.Empty;
        public string CancelledOn { get; set; } = string.Empty;
        public string CancellationPaidOn { get; set; } = string.Empty;
        public string OrderedFor { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
    }
}
