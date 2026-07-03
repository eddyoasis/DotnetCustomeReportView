using DataWarehousePower.Authorization;
using DataWarehousePower.Helper;
using DataWarehousePower.Models;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using System.IO.Compression;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
public sealed class ReportFolderController(
    IOptionsSnapshot<RemoteFolderExportLocationAppSetting> remoteFolderExportLocationAppSetting,
    IConfiguration configuration,
    IColumnPreferenceService columnPreferenceService,
    ILogger<ReportFolderController> logger) : Controller
{
    private const string IisVirtualDirectoryReportFolderNameSettingKey = "IISVirtualDirectoryReportFoldername";
    private const string ReportFolderPhysicalPathSettingKey = "ReportFolderPhysicalPath";
    private const int DefaultPageSize = 20;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    [HttpGet]
    public IActionResult Index(DateTime? selectedDate = null, string? searchText = null, string? selectedType = null, string? sortBy = null, string? sortDirection = null, int page = 1)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate, searchText, selectedType, sortBy, sortDirection, page);

        if (string.IsNullOrWhiteSpace(viewModel.VirtualDirectoryName))
        {
            TempData["Error"] = $"Configuration '{IisVirtualDirectoryReportFolderNameSettingKey}' is missing.";
        }

        if (string.IsNullOrWhiteSpace(viewModel.PhysicalBasePath))
        {
            TempData["Error"] = $"Configuration '{ReportFolderPhysicalPathSettingKey}' is missing.";
        }

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Download(DateTime selectedDate, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("File name is required.");
        }

        string sanitizedFileName = Path.GetFileName(fileName);
        if (!string.Equals(fileName, sanitizedFileName, StringComparison.Ordinal))
        {
            return BadRequest("Invalid file name.");
        }

        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string userPathSegment = SanitizePathSegment(userId);
        //string physicalBasePath = NormalizeBasePath(configuration[ReportFolderPhysicalPathSettingKey]);
        string physicalBasePath = NormalizeBasePath(remoteFolderExportLocationAppSetting.Value.UserReportFolderPhysicalPath);

        if (string.IsNullOrWhiteSpace(physicalBasePath))
        {
            return NotFound();
        }

        string folderPhysicalPath = BuildFolderPhysicalPath(physicalBasePath, userPathSegment, selectedDate.Date);
        string filePhysicalPath = Path.Combine(folderPhysicalPath, sanitizedFileName);

        if (!System.IO.File.Exists(filePhysicalPath))
        {
            return NotFound();
        }

        try
        {
            string contentType = ContentTypeProvider.TryGetContentType(sanitizedFileName, out string? resolvedContentType)
                ? resolvedContentType
                : "application/octet-stream";

            FileStream fileStream = new(filePhysicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return File(fileStream, contentType, sanitizedFileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download report file: {FilePath}", filePhysicalPath);
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to download file.");
        }
    }

    [HttpGet]
    public IActionResult DownloadAll(DateTime? selectedDate = null, string? searchText = null, string? selectedType = null, string? sortBy = null, string? sortDirection = null)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate, searchText, selectedType, sortBy, sortDirection, 1);
        if (string.IsNullOrWhiteSpace(viewModel.FolderPhysicalPath) || !Directory.Exists(viewModel.FolderPhysicalPath))
        {
            return NotFound();
        }

        string archiveFileName = $"report-files-{viewModel.SelectedDate:yyyy-MM-dd}_({DateTimeHelper.GetCurrentLocalTime():yyyy-MM-dd_HHmm}).zip";
        MemoryStream memoryStream = new();

        using (ZipArchive archive = new(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ReportFolderFileItemViewModel file in GetAllFiles(viewModel.FolderPhysicalPath, viewModel.SearchText, viewModel.SelectedType, viewModel.SortBy, viewModel.SortDirection))
            {
                string filePath = Path.Combine(viewModel.FolderPhysicalPath, file.FileName);
                if (!System.IO.File.Exists(filePath))
                {
                    continue;
                }

                ZipArchiveEntry entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                using Stream entryStream = entry.Open();
                using FileStream sourceStream = System.IO.File.OpenRead(filePath);
                sourceStream.CopyTo(entryStream);
            }
        }

        memoryStream.Position = 0;
        return File(memoryStream, "application/zip", archiveFileName);
    }

    private ReportFolderViewModel BuildViewModel(DateTime? selectedDate, string? searchText, string? selectedType, string? sortBy, string? sortDirection, int page)
    {
        DateTime effectiveDate = (selectedDate ?? DateTime.Today).Date;
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string userPathSegment = SanitizePathSegment(userId);
        string virtualDirectoryName = NormalizeVirtualDirectoryName(configuration[IisVirtualDirectoryReportFolderNameSettingKey]);
        //string physicalBasePath = NormalizeBasePath(configuration[ReportFolderPhysicalPathSettingKey]);
        string physicalBasePath = NormalizeBasePath(remoteFolderExportLocationAppSetting.Value.UserReportFolderPhysicalPath);
        string folderPhysicalPath = string.IsNullOrWhiteSpace(physicalBasePath)
            ? string.Empty
            : BuildFolderPhysicalPath(physicalBasePath, userPathSegment, effectiveDate);

        string folderUrl = string.IsNullOrWhiteSpace(virtualDirectoryName)
            ? string.Empty
            : BuildFolderUrl(virtualDirectoryName, userPathSegment, effectiveDate);

        string normalizedSearchText = (searchText ?? string.Empty).Trim();
        string normalizedSelectedType = (selectedType ?? string.Empty).Trim();
        string normalizedSortBy = NormalizeSortBy(sortBy);
        string normalizedSortDirection = NormalizeSortDirection(sortDirection);

        List<ReportFolderFileItemViewModel> allFiles = GetFiles(folderPhysicalPath);
        IEnumerable<ReportFolderFileItemViewModel> filteredFiles = ApplyFilters(allFiles, normalizedSearchText, normalizedSelectedType);
        List<string> availableTypes = filteredFiles
            .Select(file => file.Type)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<ReportFolderFileItemViewModel> sortedFiles = ApplySort(filteredFiles, normalizedSortBy, normalizedSortDirection).ToList();
        int sanitizedPage = page < 1 ? 1 : page;
        int totalCount = sortedFiles.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)DefaultPageSize);
        if (sanitizedPage > totalPages)
        {
            sanitizedPage = totalPages;
        }

        List<ReportFolderFileItemViewModel> pagedFiles = sortedFiles
            .Skip((sanitizedPage - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToList();

        return new ReportFolderViewModel
        {
            SelectedDate = effectiveDate,
            UserId = userId,
            SearchText = normalizedSearchText,
            SelectedType = normalizedSelectedType,
            SortBy = normalizedSortBy,
            SortDirection = normalizedSortDirection,
            VirtualDirectoryName = virtualDirectoryName,
            PhysicalBasePath = physicalBasePath,
            FolderPhysicalPath = folderPhysicalPath,
            FolderUrl = folderUrl,
            Files = pagedFiles,
            Page = sanitizedPage,
            PageSize = DefaultPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            AvailableTypes = availableTypes
        };
    }

    private static IEnumerable<ReportFolderFileItemViewModel> ApplyFilters(IEnumerable<ReportFolderFileItemViewModel> files, string searchText, string selectedType)
    {
        IEnumerable<ReportFolderFileItemViewModel> filtered = files;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(file => file.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(selectedType))
        {
            filtered = filtered.Where(file => string.Equals(file.Type, selectedType, StringComparison.OrdinalIgnoreCase));
        }

        return filtered;
    }

    private static IEnumerable<ReportFolderFileItemViewModel> ApplySort(IEnumerable<ReportFolderFileItemViewModel> files, string sortBy, string sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.ToLowerInvariant() switch
        {
            "name" => descending
                ? files.OrderByDescending(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase),
            "type" => descending
                ? files.OrderByDescending(file => file.Type, StringComparer.OrdinalIgnoreCase).ThenByDescending(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(file => file.Type, StringComparer.OrdinalIgnoreCase).ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase),
            "size" => descending
                ? files.OrderByDescending(file => file.SizeBytes).ThenByDescending(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(file => file.SizeBytes).ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase),
            _ => descending
                ? files.OrderByDescending(file => file.DateModified).ThenByDescending(file => file.FileName, StringComparer.OrdinalIgnoreCase)
                : files.OrderBy(file => file.DateModified).ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private string BuildFolderUrl(string virtualDirectoryName, string userPathSegment, DateTime selectedDate)
    {
        string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        string relativePath = $"{virtualDirectoryName}/{Uri.EscapeDataString(userPathSegment)}/{selectedDate:yyyy-MM-dd}";
        return $"{baseUrl}/{relativePath}";
    }

    private static string NormalizeVirtualDirectoryName(string? configuredName)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            return string.Empty;
        }

        return configuredName.Trim().Trim('/');
    }

    private static string NormalizeBasePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        return configuredPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string BuildFolderPhysicalPath(string physicalBasePath, string userPathSegment, DateTime selectedDate)
    {
        return Path.Combine(physicalBasePath, userPathSegment, selectedDate.ToString("yyyy-MM-dd"));
    }

    private static List<ReportFolderFileItemViewModel> GetFiles(string folderPhysicalPath)
    {
        if (string.IsNullOrWhiteSpace(folderPhysicalPath) || !Directory.Exists(folderPhysicalPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(folderPhysicalPath)
            .Select(path => new FileInfo(path))
            .OrderByDescending(fileInfo => fileInfo.LastWriteTime)
            .ThenBy(fileInfo => fileInfo.Name, StringComparer.OrdinalIgnoreCase)
            .Select(fileInfo => new ReportFolderFileItemViewModel
            {
                FileName = fileInfo.Name,
                Type = GetFileType(fileInfo),
                SizeBytes = fileInfo.Length,
                DateModified = fileInfo.LastWriteTime
            })
            .ToList();
    }

    private static IEnumerable<ReportFolderFileItemViewModel> GetAllFiles(string folderPhysicalPath, string searchText, string selectedType, string sortBy, string sortDirection)
    {
        return ApplySort(ApplyFilters(GetFiles(folderPhysicalPath), searchText, selectedType), sortBy, sortDirection);
    }

    private static string GetFileType(FileInfo fileInfo)
    {
        return string.IsNullOrWhiteSpace(fileInfo.Extension) ? "No extension" : fileInfo.Extension.TrimStart('.').ToUpperInvariant();
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => "name",
            "type" => "type",
            "size" => "size",
            _ => "date",
        };
    }

    private static string NormalizeSortDirection(string? sortDirection)
    {
        return string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
    }

    private static string SanitizePathSegment(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "anonymous";
        }

        const string allowedChars = "-_.";
        string sanitized = new(rawValue
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || allowedChars.Contains(character) ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "anonymous" : sanitized;
    }
}