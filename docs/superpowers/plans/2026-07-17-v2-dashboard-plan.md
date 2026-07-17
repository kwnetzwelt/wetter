# V2 Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a secondary dashboard view optimized for distant reading, featuring an alternative layout with a split view.

**Architecture:** We will extend the existing `WeatherData` model to include precipitation and wind forecast data. The `WeatherService` will be updated to populate these fields from OpenMeteo and Home Assistant. A new `V2` action in `HomeController` will serve the `v2.cshtml` view, which implements a responsive 50/50 split layout. The view will reuse existing auto-refresh JS but implement its own layout and canvas rendering logic.

**Tech Stack:** ASP.NET Core MVC, C#, HTML5 Canvas, JavaScript (jQuery), Bootstrap (CSS).

## Global Constraints
- **Styling:** The background must be completely black (`#000`).
- **Layout Split:** 50/50 split on landscape, stacking vertically on portrait/narrow screens.
- **Sun position:** Always in the center of the timeline canvas. X-Axis from -24h to +24h.
- **Canvas Colors:** Temp history (yellow), Temp forecast (light yellow). Precip history (blue), Precip forecast (light blue). Wind history (grey), Wind forecast (light grey).

---

### Task 1: Update Data Model

**Files:**
- Modify: `wetter.Web/Models/WeatherData.cs`

**Interfaces:**
- Produces: `WeatherData` with `PrecipitationHistory`, and `HourlyForecast` with `Precipitation` and `WindSpeed`.

- [ ] **Step 1: Write the failing test**

*(No direct unit tests for simple DTOs currently exist in the `wetter.Tests` project, but we verify compilation.)*

```bash
dotnet build
```

- [ ] **Step 2: Write minimal implementation**

Modify `wetter.Web/Models/WeatherData.cs`. Add `PrecipitationHistory` to `WeatherData`, and `Precipitation` / `WindSpeed` to `HourlyForecast`.

```csharp
// In wetter.Web/Models/WeatherData.cs

// Add to WeatherData class:
public List<HistoryPoint> PrecipitationHistory { get; set; } = new();

// Update HourlyForecast class to include new properties:
public class HourlyForecast
{
    public DateTime Time { get; set; }
    public double Temp { get; set; }
    public int WeatherCode { get; set; }
    public double Precipitation { get; set; }
    public double WindSpeed { get; set; }
}
```

- [ ] **Step 3: Run build to verify compilation**

```bash
dotnet build
```
Expected: Build Succeeded.

- [ ] **Step 4: Commit**

```bash
git add wetter.Web/Models/WeatherData.cs
git commit -m "feat: add precipitation and wind speed to weather model"
```

---

### Task 2: Update WeatherService

**Files:**
- Modify: `wetter.Web/Services/WeatherService.cs`

**Interfaces:**
- Consumes: Updated `WeatherData` model.
- Produces: Populated `PrecipitationHistory`, `Precipitation`, and `WindSpeed` fields from API sources.

- [ ] **Step 1: Update OpenMeteo Service Logic**

Modify `wetter.Web/Services/WeatherService.cs`. Update `GetOpenMeteoWeatherAsync` to request `precipitation` and `wind_speed_10m`.

```csharp
// Update the weatherUrl string:
var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={_settings.Latitude}&longitude={_settings.Longitude}&current=temperature_2m,weather_code&hourly=temperature_2m,weather_code,precipitation,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_hours=48";

// Parse the new properties in the hourly loop:
var hPrecip = hourlyData.GetProperty("precipitation");
var hWind = hourlyData.GetProperty("wind_speed_10m");

for (int i = 1; i < hTimes.GetArrayLength() && i <= 24; i++) // increase to 24 for canvas
{
    var rawHourTime = DateTime.Parse(hTimes[i].GetString()!);
    var adjustedHourTime = DateTime.SpecifyKind(rawHourTime, DateTimeKind.Unspecified).AddSeconds(-utcOffset);
    adjustedHourTime = DateTime.SpecifyKind(adjustedHourTime, DateTimeKind.Utc);
    hourly.Add(new HourlyForecast { 
        Time = adjustedHourTime, 
        Temp = hTemps[i].GetDouble(), 
        WeatherCode = hCodes[i].GetInt32(),
        Precipitation = hPrecip[i].GetDouble(),
        WindSpeed = hWind[i].GetDouble()
    });
}
```

- [ ] **Step 2: Update HomeAssistant Service Logic**

In `GetHomeAssistantWeatherAsync`, fetch Rain history.

```csharp
// In the 4. History (24h) section:
if (!string.IsNullOrEmpty(_settings.HomeAssistantRainEntityId)) historyTasks.Add(FetchHistory(baseUrl, _settings.HomeAssistantRainEntityId, "rain", startTimeStr));

// In historyResults processing:
case "rain": weatherData.PrecipitationHistory = hr.points; break;
```

In `ParseHaForecastArray`, capture wind and precip for hourly:

```csharp
// inside type == "hourly" block:
var precip = f.TryGetProperty("precipitation", out var pProp) ? pProp.GetDouble() : 0;
var wind = f.TryGetProperty("wind_speed", out var wProp) ? wProp.GetDouble() : 0;

if (!weatherData.Hourly.Any(h => h.Time == time))
    weatherData.Hourly.Add(new HourlyForecast { 
        Time = time, 
        Temp = temp, 
        WeatherCode = MapHaConditionToWmo(condition),
        Precipitation = precip,
        WindSpeed = wind
    });
```

- [ ] **Step 3: Run build to verify compilation**

```bash
dotnet build
```
Expected: Build Succeeded.

- [ ] **Step 4: Commit**

```bash
git add wetter.Web/Services/WeatherService.cs
git commit -m "feat: populate precipitation and wind forecast data"
```

---

### Task 3: Add Controller Action

**Files:**
- Modify: `wetter.Web/Controllers/HomeController.cs`

**Interfaces:**
- Consumes: `IWeatherService.GetWeatherAsync()`
- Produces: Rendering of `v2.cshtml` with `WeatherData`.

- [ ] **Step 1: Write Controller Action**

Modify `wetter.Web/Controllers/HomeController.cs`.

```csharp
[HttpGet("Home/v2")]
public async Task<IActionResult> V2()
{
    try
    {
        var weather = await _weatherService.GetWeatherAsync();
        ViewData["Version"] = Environment.GetEnvironmentVariable("WETTER_VERSION") ?? "unknown";
        return View(weather);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching weather data for V2");
        return View(new WeatherData());
    }
}
```

- [ ] **Step 2: Run build to verify compilation**

```bash
dotnet build
```
Expected: Build Succeeded.

- [ ] **Step 3: Commit**

```bash
git add wetter.Web/Controllers/HomeController.cs
git commit -m "feat: add V2 controller action"
```

---

### Task 4: Implement v2.cshtml Structure & Left Column

**Files:**
- Create: `wetter.Web/Views/Home/v2.cshtml`

**Interfaces:**
- Consumes: `WeatherData` from Controller.
- Produces: The basic DOM structure and the Left column UI.

- [ ] **Step 1: Create View File**

Create `wetter.Web/Views/Home/v2.cshtml`.

```html
@using System.Globalization
@model wetter.Web.Models.WeatherData
@{
    Layout = "_Layout";
}

<style>
    body { background-color: #000; color: #fff; overflow: hidden; }
    .v2-container { display: flex; width: 100vw; height: 100vh; overflow: hidden; }
    .v2-left, .v2-right { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
    .v2-left { border-right: 2px solid #333; }
    
    .temp-block { flex: 1; display: flex; flex-direction: column; justify-content: center; align-items: center; border-bottom: 1px solid #222; }
    .temp-block:last-child { border-bottom: none; }
    .temp-value { font-size: clamp(3rem, 12vh, 10rem); font-weight: bold; line-height: 1; font-family: 'JetBrains Mono', monospace; }
    .temp-label { font-size: clamp(1rem, 3vh, 2rem); opacity: 0.6; text-transform: uppercase; letter-spacing: 2px; }

    @@media (orientation: portrait), (max-aspect-ratio: 1/1) {
        .v2-container { flex-direction: column; }
        .v2-left { border-right: none; border-bottom: 2px solid #333; }
        .temp-value { font-size: 8vh; }
        .temp-label { font-size: 2vh; }
    }
</style>

<div class="v2-container">
    <div class="v2-left">
        <div class="temp-block">
            <div class="temp-value" id="v2-temp-out">@(Model.CurrentTemperature?.ToString("F1") ?? "N/A")°</div>
            <div class="temp-label">Outdoors</div>
        </div>
        <div class="temp-block">
            <div class="temp-value" id="v2-temp-up">@(Model.UpstairsTemperature?.ToString("F1") ?? "N/A")°</div>
            <div class="temp-label">Upstairs</div>
        </div>
        <div class="temp-block">
            <div class="temp-value" id="v2-temp-ground">@(Model.GroundFloorTemperature?.ToString("F1") ?? "N/A")°</div>
            <div class="temp-label">Ground Level</div>
        </div>
        <div class="temp-block">
            <div class="temp-value" id="v2-temp-base">@(Model.BasementTemperature?.ToString("F1") ?? "N/A")°</div>
            <div class="temp-label">Basement</div>
        </div>
    </div>
    <div class="v2-right" id="v2-right-col">
        <!-- Right column placeholder -->
    </div>
</div>
```

- [ ] **Step 2: Commit**

```bash
git add wetter.Web/Views/Home/v2.cshtml
git commit -m "feat: implement v2 basic layout and left column"
```

---

### Task 5: Implement v2.cshtml Right Column Forecast Cards

**Files:**
- Modify: `wetter.Web/Views/Home/v2.cshtml`

**Interfaces:**
- Consumes: The `v2-right` column.

- [ ] **Step 1: Add HTML Structure**

Modify the `<div class="v2-right" id="v2-right-col">` element:

```html
    <div class="v2-right" id="v2-right-col">
        <div style="flex: 0 0 25%; display: flex; border-bottom: 1px solid #222;" id="v2-hourly-cards">
            <!-- Populated via JS -->
        </div>
        <div style="flex: 0 0 25%; display: flex; border-bottom: 1px solid #222;" id="v2-daily-cards">
            <!-- Populated via JS -->
        </div>
        <div style="flex: 1; position: relative;" id="v2-timeline-container">
            <canvas id="v2-sun-curve" style="width: 100%; height: 100%; display: block;"></canvas>
        </div>
    </div>
```

- [ ] **Step 2: Add CSS for Cards**

Add to the `<style>` block:

```css
    .fc-card { flex: 1; display: flex; flex-direction: column; justify-content: center; align-items: center; border-right: 1px solid #222; }
    .fc-card:last-child { border-right: none; }
    .fc-time { font-size: clamp(0.8rem, 2vh, 1.5rem); opacity: 0.7; }
    .fc-icon { font-size: clamp(2rem, 5vh, 4rem); margin: 0.5vh 0; }
    .fc-temp { font-size: clamp(1.2rem, 3vh, 2rem); font-weight: bold; }
```

- [ ] **Step 3: Add Update Script**

At the bottom of `v2.cshtml`, add the script block to fetch `/Home/Weather` and render cards. Note: rely on `window.getWeatherIconJs` returning valid unicode if defined globally, or define it locally if needed. (Assuming it's not globally available in V2 unless we duplicate it, let's duplicate the minimal icon mapper or rely on `_Layout`). The V1 logic defines it inside its view. Let's include a minimal self-contained JS block.

```html
<script>
(function() {
    function getWeatherIcon(code, isDay) {
        if (isDay === undefined) isDay = true;
        const icons = { 
            0: isDay ? "☀️" : "🌙", 1: isDay ? "🌤️" : "☁️", 2: isDay ? "⛅" : "☁️", 
            3: "☁️", 45: "☁️", 48: "☁️", 51: "🌦️", 53: "🌦️", 55: "🌦️", 61: "🌧️", 
            63: "🌧️", 65: "🌧️", 71: "❄️", 80: "🌧️", 81: "🌧️", 95: "⛈️" 
        };
        return icons[code] || "☁️";
    }

    function updateView() {
        fetch('/Home/Weather').then(r => r.json()).then(d => {
            document.getElementById('v2-temp-out').innerText = d.currentTemperature != null ? d.currentTemperature.toFixed(1) + '°' : 'N/A';
            document.getElementById('v2-temp-up').innerText = d.upstairsTemperature != null ? d.upstairsTemperature.toFixed(1) + '°' : 'N/A';
            document.getElementById('v2-temp-ground').innerText = d.groundFloorTemperature != null ? d.groundFloorTemperature.toFixed(1) + '°' : 'N/A';
            document.getElementById('v2-temp-base').innerText = d.basementTemperature != null ? d.basementTemperature.toFixed(1) + '°' : 'N/A';

            const hourly = d.hourly || [];
            let hHtml = '';
            hourly.slice(0, 6).forEach(h => {
                const t = new Date(h.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                hHtml += `<div class="fc-card"><div class="fc-time">${t}</div><div class="fc-icon">${getWeatherIcon(h.weatherCode, true)}</div><div class="fc-temp">${Math.round(h.temp)}°</div></div>`;
            });
            document.getElementById('v2-hourly-cards').innerHTML = hHtml;

            const daily = d.forecast || [];
            let dHtml = '';
            daily.slice(0, 6).forEach(day => {
                const dt = new Date(day.date).toLocaleDateString('de-DE', { weekday: 'short' });
                dHtml += `<div class="fc-card"><div class="fc-time">${dt}</div><div class="fc-icon">${getWeatherIcon(day.weatherCode, true)}</div><div class="fc-temp">${Math.round(day.maxTemp)}°</div></div>`;
            });
            document.getElementById('v2-daily-cards').innerHTML = dHtml;
            
            // Stash data globally for canvas rendering
            window.v2Data = d;
        });
    }

    updateView();
    setInterval(updateView, 30000); // 30s update
})();
</script>
```

- [ ] **Step 4: Commit**

```bash
git add wetter.Web/Views/Home/v2.cshtml
git commit -m "feat: implement right column forecast cards and data binding"
```

---

### Task 6: Implement v2.cshtml Timeline Canvas

**Files:**
- Modify: `wetter.Web/Views/Home/v2.cshtml`

**Interfaces:**
- Consumes: `window.v2Data` updated in Task 5.

- [ ] **Step 1: Canvas Rendering Logic**

Add the canvas drawing logic to the script block.

```javascript
// Add inside the script block below updateView:

function drawCanvas() {
    const canvas = document.getElementById('v2-sun-curve');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    
    // Auto-resize
    const rect = canvas.parentElement.getBoundingClientRect();
    if (canvas.width !== rect.width || canvas.height !== rect.height) {
        canvas.width = rect.width;
        canvas.height = rect.height;
    }
    
    const w = canvas.width, h = canvas.height;
    ctx.clearRect(0, 0, w, h);
    
    // Draw horizon
    const horizonY = h / 2;
    ctx.beginPath();
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 1;
    ctx.moveTo(0, horizonY);
    ctx.lineTo(w, horizonY);
    ctx.stroke();

    const d = window.v2Data;
    if (!d) {
        requestAnimationFrame(drawCanvas);
        return;
    }

    const now = new Date();
    const msPer24h = 24 * 60 * 60 * 1000;
    const startT = now.getTime() - msPer24h;
    const endT = now.getTime() + msPer24h;
    const totalMs = endT - startT;

    function getX(t) {
        return ((t - startT) / totalMs) * w;
    }

    function drawLine(dataArray, valSelector, timeSelector, color, minY = 0, maxY = 50) {
        if (!dataArray || !dataArray.length) return;
        const pts = dataArray.map(item => ({
            t: new Date(timeSelector(item)).getTime(),
            v: valSelector(item)
        })).filter(p => p.t >= startT && p.t <= endT).sort((a,b) => a.t - b.t);

        if (pts.length < 2) return;
        
        ctx.beginPath();
        ctx.strokeStyle = color;
        ctx.lineWidth = 2;
        
        pts.forEach((p, i) => {
            const x = getX(p.t);
            // clamp value
            let val = p.v;
            if(val < minY) val = minY;
            if(val > maxY) val = maxY;
            // Map val to Y: bottom 10% to top 10%
            const y = h * 0.9 - ((val - minY) / (maxY - minY)) * (h * 0.8);
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });
        ctx.stroke();
    }

    // Left Side (-24h)
    drawLine(d.tempHistory, p => p.value, p => p.time, '#ffc107', -10, 40); // Yellow
    drawLine(d.precipitationHistory, p => p.value, p => p.time, '#0d6efd', 0, 20); // Blue
    drawLine(d.windHistory, p => p.value, p => p.time, '#6c757d', 0, 100); // Grey

    // Right Side (+24h)
    // Filter hourly to future
    const hourlyFcast = (d.hourly || []).filter(h => new Date(h.time).getTime() > now.getTime());
    drawLine(hourlyFcast, p => p.temp, p => p.time, '#fff3cd', -10, 40); // Light Yellow
    drawLine(hourlyFcast, p => p.precipitation, p => p.time, '#8bb9fe', 0, 20); // Light Blue
    drawLine(hourlyFcast, p => p.windSpeed, p => p.time, '#adb5bd', 0, 100); // Light Grey

    // Draw Sun at Center
    const sunX = w / 2;
    const sunY = horizonY;
    
    // Pulse effect
    const pulse = Math.sin(Date.now() / 500) * 5;
    
    ctx.beginPath();
    ctx.arc(sunX, sunY, 15 + pulse, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255, 193, 7, 0.3)';
    ctx.fill();

    ctx.beginPath();
    ctx.arc(sunX, sunY, 10, 0, Math.PI * 2);
    ctx.fillStyle = '#ffc107';
    ctx.fill();

    requestAnimationFrame(drawCanvas);
}

// Start animation loop
drawCanvas();
```

- [ ] **Step 2: Commit**

```bash
git add wetter.Web/Views/Home/v2.cshtml
git commit -m "feat: implement timeline canvas rendering"
```
