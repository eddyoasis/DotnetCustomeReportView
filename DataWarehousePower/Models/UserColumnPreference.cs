using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// One row per user per report per client code. Column preferences stored as JSON.
    /// JSON shape: [{"PropertyName":"Id","IsVisible":true,"DisplayOrder":1,"CustomName":"ID"}]
    /// </summary>
    [Table("UserColumnPreferences")]
    public class UserColumnPreference
    {
        [Key, Column(Order = 0)]
        [MaxLength(128)]
        public string UserId { get; set; } = string.Empty;

        [Key, Column(Order = 1)]
        public int ReportDefinitionId { get; set; }

        [Key, Column(Order = 2)]
        [MaxLength(128)]
        public string ClientCode { get; set; } = string.Empty;

        [Required]
        public string ColumnJson { get; set; } = "[]";
    }

    /// <summary>One entry inside the ColumnJson array.</summary>
    public class ColumnJsonEntry
    {
        public string PropertyName { get; set; } = string.Empty;
        public bool   IsVisible    { get; set; } = true;
        public int    DisplayOrder { get; set; } = 1;
        public string CustomName  { get; set; } = string.Empty;
    }
}
