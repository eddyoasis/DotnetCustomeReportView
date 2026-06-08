using DataWarehousePower.Data;
using DataWarehousePower.Repositories;
using DataWarehousePower.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IReportStaffRepository,      ReportStaffRepository>();
builder.Services.AddScoped<IColumnPreferenceRepository, ColumnPreferenceRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IReportStaffService,      ReportStaffService>();
builder.Services.AddScoped<IColumnPreferenceService, ColumnPreferenceService>();

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

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
    throw; // terminate with non-zero exit code
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ReportStaff}/{action=Index}/{id?}");

app.Run();
