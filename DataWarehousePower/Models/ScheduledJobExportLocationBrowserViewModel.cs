namespace DataWarehousePower.Models;

public sealed class ScheduledJobExportLocationBrowserViewModel
{
    public string ExportLocation { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public string SelectedType { get; init; } = string.Empty;

    public string SortBy { get; init; } = "date";

    public string SortDirection { get; init; } = "desc";

    public IReadOnlyList<ReportFolderFileItemViewModel> Files { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public int TotalPages { get; init; } = 1;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public IReadOnlyList<string> AvailableTypes { get; init; } = [];
}
