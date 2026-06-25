namespace DataWarehousePower.Models;

public sealed class ReportFolderViewModel
{
    public DateTime SelectedDate { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string VirtualDirectoryName { get; init; } = string.Empty;

    public string FolderUrl { get; init; } = string.Empty;
}