using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests.Unit;

public class SaleServiceTests
{
    private readonly Fixture _fixture;

    public SaleServiceTests()
    {
        _fixture = new Fixture();
        // Prevent AutoFixture from following navigation properties
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

    private Medicine CreateMedicine(
        bool requiresPrescription = false,
        int stockQuantity = 100,
        int expiryDaysFromNow = 30)
    {
        var medicine = _fixture.Build<Medicine>()
            .With(m => m.RequiresPrescription, requiresPrescription)
            .With(m => m.StockQuantity, stockQuantity)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(expiryDaysFromNow))
            .With(m => m.Price, 50m)
            .With(m => m.Category, Category.Painkiller)
            .Create();
        return medicine;
    }

    private Prescription CreatePrescription(
        Guid medicineId,
        PrescriptionStatus status = PrescriptionStatus.Active,
        int expiresDaysFromNow = 30)
    {
        var prescription = _fixture.Build<Prescription>()
            .With(p => p.DoctorLicense, "LIC-1234-5678")
            .With(p => p.IssuedDate, DateTime.UtcNow)
            .With(p => p.ExpiresDate, DateTime.UtcNow.AddDays(expiresDaysFromNow))
            .With(p => p.Status, status)
            .With(p => p.Items, new List<PrescriptionItem>
            {
                new PrescriptionItem { MedicineId = medicineId, Quantity = 10, Dosage = "2 pills", Instructions = "Take after meals" }
            })
            .Create();
        return prescription;
    }

    [Fact]
    public async Task ProcessSale_WithExpiredMedicine_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(expiryDaysFromNow: -1); // Expired

        context.Medicines.Add(medicine);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(null, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task ProcessSale_WithoutRequiredPrescription_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(requiresPrescription: true);

        context.Medicines.Add(medicine);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(null, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a prescription*");
    }

    [Fact]
    public async Task ProcessSale_WithValidPrescription_ShouldDeductStockAndFulfill()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(requiresPrescription: true);
        var prescription = CreatePrescription(medicine.Id);

        context.Medicines.Add(medicine);
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 5 } };

        // Act
        var result = await service.ProcessSaleAsync(prescription.Id, request);

        // Assert
        result.Should().NotBeNull();
        result.TotalAmount.Should().Be(250);

        var updatedMed = await context.Medicines.FindAsync(medicine.Id);
        updatedMed!.StockQuantity.Should().Be(95);

        var updatedPrescription = await context.Prescriptions.FindAsync(prescription.Id);
        updatedPrescription!.Status.Should().Be(PrescriptionStatus.Fulfilled);
    }

    [Fact]
    public async Task ProcessSale_WithExpiredPrescription_ShouldThrowAndMarkExpired()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(requiresPrescription: true);
        var prescription = CreatePrescription(medicine.Id, expiresDaysFromNow: -5);

        context.Medicines.Add(medicine);
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(prescription.Id, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");

        var updatedPrescription = await context.Prescriptions.FindAsync(prescription.Id);
        updatedPrescription!.Status.Should().Be(PrescriptionStatus.Expired);
    }

    [Fact]
    public async Task ProcessSale_WithInsufficientStock_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(stockQuantity: 2);

        context.Medicines.Add(medicine);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 10 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(null, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    [Fact]
    public async Task ProcessSale_WithFulfilledPrescription_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var medicine = CreateMedicine(requiresPrescription: true);
        var prescription = CreatePrescription(medicine.Id, status: PrescriptionStatus.Fulfilled);

        context.Medicines.Add(medicine);
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        var request = new[] { new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(prescription.Id, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task ProcessSale_WithEmptyItems_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(null, Enumerable.Empty<SaleItemRequest>());
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public async Task ProcessSale_MedicineNotInPrescription_ShouldThrowException()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new SaleService(context);

        var prescribedMedicine = CreateMedicine(requiresPrescription: true);
        var otherMedicine = CreateMedicine(requiresPrescription: true);

        var prescription = CreatePrescription(prescribedMedicine.Id);

        context.Medicines.AddRange(prescribedMedicine, otherMedicine);
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        // Try to sell the OTHER medicine using the prescription
        var request = new[] { new SaleItemRequest { MedicineId = otherMedicine.Id, Quantity = 1 } };

        // Act & Assert
        var action = async () => await service.ProcessSaleAsync(prescription.Id, request);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in the provided prescription*");
    }
}
