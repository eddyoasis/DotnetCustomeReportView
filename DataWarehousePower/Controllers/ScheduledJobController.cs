using DataWarehousePower.Authorization;
using DataWarehousePower.Models;
using DataWarehousePower.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace DataWarehousePower.Controllers;

[Authorize(Policy = DepartmentAuthorizationPolicies.ReportAccess)]
public sealed class ScheduledJobController(
    IScheduledReportJobService scheduledReportJobService,
    IDataFileManageService dataFileManageService,
    IColumnPreferenceService columnPreferenceService,
    IConfiguration configuration,
    ILogger<ScheduledJobController> logger) : Controller
{
    private const string ExportLocationBasePathsSection = "ScheduledJob:ExportLocationBasePaths";
    private const int DefaultBrowsePageSize = 20;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public async Task<IActionResult> Index(ScheduledJobFilterViewModel? filter)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        ScheduledJobListViewModel viewModel = await scheduledReportJobService.GetListViewModelAsync(userId, filter);
        viewModel.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
        return View(viewModel);
    }

    public async Task<IActionResult> Create(int? reportDefinitionId = null, string? schemaTemplate = null, string? clientCode = null, List<string>? formats = null, string? returnUrl = null, bool isDataFile = false)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);
        string? userDepartment = ResolveUserDepartment();
        ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetCreateFormAsync(userId, userDepartment);
        viewModel.RequiresSchemaTemplateAndClientCode = !isDataFile;
        await PopulateDataFileOptionsAsync(viewModel, userId, userDepartment);
        viewModel.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (reportDefinitionId.HasValue && reportDefinitionId.Value > 0)
        {
            viewModel.ReportDefinitionId = reportDefinitionId.Value;
        }

        if (viewModel.RequiresSchemaTemplateAndClientCode && !string.IsNullOrWhiteSpace(schemaTemplate))
        {
            viewModel.SchemaTemplate = schemaTemplate.Trim();
        }

        if (viewModel.RequiresSchemaTemplateAndClientCode && !string.IsNullOrWhiteSpace(clientCode))
        {
            viewModel.ClientCode = clientCode.Trim();
        }

        if (formats is not null && formats.Count > 0)
        {
            List<string> normalizedFormats = formats
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .Select(format => format.Trim().ToLowerInvariant())
                .Where(format => format is "csv" or "excel" or "pdf")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (normalizedFormats.Count > 0)
            {
                viewModel.Formats = normalizedFormats;
            }
        }

        if (viewModel.RequiresSchemaTemplateAndClientCode &&
            viewModel.ReportDefinitionId > 0 &&
            viewModel.AvailableSchemaTemplatesByReportId.TryGetValue(viewModel.ReportDefinitionId, out List<string>? reportSchemaTemplates))
        {
            viewModel.AvailableSchemaTemplates = reportSchemaTemplates;
        }

        viewModel.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
        viewModel.AvailableExportLocationBasePaths = viewModel.AvailableExportLocationBasePathOptions
            .Select(option => option.Path)
            .ToList();

        return View("Form", viewModel);
    }

    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();
            ScheduledJobFormViewModel viewModel = await scheduledReportJobService.GetEditFormAsync(id, userId, userDepartment);
            await PopulateDataFileOptionsAsync(viewModel, userId, userDepartment);
            viewModel.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
            viewModel.AvailableExportLocationBasePaths = viewModel.AvailableExportLocationBasePathOptions
                .Select(option => option.Path)
                .ToList();
            return View("Form", viewModel);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ScheduledJobFormViewModel form)
    {
        string userId = columnPreferenceService.ResolveUserId(HttpContext);

        ValidateExportLocation(form);
        ValidateSchemaAndClientCode(form);

        if (!ModelState.IsValid)
        {
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }

        string username = ResolveAuditUsername();
        string userDepartment = ResolveUserDepartment();

        try
        {
            if (form.Id == 0)
            {
                int id = await scheduledReportJobService.CreateAsync(form, userId, username, userDepartment);
                TempData["Success"] = $"Scheduled job created (ID: {id}).";

                if (Url.IsLocalUrl(form.ReturnUrl))
                {
                    return Redirect(form.ReturnUrl!);
                }
            }
            else
            {
                await scheduledReportJobService.UpdateAsync(form, userId, username, userDepartment);
                TempData["Success"] = "Scheduled job updated.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation failed when saving scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save scheduled job {JobId}", form.Id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the job.");
            await PopulateFormLookupsAsync(form, userId, ResolveUserDepartment());
            return View("Form", form);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            await scheduledReportJobService.DeleteAsync(id, userId);
            TempData["Success"] = "Scheduled job deleted.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete scheduled job {JobId}", id);
            TempData["Error"] = "Failed to delete scheduled job.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        try
        {
            string userId = columnPreferenceService.ResolveUserId(HttpContext);
            string? userDepartment = ResolveUserDepartment();
            ScheduledJobFormViewModel form = await scheduledReportJobService.GetEditFormAsync(id, userId, userDepartment);
            form.IsActive = !form.IsActive;
            await scheduledReportJobService.UpdateAsync(
                form,
                userId,
                ResolveAuditUsername(),
                ResolveUserDepartment());
            TempData["Success"] = "Scheduled job status updated.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle scheduled job {JobId}", id);
            TempData["Error"] = "Failed to update scheduled job status.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult OpenExportLocation([FromQuery] string? exportLocation, [FromQuery] string? searchText = null, [FromQuery] string? selectedType = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, [FromQuery] int page = 1)
    {
        string normalizedExportLocation = NormalizeWindowsPath(exportLocation);
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            return BadRequest("Please select an export location first.");
        }

        List<string> availableBasePaths = GetAvailableExportLocationBasePaths();
        bool isUnderConfiguredBasePath = availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => normalizedExportLocation.StartsWith(basePath!, StringComparison.OrdinalIgnoreCase));

        if (!isUnderConfiguredBasePath)
        {
            return BadRequest("Export location must start with one of the configured base paths.");
        }

        if (!Directory.Exists(normalizedExportLocation))
        {
            return NotFound("The export location folder does not exist.");
        }

        ScheduledJobExportLocationBrowserViewModel viewModel = BuildExportLocationBrowserViewModel(
            normalizedExportLocation,
            searchText,
            selectedType,
            sortBy,
            sortDirection,
            page);

        return View("ExportLocation", viewModel);
    }

    [HttpGet]
    public IActionResult DownloadExportLocationFile([FromQuery] string? exportLocation, [FromQuery] string? fileName)
    {
        string normalizedExportLocation = NormalizeWindowsPath(exportLocation);
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            return BadRequest("Please select an export location first.");
        }

        string sanitizedFileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || !string.Equals(fileName, sanitizedFileName, StringComparison.Ordinal))
        {
            return BadRequest("Invalid file name.");
        }

        List<string> availableBasePaths = GetAvailableExportLocationBasePaths();
        bool isUnderConfiguredBasePath = availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => normalizedExportLocation.StartsWith(basePath!, StringComparison.OrdinalIgnoreCase));

        if (!isUnderConfiguredBasePath)
        {
            return BadRequest("Export location must start with one of the configured base paths.");
        }

        string filePath = Path.Combine(normalizedExportLocation, sanitizedFileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        string contentType = ContentTypeProvider.TryGetContentType(sanitizedFileName, out string? resolvedContentType)
            ? resolvedContentType
            : "application/octet-stream";

        FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(fileStream, contentType, sanitizedFileName);
    }

    private ScheduledJobExportLocationBrowserViewModel BuildExportLocationBrowserViewModel(string exportLocation, string? searchText, string? selectedType, string? sortBy, string? sortDirection, int page)
    {
        string normalizedSearchText = (searchText ?? string.Empty).Trim();
        string normalizedSelectedType = (selectedType ?? string.Empty).Trim();
        string normalizedSortBy = NormalizeSortBy(sortBy);
        string normalizedSortDirection = NormalizeSortDirection(sortDirection);

        List<ReportFolderFileItemViewModel> allFiles = GetFiles(exportLocation);
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
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)DefaultBrowsePageSize);
        if (sanitizedPage > totalPages)
        {
            sanitizedPage = totalPages;
        }

        List<ReportFolderFileItemViewModel> pagedFiles = sortedFiles
            .Skip((sanitizedPage - 1) * DefaultBrowsePageSize)
            .Take(DefaultBrowsePageSize)
            .ToList();

        return new ScheduledJobExportLocationBrowserViewModel
        {
            ExportLocation = exportLocation,
            SearchText = normalizedSearchText,
            SelectedType = normalizedSelectedType,
            SortBy = normalizedSortBy,
            SortDirection = normalizedSortDirection,
            Files = pagedFiles,
            Page = sanitizedPage,
            PageSize = DefaultBrowsePageSize,
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

    private static List<ReportFolderFileItemViewModel> GetFiles(string exportLocation)
    {
        if (string.IsNullOrWhiteSpace(exportLocation) || !Directory.Exists(exportLocation))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(exportLocation)
            .Select(path => new FileInfo(path))
            .Select(fileInfo => new ReportFolderFileItemViewModel
            {
                FileName = fileInfo.Name,
                Type = GetFileType(fileInfo),
                SizeBytes = fileInfo.Length,
                DateModified = fileInfo.LastWriteTime
            })
            .ToList();
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

    private string ResolveAuditUsername()
    {
        string? displayName = HttpContext.Session.GetString("UserDisplayName");
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return string.IsNullOrWhiteSpace(User.Identity?.Name) ? "Anonymous" : User.Identity!.Name!.Trim();
    }

    private string? ResolveUserDepartment()
        => HttpContext.Session.GetString("UserDepartment");

    private async Task PopulateFormLookupsAsync(ScheduledJobFormViewModel form, string userId, string? userDepartment)
    {
        ScheduledJobFormViewModel lookupForm = await scheduledReportJobService.GetCreateFormAsync(userId, userDepartment);
        form.AvailableReports = lookupForm.AvailableReports;
        await PopulateDataFileOptionsAsync(form, userId, userDepartment);
        form.AvailableClientCodes = lookupForm.AvailableClientCodes;
        form.AvailableClientCodeFolders = lookupForm.AvailableClientCodeFolders;
        form.AvailableSchemaTemplatesByReportId = lookupForm.AvailableSchemaTemplatesByReportId;
        form.AvailableParametersByReportId = lookupForm.AvailableParametersByReportId;
        form.AvailableSchemaTemplates = form.RequiresSchemaTemplateAndClientCode &&
            lookupForm.AvailableSchemaTemplatesByReportId.TryGetValue(form.ReportDefinitionId, out List<string>? reportClientCodes)
            ? reportClientCodes
            : [];
        form.AvailableExportLocationBasePathOptions = GetAvailableExportLocationBasePathOptions();
        form.AvailableExportLocationBasePaths = form.AvailableExportLocationBasePathOptions
            .Select(option => option.Path)
            .ToList();

        if (form.Id > 0 && string.IsNullOrWhiteSpace(form.ExistingPassword))
        {
            ScheduledJobFormViewModel editForm = await scheduledReportJobService.GetEditFormAsync(form.Id, userId, userDepartment);
            form.ExistingPassword = editForm.ExistingPassword;
        }
    }

    private async Task PopulateDataFileOptionsAsync(ScheduledJobFormViewModel form, string userId, string? userDepartment)
    {
        DataFileManageListViewModel dataFileList = await dataFileManageService.GetListViewModelAsync(
            userId,
            userDepartment ?? string.Empty);

        form.AvailableDataFiles = dataFileList.DataFiles
            .Where(dataFile => dataFile.IsActive)
            .OrderBy(dataFile => dataFile.DataFileName, StringComparer.OrdinalIgnoreCase)
            .Select(dataFile => new ReportDefinitionLookupItem
            {
                Id = dataFile.Id,
                ReportName = dataFile.DataFileName
            })
            .ToList();
    }

    private void ValidateSchemaAndClientCode(ScheduledJobFormViewModel form)
    {
        if (!form.RequiresSchemaTemplateAndClientCode)
        {
            form.SchemaTemplate = null;
            form.ClientCode = null;
            ModelState.Remove(nameof(form.SchemaTemplate));
            ModelState.Remove(nameof(form.ClientCode));
            return;
        }

        //if (string.IsNullOrWhiteSpace(form.SchemaTemplate))
        //{
        //    ModelState.AddModelError(nameof(form.SchemaTemplate), "Schema Template is required.");
        //}

        if (string.IsNullOrWhiteSpace(form.ClientCode))
        {
            ModelState.AddModelError(nameof(form.ClientCode), "Client Code is required.");
        }
    }

    private List<ExportLocationBasePathOptionViewModel> GetAvailableExportLocationBasePathOptions()
    {
        IConfigurationSection section = configuration.GetSection(ExportLocationBasePathsSection);
        List<ExportLocationBasePathOptionViewModel> configuredOptions = section
            .GetChildren()
            .Select(child =>
            {
                string path = (child["Path"] ?? child.Value ?? string.Empty).Trim();
                string label = (child["Label"] ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                return new ExportLocationBasePathOptionViewModel
                {
                    Path = path,
                    Label = string.IsNullOrWhiteSpace(label) ? path : label
                };
            })
            .Where(option => option is not null)
            .Select(option => option!)
            .ToList();

        return configuredOptions
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetAvailableExportLocationBasePaths()
    {
        return GetAvailableExportLocationBasePathOptions()
            .Select(option => option.Path)
            .ToList();
    }

    private void ValidateExportLocation(ScheduledJobFormViewModel form)
    {
        List<string> availableBasePaths = GetAvailableExportLocationBasePaths();
        if (availableBasePaths.Count == 0)
        {
            return;
        }

        string normalizedExportLocation = NormalizeWindowsPath(form.ExportLocation);
        if (string.IsNullOrWhiteSpace(normalizedExportLocation))
        {
            ModelState.AddModelError(nameof(form.ExportLocation), "Please select an export location base path.");
            return;
        }

        if (availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => string.Equals(normalizedExportLocation, basePath!, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(form.ExportLocation), "Please select an export location subfolder.");
            return;
        }

        bool isUnderConfiguredBasePath = availableBasePaths
            .Select(NormalizeBasePath)
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Any(basePath => normalizedExportLocation.StartsWith(basePath!, StringComparison.OrdinalIgnoreCase));

        if (!isUnderConfiguredBasePath)
        {
            ModelState.AddModelError(nameof(form.ExportLocation), "Export location must start with one of the configured base paths.");
        }
    }

    private static string NormalizeWindowsPath(string? path)
    {
        string normalizedPath = (path ?? string.Empty).Trim().Replace('/', '\\');

        // Normalize duplicate separators for local drive paths like C:\\temp\\folder.
        if (normalizedPath.Length >= 3 && char.IsLetter(normalizedPath[0]) && normalizedPath[1] == ':')
        {
            string root = normalizedPath[..2];
            string tail = normalizedPath[2..];
            while (tail.Contains("\\\\", StringComparison.Ordinal))
            {
                tail = tail.Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            normalizedPath = root + tail;
        }

        return normalizedPath;
    }

    private static string NormalizeBasePath(string? path)
    {
        string normalizedPath = NormalizeWindowsPath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        return normalizedPath.EndsWith('\\') ? normalizedPath : $"{normalizedPath}\\";
    }
}
