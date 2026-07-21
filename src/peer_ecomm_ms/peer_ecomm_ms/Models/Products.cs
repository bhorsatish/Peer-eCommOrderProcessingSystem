using SQLite;

namespace eComm_ms.Models
{
    /// <summary>
    /// Represents a product available in the e-commerce system.
    /// </summary>
    public partial class Products
    {
        /// <summary>
        /// Primary key identifier for the product.
        /// </summary>
        [PrimaryKey]
        public Int64 Id { get; set; }

        /// <summary>
        /// Display name of the product.
        /// </summary>
        [NotNull]
        public String Name { get; set; } = string.Empty;

        /// <summary>
        /// Price of the product in the store's currency.
        /// </summary>
        [NotNull]
        public Decimal Price { get; set; }

        /// <summary>
        /// Icon or image path that represents the product.
        /// </summary>
        [NotNull]
        public String Icon { get; set; } = string.Empty;

    }
}
