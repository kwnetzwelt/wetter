namespace wetter.Web.Models;

public class WeatherData
{
    public double? CurrentTemperature { get; set; }
    public double? Humidity { get; set; }
    public double? Pressure { get; set; }
    public double? WindSpeed { get; set; }
    public double? WindDirection { get; set; }
    public double? WindGust { get; set; }
    public double? MaxDailyGust { get; set; }
    public double? SolarRadiation { get; set; }
    public double? BasementTemperature { get; set; }
    public double? GroundFloorTemperature { get; set; }
    public double? UpstairsTemperature { get; set; }
    
    // History
    public List<HistoryPoint> TempHistory { get; set; } = new();
    public List<HistoryPoint> HumidityHistory { get; set; } = new();
    public List<HistoryPoint> WindHistory { get; set; } = new();
    public List<HistoryPoint> SolarHistory { get; set; } = new();
    public List<HistoryPoint> PrecipitationHistory { get; set; } = new();

    public int WeatherCode { get; set; }
    public List<DailyForecast> Forecast { get; set; } = new();
    public List<HourlyForecast> Hourly { get; set; } = new();
    public DateTime Sunrise { get; set; }
    public DateTime Sunset { get; set; }
    public bool IsDaylight { get; set; }
    public string Language { get; set; } = "de-DE";
    public string CityName { get; set; } = string.Empty;
    public string ClockType { get; set; } = "default";
}

public class HistoryPoint
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
}

public class HourlyForecast
{
    public DateTime Time { get; set; }
    public double Temp { get; set; }
    public int WeatherCode { get; set; }
    public double Precipitation { get; set; }
    public double WindSpeed { get; set; }
}

public class DailyForecast
{
    public DateTime Date { get; set; }
    public double MinTemp { get; set; }
    public double MaxTemp { get; set; }
    public int WeatherCode { get; set; }
}
