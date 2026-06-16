using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportEmailService(
    IOptions<ScheduledReportEmailOptions> options,
    ILogger<ScheduledReportEmailService> logger) : IScheduledReportEmailService
{
    public async Task SendExportResultAsync(
        string recipientEmail,
        string jobName,
        string reportName,
        string fileName,
        byte[] zipBytes,
        CancellationToken cancellationToken = default)
    {
        ScheduledReportEmailOptions emailOptions = options.Value;
        if (!emailOptions.Enabled)
        {
            throw new InvalidOperationException("Scheduled report email is disabled in configuration.");
        }

        if (string.IsNullOrWhiteSpace(emailOptions.SmtpHost))
        {
            throw new InvalidOperationException("Scheduled report email SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(emailOptions.SenderEmail))
        {
            throw new InvalidOperationException("Scheduled report email sender address is not configured.");
        }

        string normalizedRecipient = recipientEmail.Trim();
        using MailMessage message = new()
        {
            From = new MailAddress(emailOptions.SenderEmail, emailOptions.SenderDisplayName),
            Subject = $"{emailOptions.SubjectPrefix} Scheduled export completed: {jobName}",
            Body = $"Report '{reportName}' was exported by job '{jobName}'. The ZIP file is attached.",
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(normalizedRecipient));

        MemoryStream stream = new(zipBytes, writable: false);
        Attachment attachment = new(stream, fileName, "application/zip");
        message.Attachments.Add(attachment);

        using SmtpClient smtpClient = new(emailOptions.SmtpHost, emailOptions.SmtpPort)
        {
            EnableSsl = emailOptions.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(emailOptions.Username))
        {
            smtpClient.Credentials = new NetworkCredential(emailOptions.Username, emailOptions.Password ?? string.Empty);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message);

        logger.LogInformation(
            "Scheduled export email sent to {RecipientEmail} for job {JobName}.",
            normalizedRecipient,
            jobName);
    }
}
