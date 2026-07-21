namespace eComm_ms.Models.DTOs
{
    /// <summary>
    /// User details to be embedded in OrderDetailsDto (password is excluded for security).
    /// </summary>
    public class UserDetailsDto
    {
        public Int64 Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public Int64 RoleId { get; set; }
    }
}
