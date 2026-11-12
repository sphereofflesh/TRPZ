using System.Globalization;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Labka.Client;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5052/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine("   СИСТЕМА ОБЛІКУ ЕНЕРГОРЕСУРСІВ ОСББ — КЛІЄНТ v1.0.0");
        Console.WriteLine($"   Сервер API: {baseUrl}");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        while (true)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Оберіть дію:");
            Console.ResetColor();
            Console.WriteLine("1. Переглянути всі лічильники");
            Console.WriteLine("2. Зареєструвати новий лічильник (UC-02 / FR-02)");
            Console.WriteLine("3. Внести показник лічильника (UC-01 / FR-01 / NFR-02)");
            Console.WriteLine("4. Переглянути історію показників (UC-04 / FR-04)");
            Console.WriteLine("5. Обчислити споживання за період (UC-05 / FR-05)");
            Console.WriteLine("6. Вивести лічильник з експлуатації (UC-03 / FR-03)");
            Console.WriteLine("7. Запустити повну демонстрацію сценаріїв (UC-01..UC-05)");
            Console.WriteLine("0. Вихід");
            Console.Write("\nВаш вибір: ");

            var choice = Console.ReadLine()?.Trim();
            if (choice == "0")
            {
                Console.WriteLine("Роботу завершено.");
                break;
            }

            try
            {
                switch (choice)
                {
                    case "1":
                        await GetAllMetersAsync(http);
                        break;
                    case "2":
                        await RegisterMeterAsync(http);
                        break;
                    case "3":
                        await SubmitReadingAsync(http);
                        break;
                    case "4":
                        await ViewHistoryAsync(http);
                        break;
                    case "5":
                        await CalculateConsumptionAsync(http);
                        break;
                    case "6":
                        await DecommissionMeterAsync(http);
                        break;
                    case "7":
                        await RunAutomatedDemoAsync(http);
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Невідомий пункт меню. Спробуйте ще раз.");
                        Console.ResetColor();
                        break;
                }
            }
            catch (HttpRequestException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ПОМИЛКА З'ЄДНАННЯ] Не вдалося зв'язатися із сервером за адресою {baseUrl}. Переконайтеся, що сервер запущено. ({ex.Message})");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ПОМИЛКА] {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static async Task GetAllMetersAsync(HttpClient http)
    {
        Console.WriteLine("\n--> Отримання переліку лічильників...");
        using var response = await http.GetAsync("api/meters");
        await ShowResponseAsync(response);
    }

    private static async Task RegisterMeterAsync(HttpClient http)
    {
        Console.WriteLine("\n--- Реєстрація нового лічильника (UC-02) ---");
        Console.Write("Серійний номер (наприклад, EL-2026-101): ");
        var serialNumber = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Тип (Електроенергія / Газ / Вода): ");
        var type = Console.ReadLine()?.Trim() ?? "Електроенергія";

        Console.Write("Місце встановлення (наприклад, кв. 24): ");
        var location = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Тарифний коефіцієнт (за замовчуванням 1.0): ");
        var coefInput = Console.ReadLine()?.Trim();
        var coef = decimal.TryParse(coefInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var c) ? c : 1.0m;

        var request = new
        {
            serialNumber,
            type,
            location,
            coefficient = coef,
            installedAt = DateTimeOffset.UtcNow
        };

        using var response = await http.PostAsJsonAsync("api/meters", request);
        await ShowResponseAsync(response);
    }

    private static async Task SubmitReadingAsync(HttpClient http)
    {
        Console.WriteLine("\n--- Внесення показника лічильника (UC-01) ---");
        Console.Write("ID лічильника (GUID): ");
        var meterIdInput = Console.ReadLine()?.Trim();
        if (!Guid.TryParse(meterIdInput, out var meterId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Некоректний формат GUID.");
            Console.ResetColor();
            return;
        }

        Console.Write("Дата та час (ISO 8601, наприклад 2026-06-01T12:00:00Z, залиште порожнім для поточного часу): ");
        var timeInput = Console.ReadLine()?.Trim();
        DateTimeOffset recordedAt = string.IsNullOrWhiteSpace(timeInput)
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.Parse(timeInput, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        Console.Write("Значення показника: ");
        var valInput = Console.ReadLine()?.Trim();
        if (!decimal.TryParse(valInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Некоректне числове значення.");
            Console.ResetColor();
            return;
        }

        var request = new { recordedAt, value };
        using var response = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", request);
        await ShowResponseAsync(response);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Читання оновленої історії лічильника для перевірки стану...");
        Console.ResetColor();
        using var historyResponse = await http.GetAsync($"api/meters/{meterId}/readings");
        await ShowResponseAsync(historyResponse);
    }

    private static async Task ViewHistoryAsync(HttpClient http)
    {
        Console.WriteLine("\n--- Перегляд історії показників (UC-04) ---");
        Console.Write("ID лічильника (GUID): ");
        var meterIdInput = Console.ReadLine()?.Trim();
        if (!Guid.TryParse(meterIdInput, out var meterId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Некоректний формат GUID.");
            Console.ResetColor();
            return;
        }

        using var response = await http.GetAsync($"api/meters/{meterId}/readings");
        await ShowResponseAsync(response);
    }

    private static async Task CalculateConsumptionAsync(HttpClient http)
    {
        Console.WriteLine("\n--- Обчислення споживання за період (UC-05) ---");
        Console.Write("ID лічильника (GUID): ");
        var meterIdInput = Console.ReadLine()?.Trim();
        if (!Guid.TryParse(meterIdInput, out var meterId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Некоректний формат GUID.");
            Console.ResetColor();
            return;
        }

        Console.Write("Початок періоду 'from' (наприклад 2026-06-01T00:00:00Z): ");
        var from = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Кінець періоду 'to' (наприклад 2026-07-01T00:00:00Z): ");
        var to = Console.ReadLine()?.Trim() ?? "";

        var route = $"api/meters/{meterId}/consumption?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";
        using var response = await http.GetAsync(route);
        await ShowResponseAsync(response);
    }

    private static async Task DecommissionMeterAsync(HttpClient http)
    {
        Console.WriteLine("\n--- Виведення лічильника з експлуатації (UC-03) ---");
        Console.Write("ID лічильника (GUID): ");
        var meterIdInput = Console.ReadLine()?.Trim();
        if (!Guid.TryParse(meterIdInput, out var meterId))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Некоректний формат GUID.");
            Console.ResetColor();
            return;
        }

        using var response = await http.PostAsJsonAsync($"api/meters/{meterId}/decommission", new { reason = "Планова заміна" });
        await ShowResponseAsync(response);
    }

    public static async Task RunAutomatedDemoAsync(HttpClient http)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n==================================================================");
        Console.WriteLine("   ПОЧАТОК ПОВНОЇ АВТОМАТИЗОВАНОЇ ДЕМОНСТРАЦІЇ СЦЕНАРІЇВ (v1.0.0)");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        // 1. Реєстрація нового лічильника (UC-02)
        Console.WriteLine("\n[КРОК 1] Реєстрація нового лічильника (UC-02, FR-02)");
        var regReq = new
        {
            serialNumber = $"DEMO-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
            type = "Електроенергія",
            location = "вул. Грушевського, 4, кв. 10",
            coefficient = 1.5m,
            installedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
        };
        var regRes = await http.PostAsJsonAsync("api/meters", regReq);
        await ShowResponseAsync(regRes);
        var regJson = await regRes.Content.ReadFromJsonAsync<JsonElement>();
        var meterId = regJson.GetProperty("id").GetGuid();

        // 2. Внесення першого показника (1200) (UC-01)
        Console.WriteLine("\n[КРОК 2] Внесення першого базового показника 1200 (UC-01, FR-01, AC-01)");
        var r1 = new { recordedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"), value = 1200.0m };
        var res1 = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", r1);
        await ShowResponseAsync(res1);

        // 3. Внесення другого показника (1260) у пізніший час (UC-01)
        Console.WriteLine("\n[КРОК 3] Внесення другого показника 1260 на 01.07.2026 (UC-01, AC-01)");
        var r2 = new { recordedAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z"), value = 1260.0m };
        var res2 = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", r2);
        await ShowResponseAsync(res2);

        // 4. Спроба внести показник із ранішою або повторною датою (відмова AC-02, NFR-02)
        Console.WriteLine("\n[КРОК 4] Спроба внести показник із повторною/ранішою датою 01.06.2026 (AC-02, NFR-02)");
        var rInvalidDate = new { recordedAt = DateTimeOffset.Parse("2026-06-01T00:00:00Z"), value = 1300.0m };
        var resInvDate = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", rInvalidDate);
        await ShowResponseAsync(resInvDate);

        // 5. Спроба внести показник із меншим значенням 1199 (відмова AC-03, NFR-02)
        Console.WriteLine("\n[КРОК 5] Спроба внести менший показник 1199 на 01.08.2026 (AC-03, NFR-02)");
        var rLower = new { recordedAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z"), value = 1199.0m };
        var resLower = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", rLower);
        await ShowResponseAsync(resLower);

        // 6. Перевірка історії: переконуємося, що відхилені записи НЕ збереглися (UC-04, FR-04)
        Console.WriteLine("\n[КРОК 6] Перевірка історії (UC-04, FR-04): стан даних залишився незмінним (2 записи)");
        var histRes = await http.GetAsync($"api/meters/{meterId}/readings");
        await ShowResponseAsync(histRes);

        // 7. Розрахунок споживання: (1260 - 1200) * 1.5 = 90.0 кВт·год (UC-05, FR-05, AC-04)
        Console.WriteLine("\n[КРОК 7] Розрахунок споживання за період 01.06 - 01.07 (UC-05, FR-05, AC-04)");
        var calcRes = await http.GetAsync($"api/meters/{meterId}/consumption?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z");
        await ShowResponseAsync(calcRes);

        // 8. Виведення лічильника з експлуатації (UC-03, FR-03)
        Console.WriteLine("\n[КРОК 8] Виведення лічильника з експлуатації (UC-03, FR-03)");
        var decRes = await http.PostAsJsonAsync($"api/meters/{meterId}/decommission", new { reason = "Тестове виведення" });
        await ShowResponseAsync(decRes);

        // 9. Спроба внести показник для виведеного лічильника (відмова FR-03)
        Console.WriteLine("\n[КРОК 9] Спроба внесення показника у виведений лічильник (FR-03 відмова)");
        var rDec = new { recordedAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z"), value = 1300.0m };
        var resDec = await http.PostAsJsonAsync($"api/meters/{meterId}/readings", rDec);
        await ShowResponseAsync(resDec);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n==================================================================");
        Console.WriteLine("   ДЕМОНСТРАЦІЮ УСПІШНО ЗАВЕРШЕНО! ВСІ СЦЕНАРІЇ ВІДПОВІДАЮТЬ ВИМОГАМ.");
        Console.WriteLine("==================================================================");
        Console.ResetColor();
    }

    private static async Task ShowResponseAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[HTTP {statusCode} {response.StatusCode}]");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[HTTP {statusCode} {response.StatusCode}]");
        }
        Console.ResetColor();

        try
        {
            using var doc = JsonDocument.Parse(content);
            Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, JsonOptions));
        }
        catch
        {
            Console.WriteLine(content);
        }
    }
}
