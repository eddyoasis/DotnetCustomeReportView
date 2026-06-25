using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
public sealed class ReportFolderController(
    IConfiguration configuration,
    IColumnPreferenceService columnPreferenceService,
    ILogger<ReportFolderController> logger) : Controller
{
    private const string IisVirtualDirectoryReportFolderNameSettingKey = "IISVirtualDirectoryReportFoldername";
    private const string ReportFolderPhysicalPathSettingKey = "ReportFolderPhysicalPath";
    private const int DefaultPageSize = 20;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    [HttpGet]
    public IActionResult Index(DateTime? selectedDate = null, int page = 1)
    {
        ReportFolderViewModel viewModel = BuildViewModel(selectedDate, page);

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
        string physicalBasePath = NormalizeBasePath(configuration[ReportFolderPhysicalPathSettingKey]);

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

    private ReportFolderViewModel BuildViewModel(DateTime? selectedDate, int page)
    {
        DateTime effectiveDate = (selectedDate ?? DateTime.Today).Date;
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string userPathSegment = SanitizePathSegment(userId);
        string virtualDirectoryName = NormalizeVirtualDirectoryName(configuration[IisVirtualDirectoryReportFolderNameSettingKey]);
        string physicalBasePath = NormalizeBasePath(configuration[ReportFolderPhysicalPathSettingKey]);
        string folderPhysicalPath = string.IsNullOrWhiteSpace(physicalBasePath)
            ? string.Empty
            : BuildFolderPhysicalPath(physicalBasePath, userPathSegment, effectiveDate);

        string folderUrl = string.IsNullOrWhiteSpace(virtualDirectoryName)
            ? string.Empty
            : BuildFolderUrl(virtualDirectoryName, userPathSegment, effectiveDate);

        List<ReportFolderFileItemViewModel> files = GetFiles(folderPhysicalPath);
        int sanitizedPage = page < 1 ? 1 : page;
        int totalCount = files.Count;
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)DefaultPageSize);
        if (sanitizedPage > totalPages)
        {
            sanitizedPage = totalPages;
        }

        List<ReportFolderFileItemViewModel> pagedFiles = files
            .Skip((sanitizedPage - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToList();

        return new ReportFolderViewModel
        {
            SelectedDate = effectiveDate,
            UserId = userId,
            VirtualDirectoryName = virtualDirectoryName,
            PhysicalBasePath = physicalBasePath,
            FolderPhysicalPath = folderPhysicalPath,
            FolderUrl = folderUrl,
            Files = pagedFiles,
            Page = sanitizedPage,
            PageSize = DefaultPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
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
                DateModified = fileInfo.LastWriteTime
            })
            .ToList();
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