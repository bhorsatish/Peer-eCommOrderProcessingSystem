using SQLite;

namespace eComm_ms.Models
{
    public class StatusStates
    {
        [PrimaryKey]
        public Int64 Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
