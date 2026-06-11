using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DataWarehousePower.Models;
using ICSharpCode.SharpZipLib.Zip;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DataWarehousePower.Services
{
    public class ReportExportService(ILogger<ReportExportService> logger) : IReportExportService
    {
        private readonly ILogger<ReportExportService> _logger = logger;

        public async Task<byte[]> BuildPasswordProtectedZipAsync(
            ReportViewModel report,
            string format,
            string password,
            CancellationToken cancellationToken = default)
        {
            string normalizedFormat = format.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password is required.", nameof(password));
            }

            IReadOnlyList<ColumnDefinition> visibleColumns = report.DisplayColumns
                .Where(column => column.IsVisible)
                .OrderBy(column => column.Order)
                .ToList();

            if (visibleColumns.Count == 0)
            {
                throw new InvalidOperationException("There are no visible columns to export.");
            }

            (byte[] fileBytes, string extension) exportPayload = normalizedFormat switch
            {
                "csv" => (BuildCsv(report.Rows, visibleColumns), "csv"),
                "excel" => (BuildExcel(report.Rows, visibleColumns), "xlsx"),
                "pdf" => (BuildPdf(report, visibleColumns), "pdf"),
                _ => throw new ArgumentOutOfRangeException(nameof(format), "Supported formats are CSV, Excel, and PDF.")
            };

            string baseFileName = BuildSafeFileName(report.ReportName, normalizedFormat);
            byte[] zipBytes = BuildPasswordProtectedZip(baseFileName, exportPayload.extension, exportPayload.fileBytes, password);

            await Task.CompletedTask;
            return zipBytes;
        }

        private static byte[] BuildCsv(
            IReadOnlyList<Dictionary<string, object?>> rows,
            IReadOnlyList<ColumnDefinition> visibleColumns)
        {
            StringBuilder csvBuilder = new();
            csvBuilder.AppendLine(string.Join(",", visibleColumns.Select(column => EscapeCsv(column.DisplayLabel))));

            foreach (Dictionary<string, object?> row in rows)
            {
                List<string> cells = [];
                foreach (ColumnDefinition column in visibleColumns)
                {
                    object? value = ResolveCellValue(row, column.Key);
                    cells.Add(EscapeCsv(value?.ToString() ?? string.Empty));
                }

                csvBuilder.AppendLine(string.Join(",", cells));
            }

            return Encoding.UTF8.GetBytes(csvBuilder.ToString());
        }

        private static byte[] BuildExcel(
            IReadOnlyList<Dictionary<string, object?>> rows,
            IReadOnlyList<ColumnDefinition> visibleColumns)
        {
            using XLWorkbook workbook = new();
            IXLWorksheet worksheet = workbook.Worksheets.Add("Report");

            for (int columnIndex = 0; columnIndex < visibleColumns.Count; columnIndex++)
            {
                worksheet.Cell(1, columnIndex + 1).Value = visibleColumns[columnIndex].DisplayLabel;
                worksheet.Cell(1, columnIndex + 1).Style.Font.Bold = true;
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                Dictionary<string, object?> row = rows[rowIndex];
                for (int columnIndex = 0; columnIndex < visibleColumns.Count; columnIndex++)
                {
                    ColumnDefinition column = visibleColumns[columnIndex];
                    object? cellValue = ResolveCellValue(row, column.Key);
                    worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = cellValue?.ToString() ?? string.Empty;
                }
            }

            worksheet.Columns().AdjustToContents();

            using MemoryStream memoryStream = new();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }

        private static byte[] BuildPdf(
            ReportViewModel report,
            IReadOnlyList<ColumnDefinition> visibleColumns)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            IDocument document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(textStyle => textStyle.FontSize(10));

                    page.Header().Text(report.ReportName).SemiBold().FontSize(14);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            for (int i = 0; i < visibleColumns.Count; i++)
                            {
                                definition.RelativeColumn();
                            }
                        });

                        table.Header(header =>
                        {
                            foreach (ColumnDefinition column in visibleColumns)
                            {
                                header.Cell().Element(CellStyle).Text(column.DisplayLabel).SemiBold();
                            }
                        });

                        foreach (Dictionary<string, object?> row in report.Rows)
                        {
                            foreach (ColumnDefinition column in visibleColumns)
                            {
                                object? value = ResolveCellValue(row, column.Key);
                                headerOrCell(table).Text(value?.ToString() ?? string.Empty);
                            }
                        }
                    });
                });
            });

            return document.GeneratePdf();

            static IContainer CellStyle(IContainer container)
            {
                return container
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingHorizontal(4)
                    .PaddingVertical(2);
            }

            static IContainer headerOrCell(TableDescriptor descriptor)
            {
                return descriptor.Cell().Element(CellStyle);
            }
        }

        private static object? ResolveCellValue(Dictionary<string, object?> row, string key)
        {
            foreach (KeyValuePair<string, object?> entry in row)
            {
                if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static string EscapeCsv(string value)
        {
            bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            if (!mustQuote)
            {
                return value;
            }

            string escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private static string BuildSafeFileName(string reportName, string format)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitizedName = new(reportName
                .Select(character => invalidChars.Contains(character) ? '_' : character)
                .ToArray());

            return $"{sanitizedName}_{format}_{timestamp}";
        }

        private byte[] BuildPasswordProtectedZip(string baseFileName, string extension, byte[] fileBytes, string password)
        {
            try
            {
                using MemoryStream zipStreamBuffer = new();
                using ZipOutputStream zipOutputStream = new(zipStreamBuffer);

                zipOutputStream.SetLevel(9);
                zipOutputStream.Password = password;

                ZipEntry zipEntry = new($"{baseFileName}.{extension}")
                {
                    DateTime = DateTime.Now,
                    Size = fileBytes.LongLength
                };

                zipOutputStream.PutNextEntry(zipEntry);
                zipOutputStream.Write(fileBytes, 0, fileBytes.Length);
                zipOutputStream.CloseEntry();
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.Close();

                return zipStreamBuffer.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build password-protected ZIP for export file {BaseFileName}", baseFileName);
                throw;
            }
        }
    }
}
