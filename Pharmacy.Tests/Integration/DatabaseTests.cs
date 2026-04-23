using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace Pharmacy.Tests.Integration;

public class DatabaseTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    private DbContextOptions<PharmacyDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        using var context = new PharmacyDbContext(_options);
        context.Database.EnsureCreated();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentSales_ShouldMaintainStockConsistency()
    {
        // Arrange: create a medicine with stock = 10
        using (var context = new PharmacyDbContext(_options))
        {
            var medicine = new Medicine
            {
                Id = Guid.NewGuid(),
                Name = "Concurrent Test Medicine",
                GenericName = "ConcurrentGen",
                Manufacturer = "ConcurrentMfg",
                Price = 10m,
                StockQuantity = 10,
                ExpiryDate = DateTime.UtcNow.AddDays(365),
                RequiresPrescription = false,
                Category = Category.Painkiller
            };

            context.Medicines.Add(medicine);
            await context.SaveChangesAsync();
        }

        Guid medicineId;
        using (var context = new PharmacyDbContext(_options))
        {
            medicineId = (await context.Medicines.FirstAsync(m => m.Name == "Concurrent Test Medicine")).Id;
        }

        // Act: run 5 concurrent sales, each buying 1 unit
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var context = new PharmacyDbContext(_options);
            var service = new SaleService(context);
            try
            {
                await service.ProcessSaleAsync(null, new[]
                {
                    new SaleItemRequest { MedicineId = medicineId, Quantity = 1 }
                });
                return true;
            }
            catch
            {
                return false;
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: stock should be consistent (all 5 should succeed since stock is 10)
        using (var context = new PharmacyDbContext(_options))
        {
            var medicine = await context.Medicines.FindAsync(medicineId);
            medicine.Should().NotBeNull();
            var successfulSales = results.Count(r => r);
            // Stock should be reduced by the number of successful sales
            medicine!.StockQuantity.Should().Be(10 - successfulSales);
            medicine.StockQuantity.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task SaleWithPrescription_ShouldMaintainForeignKeyRelationship()
    {
        Guid medicineId, prescriptionId, saleId;

        // Arrange: create medicine and prescription
        using (var context = new PharmacyDbContext(_options))
        {
            var medicine = new Medicine
            {
                Id = Guid.NewGuid(),
                Name = "FK Test Medicine",
                GenericName = "FKGen",
                Manufacturer = "FKMfg",
                Price = 25m,
                StockQuantity = 100,
                ExpiryDate = DateTime.UtcNow.AddDays(365),
                RequiresPrescription = true,
                Category = Category.Antibiotic
            };

            var prescription = new Prescription
            {
                Id = Guid.NewGuid(),
                PatientName = "FK Test Patient",
                PatientPhone = "+380991111111",
                DoctorName = "Dr. FK",
                DoctorLicense = "LIC-1234-5678",
                IssuedDate = DateTime.UtcNow,
                ExpiresDate = DateTime.UtcNow.AddDays(30),
                Status = PrescriptionStatus.Active
            };

            prescription.Items.Add(new PrescriptionItem
            {
                MedicineId = medicine.Id,
                Quantity = 10,
                Dosage = "2 pills",
                Instructions = "After meals"
            });

            context.Medicines.Add(medicine);
            context.Prescriptions.Add(prescription);
            await context.SaveChangesAsync();

            medicineId = medicine.Id;
            prescriptionId = prescription.Id;
        }

        // Act: process a sale with prescription
        using (var context = new PharmacyDbContext(_options))
        {
            var service = new SaleService(context);
            var sale = await service.ProcessSaleAsync(prescriptionId, new[]
            {
                new SaleItemRequest { MedicineId = medicineId, Quantity = 2 }
            });
            saleId = sale.Id;
        }

        // Assert: verify FK relationship persists in DB
        using (var context = new PharmacyDbContext(_options))
        {
            var sale = await context.Sales
                .Include(s => s.Prescription)
                .Include(s => s.Items)
                .ThenInclude(i => i.Medicine)
                .FirstAsync(s => s.Id == saleId);

            sale.PrescriptionId.Should().Be(prescriptionId);
            sale.Prescription.Should().NotBeNull();
            sale.Prescription!.Status.Should().Be(PrescriptionStatus.Fulfilled);
            sale.Items.Should().HaveCount(1);
            sale.Items.First().Medicine.Should().NotBeNull();
            sale.Items.First().MedicineId.Should().Be(medicineId);
            sale.TotalAmount.Should().Be(50m); // 25 * 2
        }
    }

    [Fact]
    public async Task ExpiryDateQueries_ShouldReturnCorrectResults()
    {
        // Arrange: create medicines with various expiry dates
        using (var context = new PharmacyDbContext(_options))
        {
            var medicines = new[]
            {
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "Expiry Test A - Expiring in 5 days",
                    GenericName = "TestA", Manufacturer = "MfgA",
                    Price = 10, StockQuantity = 50,
                    ExpiryDate = DateTime.UtcNow.AddDays(5),
                    RequiresPrescription = false, Category = Category.Vitamin
                },
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "Expiry Test B - Expiring in 15 days",
                    GenericName = "TestB", Manufacturer = "MfgB",
                    Price = 20, StockQuantity = 30,
                    ExpiryDate = DateTime.UtcNow.AddDays(15),
                    RequiresPrescription = false, Category = Category.Painkiller
                },
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "Expiry Test C - Expiring in 60 days",
                    GenericName = "TestC", Manufacturer = "MfgC",
                    Price = 30, StockQuantity = 100,
                    ExpiryDate = DateTime.UtcNow.AddDays(60),
                    RequiresPrescription = false, Category = Category.Cardiac
                },
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "Expiry Test D - Already Expired",
                    GenericName = "TestD", Manufacturer = "MfgD",
                    Price = 5, StockQuantity = 10,
                    ExpiryDate = DateTime.UtcNow.AddDays(-5),
                    RequiresPrescription = false, Category = Category.Other
                }
            };

            context.Medicines.AddRange(medicines);
            await context.SaveChangesAsync();
        }

        // Act & Assert: query for medicines expiring within 10 days
        using (var context = new PharmacyDbContext(_options))
        {
            var service = new MedicineService(context);

            var expiringIn10 = (await service.GetExpiringMedicinesAsync(10)).ToList();
            expiringIn10.Should().Contain(m => m.Name == "Expiry Test A - Expiring in 5 days");
            expiringIn10.Should().NotContain(m => m.Name == "Expiry Test B - Expiring in 15 days");
            expiringIn10.Should().NotContain(m => m.Name == "Expiry Test C - Expiring in 60 days");
            // Already expired should NOT be in the list (ExpiryDate >= UtcNow check)
            expiringIn10.Should().NotContain(m => m.Name == "Expiry Test D - Already Expired");
        }

        // Act & Assert: query for medicines expiring within 30 days
        using (var context = new PharmacyDbContext(_options))
        {
            var service = new MedicineService(context);

            var expiringIn30 = (await service.GetExpiringMedicinesAsync(30)).ToList();
            expiringIn30.Should().Contain(m => m.Name == "Expiry Test A - Expiring in 5 days");
            expiringIn30.Should().Contain(m => m.Name == "Expiry Test B - Expiring in 15 days");
            expiringIn30.Should().NotContain(m => m.Name == "Expiry Test C - Expiring in 60 days");
        }
    }

    [Fact]
    public async Task LowStockQuery_ShouldReturnOnlyMedicinesBelowThreshold()
    {
        // Arrange
        using (var context = new PharmacyDbContext(_options))
        {
            var medicines = new[]
            {
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "LowStock Test A - Stock 3",
                    GenericName = "LSA", Manufacturer = "LSMfg",
                    Price = 10, StockQuantity = 3,
                    ExpiryDate = DateTime.UtcNow.AddDays(365),
                    RequiresPrescription = false, Category = Category.Vitamin
                },
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "LowStock Test B - Stock 50",
                    GenericName = "LSB", Manufacturer = "LSMfg",
                    Price = 20, StockQuantity = 50,
                    ExpiryDate = DateTime.UtcNow.AddDays(365),
                    RequiresPrescription = false, Category = Category.Painkiller
                },
                new Medicine
                {
                    Id = Guid.NewGuid(),
                    Name = "LowStock Test C - Stock 9",
                    GenericName = "LSC", Manufacturer = "LSMfg",
                    Price = 15, StockQuantity = 9,
                    ExpiryDate = DateTime.UtcNow.AddDays(365),
                    RequiresPrescription = false, Category = Category.Other
                }
            };

            context.Medicines.AddRange(medicines);
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = new PharmacyDbContext(_options))
        {
            var service = new MedicineService(context);
            var lowStock = (await service.GetLowStockMedicinesAsync()).ToList();

            // Assert
            lowStock.Should().Contain(m => m.Name == "LowStock Test A - Stock 3");
            lowStock.Should().NotContain(m => m.Name == "LowStock Test B - Stock 50");
            lowStock.Should().Contain(m => m.Name == "LowStock Test C - Stock 9");
            lowStock.Should().OnlyContain(m => m.StockQuantity < 10);
        }
    }
}
