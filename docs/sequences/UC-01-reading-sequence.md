# Діаграма послідовності: UC-01 Внесення показника лічильника

**Автор реалізації:** Ткаченко Богдан  
**Рецензент:** Трофименко Арсеній  
**Пов'язані вимоги:** FR-01, NFR-02, UC-01, AC-01, AC-02, AC-03  

## Опис сценарію

Клієнт (`Labka.Client`) надсилає HTTP POST запит із датою/часом (`recordedAt`) та числовим значенням (`value`) на адресу `/api/meters/{id}/readings`.
Сервер перевіряє наявність лічильника, валідує вхідні значення за правилами `ReadingRules`, звіряє попередній показник лічильника у сховищі `InMemoryEnergyStore`.
Залежно від результатів валідації:
1. Якщо час не пізніший за попередній — повертається помилка 409 `invalid_date`, стан даних залишається незмінним.
2. Якщо значення менше за попереднє — повертається помилка 409 `lower_value`, стан даних залишається незмінним.
3. Якщо лічильник виведено з експлуатації — повертається помилка 409 `meter_decommissioned`, стан даних залишається незмінним.
4. Якщо всі перевірки пройдено — новий запис додається до колекції в пам'яті (захищено блокуванням `lock`), і клієнту повертається статус 201 Created із даними збереженого показника.

## Діаграма послідовності (Mermaid)

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Оператор обліку
    participant Client as Labka.Client
    participant API as Web API (Endpoints)
    participant Service as EnergyService
    participant Rules as ReadingRules
    participant Store as InMemoryEnergyStore

    Operator->>Client: Введення ID лічильника, дати/часу та значення
    Client->>API: POST /api/meters/{id}/readings { recordedAt, value }
    API->>Service: SubmitReading(meterId, request)
    Service->>Store: AddReading(meterId, recordedAt, value)
    
    activate Store
    Note over Store: Вхід у блок lock(_lock)
    Store->>Rules: ValidValue(value)
    Rules-->>Store: true / false
    
    alt Значення невалідне (від'ємне або надмірна точність)
        Store-->>Service: AcceptReadingResult(false, "invalid_value")
        Service-->>API: 400 Bad Request (ApiError)
        API-->>Client: HTTP 400 invalid_value
        Client-->>Operator: Повідомлення: "Неприпустиме значення або точність показника"
    else Лічильник не знайдено
        Store-->>Service: AcceptReadingResult(false, "meter_not_found")
        Service-->>API: 404 Not Found (ApiError)
        API-->>Client: HTTP 404 meter_not_found
        Client-->>Operator: Повідомлення: "Лічильник не знайдено"
    else Лічильник Decommissioned
        Store-->>Service: AcceptReadingResult(false, "meter_decommissioned")
        Service-->>API: 409 Conflict (ApiError)
        API-->>Client: HTTP 409 meter_decommissioned
        Client-->>Operator: Повідомлення: "Лічильник виведено з експлуатації. Внесення показників заблоковано"
    else Отримання попереднього показника
        Store->>Store: Останній показник за RecordedAt
        alt Час <= попереднього запису (invalid_date)
            Store-->>Service: AcceptReadingResult(false, "invalid_date")
            Service-->>API: 409 Conflict (ApiError)
            API-->>Client: HTTP 409 invalid_date (дані незмінні)
            Client-->>Operator: Повідомлення: "Час нового показника повинен бути пізнішим за попередній"
        else Значення < попереднього запису (lower_value)
            Store-->>Service: AcceptReadingResult(false, "lower_value")
            Service-->>API: 409 Conflict (ApiError)
            API-->>Client: HTTP 409 lower_value (дані незмінні)
            Client-->>Operator: Повідомлення: "Нове значення показника не може бути меншим за попереднє"
        else Усі перевірки успішні
            Store->>Store: _readings.Add(new MeterReading)
            Note over Store: Запис додано до пам'яті
            Store-->>Service: AcceptReadingResult(true, null, MeterReadingDto)
            Service-->>API: 201 Created (MeterReadingDto)
            API-->>Client: HTTP 201 Created { id, meterId, recordedAt, value }
            Client->>API: GET /api/meters/{id}/readings (перевірка оновленої історії)
            API-->>Client: HTTP 200 OK [readings]
            Client-->>Operator: Успішно збережено та відображено в історії
        end
    end
    deactivate Store
```
