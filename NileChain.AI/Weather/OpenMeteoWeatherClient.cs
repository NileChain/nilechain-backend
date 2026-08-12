using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NileChain.Domain.Common;

namespace NileChain.AI.Weather;

public sealed class OpenMeteoWeatherClient : IWeatherRiskClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenMeteoWeatherClient> _logger;

    public OpenMeteoWeatherClient(HttpClient http, ILogger<OpenMeteoWeatherClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<WeatherRiskResult> AssessAsync(
        string governorate,
        DateTime deliveryDate,
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken = default)
    {
        var lat = (double?)latitude;
        var lon = (double?)longitude;
        if (lat is null || lon is null)
        {
            if (EgyptGovernorateCentroids.TryResolve(governorate, out var cLat, out var cLon))
            {
                lat = cLat;
                lon = cLon;
            }
        }

        if (lat is null || lon is null)
        {
            return new WeatherRiskResult(
                "Medium",
                "No coordinates for this governorate; weather feed skipped.",
                FromLiveFeed: false,
                null,
                null);
        }

        var day = deliveryDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url =
            $"v1/forecast?latitude={lat.Value.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={lon.Value.ToString(CultureInfo.InvariantCulture)}" +
            "&daily=precipitation_sum,precipitation_probability_max,temperature_2m_max" +
            $"&start_date={day}&end_date={day}&timezone=Africa%2FCairo";

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open-Meteo HTTP {Status} for {Governorate}", response.StatusCode, governorate);
                return TimeoutFallback(lat, lon);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("daily", out var daily))
                return TimeoutFallback(lat, lon);

            var precip = FirstNumber(daily, "precipitation_sum");
            var precipProb = FirstNumber(daily, "precipitation_probability_max");
            var tempMax = FirstNumber(daily, "temperature_2m_max");

            var (level, reason) = Classify(precip, precipProb, tempMax, governorate, day);
            return new WeatherRiskResult(level, reason, true, lat, lon);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Open-Meteo unavailable for {Governorate}", governorate);
            return TimeoutFallback(lat, lon);
        }
    }

    public static (string Level, string Reason) Classify(
        double? precipMm,
        double? precipProb,
        double? tempMaxC,
        string governorate,
        string day)
    {
        if (precipMm is >= 15 || precipProb is >= 70 || tempMaxC is >= 40)
        {
            return ("High",
                $"Open-Meteo: elevated delivery risk in {governorate} on {day} " +
                $"(precip {precipMm:0.#}mm, storm chance {precipProb:0}%, max {tempMaxC:0.#}°C).");
        }

        if (precipMm is >= 5 || precipProb is >= 40 || tempMaxC is >= 35)
        {
            return ("Medium",
                $"Open-Meteo: moderate weather variability in {governorate} on {day}.");
        }

        return ("Low",
            $"Open-Meteo: stable conditions expected in {governorate} on {day}.");
    }

    private static WeatherRiskResult TimeoutFallback(double? lat, double? lon) =>
        new("Medium",
            "Weather feed timed out or unavailable; using a conservative Medium estimate.",
            FromLiveFeed: false,
            lat,
            lon);

    private static double? FirstNumber(JsonElement daily, string name)
    {
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        if (arr.GetArrayLength() == 0)
            return null;
        var first = arr[0];
        return first.ValueKind == JsonValueKind.Number ? first.GetDouble() : null;
    }
}
