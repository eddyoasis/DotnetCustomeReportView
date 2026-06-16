namespace DataWarehousePower.Models;

public static class ScheduledJobActions
{
    public const string ExportFile = "export-file";
    public const string ExportFileAndEmailToUser = "export-file-and-email-to-user";

    public static readonly IReadOnlyList<string> All =
    [
        ExportFile,
        ExportFileAndEmailToUser
    ];
}
