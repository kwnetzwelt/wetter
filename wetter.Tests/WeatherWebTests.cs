using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;

namespace wetter.Tests;

public class WeatherWebTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Get_Index_ReturnsSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.That(response.Content.Headers.ContentType?.ToString(), Does.Contain("text/html"));
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("digital-clock"), "Digital clock should be present");
        Assert.That(content, Does.Contain("analog-clock"), "Analog clock should be present");
        Assert.That(content, Does.Contain("weather-segment"), "Weather segment should be present");
        Assert.That(content, Does.Contain("sun-tracker-segment"), "Sun tracker segment should be present");
        Assert.That(content, Does.Contain("sky-background"), "Sky background should be present");
        Assert.That(content, Does.Contain("version-label"), "Version label should be present");

        // JS Functionality checks
        Assert.That(content, Does.Contain("window.effectManager.setEffects"), "Particle effects should be initialized");
        Assert.That(content, Does.Contain("drawSunCurve()"), "Sun animation should be started");
        Assert.That(content, Does.Contain("checkServerStatus"), "Health check polling should be started");
        Assert.That(content, Does.Contain("performSoftReload"), "Soft reload logic should be present");
    }

    [Test]
    public async Task Get_Index_ContainsGitLabBadgeLink()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("gitlab-badge-container"), "GitLab badge container should be present");
        Assert.That(content, Does.Contain("https://gitlab.qwirl.eu/kai.wegner/wetter"), "GitLab repository source link should be correct and present");
    }

    [Test]
    public async Task Get_HealthApi_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.ToString(), Does.Contain("application/json"));
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Healthy"));
        Assert.That(content, Does.Contain("version"));
    }

    [Test]
    public async Task Get_WeatherJson_ReturnsJsonData()
    {
        // Act
        var response = await _client.GetAsync("/Home/Weather");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.That(response.Content.Headers.ContentType?.ToString(), Does.Contain("application/json"));
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("currentTemperature"));
        Assert.That(content, Does.Contain("cityName"));

        // Verify that sunrise/sunset and hourly forecast are in UTC
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var root = doc.RootElement;
        
        Assert.That(root.TryGetProperty("sunrise", out var sunriseProp), Is.True);
        Assert.That(root.TryGetProperty("sunset", out var sunsetProp), Is.True);
        
        var sunriseStr = sunriseProp.GetString();
        var sunsetStr = sunsetProp.GetString();
        
        Assert.That(sunriseStr, Does.EndWith("Z"), "Sunrise should be serialized as a UTC ISO string ending with Z");
        Assert.That(sunsetStr, Does.EndWith("Z"), "Sunset should be serialized as a UTC ISO string ending with Z");

        if (root.TryGetProperty("hourly", out var hourlyProp) && hourlyProp.ValueKind == System.Text.Json.JsonValueKind.Array && hourlyProp.GetArrayLength() > 0)
        {
            var firstHour = hourlyProp[0];
            if (firstHour.TryGetProperty("time", out var timeProp))
            {
                var timeStr = timeProp.GetString();
                Assert.That(timeStr, Does.EndWith("Z"), "Hourly forecast times should be serialized as UTC ISO strings ending with Z");
            }
        }
    }

    [Test]
    public async Task Get_SiteJs_ContainsCoreClasses()
    {
        // Act
        var response = await _client.GetAsync("/js/site.js");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("class Particle"), "Particle class should be in site.js");
        Assert.That(content, Does.Contain("class EffectManager"), "EffectManager class should be in site.js");
        Assert.That(content, Does.Contain("window.effectManager = new EffectManager()"), "EffectManager should be instantiated");
    }
}
