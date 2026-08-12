namespace NileChain.AI.Weather;

public interface IWeatherRiskClient
{
    Task<WeatherRiskResult> AssessAsync(
        string governorate,
        DateTime deliveryDate,
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken = default);
}
