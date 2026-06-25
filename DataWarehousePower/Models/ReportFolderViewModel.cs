namespace DataWarehousePower.Models;

public sealed class ReportFolderViewModel
{
    public DateTime SelectedDate { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string BasePath { get; init; } = string.Empty;

    public string FinalPath { get; init; } = string.Empty;
}