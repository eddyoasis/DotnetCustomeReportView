using DataWarehousePower.Data;
using DataWarehousePower.Middleware;
using DataWarehousePower.Repositories;
using DataWarehousePower.Services;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IReportService,           ReportService>();
builder.Services.AddScoped<IReportExportService,     ReportExportService>();
builder.Services.AddScoped<IColumnPreferenceService, ColumnPreferenceService>();
builder.Services.AddScoped<IReportManageService,     ReportManageService>();
builder.Services.AddScoped<IActiveDirectoryUserService, ActiveDirectoryUserService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Authentication / Authorization (Windows AD) ─────────────────────────────
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
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
app.UseAuthentication();
app.UseMiddleware<UserDisplayNameSessionMiddleware>();
app.UseAuthorization();

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

// Fallback default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
