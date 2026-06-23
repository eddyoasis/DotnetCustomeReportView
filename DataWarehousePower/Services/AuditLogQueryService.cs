using DataWarehousePower.Models;
using DataWarehousePower.Data;
using DataWarehousePower.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace DataWarehousePower.Services
{
    public class AuditLogQueryService : IAuditLogQueryService
    {
        private readonly IAuditLogRepository _repository;
        private readonly AppDbContext _context;

        public AuditLogQueryService(IAuditLogRepository repository, AppDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public async Task<AuditLogDetailViewModel?> GetDetailAsync(long id)
        {
            AuditLog? auditLog = await _repository.GetByIdAsync(id);
            if (auditLog is null)
            {
                return null;
            }

            Dictionary<string, string?> oldValues = ParseJsonObject(auditLog.OldValues);
            Dictionary<string, string?> newValues = ParseJsonObject(auditLog.NewValues);
            Dictionary<int, string> departmentLookup = await _context.Departments
                .AsNoTracking()
                .ToDictionaryAsync(department => department.Id, department => department.Name);
            Dictionary<int, string> reportLookup = await _context.ReportDefinitions
                .AsNoTracking()
                .ToDictionaryAsync(report => report.Id, report => report.ReportName);

            oldValues = EnrichDisplayValues(oldValues, departmentLookup, reportLookup);
            newValues = EnrichDisplayValues(newValues, departmentLookup, reportLookup);
            List<string> changedColumns = ParseStringList(auditLog.ChangedColumns);

            List<string> comparisonFields = oldValues.Keys
                .Concat(newValues.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(field => field)
                .ToList();

            List<AuditLogFieldComparisonViewModel> comparisons = comparisonFields
                .Select(fieldName => new AuditLogFieldComparisonViewModel
                {
                    FieldName = fieldName,
                    OldValue = oldValues.TryGetValue(fieldName, out string? oldValue) ? oldValue : null,
                    NewValue = newValues.TryGetValue(fieldName, out string? newValue) ? newValue : null,
                    IsChanged = !string.Equals(
                        oldValues.TryGetValue(fieldName, out string? oldForCompare) ? oldForCompare : null,
                        newValues.TryGetValue(fieldName, out string? newForCompare) ? newForCompare : null,
                        StringComparison.Ordinal)
                })
                .ToList();

            return new AuditLogDetailViewModel
            {
                Id = auditLog.Id,
                TimestampUtc = auditLog.TimestampUtc,
                UserId = auditLog.UserId,
                Username = string.IsNullOrWhiteSpace(auditLog.Username) ? auditLog.UserId : auditLog.Username,
                ActionType = auditLog.ActionType,
                Description = string.IsNullOrWhiteSpace(auditLog.Description)
                    ? $"{(string.IsNullOrWhiteSpace(auditLog.Username) ? auditLog.UserId : auditLog.Username)} performed {auditLog.ActionType} on {auditLog.EntityName}."
                    : auditLog.Description,
                EntityName = auditLog.EntityName,
                EntityId = auditLog.EntityId,
                CorrelationId = auditLog.CorrelationId,
                IpAddress = auditLog.IpAddress,
                Host = auditLog.Host,
                RequestMethod = auditLog.RequestMethod,
                RequestPath = auditLog.RequestPath,
                QueryString = auditLog.QueryString,
                UserAgent = auditLog.UserAgent,
                Referrer = auditLog.Referrer,
                Protocol = auditLog.Protocol,
                StatusCode = auditLog.StatusCode,
                SessionId = auditLog.SessionId,
                DurationMs = auditLog.DurationMs,
                Metadata = auditLog.Metadata,
                ChangedColumns = auditLog.ChangedColumns,
                OldValuesJson = auditLog.OldValues,
                NewValuesJson = auditLog.NewValues,
                OldValues = oldValues,
                NewValues = newValues,
                ChangedColumnsList = changedColumns,
                Comparisons = comparisons,
                IsUpdateAction = auditLog.ActionType.Contains("Update", StringComparison.OrdinalIgnoreCase)
            };
        }

        public async Task<AuditLogListViewModel> GetListAsync(AuditLogFilterViewModel filter)
        {
            (List<AuditLog> logs, int totalCount) = await _repository.SearchAsync(filter);

            AuditLogListViewModel viewModel = new()
            {
                Filter = filter,
                Items = logs.Select(a => new AuditLogListItemViewModel
                {
                    Id = a.Id,
                    TimestampUtc = a.TimestampUtc,
                    UserId = a.UserId,
                    Username = string.IsNullOrWhiteSpace(a.Username) ? a.UserId : a.Username,
                    ActionType = a.ActionType,
                    Description = string.IsNullOrWhiteSpace(a.Description)
                        ? $"{(string.IsNullOrWhiteSpace(a.Username) ? a.UserId : a.Username)} performed {a.ActionType} on {a.EntityName}."
                        : a.Description,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    IpAddress = a.IpAddress,
                    Host = a.Host,
                    RequestMethod = a.RequestMethod,
                    RequestPath = a.RequestPath,
                    ChangedColumns = a.ChangedColumns,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    Metadata = a.Metadata
                }).ToList(),
                TotalCount = totalCount,
                PageNumber = filter.PageNumber <= 0 ? 1 : filter.PageNumber,
                PageSize = filter.PageSize <= 0 ? 25 : filter.PageSize,
                AvailableUsers = await _repository.GetDistinctUsersAsync(),
                AvailableActionTypes = await _repository.GetDistinctActionTypesAsync(),
                AvailableEntityNames = await _repository.GetDistinctEntityNamesAsync()
            };

            return viewModel;
        }

        private static Dictionary<string, string?> ParseJsonObject(string? json)
        {
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(json))
            {
                return values;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return values;
                }

                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = ConvertJsonValue(property.Value);
                }
            }
            catch (JsonException)
            {
                values["Raw"] = json;
            }

            return values;
        }

        private static List<string> ParseStringList(string? json)
        {
            List<string> values = new();

            if (string.IsNullOrWhiteSpace(json))
            {
                return values;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in document.RootElement.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            values.Add(element.GetString() ?? string.Empty);
                        }
                        else
                        {
                            values.Add(element.ToString());
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed JSON and keep the raw string view only.
            }

            return values;
        }

        private static string? ConvertJsonValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out long longValue)
                    ? longValue.ToString(CultureInfo.InvariantCulture)
                    : value.TryGetDecimal(out decimal decimalValue)
                        ? decimalValue.ToString(CultureInfo.InvariantCulture)
                        : value.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.ToString()
            };
        }

        private static Dictionary<string, string?> EnrichDisplayValues(
            Dictionary<string, string?> values,
            Dictionary<int, string> departmentLookup,
            Dictionary<int, string> reportLookup)
        {
            Dictionary<string, string?> enriched = new(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string?> pair in values)
            {
                string? transformed = pair.Key switch
                {
                    "Departments" => FormatDepartmentList(pair.Value, departmentLookup),
                    "ReportDefinitionId" => FormatReportDefinition(pair.Value, reportLookup),
                    _ => pair.Value
                };

                enriched[pair.Key] = transformed;
            }

            return enriched;
        }

        private static string? FormatDepartmentList(string? value, Dictionary<int, string> departmentLookup)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            List<string> formatted = new();
            foreach (string token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, out int departmentId) && departmentLookup.TryGetValue(departmentId, out string? departmentName))
                {
                    formatted.Add($"{departmentId}({departmentName})");
                }
                else
                {
                    formatted.Add(token);
                }
            }

            return string.Join(',', formatted);
        }

        private static string? FormatReportDefinition(string? value, Dictionary<int, string> reportLookup)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (int.TryParse(value, out int reportDefinitionId) && reportLookup.TryGetValue(reportDefinitionId, out string? reportName))
            {
                return $"{reportDefinitionId}({reportName})";
            }

            return value;
        }
    }
}
