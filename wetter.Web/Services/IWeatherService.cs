using wetter.Web.Models;

namespace wetter.Web.Services;

public interface IWeatherService
{
    Task<WeatherData> GetWeatherAsync();
}
