namespace eComm_ms.Models.DTOs
{
    /// <summary>
    /// Product details to be embedded in OrderDetailsDto.
    /// </summary>
    public class ProductDetailsDto
    {
        public Int64 Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
}
