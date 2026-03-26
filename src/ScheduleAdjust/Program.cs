using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using ScheduleAdjust.Data;
using ScheduleAdjust.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Authentication - Entra ID (Azure AD)
builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration)
    .EnableTokenAcquisitionToCallDownstreamApi(
        new[] { "Calendars.ReadWrite", "OnlineMeetings.ReadWrite", "User.Read.All", "Mail.Send" })
    .AddInMemoryTokenCaches();

// EF Core - SQL Server
builder.Services.AddDbContext<ScheduleAdjustDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Graph API service (conditional: stub for development)
if (builder.Configuration.GetValue<bool>("UseStubGraphApi"))
{
    builder.Services.AddScoped<IGraphApiService, StubGraphApiService>();
}
else
{
    builder.Services.AddSingleton<GraphServiceClient>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var credential = new ClientSecretCredential(
            config["AzureAd:TenantId"],
            config["AzureAd:ClientId"],
            config["AzureAd:ClientSecret"]);
        return new GraphServiceClient(credential);
    });
    builder.Services.AddScoped<IGraphApiService, GraphApiService>();
}

// Business services
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Background service for deadline processing
builder.Services.AddHostedService<DeadlineHostedService>();

// Memory cache
builder.Services.AddMemoryCache();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ScheduleAdjustDbContext>();

// MVC
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Route registration (booking route first for GUID-based URLs)
app.MapControllerRoute(
    name: "booking-confirmed",
    pattern: "Booking/{guid:guid}/Confirmed/{responseId:int}",
    defaults: new { controller = "Booking", action = "Confirmed" });

app.MapControllerRoute(
    name: "booking-submit",
    pattern: "Booking/{guid:guid}",
    defaults: new { controller = "Booking", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Schedule}/{action=Index}/{id?}");

// Health check endpoint for Azure App Service
app.MapHealthChecks("/health");

app.Run();
