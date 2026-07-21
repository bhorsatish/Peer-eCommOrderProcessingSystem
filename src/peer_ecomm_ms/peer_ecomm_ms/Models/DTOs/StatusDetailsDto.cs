namespace eComm_ms.Models.DTOs
{
    /// <summary>
    /// Status details to be embedded in OrderDetailsDto.
    /// </summary>
    public class StatusDetailsDto
    {
        public Int64 Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
