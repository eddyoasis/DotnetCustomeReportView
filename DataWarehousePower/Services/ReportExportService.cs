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

        public Task<byte[]> BuildPasswordProtectedZipAsync(
            ReportViewModel report,
            IReadOnlyCollection<string> formats,
            string password,
            string zipSubFileName,
            CsvExportSplitOptions? csvSplitOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password is required.", nameof(password));
            }

            if (formats is null || formats.Count == 0)
            {
                throw new ArgumentException("At least one export format must be provided.", nameof(formats));
            }

            List<string> normalizedFormats = formats
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Select(format => format.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (normalizedFormats.Count == 0)
            {
                throw new ArgumentException("At least one export format must be provided.", nameof(formats));
            }

            List<string> invalidFormats = normalizedFormats
                .Where(format => format is not ("csv" or "excel" or "pdf"))
                .Distinct()
                .ToList();

            if (invalidFormats.Count > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(formats), $"Unsupported format(s): {string.Join(", ", invalidFormats)}. Supported formats are CSV, Excel, and PDF.");
            }

            IReadOnlyList<ColumnDefinition> visibleColumns = report.DisplayColumns
                .Where(column => column.IsVisible)
                .OrderBy(column => column.Order)
                .ToList();

            if (visibleColumns.Count == 0)
            {
                throw new InvalidOperationException("There are no visible columns to export.");
            }

            List<(string FileName, byte[] FileBytes)> exportFiles = [];
            foreach (string normalizedFormat in normalizedFormats)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string baseFileName = BuildSafeFileName(zipSubFileName, normalizedFormat);

                if (normalizedFormat == "csv")
                {
                    IReadOnlyList<CsvExportChunk> csvChunks = BuildCsvChunks(report.Rows, visibleColumns, csvSplitOptions);
                    if (csvChunks.Count == 1)
                    {
                        exportFiles.Add(($"{baseFileName}.csv", csvChunks[0].Bytes));
                    }
                    else
                    {
                        for (int chunkIndex = 0; chunkIndex < csvChunks.Count; chunkIndex++)
                        {
                            CsvExportChunk chunk = csvChunks[chunkIndex];
                            exportFiles.Add(($"{baseFileName}_part{chunk.PartNumber:D3}.csv", chunk.Bytes));
                        }
                    }

                    continue;
                }

                (byte[] fileBytes, string extension) exportPayload = normalizedFormat switch
                {
                    "excel" => (BuildExcel(report.Rows, visibleColumns), "xlsx"),
                    "pdf" => (BuildPdf(report, visibleColumns), "pdf"),
                    _ => throw new ArgumentOutOfRangeException(nameof(formats), "Supported formats are CSV, Excel, and PDF.")
                };

                exportFiles.Add(($"{baseFileName}.{exportPayload.extension}", exportPayload.fileBytes));
            }

            byte[] zipBytes = BuildPasswordProtectedZip(exportFiles, password);

            return Task.FromResult(zipBytes);
        }

        private static IReadOnlyList<CsvExportChunk> BuildCsvChunks(
            IReadOnlyList<Dictionary<string, object?>> rows,
            IReadOnlyList<ColumnDefinition> visibleColumns,
            CsvExportSplitOptions? csvSplitOptions)
        {
            string header = string.Join(",", visibleColumns.Select(column => EscapeCsv(column.DisplayLabel)));
            string headerLine = $"{header}{Environment.NewLine}";
            int headerBytes = Encoding.UTF8.GetByteCount(headerLine);

            int? maxRowsPerFile = csvSplitOptions?.MaxRowsPerFile is > 0
                ? csvSplitOptions.MaxRowsPerFile
                : null;
            long? maxBytesPerFile = csvSplitOptions?.MaxBytesPerFile is > 0
                ? csvSplitOptions.MaxBytesPerFile
                : null;

            bool splitByRowCount = maxRowsPerFile.HasValue;
            bool splitByFileSize = maxBytesPerFile.HasValue;

            if (!splitByRowCount && !splitByFileSize)
            {
                return [new CsvExportChunk(1, BuildSingleCsv(rows, visibleColumns))];
            }

            List<CsvExportChunk> chunks = [];
            StringBuilder csvBuilder = new();
            csvBuilder.Append(headerLine);
            int rowsInChunk = 0;
            long bytesInChunk = headerBytes;
            int partNumber = 1;

            foreach (Dictionary<string, object?> row in rows)
            {
                List<string> cells = [];
                foreach (ColumnDefinition column in visibleColumns)
                {
                    object? value = ResolveCellValue(row, column.Key);
                    cells.Add(EscapeCsv(value?.ToString() ?? string.Empty));
                }

                string rowLine = $"{string.Join(",", cells)}{Environment.NewLine}";
                long rowBytes = Encoding.UTF8.GetByteCount(rowLine);

                bool reachedRowLimit = splitByRowCount && rowsInChunk >= maxRowsPerFile;
                bool wouldExceedSizeLimit = splitByFileSize && rowsInChunk > 0 && bytesInChunk + rowBytes > maxBytesPerFile;

                if (reachedRowLimit || wouldExceedSizeLimit)
                {
                    chunks.Add(new CsvExportChunk(partNumber, Encoding.UTF8.GetBytes(csvBuilder.ToString())));
                    partNumber++;
                    csvBuilder.Clear();
                    csvBuilder.Append(headerLine);
                    rowsInChunk = 0;
                    bytesInChunk = headerBytes;
                }

                csvBuilder.Append(rowLine);
                rowsInChunk++;
                bytesInChunk += rowBytes;
            }

            chunks.Add(new CsvExportChunk(partNumber, Encoding.UTF8.GetBytes(csvBuilder.ToString())));

            return chunks;
        }

        private static byte[] BuildSingleCsv(
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

        private static string BuildSafeFileName(string zipSubFileName, string format)
        {
            return zipSubFileName.Replace("format", format);
        }

        private sealed record CsvExportChunk(int PartNumber, byte[] Bytes);

        //private static string BuildSafeFileName(string reportName, string format)
        //{
        //    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        //    char[] invalidChars = Path.GetInvalidFileNameChars();
        //    string sanitizedName = new(reportName
        //        .Select(character => invalidChars.Contains(character) ? '_' : character)
        //        .ToArray());

        //    return $"{sanitizedName}_{format}_{timestamp}";
        //}

        private byte[] BuildPasswordProtectedZip(IReadOnlyCollection<(string FileName, byte[] FileBytes)> files, string password)
        {
            try
            {
                using MemoryStream zipStreamBuffer = new();
                using ZipOutputStream zipOutputStream = new(zipStreamBuffer);

                zipOutputStream.SetLevel(9);
                zipOutputStream.Password = password;

                foreach ((string fileName, byte[] fileBytes) in files)
                {
                    ZipEntry zipEntry = new(fileName)
                    {
                        DateTime = DateTime.Now,
                        Size = fileBytes.LongLength
                    };

                    zipOutputStream.PutNextEntry(zipEntry);
                    zipOutputStream.Write(fileBytes, 0, fileBytes.Length);
                    zipOutputStream.CloseEntry();
                }

                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.Close();

                return zipStreamBuffer.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build password-protected ZIP for report export files.");
                throw;
            }
        }
    }
}
