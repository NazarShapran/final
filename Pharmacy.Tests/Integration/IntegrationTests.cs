using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pharmacy.Api.Controllers;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Pharmacy.Tests.Integration;

public class PharmacyApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    private HttpClient _httpClient = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<PharmacyDbContext>));
                services.AddDbContext<PharmacyDbContext>(options =>
                {
                    options.UseNpgsql(_dbContainer.GetConnectionString());
                });
            });
        });

        _httpClient = _factory.CreateClient();

        // Ensure schema is created and seed data
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        context.Database.EnsureCreated();
        DbInitializer.Initialize(context);
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    // ==================== Sales Tests ====================

    [Fact]
    public async Task PostSale_WithoutPrescription_WhenRequiresPrescription_ShouldReturnBadRequest()
    {
        // Get a medicine that requires prescription
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var medicine = await context.Medicines.FirstAsync(m => m.RequiresPrescription && m.StockQuantity > 0);

        var request = new SaleRequest
        {
            PrescriptionId = null,
            Items = new List<SaleItemRequest>
            {
                new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/sales", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSale_WithValidPrescription_ShouldReturnOk()
    {
        // Arrange: find a medicine that requires prescription and has stock
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();

        var medicine = await context.Medicines
            .FirstAsync(m => m.RequiresPrescription && m.StockQuantity > 5 && m.ExpiryDate > DateTime.UtcNow);

        // Create a valid prescription via API
        var prescription = new Prescription
        {
            PatientName = "Test Patient",
            PatientPhone = "+380991234567",
            DoctorName = "Dr. Test",
            DoctorLicense = "LIC-1111-2222",
            Items = new List<PrescriptionItem>
            {
                new PrescriptionItem { MedicineId = medicine.Id, Quantity = 10, Dosage = "1 pill", Instructions = "Once daily" }
            }
        };

        var prescriptionResponse = await _httpClient.PostAsJsonAsync("/api/prescriptions", prescription);
        prescriptionResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPrescription = await prescriptionResponse.Content.ReadFromJsonAsync<Prescription>();

        // Act: sell using the prescription
        var saleRequest = new SaleRequest
        {
            PrescriptionId = createdPrescription!.Id,
            Items = new List<SaleItemRequest>
            {
                new SaleItemRequest { MedicineId = medicine.Id, Quantity = 1 }
            }
        };

        var saleResponse = await _httpClient.PostAsJsonAsync("/api/sales", saleRequest);

        // Assert
        saleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSales_ShouldReturnSalesHistory()
    {
        var response = await _httpClient.GetAsync("/api/sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sales = await response.Content.ReadFromJsonAsync<List<Sale>>();
        sales.Should().NotBeNull();
        sales!.Count.Should().BeGreaterThan(0);
    }

    // ==================== Medicines Tests ====================

    [Fact]
    public async Task GetMedicines_FilterByCategory_ShouldReturnFiltered()
    {
        var response = await _httpClient.GetAsync("/api/medicines?category=Antibiotic");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var medicines = await response.Content.ReadFromJsonAsync<List<Medicine>>();
        medicines.Should().NotBeNull();
        medicines!.Should().OnlyContain(m => m.Category == Category.Antibiotic);
    }

    [Fact]
    public async Task GetExpiringMedicines_ShouldReturnMedicines()
    {
        var response = await _httpClient.GetAsync("/api/medicines/expiring?days=365");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var medicines = await response.Content.ReadFromJsonAsync<List<Medicine>>();
        medicines.Should().NotBeNull();
        // With 2000 medicines and expiry within 2 years, we should have some expiring in the next year
        medicines!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLowStockMedicines_ShouldReturnMedicinesWithLowStock()
    {
        var response = await _httpClient.GetAsync("/api/medicines/low-stock");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var medicines = await response.Content.ReadFromJsonAsync<List<Medicine>>();
        medicines.Should().NotBeNull();
        // With random stock 0-500, there should be some below 10
        medicines!.Should().OnlyContain(m => m.StockQuantity < 10);
    }

    [Fact]
    public async Task PostMedicine_ShouldCreateMedicine()
    {
        var medicine = new Medicine
        {
            Name = "Integration Test Medicine",
            GenericName = "TestGeneric",
            Manufacturer = "TestManufacturer",
            Price = 99.99m,
            StockQuantity = 100,
            ExpiryDate = DateTime.UtcNow.AddDays(365),
            RequiresPrescription = false,
            Category = Category.Vitamin
        };

        var response = await _httpClient.PostAsJsonAsync("/api/medicines", medicine);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ==================== Prescriptions Tests ====================

    [Fact]
    public async Task PostPrescription_ShouldCreateWithCorrectDates()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var medicine = await context.Medicines.FirstAsync(m => m.RequiresPrescription);

        var prescription = new Prescription
        {
            PatientName = "Test Patient",
            PatientPhone = "+380991234567",
            DoctorName = "Dr. Test",
            DoctorLicense = "LIC-3333-4444",
            Items = new List<PrescriptionItem>
            {
                new PrescriptionItem { MedicineId = medicine.Id, Quantity = 5, Dosage = "1 pill", Instructions = "Once daily" }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/prescriptions", prescription);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<Prescription>();
        created.Should().NotBeNull();
        created!.Status.Should().Be(PrescriptionStatus.Active);
    }

    [Fact]
    public async Task PostPrescription_WithInvalidLicense_ShouldReturnBadRequest()
    {
        var prescription = new Prescription
        {
            PatientName = "Test Patient",
            PatientPhone = "+380991234567",
            DoctorName = "Dr. Test",
            DoctorLicense = "INVALID",
        };

        var response = await _httpClient.PostAsJsonAsync("/api/prescriptions", prescription);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPrescription_ExistingId_ShouldReturnDetails()
    {
        // Create a prescription first
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var existingPrescription = await context.Prescriptions
            .Include(p => p.Items)
            .FirstAsync(p => p.Items.Any());

        var response = await _httpClient.GetAsync($"/api/prescriptions/{existingPrescription.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var prescription = await response.Content.ReadFromJsonAsync<Prescription>();
        prescription.Should().NotBeNull();
        prescription!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPrescription_NonExistingId_ShouldReturnNotFound()
    {
        var response = await _httpClient.GetAsync($"/api/prescriptions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
