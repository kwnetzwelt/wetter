using System.Text.Json;
using Microsoft.Extensions.Options;
using wetter.Web.Models;

namespace wetter.Web.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly WeatherSettings _settings;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IOptions<WeatherSettings> settings, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(2);
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WeatherData> GetWeatherAsync()
    {
        if (_settings.UseHomeAssistant && !string.IsNullOrEmpty(_settings.HomeAssistantToken))
        {
            return await GetHomeAssistantWeatherAsync();
        }

        return await GetOpenMeteoWeatherAsync();
    }

    private async Task<WeatherData> GetHomeAssistantWeatherAsync()
    {
        var baseUrl = _settings.HomeAssistantUrl.TrimEnd('/');
        var weatherData = new WeatherData
        {
            CityName = _settings.CityName,
            Language = _settings.Language,
            ClockType = _settings.ClockType
        };

        try
        {
            // 1. Current State
            var mainState = await GetHaStateAsync(baseUrl, _settings.HomeAssistantEntityId);
            if (mainState == null)
            {
                return weatherData; // HA is unreachable; return empty model immediately to avoid waiting for further timeouts
            }

            var state = mainState.Value.GetProperty("state").GetString() ?? "sunny";
            var attr = mainState.Value.GetProperty("attributes");
            weatherData.CurrentTemperature = attr.TryGetProperty("temperature", out var t) ? (double?)t.GetDouble() : null;
                weatherData.WeatherCode = MapHaConditionToWmo(state);
                weatherData.Humidity = attr.TryGetProperty("humidity", out var h) ? h.GetDouble() : null;
                weatherData.Pressure = attr.TryGetProperty("pressure", out var p) ? p.GetDouble() : null;
                weatherData.WindSpeed = attr.TryGetProperty("wind_speed", out var ws) ? ws.GetDouble() : null;
                weatherData.WindDirection = attr.TryGetProperty("wind_bearing", out var wb) ? wb.GetDouble() : null;
            

            // 2. Granular Sensors
            var tasks = new List<Task<(string key, double? value)>>();
            if (!string.IsNullOrEmpty(_settings.HomeAssistantTempEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantTempEntityId, "temp"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantHumidityEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantHumidityEntityId, "humi"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantPressureEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantPressureEntityId, "pres"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantWindSpeedEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantWindSpeedEntityId, "wspe"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantWindDirectionEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantWindDirectionEntityId, "wdir"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantWindGustEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantWindGustEntityId, "wgus"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantMaxDailyGustEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantMaxDailyGustEntityId, "mgus"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantSolarRadiationEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantSolarRadiationEntityId, "srad"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantRainEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantRainEntityId, "rain"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantBasementTempEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantBasementTempEntityId, "base_temp"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantGroundFloorTempEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantGroundFloorTempEntityId, "ground_temp"));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantUpstairsTempEntityId)) tasks.Add(FetchSensor(baseUrl, _settings.HomeAssistantUpstairsTempEntityId, "up_temp"));

            var results = await Task.WhenAll(tasks);
            double? rainValue = null;
            foreach (var r in results)
            {
                if (!r.value.HasValue) continue;
                switch (r.key)
                {
                    case "temp": weatherData.CurrentTemperature = r.value.Value; break;
                    case "humi": weatherData.Humidity = r.value.Value; break;
                    case "pres": weatherData.Pressure = r.value.Value; break;
                    case "wspe": weatherData.WindSpeed = r.value.Value; break;
                    case "wdir": weatherData.WindDirection = r.value.Value; break;
                    case "wgus": weatherData.WindGust = r.value.Value; break;
                    case "mgus": weatherData.MaxDailyGust = r.value.Value; break;
                    case "srad": weatherData.SolarRadiation = r.value.Value; break;
                    case "rain": rainValue = r.value.Value; break;
                    case "base_temp": weatherData.BasementTemperature = r.value.Value; break;
                    case "ground_temp": weatherData.GroundFloorTemperature = r.value.Value; break;
                    case "up_temp": weatherData.UpstairsTemperature = r.value.Value; break;
                }
            }

            // 4. History (24h)
            var historyTasks = new List<Task<(string key, List<HistoryPoint> points)>>();
            var startTimeStr = DateTime.UtcNow.AddHours(-24).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var tempId = !string.IsNullOrEmpty(_settings.HomeAssistantTempEntityId) ? _settings.HomeAssistantTempEntityId : _settings.HomeAssistantEntityId;
            historyTasks.Add(FetchHistory(baseUrl, tempId, "temp", startTimeStr));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantHumidityEntityId)) historyTasks.Add(FetchHistory(baseUrl, _settings.HomeAssistantHumidityEntityId, "humi", startTimeStr));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantWindSpeedEntityId)) historyTasks.Add(FetchHistory(baseUrl, _settings.HomeAssistantWindSpeedEntityId, "wspe", startTimeStr));
            if (!string.IsNullOrEmpty(_settings.HomeAssistantSolarRadiationEntityId)) historyTasks.Add(FetchHistory(baseUrl, _settings.HomeAssistantSolarRadiationEntityId, "srad", startTimeStr));

            var historyResults = await Task.WhenAll(historyTasks);
            foreach (var hr in historyResults)
            {
                switch (hr.key)
                {
                    case "temp": weatherData.TempHistory = hr.points; break;
                    case "humi": weatherData.HumidityHistory = hr.points; break;
                    case "wspe": weatherData.WindHistory = hr.points; break;
                    case "srad": weatherData.SolarHistory = hr.points; break;
                }
            }

            // 4. Sun - USE RAW UTC, BUT NORMALIZE DATE FOR 24H GRAPH Context
            var sunState = await GetHaStateAsync(baseUrl, "sun.sun");
            if (sunState != null)
            {
                var sunAttr = sunState.Value.GetProperty("attributes");
                // next_rising/setting are ALWAYS UTC ISO strings
                var nextRising = DateTime.Parse(sunAttr.GetProperty("next_rising").GetString()!).ToUniversalTime();
                var nextSetting = DateTime.Parse(sunAttr.GetProperty("next_setting").GetString()!).ToUniversalTime();
                
                // Convert to Local Time ONLY to extract the relative hour/minute
                var riseLocal = nextRising.ToLocalTime();
                var setLocal = nextSetting.ToLocalTime();
                
                // Re-normalize to "Today" based on Local Time date
                var nowLocal = DateTime.Now;
                weatherData.Sunrise = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, riseLocal.Hour, riseLocal.Minute, 0, DateTimeKind.Local).ToUniversalTime();
                weatherData.Sunset = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, setLocal.Hour, setLocal.Minute, 0, DateTimeKind.Local).ToUniversalTime();
                
                // If sunset is already passed or before sunrise in this normalized context, fix it
                if (weatherData.Sunset < weatherData.Sunrise) weatherData.Sunset = weatherData.Sunset.AddDays(1);
            }
            
            // 5. Forecast
            await FetchModernHaForecasts(baseUrl, weatherData);

            // 6. Finalize states and overrides
            weatherData.IsDaylight = DateTime.UtcNow >= weatherData.Sunrise && DateTime.UtcNow <= weatherData.Sunset;
            ApplySensorOverrides(weatherData, rainValue);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching Home Assistant weather data."); }

        return weatherData;
    }

    private async Task<(string key, List<HistoryPoint> points)> FetchHistory(string baseUrl, string entityId, string key, string startTime)
    {
        var url = $"{baseUrl}/api/history/period/{startTime}?filter_entity_id={entityId}&minimal_response";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_settings.HomeAssistantToken}");
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var entityHistory = root[0];
                    var points = new List<HistoryPoint>();
                    foreach (var stateObj in entityHistory.EnumerateArray())
                    {
                        double val = 0;
                        if (stateObj.TryGetProperty("state", out var sProp))
                        {
                            if (sProp.ValueKind == JsonValueKind.Number) val = sProp.GetDouble();
                            else if (sProp.ValueKind == JsonValueKind.String && double.TryParse(sProp.GetString(), out var dVal)) val = dVal;
                            else continue;
                        }
                        else continue;

                        string? timeStr = null;
                        if (stateObj.TryGetProperty("last_changed", out var lc)) timeStr = lc.GetString();
                        else if (stateObj.TryGetProperty("last_updated", out var lu)) timeStr = lu.GetString();
                        else if (stateObj.TryGetProperty("lc", out var lcShort)) timeStr = lcShort.GetString();
                        else if (stateObj.TryGetProperty("lu", out var luShort)) timeStr = luShort.GetString();

                        if (timeStr != null) points.Add(new HistoryPoint { Time = DateTime.Parse(timeStr).ToUniversalTime(), Value = val });
                    }
                    return (key, points);
                }
            }
        }
        catch { }
        return (key, new List<HistoryPoint>());
    }

    private async Task FetchModernHaForecasts(string baseUrl, WeatherData weatherData)
    {
        string[] types = { "hourly", "daily" };
        foreach (var type in types)
        {
            try
            {
                var url = $"{baseUrl}/api/services/weather/get_forecasts?return_response";
                var payload = new { entity_id = _settings.HomeAssistantEntityId, type = type };
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Authorization", $"Bearer {_settings.HomeAssistantToken}");
                request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    FindAndParseForecast(doc.RootElement, weatherData, type);
                }
            }
            catch { }
        }
    }

    private void FindAndParseForecast(JsonElement element, WeatherData weatherData, string type)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name == "forecast" && prop.Value.ValueKind == JsonValueKind.Array) ParseHaForecastArray(prop.Value, weatherData, type);
                else FindAndParseForecast(prop.Value, weatherData, type);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) FindAndParseForecast(item, weatherData, type);
    }

    private void ParseHaForecastArray(JsonElement forecastList, WeatherData weatherData, string type)
    {
        foreach (var f in forecastList.EnumerateArray())
        {
            if (!f.TryGetProperty("datetime", out var dtProp)) continue;
            var time = DateTime.Parse(dtProp.GetString()!).ToUniversalTime();
            var condition = f.TryGetProperty("condition", out var condProp) ? condProp.GetString() ?? "sunny" : "sunny";
            var temp = f.TryGetProperty("temperature", out var tempProp) ? tempProp.GetDouble() : 0;
            
            if (type == "daily")
            {
                // Accept any entry from a daily forecast list, ensuring we only keep one per day
                if (!weatherData.Forecast.Any(d => d.Date.Date == time.Date))
                {
                    weatherData.Forecast.Add(new DailyForecast 
                    { 
                        Date = time, 
                        MaxTemp = temp, 
                        MinTemp = f.TryGetProperty("templow", out var low) ? low.GetDouble() : temp - 5, 
                        WeatherCode = MapHaConditionToWmo(condition) 
                    });
                }
            }
            else if (type == "hourly")
            {
                // For hourly, just take the next 24 hours
                if (time > DateTime.UtcNow.AddMinutes(-30) && weatherData.Hourly.Count < 24)
                {
                    if (!weatherData.Hourly.Any(h => h.Time == time))
                        weatherData.Hourly.Add(new HourlyForecast { Time = time, Temp = temp, WeatherCode = MapHaConditionToWmo(condition) });
                }
            }
        }
        weatherData.Forecast = weatherData.Forecast.OrderBy(d => d.Date).ToList();
        weatherData.Hourly = weatherData.Hourly.OrderBy(h => h.Time).ToList();
    }

    private async Task<(string key, double? value)> FetchSensor(string baseUrl, string entityId, string key)
    {
        var state = await GetHaStateAsync(baseUrl, entityId);
        if (state != null && double.TryParse(state.Value.GetProperty("state").GetString(), out var val)) return (key, val);
        return (key, null);
    }

    private async Task<JsonElement?> GetHaStateAsync(string baseUrl, string entityId)
    {
        try
        {
            var url = $"{baseUrl}/api/states/{entityId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_settings.HomeAssistantToken}");
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonDocument.Parse(json).RootElement;
            }
        }
        catch { }
        return null;
    }

    private void ApplySensorOverrides(WeatherData weatherData, double? rainValue)
    {
        // 1. Rain Priority
        if (rainValue.HasValue && rainValue.Value > 0)
        {
            if (rainValue.Value > 2.5) weatherData.WeatherCode = 65; // Heavy rain
            else if (rainValue.Value > 0.5) weatherData.WeatherCode = 63; // Moderate rain
            else weatherData.WeatherCode = 61; // Light rain
            return;
        }

        // 2. Solar Radiation Override (Daylight only)
        // Adjusts weather code based on actual vs. expected clear-sky solar radiation.
        // During early morning/late evening, expected clear-sky solar radiation is low,
        // so we avoid forcing a Cloudy override.
        if (weatherData.IsDaylight && weatherData.SolarRadiation.HasValue)
        {
            var now = DateTime.UtcNow;
            if (weatherData.Sunrise != default && weatherData.Sunset != default && weatherData.Sunset > weatherData.Sunrise)
            {
                double totalDaylightMinutes = (weatherData.Sunset - weatherData.Sunrise).TotalMinutes;
                double minutesSinceSunrise = (now - weatherData.Sunrise).TotalMinutes;
                double fraction = minutesSinceSunrise / totalDaylightMinutes;

                if (fraction >= 0.0 && fraction <= 1.0)
                {
                    // Sinusoidal model of expected clear-sky solar radiation based on daylight fraction.
                    // Max radiation at solar noon is assumed to be 800 W/m² under clear skies.
                    const double maxSolarRadiation = 800.0;
                    double expectedClearSky = maxSolarRadiation * Math.Sin(Math.PI * fraction);

                    if (expectedClearSky >= 120.0)
                    {
                        double ratio = weatherData.SolarRadiation.Value / expectedClearSky;

                        if (ratio < 0.25)
                        {
                            weatherData.WeatherCode = 3; // Cloudy
                        }
                        else if (ratio < 0.60)
                        {
                            weatherData.WeatherCode = 1; // Partly Cloudy
                        }
                        else if (ratio >= 0.75 && weatherData.WeatherCode > 0)
                        {
                            weatherData.WeatherCode = 0; // Clear
                        }
                    }
                    else
                    {
                        // Morning or evening hour: sun is very low. 
                        // Do not force "Cloudy" if actual radiation is low because low readings are expected.
                        // However, if the actual radiation is high, we can confidently override to Clear or Partly Cloudy.
                        if (weatherData.SolarRadiation.Value > 150.0 && weatherData.WeatherCode > 0)
                        {
                            weatherData.WeatherCode = 0; // Clear
                        }
                        else if (weatherData.SolarRadiation.Value > 50.0 && weatherData.WeatherCode > 1)
                        {
                            if (weatherData.WeatherCode == 3)
                            {
                                weatherData.WeatherCode = 1; // Partly Cloudy
                            }
                        }
                    }
                }
            }
            else
            {
                // Fallback threshold logic if sunrise/sunset are not available or invalid
                if (weatherData.SolarRadiation.Value < 100) weatherData.WeatherCode = 3; // Cloudy
                else if (weatherData.SolarRadiation.Value < 300) weatherData.WeatherCode = 1; // Partly Cloudy
                else if (weatherData.SolarRadiation.Value > 500 && weatherData.WeatherCode > 0) weatherData.WeatherCode = 0; // Clear
            }
        }

        // 3. Humidity/Fog Override
        if (weatherData.Humidity.HasValue && weatherData.Humidity.Value > 95 && weatherData.CurrentTemperature.HasValue && weatherData.CurrentTemperature.Value < 15)
        {
            // High humidity + low-ish temp often means fog
            if (weatherData.WeatherCode < 45) weatherData.WeatherCode = 45;
        }

        // 4. Wind/Storm Override
        if (weatherData.WindSpeed.HasValue)
        {
            if (weatherData.WindSpeed.Value > 75) weatherData.WeatherCode = 95; // Storm
            else if (weatherData.WindSpeed.Value > 40 && weatherData.WeatherCode < 1) weatherData.WeatherCode = 1; // Windy
        }
    }

    private int MapHaConditionToWmo(string condition)
 => condition.ToLower() switch
    {
        "clear-night" => 0, 
        "sunny" => 0, 
        "partlycloudy" or "partly-cloudy" => 1, 
        "cloudy" or "overcast" => 3, 
        "fog" => 45, 
        "hail" => 56, 
        "lightning" or "lightning-rain" => 95, 
        "pouring" => 81, 
        "rainy" => 61, 
        "snowy" => 71, 
        "snowy-rainy" => 66, 
        "windy" or "windy-variant" => 1, 
        "exceptional" => 3, 
        _ => 3
    };

    private async Task<WeatherData> GetOpenMeteoWeatherAsync()
    {
        var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={_settings.Latitude}&longitude={_settings.Longitude}&current=temperature_2m,weather_code&hourly=temperature_2m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_hours=13";
        var geoUrl = $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={_settings.Latitude}&longitude={_settings.Longitude}&localityLanguage=en";
        string cityName = _settings.CityName; double currentTemp = 0; int weatherCode = 3; // Default to cloudy if fetch fails
        var sunrise = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(6), DateTimeKind.Utc);
        var sunset = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(20), DateTimeKind.Utc);
        var forecast = new List<DailyForecast>(); var hourly = new List<HourlyForecast>();
        try 
        {
            var weatherTask = _httpClient.GetAsync(weatherUrl);
            var geoTask = _httpClient.GetAsync(geoUrl);
            await Task.WhenAll(weatherTask, geoTask);
            var weatherResponse = await weatherTask; var geoResponse = await geoTask;
            if (weatherResponse.IsSuccessStatusCode)
            {
                var json = await weatherResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var current = root.GetProperty("current");
                currentTemp = current.GetProperty("temperature_2m").GetDouble();
                weatherCode = current.GetProperty("weather_code").GetInt32();
                var daily = root.GetProperty("daily");
                int utcOffset = root.TryGetProperty("utc_offset_seconds", out var offsetProp) ? offsetProp.GetInt32() : 0;

                var rawSunrise = DateTime.Parse(daily.GetProperty("sunrise")[0].GetString()!);
                var rawSunset = DateTime.Parse(daily.GetProperty("sunset")[0].GetString()!);

                sunrise = DateTime.SpecifyKind(rawSunrise, DateTimeKind.Unspecified).AddSeconds(-utcOffset);
                sunrise = DateTime.SpecifyKind(sunrise, DateTimeKind.Utc);

                sunset = DateTime.SpecifyKind(rawSunset, DateTimeKind.Unspecified).AddSeconds(-utcOffset);
                sunset = DateTime.SpecifyKind(sunset, DateTimeKind.Utc);

                var times = daily.GetProperty("time");
                var codes = daily.GetProperty("weather_code");
                var maxs = daily.GetProperty("temperature_2m_max");
                var mins = daily.GetProperty("temperature_2m_min");
                for (int i = 0; i < times.GetArrayLength(); i++)
                    forecast.Add(new DailyForecast { Date = DateTime.Parse(times[i].GetString()!), WeatherCode = codes[i].GetInt32(), MaxTemp = maxs[i].GetDouble(), MinTemp = mins[i].GetDouble() });
                var hourlyData = root.GetProperty("hourly");
                var hTimes = hourlyData.GetProperty("time");
                var hTemps = hourlyData.GetProperty("temperature_2m");
                var hCodes = hourlyData.GetProperty("weather_code");
                for (int i = 1; i < hTimes.GetArrayLength() && i <= 12; i++)
                {
                    var rawHourTime = DateTime.Parse(hTimes[i].GetString()!);
                    var adjustedHourTime = DateTime.SpecifyKind(rawHourTime, DateTimeKind.Unspecified).AddSeconds(-utcOffset);
                    adjustedHourTime = DateTime.SpecifyKind(adjustedHourTime, DateTimeKind.Utc);
                    hourly.Add(new HourlyForecast { Time = adjustedHourTime, Temp = hTemps[i].GetDouble(), WeatherCode = hCodes[i].GetInt32() });
                }
            }
            else 
            {
                _logger.LogWarning("Open-Meteo API returned status code {StatusCode}", weatherResponse.StatusCode);
            }

            if (geoResponse.IsSuccessStatusCode)
            {
                var json = await geoResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                cityName = doc.RootElement.GetProperty("locality").GetString() ?? doc.RootElement.GetProperty("city").GetString() ?? _settings.CityName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Open-Meteo weather data.");
        }
        var now = DateTime.UtcNow;
        return new WeatherData { CurrentTemperature = currentTemp, WeatherCode = weatherCode, Sunrise = sunrise, Sunset = sunset, Language = _settings.Language, CityName = cityName, ClockType = _settings.ClockType, Forecast = forecast, Hourly = hourly, IsDaylight = now >= sunrise && now <= sunset };
    }
}
