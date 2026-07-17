# V2 Weather Dashboard Design

## Overview
A new route (`Home/V2`) will be added to the weather application providing an alternative high-contrast, black-background dashboard optimized for distant readability. The existing `Index` view will remain unchanged.

## Backend Changes
- **Data Model (`WeatherData.cs`)**:
  - Add `PrecipitationHistory` (`List<HistoryPoint>`).
  - Add `Precipitation` (`double`) and `WindSpeed` (`double`) to `HourlyForecast`.
- **Weather Services**:
  - `WeatherService.cs`:
    - Extend OpenMeteo request to include `precipitation` and `wind_speed_10m` in the `hourly` parameters.
    - Extend HomeAssistant data fetching to grab history for `_settings.HomeAssistantRainEntityId`.
    - Extract precipitation and wind speed from HA's hourly forecast payload.
- **Routing**:
  - Add a `V2()` action to `HomeController.cs` that mirrors `Index()` but returns the `v2.cshtml` view.

## Frontend UI (`v2.cshtml`)
- **Layout**: 50/50 split (Left/Right) on landscape displays, stacking vertically on portrait/narrow screens.
- **Styling**: `background-color: black; color: white;`.
- **Left Panel (Current Temperatures)**:
  - Flex column with 4 evenly spaced blocks.
  - Huge typography for distant readability.
  - Labels: Outdoors, Upstairs, Ground Level, Basement.
- **Right Panel (Forecast & Timeline)**:
  - **Top Row (Hourly)**: 6 cards showing the next 6 hours (Time, Icon, Temp).
  - **Middle Row (Daily)**: 6 cards showing the next 6 days (Day, Icon, Temp).
  - **Bottom Row (Timeline Canvas)**:
    - Central Sun icon denoting "Now".
    - X-Axis ranges from -24h (left edge) to +24h (right edge).
    - Left of Center (-24h history): 
      - Temperature (Yellow line)
      - Precipitation (Blue line)
      - Wind Speed (Grey line)
    - Right of Center (+24h forecast): 
      - Temperature (Light Yellow line)
      - Precipitation (Light Blue line)
      - Wind Speed (Light Grey line)

## Dependencies & Considerations
- Reuses existing polling logic and auto-refresh mechanisms from the original view.
- Requires updating the JSON endpoint `/Home/Weather` or ensuring `v2.cshtml` script fetches data and parses the new properties correctly.
