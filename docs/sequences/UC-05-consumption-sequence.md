# Діаграма послідовності: UC-05 Обчислення споживання енергоресурсу

**Автор реалізації:** Трофименко Арсеній  
**Рецензент:** Ткаченко Богдан  
**Пов'язані вимоги:** FR-05, UC-05, AC-04  

## Опис сценарію

Енергоменеджер або оператор через клієнт запитує обчислення споживання за вказаний проміжок часу (`from`, `to`).
Сервер виконує:
1. Валідацію дат у форматі ISO 8601 та перевірку `from < to`.
2. Пошук лічильника та показників у заданому інтервалі.
3. Якщо показників менше 2 — повертається помилка 404 `boundary_reading_not_found`.
4. Якщо показники присутні — обчислюється різниця між кінцевим і початковим значенням, помножена на коефіцієнт лічильника: `Consumption = (EndValue - StartValue) * Coefficient`.
5. Клієнту повертається статус 200 OK з `ConsumptionDto`.

## Діаграма послідовності (Mermaid)

```mermaid
sequenceDiagram
    autonumber
    actor Manager as Енергоменеджер
    participant Client as Labka.Client
    participant API as Web API (Endpoints)
    participant Service as EnergyService
    participant Store as InMemoryEnergyStore

    Manager->>Client: Введення ID лічильника, дат from та to
    Client->>API: GET /api/meters/{id}/consumption?from={from}&to={to}
    API->>Service: CalculateConsumption(meterId, from, to)
    Service->>Store: CalculateConsumption(meterId, from, to)
    
    activate Store
    Note over Store: lock(_lock)
    Store->>Store: Пошук лічильника та показників у періоді [from, to]
    
    alt Лічильник не знайдено
        Store-->>Service: (false, "meter_not_found")
        Service-->>API: 404 Not Found
        API-->>Client: HTTP 404 meter_not_found
    else from >= to
        Store-->>Service: (false, "invalid_period")
        Service-->>API: 400 Bad Request
        API-->>Client: HTTP 400 invalid_period
    else Показників у періоді < 2
        Store-->>Service: (false, "boundary_reading_not_found")
        Service-->>API: 404 Not Found
        API-->>Client: HTTP 404 boundary_reading_not_found
    else Показники знайдено
        Note over Store: StartValue = period.First().Value<br/>EndValue = period.Last().Value<br/>Consumption = (EndValue - StartValue) * Coefficient
        Store-->>Service: (true, null, ConsumptionDto)
        Service-->>API: 200 OK (ConsumptionDto)
        API-->>Client: HTTP 200 OK { meterId, startValue, endValue, consumption, coefficient }
        Client-->>Manager: Відображення розрахованого споживання
    end
    deactivate Store
```
