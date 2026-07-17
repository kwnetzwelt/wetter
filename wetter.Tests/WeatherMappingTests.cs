using NUnit.Framework;
using wetter.Web.Services;
using wetter.Web.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Net;
using System.Text.Json;

namespace wetter.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public string WeatherJson { get; set; } = "{}";
    public string GeoJson { get; set; } = "{}";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (request.RequestUri!.ToString().Contains("api.open-meteo.com"))
        {
            response.Content = new StringContent(WeatherJson);
        }
        else if (request.RequestUri!.ToString().Contains("api.bigdatacloud.net"))
        {
            response.Content = new StringContent(GeoJson);
        }
        return Task.FromResult(response);
    }
}

public class WeatherMappingTests
{
    private WeatherService _service;

    [SetUp]
    public void Setup()
    {
        var settings = new WeatherSettings();
        var options = Options.Create(settings);
        // Using a real HttpClient and null for logger
        _service = new WeatherService(new HttpClient(), options, null!);
    }

    [Test]
    public void MapHaConditionToWmo_MapsCorrectly()
    {
        var method = typeof(WeatherService).GetMethod("MapHaConditionToWmo", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(method.Invoke(_service, new object[] { "sunny" }), Is.EqualTo(0));
        Assert.That(method.Invoke(_service, new object[] { "cloudy" }), Is.EqualTo(3));
        Assert.That(method.Invoke(_service, new object[] { "overcast" }), Is.EqualTo(3));
        Assert.That(method.Invoke(_service, new object[] { "partlycloudy" }), Is.EqualTo(1));
        Assert.That(method.Invoke(_service, new object[] { "partly-cloudy" }), Is.EqualTo(1));
        Assert.That(method.Invoke(_service, new object[] { "mostly-cloudy" }), Is.EqualTo(3));
        Assert.That(method.Invoke(_service, new object[] { "exceptional" }), Is.EqualTo(3), "Exceptional should now be 3 (Cloudy)");
        Assert.That(method.Invoke(_service, new object[] { "unknown_condition" }), Is.EqualTo(3), "Unknown should default to 3");
    }

    [Test]
    public void ApplySensorOverrides_SolarRadiation_Midday()
    {
        var method = typeof(WeatherService).GetMethod("ApplySensorOverrides", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "ApplySensorOverrides method should exist");

        var now = DateTime.UtcNow;

        // Midday scenario (fraction = 0.5, expected clear sky = 800 W/m²)
        double fMidday = 0.5;
        double hMidday = 10.0;
        var sunriseMidday = now.AddHours(-fMidday * hMidday);
        var sunsetMidday = now.AddHours((1.0 - fMidday) * hMidday);

        // Case A: Midday with very low actual solar radiation (50 W/m² < 0.25 * 800) -> overrides to Cloudy (3)
        var wdA = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMidday,
            Sunset = sunsetMidday,
            SolarRadiation = 50,
            WeatherCode = 0 // Clear initially
        };
        method.Invoke(_service, new object[] { wdA, null! });
        Assert.That(wdA.WeatherCode, Is.EqualTo(3), "Midday with very low radiation should override to Cloudy");

        // Case B: Midday with medium solar radiation (300 W/m² -> ratio 0.375) -> overrides to Partly Cloudy (1)
        var wdB = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMidday,
            Sunset = sunsetMidday,
            SolarRadiation = 300,
            WeatherCode = 0 // Clear initially
        };
        method.Invoke(_service, new object[] { wdB, null! });
        Assert.That(wdB.WeatherCode, Is.EqualTo(1), "Midday with medium radiation should override to Partly Cloudy");

        // Case C: Midday with high solar radiation (700 W/m² -> ratio 0.875) -> overrides to Clear (0)
        var wdC = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMidday,
            Sunset = sunsetMidday,
            SolarRadiation = 700,
            WeatherCode = 3 // Cloudy initially
        };
        method.Invoke(_service, new object[] { wdC, null! });
        Assert.That(wdC.WeatherCode, Is.EqualTo(0), "Midday with high radiation should override to Clear");
    }

    [Test]
    public void ApplySensorOverrides_SolarRadiation_MorningEvening()
    {
        var method = typeof(WeatherService).GetMethod("ApplySensorOverrides", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "ApplySensorOverrides method should exist");

        var now = DateTime.UtcNow;

        // Early morning scenario (fraction = 0.02, expected clear sky = 800 * sin(0.02 * PI) = 50.2 W/m² < 120.0 W/m²)
        double fMorning = 0.02;
        double hMorning = 10.0;
        var sunriseMorning = now.AddHours(-fMorning * hMorning);
        var sunsetMorning = now.AddHours((1.0 - fMorning) * hMorning);

        // Case D: Low actual radiation (10 W/m²) -> should NOT force Cloudy (3) when initially Clear (0)
        var wdD = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMorning,
            Sunset = sunsetMorning,
            SolarRadiation = 10,
            WeatherCode = 0 // Clear initially
        };
        method.Invoke(_service, new object[] { wdD, null! });
        Assert.That(wdD.WeatherCode, Is.EqualTo(0), "Morning with low radiation should NOT force Cloudy; should keep Clear");

        // Case E: High actual radiation (180 W/m²) -> overrides to Clear (0) when initially Cloudy (3)
        var wdE = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMorning,
            Sunset = sunsetMorning,
            SolarRadiation = 180,
            WeatherCode = 3 // Cloudy initially
        };
        method.Invoke(_service, new object[] { wdE, null! });
        Assert.That(wdE.WeatherCode, Is.EqualTo(0), "Morning with surprisingly high radiation should override to Clear");

        // Case F: Decent actual radiation (60 W/m²) -> upgrades to Partly Cloudy (1) when initially Cloudy (3)
        var wdF = new WeatherData
        {
            IsDaylight = true,
            Sunrise = sunriseMorning,
            Sunset = sunsetMorning,
            SolarRadiation = 60,
            WeatherCode = 3 // Cloudy initially
        };
        method.Invoke(_service, new object[] { wdF, null! });
        Assert.That(wdF.WeatherCode, Is.EqualTo(1), "Morning with decent radiation should upgrade Cloudy to Partly Cloudy");
    }

    [Test]
    public void ParseHaForecastArray_ParsesPrecipitationAndWindSpeed()
    {
        var method = typeof(WeatherService).GetMethod("ParseHaForecastArray", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "ParseHaForecastArray method should exist");

        var weatherData = new WeatherData();
        var futureDate = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:00:00Z");
        var json = $$"""
        [
            {
                "datetime": "{{futureDate}}",
                "condition": "rainy",
                "temperature": 15.0,
                "precipitation": 2.5,
                "wind_speed": 12.3
            }
        ]
        """;
        var doc = System.Text.Json.JsonDocument.Parse(json);
        
        method.Invoke(_service, new object[] { doc.RootElement, weatherData, "hourly" });

        Assert.That(weatherData.Hourly.Count, Is.EqualTo(1));
        Assert.That(weatherData.Hourly[0].Precipitation, Is.EqualTo(2.5));
        Assert.That(weatherData.Hourly[0].WindSpeed, Is.EqualTo(12.3));
    }

    [Test]
    public async Task GetOpenMeteoWeatherAsync_ParsesPrecipitationAndWindSpeed()
    {
        var mockHandler = new MockHttpMessageHandler();
        mockHandler.WeatherJson = """
        {
            "utc_offset_seconds": 0,
            "current": {
                "temperature_2m": 10.0,
                "weather_code": 0
            },
            "hourly": {
                "time": [ "2024-01-01T00:00", "2024-01-01T01:00", "2024-01-01T02:00" ],
                "temperature_2m": [ 10.0, 11.0, 12.0 ],
                "weather_code": [ 0, 1, 2 ],
                "precipitation": [ 0.0, 2.5, 5.0 ],
                "wind_speed_10m": [ 10.0, 15.0, 20.0 ]
            },
            "daily": {
                "time": [ "2024-01-01" ],
                "weather_code": [ 0 ],
                "temperature_2m_max": [ 12.0 ],
                "temperature_2m_min": [ 8.0 ],
                "sunrise": [ "2024-01-01T06:00" ],
                "sunset": [ "2024-01-01T18:00" ]
            }
        }
        """;
        mockHandler.GeoJson = """{"locality": "TestCity"}""";

        var settings = new WeatherSettings { Latitude = 50.0, Longitude = 10.0, UseHomeAssistant = false };
        var options = Options.Create(settings);
        var httpClient = new HttpClient(mockHandler);
        var service = new WeatherService(httpClient, options, null!);

        var method = typeof(WeatherService).GetMethod("GetOpenMeteoWeatherAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "GetOpenMeteoWeatherAsync method should exist");

        var task = (Task<WeatherData>)method.Invoke(service, null)!;
        var weatherData = await task;

        Assert.That(weatherData.Hourly.Count, Is.GreaterThan(0));
        Assert.That(weatherData.Hourly[0].Precipitation, Is.EqualTo(2.5));
        Assert.That(weatherData.Hourly[0].WindSpeed, Is.EqualTo(15.0));
    }
}
