using System.ComponentModel.DataAnnotations;

namespace DataWarehousePower.Models
{
    /// <summary>Filters for the report manage list.</summary>
    public class ReportManageFilterViewModel
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>List view — all report definitions.</summary>
    public class ReportManageListViewModel
    {
        public ReportManageFilterViewModel Filter { get; set; } = new();
        public List<ReportDefinition> Reports { get; set; } = new();
    }

    /// <summary>Create / Edit form for a ReportDefinition + its columns.</summary>
    public class ReportManageFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Report name is required.")]
        [MaxLength(200)]
        [Display(Name = "Report Name")]
        public string ReportName { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Source Table")]
        public string? SourceTable { get; set; }

            /// <summary>Dropdown options loaded from the selected source database (tables + views).</summary>
            public List<string> SourceTableOptions { get; set; } = new();

        [MaxLength(200)]
        [Display(Name = "Source Database")]
        public string? SourceDatabase { get; set; }

        /// <summary>Dropdown options loaded from the connected SQL Server database list.</summary>
        public List<string> SourceDatabaseOptions { get; set; } = new();

        [MaxLength(200)]
        [Display(Name = "Source Stored Procedure")]
        public string? SourceSP { get; set; }

        [Display(Name = "Parameters")]
        public string? Parameters { get; set; }

        /// <summary>Dropdown options loaded from the selected source database stored procedures.</summary>
        public List<string> SourceSPOptions { get; set; } = new();

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        [Display(Name = "Departments")]
        public string? Departments { get; set; }

        /// <summary>Active departments shown as checkbox options.</summary>
        public List<DepartmentSelectionItem> ActiveDepartmentOptions { get; set; } = new();

        /// <summary>Selected department IDs from the checkbox list.</summary>
        public List<int> SelectedDepartmentIds { get; set; } = new();

        /// <summary>Columns bound from the dynamic form rows.</summary>
        public List<ReportColumnFormModel> Columns { get; set; } = new();
    }

    public class ReportColumnFormModel
    {
        public int Id { get; set; }   // 0 = new row

        [Required(ErrorMessage = "Property name is required.")]
        [MaxLength(100)]
        [Display(Name = "Property Name (DB column)")]
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

        /// <summary>Marked true by the UI when the user removes a row.</summary>
        public bool IsDeleted { get; set; } = false;
    }

    public class ReportParameterFormModel
    {
        public string Name { get; set; } = string.Empty;
        public string? DefaultValue { get; set; }
    }

    public class DepartmentSelectionItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
