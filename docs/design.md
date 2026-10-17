# Проєктна модель v1.0

## 1. Use Case Diagram

```mermaid
flowchart LR
    Operator["Оператор обліку"]
    Manager["Енергоменеджер ОСББ"]

    subgraph System["Система обліку енергоресурсів ОСББ"]
        UC01["UC-01 Внести показник"]
        UC02["UC-02 Зареєструвати лічильник"]
        UC03["UC-03 Вивести лічильник з експлуатації"]
        UC04["UC-04 Переглянути історію показників"]
        UC05["UC-05 Обчислити споживання"]
    end

    Operator --> UC01
    Operator --> UC02
    Operator --> UC03
    Operator --> UC04
    Manager --> UC04
    Manager --> UC05
```

## 2. Class Diagram

```mermaid
classDiagram
    class Operator {
        +id: string
        +fullName: string
        +enterReading(meter: Meter, dateTime: DateTime, value: decimal): void
        +registerMeter(serialNumber: string, type: string, location: string): Meter
        +decommissionMeter(meter: Meter): void
    }

    class Manager {
        +id: string
        +fullName: string
        +viewHistory(meter: Meter): MeterReading[]
        +calculateConsumption(meter: Meter, start: DateTime, end: DateTime): decimal
    }

    class Meter {
        +id: string
        +serialNumber: string
        +type: string
        +location: string
        +status: MeterStatus
        +installedAt: DateTime
        +addReading(reading: MeterReading): void
        +decommission(): void
    }

    class MeterReading {
        +id: string
        +meterId: string
        +readingDateTime: DateTime
        +value: decimal
        +validate(): bool
        +save(): void
    }

    class ConsumptionReport {
        +periodStart: DateTime
        +periodEnd: DateTime
        +totalConsumption: decimal
        +calculate(meter: Meter): decimal
    }

    class MeterStatus {
        <<enumeration>>
        Active
        Suspended
        Decommissioned
    }

    Operator --> Meter
    Manager --> Meter
    Meter "1" --> "0..*" MeterReading
    Meter --> MeterStatus
    Meter --> ConsumptionReport
    MeterReading --> ConsumptionReport
```

## 3. State Machine Diagram

```mermaid
stateDiagram-v2
    [*] --> Registered
    Registered --> Active : activateMeter()
    Active --> Active : submitReading(value)
    Active --> Suspended : maintenance()
    Suspended --> Active : restore()
    Active --> Decommissioned : decommission()
    Decommissioned --> [*]
```

У цьому варіанті станів лічильник має життєвий цикл: зареєстрований → активний → призупинений/виведений з експлуатації. Така діаграма необхідна, бо наявна логіка зміни стану лічильника впливає на правила внесення показників.
