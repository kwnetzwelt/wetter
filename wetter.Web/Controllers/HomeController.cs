using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using wetter.Web.Models;
using wetter.Web.Services;

namespace wetter.Web.Controllers;
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWeatherService _weatherService;

    public HomeController(ILogger<HomeController> logger, IWeatherService weatherService)
    {
        _logger = logger;
        _weatherService = weatherService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var weather = await _weatherService.GetWeatherAsync();
            ViewData["Version"] = Environment.GetEnvironmentVariable("WETTER_VERSION") ?? "unknown";
            return View(weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data");
            return View(new WeatherData());
        }
    }

    public async Task<IActionResult> Weather()
    {
        try
        {
            var weather = await _weatherService.GetWeatherAsync();
            return Json(weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather JSON data");
            // Return empty but valid model as fallback to keep frontend running
            return Json(new WeatherData { CityName = "Service Unavailable" });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
