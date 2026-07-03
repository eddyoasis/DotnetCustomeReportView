using DataWarehousePower.Authorization;
using DataWarehousePower.Data;
using DataWarehousePower.Middleware;
using DataWarehousePower.Models.AppSettings;
using DataWarehousePower.Repositories;
using DataWarehousePower.Services;
using Hangfire;
using Hangfire.SqlServer;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

XmlConfigurator.Configure(new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));

builder.Services.Configure<SmtpAppSetting>(builder.Configuration.GetSection("SmtpAppSettings"));
builder.Services.Configure<RemoteFolderExportLocationAppSetting>(builder.Configuration.GetSection("RemoteFolderExportLocationAppSettings"));
builder.Services.Configure<ClientCodeLookupOptions>(
    builder.Configuration.GetSection(ClientCodeLookupOptions.SectionName));
builder.Services.Configure<ClientCodeFolderLookupOptions>(
    builder.Configuration.GetSection(ClientCodeFolderLookupOptions.SectionName));
builder.Services.Configure<RequestAuditLoggingOptions>(
    builder.Configuration.GetSection(RequestAuditLoggingOptions.SectionName));

// ── EF Core ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IReportStaffRepository,      ReportStaffRepository>();
builder.Services.AddScoped<IColumnPreferenceRepository, ColumnPreferenceRepository>();
builder.Services.AddScoped<IReportRepository,           ReportRepository>();
builder.Services.AddScoped<IReportManageRepository,     ReportManageRepository>();
builder.Services.AddScoped<IAuditLogRepository,         AuditLogRepository>();
builder.Services.AddScoped<IScheduledReportJobRepository, ScheduledReportJobRepository>();
builder.Services.AddScoped<IDepartmentRepository,       DepartmentRepository>();
builder.Services.AddScoped<IDataFileColumnRepository,   DataFileColumnRepository>();
builder.Services.AddScoped<IDataFileManageRepository, DataFileManageRepository>();
builder.Services.AddScoped<IReportConnectionStringRepository, ReportConnectionStringRepository>();
builder.Services.AddScoped<IDepartmentConnectionRepository, DepartmentConnectionRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IReportService,           ReportService>();
builder.Services.AddScoped<IReportExportService,     ReportExportService>();
builder.Services.AddScoped<IColumnPreferenceService, ColumnPreferenceService>();
builder.Services.AddScoped<IReportManageService,     ReportManageService>();
builder.Services.AddScoped<IActiveDirectoryUserService, ActiveDirectoryUserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IHangfireDataProtectionService, HangfireDataProtectionService>();
builder.Services.AddScoped<IScheduledReportExecutionService, ScheduledReportExecutionService>();
builder.Services.AddScoped<IScheduledReportJobService, ScheduledReportJobService>();
builder.Services.AddScoped<IScheduledReportEmailService, ScheduledReportEmailService>();
builder.Services.AddScoped<IDepartmentService,       DepartmentService>();
builder.Services.AddScoped<IDataFileColumnService,   DataFileColumnService>();
builder.Services.AddScoped<IDataFileManageService, DataFileManageService>();
builder.Services.AddScoped<IReportConnectionStringService, ReportConnectionStringService>();
builder.Services.AddScoped<IDepartmentConnectionService, DepartmentConnectionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<DepartmentAuthorizationOptions>(
    builder.Configuration.GetSection(DepartmentAuthorizationOptions.SectionName));
builder.Services.AddSingleton<IAuthorizationHandler, DepartmentAccessAuthorizationHandler>();

// ── Data Protection ──────────────────────────────────────────────────────────
// Persist keys to a stable directory so they survive app pool recycles and
// redeployments. Configure DataProtection:KeysPath in appsettings.json on
// each environment to an absolute path outside the deployment folder.
string? dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
IDataProtectionBuilder dpBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("DataWarehousePower");
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<HangfireOptions>(
    builder.Configuration.GetSection(HangfireOptions.SectionName));
builder.Services.AddScoped<IAuditLogCleanupJob, AuditLogCleanupJob>();
builder.Services.AddHangfire(configuration =>
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            SchemaName = "ReportViewer_Hangfire",
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        });
});
builder.Services.AddHangfireServer();

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Authentication / Authorization (Windows AD) ─────────────────────────────
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

AuthorizationBuilder authorizationBuilder = builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

authorizationBuilder.AddPolicy(
    DepartmentAuthorizationPolicies.ReportAccess,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new DepartmentAccessRequirement("Report")));

authorizationBuilder.AddPolicy(
    DepartmentAuthorizationPolicies.ReportManageAccess,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new DepartmentAccessRequirement("ReportManage")));

authorizationBuilder.AddPolicy(
    DepartmentAuthorizationPolicies.AuditlogAccess,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new DepartmentAccessRequirement("Auditlog")));

authorizationBuilder.AddPolicy(
    DepartmentAuthorizationPolicies.JobDashboardAccess,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new DepartmentAccessRequirement("JobDashboard")));

authorizationBuilder.AddPolicy(
    DepartmentAuthorizationPolicies.AdminAccess,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new DepartmentAccessRequirement("Admin")));

var app = builder.Build();
const string ChallengeCookieName = "dw_auth_challenge";

// ── Auto-migrate on startup ───────────────────────────────────────────────────
try
{
    //using var scope = app.Services.CreateScope();
    //var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    //db.Database.Migrate();
}
catch (Exception ex)
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Startup");
    logger.LogCritical(ex, "Database migration failed. Application cannot start.");
    throw;
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<RequestExceptionLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
static bool IsInteractiveBrowserNavigation(HttpRequest request)
{
    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
    {
        return false;
    }

    // Keep API/AJAX responses as raw status codes instead of redirecting to HTML pages.
    if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    string acceptHeader = request.Headers.Accept.ToString();
    return !string.IsNullOrWhiteSpace(acceptHeader) &&
           acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase);
}

app.UseStatusCodePages(async context =>
{
    var request = context.HttpContext.Request;
    var response = context.HttpContext.Response;
    var user = context.HttpContext.User;
    bool isInteractiveNavigation = IsInteractiveBrowserNavigation(request);

    // Clear stale marker once authentication succeeds.
    if (user?.Identity?.IsAuthenticated == true && request.Cookies.ContainsKey(ChallengeCookieName))
    {
        response.Cookies.Delete(ChallengeCookieName);
    }

    if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        if (isInteractiveNavigation)
        {
            response.Redirect("/Home/NoPermission");
        }

        return;
    }

    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        if (!isInteractiveNavigation)
        {
            return;
        }

        bool hasAuthorizationHeader = request.Headers.ContainsKey("Authorization");
        bool hasChallengeMarker = request.Cookies.ContainsKey(ChallengeCookieName);

        // Step 1: issue Negotiate challenge without redirect so domain users can auto-login.
        if (!hasAuthorizationHeader && !hasChallengeMarker)
        {
            response.Cookies.Append(
                ChallengeCookieName,
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = request.IsHttps,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(2)
                });

            return;
        }

        // If credentials were attempted and still failed, or no credentials arrived after the
        // initial challenge, redirect to Unauthorized page.
        if (hasAuthorizationHeader || hasChallengeMarker)
        {
            response.Cookies.Delete(ChallengeCookieName);
            response.Redirect("/Home/Unauthorized");
        }
    }
});
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.Request.Cookies.ContainsKey(ChallengeCookieName))
    {
        context.Response.Cookies.Delete(ChallengeCookieName);
    }

    await next();
});
app.UseMiddleware<UserDisplayNameSessionMiddleware>();
app.UseMiddleware<RequestAuditLoggingMiddleware>();
app.UseAuthorization();

HangfireOptions hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;
TimeZoneInfo hangfireTimeZone = HangfireTimeZoneResolver.Resolve(hangfireOptions.TimeZoneId);
string dashboardPath = string.IsNullOrWhiteSpace(hangfireOptions.DashboardPath)
    ? "/hangfire"
    : hangfireOptions.DashboardPath;

app.MapHangfireDashboard(dashboardPath, new DashboardOptions
{
    // Remove the default LocalRequestsOnlyAuthorizationFilter so that
    // non-localhost browsers (e.g. UAT users) are not blocked by Hangfire
    // before ASP.NET Core's RequireAuthorization policy even runs.
    Authorization = []
})
.RequireAuthorization(DepartmentAuthorizationPolicies.AuditlogAccess);

RecurringJob.AddOrUpdate<IAuditLogCleanupJob>(
    "audit-log-cleanup",
    job => job.DeleteExpiredLogsAsync(),
    hangfireOptions.AuditLogCleanupCron,
    new RecurringJobOptions
    {
        TimeZone = hangfireTimeZone
    });

using (IServiceScope scope = app.Services.CreateScope())
{
    IScheduledReportJobService scheduledReportJobService = scope.ServiceProvider.GetRequiredService<IScheduledReportJobService>();
    await scheduledReportJobService.SyncRecurringJobsAsync();
}

// Root "/" → redirect to /Report/List which picks the first report
app.MapGet("/", () => Results.Redirect("/Report/List"));

// /Report/List  → picks the first available report
app.MapControllerRoute(
    name: "reportList",
    pattern: "Report/List",
    defaults: new { controller = "Report", action = "List" });

// /Report/{id}  and  /Report/{id}/SavePreferences
app.MapControllerRoute(
    name: "report",
    pattern: "Report/{id:int}/{action=Index}",
    defaults: new { controller = "Report" });

// /ReportManage  (CRUD management)
app.MapControllerRoute(
    name: "reportManage",
    pattern: "ReportManage/{action=Index}/{id?}",
    defaults: new { controller = "ReportManage" });

app.MapControllerRoute(
    name: "scheduledJobs",
    pattern: "ScheduledJob/{action=Index}/{id?}",
    defaults: new { controller = "ScheduledJob" });

// /AuditLog
app.MapControllerRoute(
    name: "auditLog",
    pattern: "AuditLog/{action=Index}/{id?}",
    defaults: new { controller = "AuditLog" });

// Fallback default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
