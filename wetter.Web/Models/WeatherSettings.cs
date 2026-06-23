namespace wetter.Web.Models;

public class WeatherSettings
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string Language { get; set; } = "de-DE";
    public string ClockType { get; set; } = "default";

    // Home Assistant Integration
    public bool UseHomeAssistant { get; set; } = false;
    public string HomeAssistantUrl { get; set; } = "http://192.168.1.100:8123";
    public string HomeAssistantToken { get; set; } = string.Empty;
    public string HomeAssistantEntityId { get; set; } = "weather.home"; // Main entity for forecast/icon
    
    // Granular Sensor IDs
    public string? HomeAssistantTempEntityId { get; set; }
    public string? HomeAssistantHumidityEntityId { get; set; }
    public string? HomeAssistantPressureEntityId { get; set; }
    public string? HomeAssistantWindSpeedEntityId { get; set; }
    public string? HomeAssistantWindDirectionEntityId { get; set; }
    public string? HomeAssistantWindGustEntityId { get; set; }
    public string? HomeAssistantMaxDailyGustEntityId { get; set; }
    public string? HomeAssistantSolarRadiationEntityId { get; set; }
    public string? HomeAssistantRainEntityId { get; set; }
    public string? HomeAssistantBasementTempEntityId { get; set; }
    public string? HomeAssistantGroundFloorTempEntityId { get; set; }
    public string? HomeAssistantUpstairsTempEntityId { get; set; }
}
