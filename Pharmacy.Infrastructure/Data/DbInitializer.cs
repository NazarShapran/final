using Bogus;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;

namespace Pharmacy.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(PharmacyDbContext context)
    {
        if (context.Medicines.Any() || context.Prescriptions.Any() || context.Sales.Any())
            return; // DB has been seeded

        // Allow Bogus to bypass navigation property validation if needed
        Randomizer.Seed = new Random(42);

        var medicineFaker = new Faker<Medicine>()
            .RuleFor(m => m.Id, f => f.Random.Guid())
            .RuleFor(m => m.Name, f => f.Commerce.ProductName())
            .RuleFor(m => m.GenericName, f => f.Commerce.ProductMaterial())
            .RuleFor(m => m.Manufacturer, f => f.Company.CompanyName())
            .RuleFor(m => m.Price, f => f.Random.Decimal(1m, 500m))
            .RuleFor(m => m.StockQuantity, f => f.Random.Int(0, 500))
            .RuleFor(m => m.ExpiryDate, f => DateTime.SpecifyKind(f.Date.Future(2), DateTimeKind.Utc))
            .RuleFor(m => m.RequiresPrescription, f => f.Random.Bool())
            .RuleFor(m => m.Category, f => f.PickRandom<Category>());

        var medicines = medicineFaker.Generate(2000);
        context.Medicines.AddRange(medicines);
        // Save here so we can reference them
        context.SaveChanges();

        var prescriptionFaker = new Faker<Prescription>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.PatientName, f => f.Name.FullName())
            .RuleFor(p => p.PatientPhone, f => f.Phone.PhoneNumber())
            .RuleFor(p => p.DoctorName, f => $"Dr. {f.Name.FullName()}")
            .RuleFor(p => p.DoctorLicense, f => f.Random.Replace("LIC-####-####"))
            .RuleFor(p => p.IssuedDate, f => DateTime.SpecifyKind(f.Date.Past(1), DateTimeKind.Utc))
            .RuleFor(p => p.ExpiresDate, (f, p) => DateTime.SpecifyKind(p.IssuedDate.AddDays(30), DateTimeKind.Utc))
            .RuleFor(p => p.Status, f => f.PickRandom<PrescriptionStatus>());

        var prescriptions = prescriptionFaker.Generate(3000);
        
        var prescriptionItemFaker = new Faker<PrescriptionItem>()
            .RuleFor(i => i.Id, f => f.Random.Guid())
            .RuleFor(i => i.MedicineId, f => f.PickRandom(medicines).Id)
            .RuleFor(i => i.Dosage, f => $"{f.Random.Int(1, 3)} pills")
            .RuleFor(i => i.Quantity, f => f.Random.Int(10, 60))
            .RuleFor(i => i.Instructions, f => "Take after meals");

        foreach (var p in prescriptions)
        {
            var pItems = prescriptionItemFaker.Generate(new Random().Next(1, 4));
            foreach(var item in pItems) {
                item.PrescriptionId = p.Id;
            }
            p.Items = pItems;
        }

        context.Prescriptions.AddRange(prescriptions);
        context.SaveChanges();

        var saleFaker = new Faker<Sale>()
            .RuleFor(s => s.Id, f => f.Random.Guid())
            .RuleFor(s => s.SoldAt, f => DateTime.SpecifyKind(f.Date.Recent(30), DateTimeKind.Utc))
            .RuleFor(s => s.TotalAmount, 0m); // Calculated later

        var sales = saleFaker.Generate(5000);
        
        var saleItemFaker = new Faker<SaleItem>()
            .RuleFor(i => i.Id, f => f.Random.Guid())
            // Will set Medicine, SaleId, Quantity, UnitPrice manually to ensure consistency
            ;

        foreach (var sale in sales)
        {
            var itemsCount = new Random().Next(1, 5);
            decimal total = 0;
            for (int i = 0; i < itemsCount; i++)
            {
                var med = medicines[new Random().Next(medicines.Count)];
                var qty = new Random().Next(1, 4);
                total += med.Price * qty;
                
                var saleItem = new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    MedicineId = med.Id,
                    Quantity = qty,
                    UnitPrice = med.Price
                };
                sale.Items.Add(saleItem);
            }
            sale.TotalAmount = total;
            
            // Randomly link to a fulfilled prescription
            if (new Random().NextDouble() > 0.7)
            {
                var pres = prescriptions[new Random().Next(prescriptions.Count)];
                sale.PrescriptionId = pres.Id;
                pres.Status = PrescriptionStatus.Fulfilled;
            }
        }

        context.Sales.AddRange(sales);
        context.SaveChanges();
    }
}
