using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataWarehousePower.Models
{
    /// <summary>
    /// One row per user. All column preferences are stored as a JSON array in ColumnJson.
    /// JSON shape: [{"PropertyName":"Id","IsVisible":true,"DisplayOrder":1,"CustomName":"ID"}]
    /// </summary>
    [Table("UserColumnPreferences")]
    public class UserColumnPreference
    {
        /// <summary>UserId (UUID v4 string from browser cookie) is the primary key.</summary>
        [Key]
        [MaxLength(128)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// JSON array of column preference entries.
        /// Serialised/deserialised by the service layer.
        /// </summary>
        [Required]
        public string ColumnJson { get; set; } = "[]";
    }

    /// <summary>
    /// Represents one column entry inside the ColumnJson array.
    /// </summary>
    public class ColumnJsonEntry
    {
        public string PropertyName { get; set; } = string.Empty;
        public bool   IsVisible    { get; set; } = true;
        public int    DisplayOrder { get; set; } = 1;
        public string CustomName  { get; set; } = string.Empty;
    }
}
