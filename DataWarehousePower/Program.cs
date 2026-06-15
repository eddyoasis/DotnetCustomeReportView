using DataWarehousePower.Authorization;
using DataWarehousePower.Data;
using DataWarehousePower.Middleware;
using DataWarehousePower.Repositories;
using DataWarehousePower.Services;
using Hangfire;
using Hangfire.SqlServer;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

XmlConfigurator.Configure(new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));

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
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<DepartmentAuthorizationOptions>(
    builder.Configuration.GetSection(DepartmentAuthorizationOptions.SectionName));
builder.Services.AddSingleton<IAuthorizationHandler, DepartmentAccessAuthorizationHandler>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseStatusCodePages(async context =>
{
    var request = context.HttpContext.Request;
    var response = context.HttpContext.Response;
    var user = context.HttpContext.User;

    // Clear stale marker once authentication succeeds.
    if (user?.Identity?.IsAuthenticated == true && request.Cookies.ContainsKey(ChallengeCookieName))
    {
        response.Cookies.Delete(ChallengeCookieName);
    }

    if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        response.Redirect("/Home/NoPermission");
        return;
    }

    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
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
app.UseAuthorization();

HangfireOptions hangfireOptions = app.Services.GetRequiredService<IOptions<HangfireOptions>>().Value;
string dashboardPath = string.IsNullOrWhiteSpace(hangfireOptions.DashboardPath)
    ? "/hangfire"
    : hangfireOptions.DashboardPath;

app.MapHangfireDashboard(dashboardPath)
    .RequireAuthorization(DepartmentAuthorizationPolicies.AuditlogAccess);

RecurringJob.AddOrUpdate<IAuditLogCleanupJob>(
    "audit-log-cleanup",
    job => job.DeleteExpiredLogsAsync(),
    hangfireOptions.AuditLogCleanupCron);

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
