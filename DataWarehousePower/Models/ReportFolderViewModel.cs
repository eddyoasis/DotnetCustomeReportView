namespace DataWarehousePower.Models;

public sealed class ReportFolderViewModel
{
    public DateTime SelectedDate { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public string SelectedType { get; init; } = string.Empty;

    public string SortBy { get; init; } = "date";

    public string SortDirection { get; init; } = "desc";

    public string VirtualDirectoryName { get; init; } = string.Empty;

    public string PhysicalBasePath { get; init; } = string.Empty;

    public string FolderPhysicalPath { get; init; } = string.Empty;

    public string FolderUrl { get; init; } = string.Empty;

    public IReadOnlyList<ReportFolderFileItemViewModel> Files { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public int TotalPages { get; init; } = 1;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public IReadOnlyList<string> AvailableTypes { get; init; } = [];
}

public sealed class ReportFolderFileItemViewModel
{
    public string FileName { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTime DateModified { get; init; }
}