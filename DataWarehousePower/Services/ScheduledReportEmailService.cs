using DataWarehousePower.Models.AppSettings;
using FluentEmail.Core;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace DataWarehousePower.Services;

public sealed class ScheduledReportEmailService(
    IOptionsSnapshot<SmtpAppSetting> _smtpAppSetting,
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
        SmtpAppSetting smtpAppSetting = _smtpAppSetting.Value;

        if (!smtpAppSetting.IsEnabled)
        {
            return;
        }

        MemoryStream stream = new(zipBytes, writable: false);
        Attachment attachment = new(stream, fileName, "application/zip");

        string normalizedRecipient = recipientEmail.Trim();
        var subject = $"Scheduled export completed: {jobName}";
        var body = $"Report '{reportName}' was exported by job '{jobName}'. The ZIP file is attached.";
        List<string> recipientsTo = new List<string> { normalizedRecipient };
        List<string> recipientsCC = new List<string>();

        await SendEmailAsync(recipientsTo, recipientsCC, subject, body, attachment);

        logger.LogInformation(
            "Scheduled export email sent to {RecipientEmail} for job {JobName}.",
            normalizedRecipient,
            jobName);
    }

    private Task SendEmailAsync(List<string> recipientsTo, List<string> recipientsCC, string subject, string body, Attachment attachment)
    {
        var smtpAppSetting = _smtpAppSetting.Value;

        // --- Email Configuration ---
        string smtpHost = smtpAppSetting.Host;
        string senderEmail = smtpAppSetting.EmailFrom;
        string emailSubject = subject;
        string strMailBody = body;

        try
        {
            using MailMessage mailMessage = new();


            if (smtpAppSetting.IsTestEmailTo)
            {
                recipientsTo = smtpAppSetting.EmailTo;
            }

            if (recipientsCC.Any())
            {
                mailMessage.CC.AddRange(recipientsCC.Select(e => new MailAddress(e)));
            }

            if (attachment != null)
            {
                mailMessage.Attachments.Add(attachment);
            }

            mailMessage.From = new MailAddress(senderEmail);
            mailMessage.To.AddRange(recipientsTo.Select(e => new MailAddress(e)));
            mailMessage.Subject = emailSubject;
            mailMessage.IsBodyHtml = true;
            mailMessage.Priority = MailPriority.High;

            string formattedMessage = strMailBody.Replace("    ", Environment.NewLine + Environment.NewLine);
            mailMessage.Body = formattedMessage;

            using SmtpClient smtpClient = new(smtpHost);

            smtpClient.Send(mailMessage);
        }
        catch (SmtpException)
        {
            // Log exception if needed
        }
        catch (Exception)
        {
            // Log exception if needed
        }

        return Task.CompletedTask;
    }
}
