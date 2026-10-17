using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Labka.Tests;

public class WeatherForecastTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] AllowedSummaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly HttpClient _client;

    public WeatherForecastTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReturnsFiveForecasts()
    {
        var forecast = await _client.GetFromJsonAsync<List<Forecast>>("/weatherforecast");

        Assert.NotNull(forecast);
        Assert.Equal(5, forecast!.Count);
    }

    [Fact]
    public async Task TemperatureAndSummaryMatchTheContract()
    {
        var forecast = await _client.GetFromJsonAsync<List<Forecast>>("/weatherforecast");

        Assert.NotNull(forecast);
        foreach (var item in forecast!)
        {
            Assert.InRange(item.TemperatureC, -20, 54);
            Assert.Contains(item.Summary, AllowedSummaries);
            Assert.Equal(32 + (int)(item.TemperatureC / 0.5556), item.TemperatureF);
        }
    }

    [Fact]
    public async Task DatesAreTheNextFiveDays()
    {
        var forecast = await _client.GetFromJsonAsync<List<Forecast>>("/weatherforecast");
        var today = DateOnly.FromDateTime(DateTime.Now);

        Assert.NotNull(forecast);
        for (var i = 0; i < forecast!.Count; i++)
        {
            Assert.Equal(today.AddDays(i + 1), forecast[i].Date);
        }
    }

    [Fact]
    public async Task UnknownRouteReturnsNotFound()
    {
        var response = await _client.GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record Forecast(DateOnly Date, int TemperatureC, string? Summary, int TemperatureF);
}
