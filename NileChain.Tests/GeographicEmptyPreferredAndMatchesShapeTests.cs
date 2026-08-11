using NileChain.AI.Matching;
using NileChain.Application.Dtos.Farm;
using System.Text.Json;

namespace NileChain.Tests;

public class GeographicEmptyPreferredTests
{
    [Fact]
    public void Exact_WithEmptyPreferred_FailsClosed()
    {
        Assert.False(
            GeographicMatching.IsEligible(
                "Giza",
                Array.Empty<string>(),
                GeographicMatching.Scope.Exact));
    }

    [Fact]
    public void Nearby_WithEmptyPreferred_FailsClosed()
    {
        Assert.False(
            GeographicMatching.IsEligible(
                "Cairo",
                Array.Empty<string>(),
                GeographicMatching.Scope.Nearby));
    }

    [Fact]
    public void Nationwide_WithEmptyPreferred_StillEligible()
    {
        Assert.True(
            GeographicMatching.IsEligible(
                "Luxor",
                Array.Empty<string>(),
                GeographicMatching.Scope.Nationwide));
    }
}

/// <summary>
/// Contract test: farm matches list response must expose the paged shape
/// (items/summary/totalCount), not a bare array.
/// </summary>
public class FarmMatchesListResponseShapeTests
{
    [Fact]
    public void SerializedResponse_HasPagedProperties()
    {
        var response = new FarmMatchesListResponse
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            Summary = new FarmMatchSummaryDto { Total = 0 },
            NewMatches = []
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("items", out _));
        Assert.True(root.TryGetProperty("summary", out _));
        Assert.True(root.TryGetProperty("totalCount", out _));
        Assert.True(root.TryGetProperty("page", out _));
        Assert.True(root.TryGetProperty("pageSize", out _));
        Assert.True(root.TryGetProperty("newMatches", out _));
    }
}
