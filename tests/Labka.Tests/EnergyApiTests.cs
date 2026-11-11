using System.Net;
using System.Net.Http.Json;
using Labka.Contracts;
using Labka.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Labka.Tests;

public class EnergyApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public EnergyApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void ResetStore()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEnergyStore>();
        store.Reset();
    }

    [Fact]
    public async Task GetAllMeters_ReturnsSeededMeters()
    {
        ResetStore();

        var meters = await _client.GetFromJsonAsync<List<MeterDto>>("/api/meters");

        Assert.NotNull(meters);
        Assert.NotEmpty(meters!);
        Assert.Contains(meters!, m => m.SerialNumber == "EL-2026-001");
    }

    [Fact]
    public async Task RegisterMeter_ValidData_Returns201AndAvailableInList()
    {
        ResetStore();

        var request = new RegisterMeterRequest(
            SerialNumber: "GAS-TEST-01",
            Type: "Газ",
            Location: "кв. 77",
            InstalledAt: DateTimeOffset.UtcNow,
            Coefficient: 1.25m
        );

        var response = await _client.PostAsJsonAsync("/api/meters", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MeterDto>();
        Assert.NotNull(created);
        Assert.Equal("GAS-TEST-01", created!.SerialNumber);
        Assert.Equal("Active", created.Status);

        var all = await _client.GetFromJsonAsync<List<MeterDto>>("/api/meters");
        Assert.Contains(all!, m => m.Id == created.Id);
    }

    [Fact]
    public async Task RegisterMeter_InvalidData_Returns400()
    {
        ResetStore();

        var emptySerial = new RegisterMeterRequest("", "Газ", "кв. 1");
        var res1 = await _client.PostAsJsonAsync("/api/meters", emptySerial);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);

        var invalidCoef = new RegisterMeterRequest("SN-1", "Газ", "кв. 1", null, -0.5m);
        var res2 = await _client.PostAsJsonAsync("/api/meters", invalidCoef);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task SubmitReading_UC01_AcceptsValidReadingsAndStoresInOrder()
    {
        ResetStore();
        var meterId = InMemoryEnergyStore.DefaultMeterId;

        // First reading: 1200 on 2026-06-01
        var r1 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m);
        var res1 = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Second reading: 1260 on 2026-07-01
        var r2 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), 1260m);
        var res2 = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r2);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

        // Verify history contains exactly 2 readings in ascending order
        var history = await _client.GetFromJsonAsync<List<MeterReadingDto>>($"/api/meters/{meterId}/readings");
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.Equal(1200m, history[0].Value);
        Assert.Equal(1260m, history[1].Value);
        Assert.True(history[1].RecordedAt > history[0].RecordedAt);
    }

    [Fact]
    public async Task SubmitReading_EarlierOrSameDate_Returns409InvalidDate_DataUnchanged()
    {
        ResetStore();
        var meterId = InMemoryEnergyStore.DefaultMeterId;

        // Base reading: 1200 on 2026-06-01
        var r1 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m);
        await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r1);

        // Attempt same date
        var sameTime = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1250m);
        var resSame = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", sameTime);
        Assert.Equal(HttpStatusCode.Conflict, resSame.StatusCode);
        var errSame = await resSame.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("invalid_date", errSame!.Code);

        // Attempt earlier date
        var earlier = new SubmitReadingRequest(DateTimeOffset.Parse("2026-05-31T00:00:00Z"), 1250m);
        var resEarlier = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", earlier);
        Assert.Equal(HttpStatusCode.Conflict, resEarlier.StatusCode);
        var errEarlier = await resEarlier.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("invalid_date", errEarlier!.Code);

        // Verify data was NOT changed (remains 1 reading)
        var history = await _client.GetFromJsonAsync<List<MeterReadingDto>>($"/api/meters/{meterId}/readings");
        Assert.Single(history!);
        Assert.Equal(1200m, history![0].Value);
    }

    [Fact]
    public async Task SubmitReading_LowerValueLaterDate_Returns409LowerValue_DataUnchanged()
    {
        ResetStore();
        var meterId = InMemoryEnergyStore.DefaultMeterId;

        // Base reading: 1200 on 2026-06-01
        var r1 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m);
        await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r1);

        // Lower value 1199 on 2026-07-01
        var lower = new SubmitReadingRequest(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), 1199m);
        var resLower = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", lower);
        Assert.Equal(HttpStatusCode.Conflict, resLower.StatusCode);
        var err = await resLower.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("lower_value", err!.Code);

        // Verify data was NOT changed (remains 1 reading)
        var history = await _client.GetFromJsonAsync<List<MeterReadingDto>>($"/api/meters/{meterId}/readings");
        Assert.Single(history!);
        Assert.Equal(1200m, history![0].Value);
    }

    [Fact]
    public async Task SubmitReading_EqualValueLaterDate_Returns201()
    {
        ResetStore();
        var meterId = InMemoryEnergyStore.DefaultMeterId;

        var r1 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m);
        await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r1);

        var r2 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), 1200m);
        var res2 = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r2);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

        var history = await _client.GetFromJsonAsync<List<MeterReadingDto>>($"/api/meters/{meterId}/readings");
        Assert.Equal(2, history!.Count);
    }

    [Fact]
    public async Task DecommissionMeter_DecommissionsAndBlocksReadings()
    {
        ResetStore();
        var meterId = InMemoryEnergyStore.DefaultMeterId;

        // Initial reading while active
        var r1 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m);
        var res1 = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Decommission meter
        var decRes = await _client.PostAsJsonAsync($"/api/meters/{meterId}/decommission", new { });
        Assert.Equal(HttpStatusCode.OK, decRes.StatusCode);

        // Verify meter status is Decommissioned
        var meter = await _client.GetFromJsonAsync<MeterDto>($"/api/meters/{meterId}");
        Assert.Equal("Decommissioned", meter!.Status);

        // Attempt reading on decommissioned meter
        var r2 = new SubmitReadingRequest(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), 1300m);
        var res2 = await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", r2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
        var err = await res2.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("meter_decommissioned", err!.Code);

        // Verify history unchanged (still 1 reading)
        var history = await _client.GetFromJsonAsync<List<MeterReadingDto>>($"/api/meters/{meterId}/readings");
        Assert.Single(history!);
    }

    [Fact]
    public async Task CalculateConsumption_ComputesAccurateValueWithCoefficient()
    {
        ResetStore();

        // Create meter with coefficient 1.5
        var regReq = new RegisterMeterRequest("TEST-CALC-01", "Електроенергія", "кв. 10", DateTimeOffset.UtcNow, 1.5m);
        var regRes = await _client.PostAsJsonAsync("/api/meters", regReq);
        var meter = await regRes.Content.ReadFromJsonAsync<MeterDto>();
        var meterId = meter!.Id;

        // Add 1200 on 2026-06-01 and 1260 on 2026-07-01
        await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", new SubmitReadingRequest(DateTimeOffset.Parse("2026-06-01T00:00:00Z"), 1200m));
        await _client.PostAsJsonAsync($"/api/meters/{meterId}/readings", new SubmitReadingRequest(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), 1260m));

        // Calculate: (1260 - 1200) * 1.5 = 90.0
        var url = $"/api/meters/{meterId}/consumption?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z";
        var res = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var calc = await res.Content.ReadFromJsonAsync<ConsumptionDto>();
        Assert.NotNull(calc);
        Assert.Equal(1200m, calc!.StartValue);
        Assert.Equal(1260m, calc.EndValue);
        Assert.Equal(90.0m, calc.Consumption);
        Assert.Equal(1.5m, calc.Coefficient);
    }
}
