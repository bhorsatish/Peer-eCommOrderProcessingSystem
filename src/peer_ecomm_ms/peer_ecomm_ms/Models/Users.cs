using SQLite;
using System.Text.Json.Serialization;

namespace eComm_ms.Models
{
    public class Users
    {
        [PrimaryKey]
        public Int64 Id { get; set; }

        [NotNull]
        public String UserId { get; set; } = string.Empty;

        [NotNull]
        public Int64 RoleId { get; set; }

        [NotNull]
        [JsonIgnore]
        public String Password { get; set; } = string.Empty;
    }
}
