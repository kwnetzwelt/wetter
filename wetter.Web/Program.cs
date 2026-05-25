using wetter.Web.Models;
using wetter.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.AddServerHeader = false;
});

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<WeatherSettings>(builder.Configuration.GetSection("WeatherSettings"));
builder.Services.AddHttpClient<IWeatherService, WeatherService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/api/health", () => {
    var version = Environment.GetEnvironmentVariable("WETTER_VERSION") ?? "unknown";
    return Results.Ok(new { 
        status = "Healthy", 
        version = version, 
        serverTime = DateTime.UtcNow.ToString("o") 
    });
});

app.Run();

public partial class Program { }
