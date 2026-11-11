using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Labka.Contracts;
using Labka.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-Memory storage singleton (thread-safe with lock)
builder.Services.AddSingleton<IEnergyStore, InMemoryEnergyStore>();
builder.Services.AddScoped<EnergyService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// Global Exception Handler to avoid leaking internal stack traces (NFR-04)
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiError("invalid_request", $"Некоректне тіло запиту: {ex.Message}"));
    }
    catch (JsonException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiError("invalid_json", $"Помилка парсингу JSON: {ex.Message}"));
    }
    catch (Exception)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiError("internal_error", "Виникла непередбачена внутрішня помилка сервера."));
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow }))
   .WithName("HealthCheck")
   .WithOpenApi();

// Retain weatherforecast for backward compatibility with initial tests
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// --- Energy Accounting API Endpoints (v1.0.0) ---

// UC-02: Get all meters
app.MapGet("/api/meters", (EnergyService service) =>
{
    var meters = service.GetAllMeters();
    return Results.Ok(meters);
})
.WithName("GetAllMeters")
.WithOpenApi();

// UC-02: Get meter by ID
app.MapGet("/api/meters/{id:guid}", (Guid id, EnergyService service) =>
{
    var meter = service.GetMeter(id);
    return meter is not null
        ? Results.Ok(meter)
        : Results.NotFound(new ApiError("meter_not_found", $"Лічильник із ID {id} не знайдено."));
})
.WithName("GetMeterById")
.WithOpenApi();

// UC-02: Register new meter (FR-02)
app.MapPost("/api/meters", (RegisterMeterRequest request, EnergyService service) =>
{
    var (success, errorCode, message, meter) = service.RegisterMeter(request);
    if (!success)
    {
        return Results.BadRequest(new ApiError(errorCode ?? "invalid_meter", message ?? "Помилка валідації лічильника."));
    }

    return Results.Created($"/api/meters/{meter!.Id}", meter);
})
.WithName("RegisterMeter")
.WithOpenApi();

// UC-03: Decommission meter (FR-03)
app.MapPost("/api/meters/{id:guid}/decommission", (Guid id, EnergyService service) =>
{
    var (success, errorCode, message) = service.DecommissionMeter(id);
    if (!success)
    {
        return errorCode == "meter_not_found"
            ? Results.NotFound(new ApiError(errorCode, message!))
            : Results.Conflict(new ApiError(errorCode ?? "decommission_failed", message!));
    }

    return Results.Ok(new { message = "Лічильник успішно виведено з експлуатації." });
})
.WithName("DecommissionMeter")
.WithOpenApi();

// UC-01: Enter meter reading (FR-01, NFR-02)
app.MapPost("/api/meters/{id:guid}/readings", (Guid id, SubmitReadingRequest request, EnergyService service) =>
{
    var result = service.SubmitReading(id, request);
    if (result.Accepted)
    {
        return Results.Created($"/api/meters/{id}/readings", result.Reading);
    }

    return result.Code switch
    {
        "meter_not_found" => Results.NotFound(new ApiError(result.Code, result.Message!)),
        "invalid_value" => Results.BadRequest(new ApiError(result.Code, result.Message!)),
        "meter_decommissioned" => Results.Conflict(new ApiError(result.Code, result.Message!)),
        "invalid_date" => Results.Conflict(new ApiError(result.Code, result.Message!)),
        "lower_value" => Results.Conflict(new ApiError(result.Code, result.Message!)),
        _ => Results.Json(new ApiError(result.Code ?? "internal_error", result.Message ?? "Неочікувана помилка."), statusCode: 500)
    };
})
.WithName("SubmitReading")
.WithOpenApi();

// UC-04: View meter reading history (FR-04)
app.MapGet("/api/meters/{id:guid}/readings", (Guid id, DateTimeOffset? from, DateTimeOffset? to, EnergyService service) =>
{
    var meter = service.GetMeter(id);
    if (meter is null)
    {
        return Results.NotFound(new ApiError("meter_not_found", $"Лічильник із ID {id} не знайдено."));
    }

    var readings = service.GetReadings(id, from, to);
    return Results.Ok(readings);
})
.WithName("GetMeterReadings")
.WithOpenApi();

// UC-05: Calculate consumption (FR-05)
app.MapGet("/api/meters/{id:guid}/consumption", (Guid id, string? from, string? to, EnergyService service) =>
{
    if (string.IsNullOrWhiteSpace(from) || !DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fromDate))
    {
        return Results.BadRequest(new ApiError("invalid_from_date", "Потрібно вказати коректну початкову дату 'from' у форматі ISO 8601."));
    }

    if (string.IsNullOrWhiteSpace(to) || !DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var toDate))
    {
        return Results.BadRequest(new ApiError("invalid_to_date", "Потрібно вказати коректну кінцеву дату 'to' у форматі ISO 8601."));
    }

    var (success, errorCode, message, result) = service.CalculateConsumption(id, fromDate, toDate);
    if (!success)
    {
        return errorCode switch
        {
            "meter_not_found" => Results.NotFound(new ApiError(errorCode, message!)),
            "invalid_period" => Results.BadRequest(new ApiError(errorCode, message!)),
            "boundary_reading_not_found" => Results.NotFound(new ApiError(errorCode, message!)),
            _ => Results.BadRequest(new ApiError(errorCode ?? "calculation_error", message!))
        };
    }

    return Results.Ok(result);
})
.WithName("CalculateConsumption")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Потрібно, щоб тестовий проєкт міг підняти застосунок через WebApplicationFactory.
public partial class Program { }
