using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests.Unit;

public class MedicineServiceTests
{
    private readonly Fixture _fixture;

    public MedicineServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    private DbContextOptions<PharmacyDbContext> GetInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task GetMedicines_FilterByCategory_ShouldReturnOnlyMatching()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var antibiotic = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Antibiotic)
            .With(m => m.StockQuantity, 50)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 20m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        var vitamin = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Vitamin)
            .With(m => m.StockQuantity, 30)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 10m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        context.Medicines.AddRange(antibiotic, vitamin);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMedicinesAsync("Antibiotic", null, null);

        // Assert
        result.Should().HaveCount(1);
        result.First().Category.Should().Be(Category.Antibiotic);
    }

    [Fact]
    public async Task GetMedicines_FilterByInStock_ShouldExcludeOutOfStock()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var inStock = _fixture.Build<Medicine>()
            .With(m => m.StockQuantity, 50)
            .With(m => m.Category, Category.Other)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 20m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        var outOfStock = _fixture.Build<Medicine>()
            .With(m => m.StockQuantity, 0)
            .With(m => m.Category, Category.Other)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 10m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        context.Medicines.AddRange(inStock, outOfStock);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetMedicinesAsync(null, null, true);

        // Assert
        result.Should().HaveCount(1);
        result.First().StockQuantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLowStockMedicines_ShouldReturnMedicinesBelowThreshold()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var lowStock = _fixture.Build<Medicine>()
            .With(m => m.StockQuantity, 5)
            .With(m => m.Category, Category.Cardiac)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 100m)
            .With(m => m.RequiresPrescription, true)
            .Create();

        var normalStock = _fixture.Build<Medicine>()
            .With(m => m.StockQuantity, 50)
            .With(m => m.Category, Category.Vitamin)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 10m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        context.Medicines.AddRange(lowStock, normalStock);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetLowStockMedicinesAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().StockQuantity.Should().BeLessThan(10);
    }

    [Fact]
    public async Task GetExpiringMedicines_ShouldReturnMedicinesExpiringWithinDays()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var expiringSoon = _fixture.Build<Medicine>()
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(5))
            .With(m => m.Category, Category.Painkiller)
            .With(m => m.StockQuantity, 20)
            .With(m => m.Price, 30m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        var notExpiring = _fixture.Build<Medicine>()
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(365))
            .With(m => m.Category, Category.Vitamin)
            .With(m => m.StockQuantity, 100)
            .With(m => m.Price, 10m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        context.Medicines.AddRange(expiringSoon, notExpiring);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetExpiringMedicinesAsync(10);

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(expiringSoon.Id);
    }

    [Fact]
    public async Task AddMedicine_ShouldPersistToDatabase()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var medicine = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Antibiotic)
            .With(m => m.StockQuantity, 100)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(180))
            .With(m => m.Price, 25m)
            .With(m => m.RequiresPrescription, true)
            .Create();

        // Act
        var result = await service.AddMedicineAsync(medicine);

        // Assert
        result.Should().NotBeNull();
        var fromDb = await context.Medicines.FindAsync(result.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Name.Should().Be(medicine.Name);
    }

    [Fact]
    public async Task UpdateMedicine_ExistingMedicine_ShouldUpdateFields()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var medicine = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Other)
            .With(m => m.StockQuantity, 50)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(90))
            .With(m => m.Price, 15m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        context.Medicines.Add(medicine);
        await context.SaveChangesAsync();

        var updated = _fixture.Build<Medicine>()
            .With(m => m.Name, "Updated Name")
            .With(m => m.Price, 99m)
            .With(m => m.StockQuantity, 200)
            .With(m => m.Category, Category.Cardiac)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(365))
            .With(m => m.RequiresPrescription, true)
            .Create();

        // Act
        var result = await service.UpdateMedicineAsync(medicine.Id, updated);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Price.Should().Be(99m);
        result.StockQuantity.Should().Be(200);
    }

    [Fact]
    public async Task UpdateMedicine_NonExistingId_ShouldReturnNull()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new MedicineService(context);

        var medicine = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Other)
            .With(m => m.StockQuantity, 50)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(90))
            .With(m => m.Price, 15m)
            .With(m => m.RequiresPrescription, false)
            .Create();

        // Act
        var result = await service.UpdateMedicineAsync(Guid.NewGuid(), medicine);

        // Assert
        result.Should().BeNull();
    }
}
