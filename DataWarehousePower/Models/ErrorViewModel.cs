namespace DataWarehousePower.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public string FriendlyMessage { get; set; } = "Something went wrong while processing your request. Please try again or contact support if the issue continues.";

    public string? RequestPath { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
