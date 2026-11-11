# Діаграма послідовності: UC-02 Реєстрація нового лічильника

**Автор реалізації:** Трофименко Арсеній  
**Рецензент:** Ткаченко Богдан  
**Пов'язані вимоги:** FR-02, UC-02, AC-02  

## Опис сценарію

Клієнт надсилає HTTP POST запит на адресу `/api/meters` із серійним номером, типом ресурсу, локацією встановлення та коефіцієнтом.
Сервер валідує параметри:
1. Якщо серійний номер, тип або локація порожні — повертається помилка 400 Bad Request.
2. Якщо коефіцієнт не є додатним числом — повертається помилка 400 Bad Request.
3. Якщо дані валідні — створюється новий об'єкт `Meter` у стані `Active`, зберігається в сховищі `InMemoryEnergyStore` (під блокуванням `lock`), і клієнту повертається статус 201 Created із DTO зареєстрованого лічильника.

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

    Operator->>Client: Введення серійного номера, типу, локації, коефіцієнта
    Client->>API: POST /api/meters { serialNumber, type, location, coefficient, installedAt }
    API->>Service: RegisterMeter(request)
    
    Service->>Rules: ValidCoefficient(coef)
    Rules-->>Service: true / false
    
    alt Помилка валідації полів або коефіцієнта
        Service-->>API: (false, "invalid_parameter", message)
        API-->>Client: HTTP 400 Bad Request (ApiError)
        Client-->>Operator: Повідомлення про некоректні дані
    else Дані валідні
        Service->>Store: AddMeter(new Meter)
        activate Store
        Note over Store: lock(_lock) -> _meters.Add(meter)
        Store-->>Service: meter
        deactivate Store
        
        Service-->>API: (true, null, MeterDto)
        API-->>Client: HTTP 201 Created { id, serialNumber, status="Active", ... }
        Client-->>Operator: Підтвердження реєстрації та відображення ID лічильника
    end
```
