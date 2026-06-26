using DataWarehousePower.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;

namespace DataWarehousePower.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private bool _isSavingAuditLogs;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<ReportStaff>          ReportStaff           { get; set; }
        public DbSet<ReportDefinition>     ReportDefinitions     { get; set; }
        public DbSet<ReportColumn>         ReportColumns         { get; set; }
        public DbSet<UserColumnPreference> UserColumnPreferences { get; set; }
        public DbSet<AuditLog>             AuditLogs             { get; set; }
        public DbSet<ScheduledReportJob>   ScheduledReportJobs   { get; set; }
        public DbSet<Department>           Departments           { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (_isSavingAuditLogs)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            List<PendingAuditEntry> pendingAuditEntries = PrepareAuditEntries();

            _isSavingAuditLogs = true;
            try
            {
                int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                if (pendingAuditEntries.Count > 0)
                {
                    foreach (PendingAuditEntry pendingAuditEntry in pendingAuditEntries)
                    {
                        foreach (PropertyEntry temporaryProperty in pendingAuditEntry.TemporaryProperties)
                        {
                            if (temporaryProperty.Metadata.IsPrimaryKey())
                            {
                                pendingAuditEntry.KeyValues[temporaryProperty.Metadata.Name] = temporaryProperty.CurrentValue;
                            }
                            else
                            {
                                pendingAuditEntry.NewValues[temporaryProperty.Metadata.Name] = temporaryProperty.CurrentValue;
                            }
                        }

                        AuditLogs.Add(pendingAuditEntry.ToAuditLog());
                    }

                    await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                }

                return result;
            }
            finally
            {
                _isSavingAuditLogs = false;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.HasKey(a => a.Id);
                e.Property(a => a.UserId).HasMaxLength(128);
                e.Property(a => a.Username).HasMaxLength(128);
                e.Property(a => a.ActionType).HasMaxLength(64);
                e.Property(a => a.Description).HasColumnType("nvarchar(max)");
                e.Property(a => a.EntityName).HasMaxLength(128);
                e.Property(a => a.EntityId).HasMaxLength(256);
                e.Property(a => a.CorrelationId).HasMaxLength(128);
                e.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
                e.Property(a => a.NewValues).HasColumnType("nvarchar(max)");
                e.Property(a => a.ChangedColumns).HasColumnType("nvarchar(max)");
                e.Property(a => a.Metadata).HasColumnType("nvarchar(max)");
                e.Property(a => a.IpAddress).HasMaxLength(64);
                e.Property(a => a.Host).HasMaxLength(256);
                e.Property(a => a.RequestMethod).HasMaxLength(16);
                e.Property(a => a.RequestPath).HasMaxLength(2048);
                e.Property(a => a.QueryString).HasMaxLength(2048);
                e.Property(a => a.UserAgent).HasMaxLength(1024);
                e.Property(a => a.Referrer).HasMaxLength(1024);
                e.Property(a => a.Protocol).HasMaxLength(16);
                e.Property(a => a.SessionId).HasMaxLength(128);
                e.HasIndex(a => a.TimestampUtc);
                e.HasIndex(a => a.UserId);
            });

            // ── UserColumnPreference: identity PK with unique scope key ──
            modelBuilder.Entity<UserColumnPreference>(e =>
            {
                e.HasKey(p => p.Id);
                e.Property(p => p.Id).ValueGeneratedOnAdd();
                e.Property(p => p.SchemaTemplate)
                    .HasMaxLength(128)
                    .HasDefaultValue(string.Empty);
                e.Property(p => p.ColumnJson).HasColumnType("nvarchar(max)");
                e.HasIndex(p => p.UserId);
                e.HasIndex(p => new { p.UserId, p.ReportDefinitionId, p.SchemaTemplate }).IsUnique();
            });

            // ── ReportDefinition ──────────────────────────────────────────────
            modelBuilder.Entity<ReportDefinition>()
                .HasMany(r => r.Columns)
                .WithOne(c => c.Report)
                .HasForeignKey(c => c.ReportDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScheduledReportJob>(e =>
            {
                e.HasKey(job => job.Id);
                e.Property(job => job.JobName).HasMaxLength(128);
                e.Property(job => job.HangfireJobId).HasMaxLength(128);
                e.Property(job => job.Format).HasMaxLength(16);
                e.Property(job => job.CronExpression).HasMaxLength(128);
                e.Property(job => job.JobAction).HasMaxLength(64);
                e.Property(job => job.RecipientEmail).HasMaxLength(2048);
                e.Property(job => job.SchemaTemplate).HasMaxLength(128);
                e.Property(job => job.ClientCode).HasMaxLength(128);
                e.Property(job => job.Parameters).HasColumnType("nvarchar(max)");
                e.Property(job => job.ExportLocation).HasMaxLength(512);
                e.Property(job => job.ExportToLocalFolder).HasDefaultValue(false);
                e.Property(job => job.IsCustom).HasDefaultValue(true);
                e.Property(job => job.EncryptedPassword).HasMaxLength(512);
                e.Property(job => job.CreatedByUserId).HasMaxLength(128);
                e.Property(job => job.CreatedByUsername).HasMaxLength(128);
                e.Property(job => job.UpdatedByUserId).HasMaxLength(128);
                e.Property(job => job.UpdatedByUsername).HasMaxLength(128);
                e.HasIndex(job => job.HangfireJobId).IsUnique();
                e.HasOne(job => job.ReportDefinition)
                    .WithMany()
                    .HasForeignKey(job => job.ReportDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Seed: Staff Report ────────────────────────────────────────────
            modelBuilder.Entity<ReportDefinition>().HasData(
                new ReportDefinition { Id = 1, ReportName = "Staff Report", SourceTable = "ReportStaff", SourceSP = null, IsActive = true }
            );

            modelBuilder.Entity<ReportColumn>().HasData(
                new ReportColumn { Id = 1, ReportDefinitionId = 1, PropertyName = "Id",   DefaultLabel = "ID",   DisplayOrder = 1 },
                new ReportColumn { Id = 2, ReportDefinitionId = 1, PropertyName = "Name", DefaultLabel = "Name", DisplayOrder = 2 },
                new ReportColumn { Id = 3, ReportDefinitionId = 1, PropertyName = "Age",  DefaultLabel = "Age",  DisplayOrder = 3 }
            );

            // ── Seed: Staff data ──────────────────────────────────────────────
            modelBuilder.Entity<ReportStaff>().HasData(
                new ReportStaff { Id = 1, Name = "Alice Johnson",  Age = 30 },
                new ReportStaff { Id = 2, Name = "Bob Smith",      Age = 45 },
                new ReportStaff { Id = 3, Name = "Carol Williams", Age = 28 },
                new ReportStaff { Id = 4, Name = "David Brown",    Age = 52 },
                new ReportStaff { Id = 5, Name = "Eve Davis",      Age = 35 }
            );
        }

        private List<PendingAuditEntry> PrepareAuditEntries()
        {
            ChangeTracker.DetectChanges();

            string userId = ResolveCurrentUserId();
            string? correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier;
            DateTime timestampUtc = DateTime.UtcNow;
            HttpContext? httpContext = _httpContextAccessor?.HttpContext;
            string? ipAddress = ResolveIpAddress(httpContext);
            string? host = ResolveHost(httpContext);
            string? requestMethod = ResolveRequestMethod(httpContext);
            string? requestPath = ResolveRequestPath(httpContext);
            string? queryString = ResolveQueryString(httpContext);
            string? userAgent = ResolveHeaderValue(httpContext, "User-Agent", 1024);
            string? referrer = ResolveHeaderValue(httpContext, "Referer", 1024);
            string? protocol = ResolveProtocol(httpContext);
            int? statusCode = ResolveStatusCode(httpContext);
            string? sessionId = ResolveSessionId(httpContext);

            List<PendingAuditEntry> pendingAuditEntries = new();

            foreach (EntityEntry entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog ||
                    entry.State is EntityState.Detached or EntityState.Unchanged)
                {
                    continue;
                }

                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                {
                    continue;
                }

                PendingAuditEntry pendingAuditEntry = new(entry)
                {
                    UserId = userId,
                    Username = ResolveCurrentUsername(),
                    CorrelationId = correlationId,
                    IpAddress = ipAddress,
                    Host = host,
                    RequestMethod = requestMethod,
                    RequestPath = requestPath,
                    QueryString = queryString,
                    UserAgent = userAgent,
                    Referrer = referrer,
                    Protocol = protocol,
                    StatusCode = statusCode,
                    SessionId = sessionId,
                    TimestampUtc = timestampUtc,
                    ActionType = entry.State switch
                    {
                        EntityState.Added => "Create",
                        EntityState.Modified => "Update",
                        EntityState.Deleted => "Delete",
                        _ => "Unknown"
                    },
                    EntityName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name
                };

                foreach (PropertyEntry property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        pendingAuditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        pendingAuditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            pendingAuditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            pendingAuditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                            {
                                pendingAuditEntry.ChangedColumns.Add(propertyName);
                                pendingAuditEntry.OldValues[propertyName] = property.OriginalValue;
                                pendingAuditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }

                if (entry.State == EntityState.Modified && pendingAuditEntry.ChangedColumns.Count == 0)
                {
                    continue;
                }

                pendingAuditEntry.ResourceLabel = entry.Entity is ReportColumn
                    ? ResolveReportColumnResourceLabel(entry, pendingAuditEntry.ActionType, pendingAuditEntry.NewValues, pendingAuditEntry.OldValues)
                    : null;

                pendingAuditEntries.Add(pendingAuditEntry);
            }

            // Consolidate all ReportColumn entries into one summary entry per (report, action) group.
            List<PendingAuditEntry> columnEntries    = pendingAuditEntries.Where(IsReportColumnEntry).ToList();
            List<PendingAuditEntry> nonColumnEntries = pendingAuditEntries.Where(e => !IsReportColumnEntry(e)).ToList();

            foreach (PendingAuditEntry consolidated in BuildConsolidatedColumnAuditEntries(columnEntries))
            {
                AuditLogs.Add(consolidated.ToAuditLog());
            }

            foreach (PendingAuditEntry pendingAuditEntry in nonColumnEntries.Where(a => a.TemporaryProperties.Count == 0))
            {
                AuditLogs.Add(pendingAuditEntry.ToAuditLog());
            }

            return nonColumnEntries.Where(a => a.TemporaryProperties.Count > 0).ToList();
        }

        private static bool IsReportColumnEntry(PendingAuditEntry entry)
            => string.Equals(entry.EntityName, "TBL_ReportColumns", StringComparison.OrdinalIgnoreCase);

        private List<PendingAuditEntry> BuildConsolidatedColumnAuditEntries(List<PendingAuditEntry> columnEntries)
        {
            if (columnEntries.Count == 0)
                return new();

            var consolidated = new List<PendingAuditEntry>();

            var groups = columnEntries.GroupBy(e => (
                ReportDefinitionId: (e.Entry.Entity as ReportColumn)?.ReportDefinitionId ?? 0,
                e.ActionType
            ));

            foreach (var group in groups)
            {
                var items = group.ToList();
                int reportDefinitionId = group.Key.ReportDefinitionId;
                string actionType = group.Key.ActionType;
                int count = items.Count;

                string? reportName = reportDefinitionId > 0
                    ? ReportDefinitions.AsNoTracking()
                        .Where(r => r.Id == reportDefinitionId)
                        .Select(r => r.ReportName)
                        .FirstOrDefault()
                    : null;

                string actionLabel = actionType switch
                {
                    "Create" => "added",
                    "Update" => "updated",
                    "Delete" => "removed",
                    _ => actionType.ToLowerInvariant()
                };

                string countNoun = count == 1 ? "column" : "columns";
                string resourceLabel = string.IsNullOrWhiteSpace(reportName)
                    ? $"{count} {countNoun} {actionLabel}"
                    : $"{reportName} - {count} {countNoun} {actionLabel}";

                PendingAuditEntry first = items[0];
                PendingAuditEntry summary = new(first.Entry)
                {
                    UserId        = first.UserId,
                    Username      = first.Username,
                    CorrelationId = first.CorrelationId,
                    IpAddress     = first.IpAddress,
                    Host          = first.Host,
                    RequestMethod = first.RequestMethod,
                    RequestPath   = first.RequestPath,
                    QueryString   = first.QueryString,
                    UserAgent     = first.UserAgent,
                    Referrer      = first.Referrer,
                    Protocol      = first.Protocol,
                    StatusCode    = first.StatusCode,
                    SessionId     = first.SessionId,
                    TimestampUtc  = first.TimestampUtc,
                    ActionType    = actionType,
                    EntityName    = first.EntityName,
                    ResourceLabel = resourceLabel,
                };

                if (reportDefinitionId > 0)
                    summary.KeyValues["ReportDefinitionId"] = reportDefinitionId;

                List<Dictionary<string, object?>> columnsNewValues = items
                    .Where(e => e.NewValues.Count > 0)
                    .Select(e => e.NewValues
                        .Where(kv => kv.Key != "ReportDefinitionId")
                        .ToDictionary(kv => kv.Key, kv => kv.Value))
                    .ToList();

                List<Dictionary<string, object?>> columnsOldValues = items
                    .Where(e => e.OldValues.Count > 0)
                    .Select(e => e.OldValues
                        .Where(kv => kv.Key != "ReportDefinitionId")
                        .ToDictionary(kv => kv.Key, kv => kv.Value))
                    .ToList();

                if (columnsNewValues.Count > 0)
                {
                    summary.NewValues["Count"]   = count;
                    summary.NewValues["Columns"] = columnsNewValues;
                }

                if (columnsOldValues.Count > 0)
                {
                    summary.OldValues["Count"]   = count;
                    summary.OldValues["Columns"] = columnsOldValues;
                }

                consolidated.Add(summary);
            }

            return consolidated;
        }

        private string ResolveCurrentUserId()
        {
            HttpContext? context = _httpContextAccessor?.HttpContext;
            if (context is null)
            {
                return "system";
            }

            string? candidate = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? context.User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(candidate) &&
                context.Request.Cookies.TryGetValue("dwp_user_id", out string? cookieUserId))
            {
                candidate = cookieUserId;
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return "anonymous";
            }

            string normalized = candidate.Trim();

            int slashIndex = normalized.LastIndexOf('\\');
            if (slashIndex >= 0 && slashIndex < normalized.Length - 1)
            {
                normalized = normalized[(slashIndex + 1)..];
            }

            int atIndex = normalized.IndexOf('@');
            if (atIndex > 0)
            {
                normalized = normalized[..atIndex];
            }

            return string.IsNullOrWhiteSpace(normalized) ? "anonymous" : normalized;
        }

        private string ResolveCurrentUsername()
        {
            HttpContext? context = _httpContextAccessor?.HttpContext;
            if (context is null)
            {
                return "System";
            }

            string? displayName = context.Session.GetString("UserDisplayName");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            string? identityName = context.User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(identityName))
            {
                return identityName.Trim();
            }

            string userId = ResolveCurrentUserId();
            return string.Equals(userId, "anonymous", StringComparison.OrdinalIgnoreCase) ? "Anonymous" : userId;
        }

        private static string BuildDescription(
            string username,
            string actionType,
            string entityName,
            string? resourceLabelOverride,
            Dictionary<string, object?> keyValues,
            Dictionary<string, object?> newValues,
            Dictionary<string, object?> oldValues,
            DateTime timestampUtc)
        {
            string? preferredEntityLabel = ResolvePreferredEntityLabel(entityName, actionType, newValues, oldValues);
            string resourceLabel = keyValues.Count > 0
                ? string.Join(", ", keyValues.Select(pair => $"{pair.Key}={pair.Value ?? "null"}"))
                : entityName;

            if (!string.IsNullOrWhiteSpace(resourceLabelOverride))
            {
                resourceLabel = resourceLabelOverride;
            }

            if (string.IsNullOrWhiteSpace(resourceLabelOverride) && !string.IsNullOrWhiteSpace(preferredEntityLabel))
            {
                resourceLabel = preferredEntityLabel;
            }

            string actionLabel = actionType.ToLowerInvariant() switch
            {
                "create" => "created",
                "update" => "updated",
                "delete" => "deleted",
                _ => actionType.ToLowerInvariant()
            };

            if (string.Equals(actionType, "Update", StringComparison.OrdinalIgnoreCase) && oldValues.Count > 0 && newValues.Count > 0)
            {
                List<string> changes = new();
                foreach (string fieldName in oldValues.Keys.Union(newValues.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    oldValues.TryGetValue(fieldName, out object? oldValue);
                    newValues.TryGetValue(fieldName, out object? newValue);

                    if (!Equals(oldValue, newValue))
                    {
                        changes.Add($"{fieldName}: {oldValue ?? "null"} -> {newValue ?? "null"}");
                    }
                }

                string changeSummary = changes.Count > 0
                    ? $" Changes: {string.Join("; ", changes)}."
                    : string.Empty;

                return $"{username} updated {entityName} ({resourceLabel}) at {timestampUtc:yyyy-MM-dd HH:mm:ss} UTC.{changeSummary}";
            }

            return $"{username} {actionLabel} {entityName} ({resourceLabel}) at {timestampUtc:yyyy-MM-dd HH:mm:ss} UTC.";
        }

        private static string? ResolvePreferredEntityLabel(
            string entityName,
            string actionType,
            Dictionary<string, object?> newValues,
            Dictionary<string, object?> oldValues)
        {
            if (string.Equals(entityName, "TBL_ReportDefinitions", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object?> reportValues = string.Equals(actionType, "Create", StringComparison.OrdinalIgnoreCase)
                    ? newValues
                    : string.Equals(actionType, "Delete", StringComparison.OrdinalIgnoreCase)
                        ? oldValues
                        : oldValues.Count > 0 ? oldValues : newValues;

                if (TryResolveNonEmptyString(reportValues, "ReportName", out string reportName))
                {
                    return reportName;
                }

                if (TryResolveNonEmptyString(reportValues, "SourceTable", out string sourceTable))
                {
                    return sourceTable;
                }

                if (TryResolveNonEmptyString(reportValues, "SourceSP", out string sourceSp))
                {
                    return sourceSp;
                }

                return null;
            }

            // For report column audit entries, show the business name instead of the identity key.
            if (!string.Equals(actionType, "Create", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(actionType, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.Equals(entityName, "TBL_ReportColumns", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Dictionary<string, object?> candidateValues = string.Equals(actionType, "Create", StringComparison.OrdinalIgnoreCase)
                ? newValues
                : oldValues;

            if (TryResolveNonEmptyString(candidateValues, "PropertyName", out string propertyName))
            {
                return propertyName;
            }

            if (TryResolveNonEmptyString(candidateValues, "DefaultLabel", out string defaultLabel))
            {
                return defaultLabel;
            }

            return null;
        }

        private static bool TryResolveNonEmptyString(
            Dictionary<string, object?> values,
            string key,
            out string result)
        {
            if (values.TryGetValue(key, out object? value) && value is string text && !string.IsNullOrWhiteSpace(text))
            {
                result = text.Trim();
                return true;
            }

            result = string.Empty;
            return false;
        }

        private string? ResolveReportColumnResourceLabel(
            EntityEntry entry,
            string actionType,
            Dictionary<string, object?> newValues,
            Dictionary<string, object?> oldValues)
        {
            ReportColumn? reportColumn = entry.Entity as ReportColumn;
            int? reportDefinitionId = reportColumn?.ReportDefinitionId > 0
                ? reportColumn.ReportDefinitionId
                : null;

            if (reportDefinitionId is null &&
                entry.Properties.FirstOrDefault(property => string.Equals(property.Metadata.Name, nameof(ReportColumn.ReportDefinitionId), StringComparison.OrdinalIgnoreCase)) is PropertyEntry reportDefinitionProperty)
            {
                if (reportDefinitionProperty.CurrentValue is int currentReportDefinitionId && currentReportDefinitionId > 0)
                {
                    reportDefinitionId = currentReportDefinitionId;
                }
                else if (reportDefinitionProperty.OriginalValue is int originalReportDefinitionId && originalReportDefinitionId > 0)
                {
                    reportDefinitionId = originalReportDefinitionId;
                }
            }

            string? reportName = reportColumn?.Report?.ReportName;
            if (string.IsNullOrWhiteSpace(reportName) && reportDefinitionId is not null)
            {
                reportName = ReportDefinitions.AsNoTracking()
                    .Where(report => report.Id == reportDefinitionId.Value)
                    .Select(report => report.ReportName)
                    .FirstOrDefault();
            }

            Dictionary<string, object?> columnValues = string.Equals(actionType, "Delete", StringComparison.OrdinalIgnoreCase)
                ? oldValues
                : newValues.Count > 0 ? newValues : oldValues;

            string? columnLabel = ResolveReportColumnLabel(columnValues);

            if (!string.IsNullOrWhiteSpace(reportName) && !string.IsNullOrWhiteSpace(columnLabel))
            {
                return $"{reportName.Trim()} - {columnLabel}";
            }

            return string.IsNullOrWhiteSpace(reportName)
                ? columnLabel
                : reportName.Trim();
        }

        private static string? ResolveIpAddress(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            string? forwarded = ResolveHeaderValue(context, "X-Forwarded-For", 256);
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                string firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return firstIp.Length > 64 ? firstIp[..64] : firstIp;
                }
            }

            string? realIp = ResolveHeaderValue(context, "X-Real-IP", 64);
            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return realIp;
            }

            string? remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
            {
                return null;
            }

            return remoteIp.Length > 64 ? remoteIp[..64] : remoteIp;
        }

        private static string? ResolveHost(HttpContext? context)
        {
            string? host = context?.Request.Host.Value;
            if (string.IsNullOrWhiteSpace(host))
            {
                return null;
            }

            string trimmed = host.Trim();
            return trimmed.Length > 256 ? trimmed[..256] : trimmed;
        }

        private static string? ResolveRequestMethod(HttpContext? context)
        {
            string? method = context?.Request.Method;
            if (string.IsNullOrWhiteSpace(method))
            {
                return null;
            }

            string trimmed = method.Trim();
            return trimmed.Length > 16 ? trimmed[..16] : trimmed;
        }

        private static string? ResolveRequestPath(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            string pathValue = $"{context.Request.PathBase}{context.Request.Path}";
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            return pathValue.Length > 2048 ? pathValue[..2048] : pathValue;
        }

        private static string? ResolveQueryString(HttpContext? context)
        {
            string? queryValue = context?.Request.QueryString.Value;
            if (string.IsNullOrWhiteSpace(queryValue))
            {
                return null;
            }

            return queryValue.Length > 2048 ? queryValue[..2048] : queryValue;
        }

        private static string? ResolveProtocol(HttpContext? context)
        {
            string? protocol = context?.Request.Protocol;
            if (string.IsNullOrWhiteSpace(protocol))
            {
                return null;
            }

            string trimmed = protocol.Trim();
            return trimmed.Length > 16 ? trimmed[..16] : trimmed;
        }

        private static int? ResolveStatusCode(HttpContext? context)
            => context?.Response?.StatusCode;

        private static string? ResolveSessionId(HttpContext? context)
        {
            if (context is null)
            {
                return null;
            }

            try
            {
                string? sessionId = context.Session.Id;
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return null;
                }

                string trimmed = sessionId.Trim();
                return trimmed.Length > 128 ? trimmed[..128] : trimmed;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static string? ResolveHeaderValue(HttpContext? context, string headerName, int maxLength)
        {
            if (context is null)
            {
                return null;
            }

            if (!context.Request.Headers.TryGetValue(headerName, out StringValues values))
            {
                return null;
            }

            string? value = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        }

        private static string? ResolveReportColumnLabel(Dictionary<string, object?> values)
        {
            if (TryResolveNonEmptyString(values, nameof(ReportColumn.PropertyName), out string propertyName))
            {
                return propertyName;
            }

            if (TryResolveNonEmptyString(values, nameof(ReportColumn.DefaultLabel), out string defaultLabel))
            {
                return defaultLabel;
            }

            return null;
        }

        private sealed class PendingAuditEntry(EntityEntry entry)
        {
            public EntityEntry Entry { get; } = entry;
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string ActionType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string EntityName { get; set; } = string.Empty;
            public string? CorrelationId { get; set; }
            public string? IpAddress { get; set; }
            public string? Host { get; set; }
            public string? RequestMethod { get; set; }
            public string? RequestPath { get; set; }
            public string? QueryString { get; set; }
            public string? UserAgent { get; set; }
            public string? Referrer { get; set; }
            public string? Protocol { get; set; }
            public int? StatusCode { get; set; }
            public string? SessionId { get; set; }
            public DateTime TimestampUtc { get; set; }
            public Dictionary<string, object?> KeyValues { get; } = new();
            public Dictionary<string, object?> OldValues { get; } = new();
            public Dictionary<string, object?> NewValues { get; } = new();
            public List<string> ChangedColumns { get; } = new();
            public List<PropertyEntry> TemporaryProperties { get; } = new();
            public string? ResourceLabel { get; set; }

            public AuditLog ToAuditLog()
            {
                return new AuditLog
                {
                    UserId = UserId,
                    Username = Username,
                    ActionType = ActionType,
                    Description = string.IsNullOrWhiteSpace(Description)
                        ? BuildDescription(Username, ActionType, EntityName, ResourceLabel, KeyValues, NewValues, OldValues, TimestampUtc)
                        : Description,
                    EntityName = EntityName,
                    EntityId = KeyValues.Count == 0 ? null : JsonSerializer.Serialize(KeyValues),
                    OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
                    NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues),
                    ChangedColumns = ChangedColumns.Count == 0 ? null : JsonSerializer.Serialize(ChangedColumns),
                    CorrelationId = CorrelationId,
                    IpAddress = IpAddress,
                    Host = Host,
                    RequestMethod = RequestMethod,
                    RequestPath = RequestPath,
                    QueryString = QueryString,
                    UserAgent = UserAgent,
                    Referrer = Referrer,
                    Protocol = Protocol,
                    StatusCode = StatusCode,
                    SessionId = SessionId,
                    TimestampUtc = TimestampUtc
                };
            }
        }
    }
}
