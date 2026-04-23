using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly PharmacyDbContext _context;

    public SaleService(PharmacyDbContext context)
    {
        _context = context;
    }

    public async Task<Sale> ProcessSaleAsync(Guid? prescriptionId, IEnumerable<SaleItemRequest> itemRequests)
    {
        if (!itemRequests.Any())
            throw new ArgumentException("Sale must contain at least one item.");

        var itemsList = itemRequests.ToList();
        var medicineIds = itemsList.Select(i => i.MedicineId).ToList();
        var medicines = await _context.Medicines
            .Where(m => medicineIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        if (medicines.Count != itemsList.Select(i => i.MedicineId).Distinct().Count())
            throw new ArgumentException("One or more medicines not found.");

        Prescription? prescription = null;
        if (prescriptionId.HasValue)
        {
            prescription = await _context.Prescriptions
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId.Value);

            if (prescription == null)
                throw new ArgumentException("Prescription not found.");
                
            if (prescription.Status != PrescriptionStatus.Active)
                throw new InvalidOperationException("Prescription is not active (fulfilled or expired).");
                
            if (prescription.ExpiresDate < DateTime.UtcNow)
            {
                prescription.Status = PrescriptionStatus.Expired;
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Prescription has expired.");
            }
        }

        var sale = new Sale
        {
            PrescriptionId = prescriptionId,
            SoldAt = DateTime.UtcNow
        };

        decimal totalAmount = 0;

        foreach (var request in itemsList)
        {
            var med = medicines[request.MedicineId];
            
            if (med.ExpiryDate < DateTime.UtcNow)
                throw new InvalidOperationException($"Medicine {med.Name} is expired and cannot be sold.");

            if (med.StockQuantity < request.Quantity)
                throw new InvalidOperationException($"Insufficient stock for {med.Name}.");

            if (med.RequiresPrescription)
            {
                if (prescription == null)
                    throw new InvalidOperationException($"Medicine {med.Name} requires a prescription.");
                    
                // Ensure the medicine is in the prescription with correct quantity limits, etc. if required.
                // For simplicity, we just check if it's prescribed.
                var prescribedItem = prescription.Items.FirstOrDefault(i => i.MedicineId == med.Id);
                if (prescribedItem == null)
                    throw new InvalidOperationException($"Medicine {med.Name} is not in the provided prescription.");
            }

            med.StockQuantity -= request.Quantity;
            
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                MedicineId = med.Id,
                Quantity = request.Quantity,
                UnitPrice = med.Price
            };
            
            sale.Items.Add(saleItem);
            totalAmount += med.Price * request.Quantity;
        }

        sale.TotalAmount = totalAmount;

        if (prescription != null)
        {
            prescription.Status = PrescriptionStatus.Fulfilled;
        }

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync();

        return sale;
    }

    public async Task<IEnumerable<Sale>> GetSalesHistoryAsync()
    {
        return await _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Medicine)
            .OrderByDescending(s => s.SoldAt)
            .Take(100)
            .ToListAsync();
    }
}
