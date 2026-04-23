# 💊 Pharmacy Management System

Система управління аптечним інвентарем та рецептами. Побудована на ASP.NET Core з PostgreSQL.

## 🏗️ Архітектура

```
Pharmacy.Core              → Сутності, інтерфейси (моделі даних)
Pharmacy.Infrastructure    → Реалізація сервісів, база даних (EF Core + PostgreSQL)
Pharmacy.Api               → REST API контролери
Pharmacy.Tests             → Unit + Integration + Database тести
k6/                        → Навантажувальне тестування
.github/workflows/         → CI/CD pipeline
```

## 📦 Сутності

- **Medicine** — ліки (назва, ціна, запас, термін, категорія, рецептурність)
- **Prescription** — рецепт (пацієнт, лікар, ліцензія, статус, дійсність 30 днів)
- **PrescriptionItem** — позиція рецепта (ліко, дозування, кількість)
- **Sale** — продаж (сума, дата, опціональний рецепт)
- **SaleItem** — позиція продажу (ліко, кількість, ціна)

## 🔗 API Ендпоінти

| Метод | Маршрут | Опис |
|-------|---------|------|
| GET | `/api/medicines` | Список ліків (фільтр: category, requiresPrescription, inStock) |
| POST | `/api/medicines` | Додати ліки |
| PUT | `/api/medicines/{id}` | Оновити ліки |
| GET | `/api/medicines/expiring?days=N` | Ліки з терміном до N днів |
| GET | `/api/medicines/low-stock` | Ліки з запасом < 10 |
| POST | `/api/prescriptions` | Створити рецепт |
| GET | `/api/prescriptions/{id}` | Деталі рецепта |
| POST | `/api/sales` | Обробити продаж |
| GET | `/api/sales` | Історія продажів |

## ⚙️ Бізнес-правила

- Рецептурні ліки не продаються без дійсного рецепта
- Рецепт дійсний 30 днів від дати видачі
- Не можна продавати прострочені ліки
- Запас автоматично зменшується при продажу
- Рецепт позначається як Fulfilled після продажу
- Ліцензія лікаря валідується за форматом `LIC-####-####`
- Поріг низького запасу: 10 одиниць

## 🚀 Запуск проекту

### Передумови

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (або Docker)
- [Docker](https://www.docker.com/) (для тестів з Testcontainers)

### 1. Запуск PostgreSQL через Docker

```bash
docker run --name pharmacy-db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=PharmacyDb -p 5432:5432 -d postgres:15-alpine
```

### 2. Налаштувати connection string

В `Pharmacy.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=PharmacyDb;Username=postgres;Password=postgres"
  }
}
```

### 3. Запустити API

```bash
dotnet run --project Pharmacy.Api
```

API буде доступне на `http://localhost:5000`

При першому запуску база автоматично створюється і наповнюється 10 000+ записами.

### 4. Запустити тести

```bash
# Модульні тести (не потребують Docker)
dotnet test --filter "FullyQualifiedName~Unit"

# Всі тести (потребують Docker для Testcontainers)
dotnet test
```

### 5. Навантажувальне тестування (k6)

```bash
# Встановити k6: https://k6.io/docs/get-started/installation/
k6 run k6/medicines-search.js
k6 run k6/sales-stress.js
```

## 🧪 Тестування

| Тип | Кількість | Інструменти |
|-----|-----------|-------------|
| Модульні тести | 20 | xUnit, AutoFixture, FluentAssertions, InMemory DB |
| Інтеграційні тести | 10 | WebApplicationFactory, Testcontainers, PostgreSQL |
| Тести БД | 4 | Testcontainers, PostgreSQL |
| k6 навантаження | 2 сценарії | Load test (пошук), Stress test (продажі) |

## 🛠️ Технології

- ASP.NET Core 10
- Entity Framework Core + PostgreSQL
- xUnit + FluentAssertions + AutoFixture
- Testcontainers.PostgreSql
- Bogus (генерація тестових даних)
- k6 (performance testing)
- GitHub Actions (CI/CD)
