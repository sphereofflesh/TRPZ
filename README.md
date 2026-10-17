# TRPZ

Навчальний репозиторій команди. Документація — у теці `docs`.

## Структура

- `Labka` — Web API на ASP.NET Core (net8.0), ендпоінт `GET /weatherforecast`;
- `tests/Labka.Tests` — автоматичні тести цього API;
- `docs` — розподіл ролей і тест-план;
- `.github/workflows/ci.yml` — CI: restore, build, test.

## Вимоги

- .NET SDK 8.0
- Git

## Запуск

```
dotnet restore
dotnet run --project Labka
```

Застосунок відкривається на http://localhost:5052, Swagger — на `/swagger`.

## Тести

```
dotnet test
```

## Перевірка змін

Кожна зміна вноситься в окремій гілці через Pull Request. CI виконує `dotnet restore`, `dotnet build` і `dotnet test`.
