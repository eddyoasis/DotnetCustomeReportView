using System.ComponentModel.DataAnnotations;

namespace DataWarehousePower.Models
{
    public class DataFileManageFilterViewModel
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
    }

    public class DataFileManageListViewModel
    {
        public DataFileManageFilterViewModel Filter { get; set; } = new();
        public List<DataFileDefinition> DataFiles { get; set; } = new();
    }

    public class DataFileManageFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Data file name is required.")]
        [MaxLength(200)]
        [Display(Name = "Data File Name")]
        public string DataFileName { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Source Database")]
        public string? SourceDatabase { get; set; }

        public List<string> SourceDatabaseOptions { get; set; } = new();

        [MaxLength(200)]
        [Display(Name = "Source Table")]
        public string? SourceTable { get; set; }

        public List<string> SourceTableOptions { get; set; } = new();

        [MaxLength(200)]
        [Display(Name = "Source Stored Procedure")]
        public string? SourceSP { get; set; }

        public List<string> SourceSPOptions { get; set; } = new();

        [Display(Name = "Parameters")]
        public string? Parameters { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        [Display(Name = "Departments")]
        public string? Departments { get; set; }

        public string? UserId { get; set; }

        public List<DepartmentSelectionItem> ActiveDepartmentOptions { get; set; } = new();
        public List<int> SelectedDepartmentIds { get; set; } = new();

        public List<DataFileColumnFormModel> Columns { get; set; } = new();
    }

    public class DataFileColumnFormModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Property name is required.")]
        [MaxLength(100)]
        [Display(Name = "Property Name")]
        public string PropertyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Label is required.")]
        [MaxLength(100)]
        [Display(Name = "Default Label")]
        public string DefaultLabel { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Mapping Parameter")]
        public string? MappingParameter { get; set; }

        [Range(1, 999)]
        [Display(Name = "Display Order")]
        public int DisplayOrder { get; set; } = 1;

        public bool IsDeleted { get; set; }
    }
}
