namespace NileChain.AI.Weather;

public sealed record WeatherRiskResult(
    string RiskLevel,
    string Reason,
    bool FromLiveFeed,
    double? Latitude,
    double? Longitude);
