using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests.Unit;

public class PrescriptionServiceTests
{
    private readonly Fixture _fixture;

    public PrescriptionServiceTests()
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
    public async Task CreatePrescription_WithValidLicense_ShouldSetDatesAndStatus()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new PrescriptionService(context);

        var prescription = _fixture.Build<Prescription>()
            .With(p => p.DoctorLicense, "LIC-1234-5678")
            .With(p => p.Items, new List<PrescriptionItem>())
            .Create();

        // Act
        var result = await service.CreatePrescriptionAsync(prescription);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(PrescriptionStatus.Active);
        result.ExpiresDate.Should().BeCloseTo(result.IssuedDate.AddDays(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreatePrescription_WithEmptyLicense_ShouldThrow()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new PrescriptionService(context);

        var prescription = _fixture.Build<Prescription>()
            .With(p => p.DoctorLicense, "")
            .With(p => p.Items, new List<PrescriptionItem>())
            .Create();

        // Act & Assert
        var action = async () => await service.CreatePrescriptionAsync(prescription);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*required*");
    }

    [Fact]
    public async Task CreatePrescription_WithInvalidLicenseFormat_ShouldThrow()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new PrescriptionService(context);

        var prescription = _fixture.Build<Prescription>()
            .With(p => p.DoctorLicense, "INVALID-FORMAT")
            .With(p => p.Items, new List<PrescriptionItem>())
            .Create();

        // Act & Assert
        var action = async () => await service.CreatePrescriptionAsync(prescription);
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*format*");
    }

    [Fact]
    public async Task GetPrescription_ExistingId_ShouldReturnWithItems()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new PrescriptionService(context);

        var medicine = _fixture.Build<Medicine>()
            .With(m => m.Category, Category.Antibiotic)
            .With(m => m.StockQuantity, 50)
            .With(m => m.ExpiryDate, DateTime.UtcNow.AddDays(60))
            .With(m => m.Price, 20m)
            .With(m => m.RequiresPrescription, true)
            .Create();

        context.Medicines.Add(medicine);
        await context.SaveChangesAsync();

        var prescription = _fixture.Build<Prescription>()
            .With(p => p.DoctorLicense, "LIC-9999-0000")
            .With(p => p.IssuedDate, DateTime.UtcNow)
            .With(p => p.ExpiresDate, DateTime.UtcNow.AddDays(30))
            .With(p => p.Status, PrescriptionStatus.Active)
            .With(p => p.Items, new List<PrescriptionItem>
            {
                new PrescriptionItem { MedicineId = medicine.Id, Quantity = 10, Dosage = "1 pill", Instructions = "Twice daily" }
            })
            .Create();

        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        // Act — use a fresh context to ensure Include works
        using var readContext = new PharmacyDbContext(options);
        var readService = new PrescriptionService(readContext);
        var result = await readService.GetPrescriptionByIdAsync(prescription.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPrescription_NonExistingId_ShouldReturnNull()
    {
        // Arrange
        var options = GetInMemoryOptions(Guid.NewGuid().ToString());
        using var context = new PharmacyDbContext(options);
        var service = new PrescriptionService(context);

        // Act
        var result = await service.GetPrescriptionByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }
}
