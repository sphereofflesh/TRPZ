# Тест-план Labka

## Що перевіряємо

Веб-застосунок `Labka` (ASP.NET Core) та його ендпоінт `GET /weatherforecast`.

## Як запустити

```
dotnet run --project Labka
```

Застосунок працює на http://localhost:5052, Swagger — на /swagger.

## Перевірки

1. `GET /weatherforecast` повертає код 200 і JSON.
2. У відповіді рівно 5 записів.
3. Кожен запис має поля `date`, `temperatureC`, `summary`, `temperatureF`.
4. `date` — це 5 послідовних днів, починаючи із завтрашнього.
5. `temperatureC` у межах від -20 до 54.
6. `temperatureF` відповідає формулі 32 + temperatureC / 0.5556.
7. `summary` — одне зі значень зі списку в коді (Freezing, Bracing, Chilly, Cool, Mild, Warm, Balmy, Hot, Sweltering, Scorching).
8. Swagger відкривається на `/swagger`.
9. Невідома адреса повертає 404, а `POST /weatherforecast` — 405.

## Автоматичні перевірки

Пункти 1–7 і 9 покриті тестами в `tests/Labka.Tests`. Вони виконуються командою `dotnet test` і на кожному Pull Request у CI (workflow `CI`, крок Test).

Пункт 8 (Swagger) і відповідь 405 на `POST /weatherforecast` перевіряються вручну.

## Результат

Усі перевірки пройдено.
