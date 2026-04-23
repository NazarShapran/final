using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services;

public class MedicineService : IMedicineService
{
    private readonly PharmacyDbContext _context;

    public MedicineService(PharmacyDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Medicine>> GetMedicinesAsync(string? category, bool? requiresPrescription, bool? inStock)
    {
        var query = _context.Medicines.AsQueryable();

        if (!string.IsNullOrEmpty(category) && Enum.TryParse<Category>(category, true, out var catEnum))
        {
            query = query.Where(m => m.Category == catEnum);
        }

        if (requiresPrescription.HasValue)
        {
            query = query.Where(m => m.RequiresPrescription == requiresPrescription.Value);
        }

        if (inStock.HasValue && inStock.Value)
        {
            query = query.Where(m => m.StockQuantity > 0);
        }

        return await query.ToListAsync();
    }

    public async Task<Medicine> AddMedicineAsync(Medicine medicine)
    {
        _context.Medicines.Add(medicine);
        await _context.SaveChangesAsync();
        return medicine;
    }

    public async Task<Medicine?> UpdateMedicineAsync(Guid id, Medicine medicine)
    {
        var existing = await _context.Medicines.FindAsync(id);
        if (existing == null) return null;

        existing.Name = medicine.Name;
        existing.GenericName = medicine.GenericName;
        existing.Manufacturer = medicine.Manufacturer;
        existing.Price = medicine.Price;
        existing.StockQuantity = medicine.StockQuantity;
        existing.ExpiryDate = medicine.ExpiryDate;
        existing.RequiresPrescription = medicine.RequiresPrescription;
        existing.Category = medicine.Category;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<IEnumerable<Medicine>> GetExpiringMedicinesAsync(int days)
    {
        var threshold = DateTime.UtcNow.AddDays(days);
        return await _context.Medicines
            .Where(m => m.ExpiryDate <= threshold && m.ExpiryDate >= DateTime.UtcNow)
            .ToListAsync();
    }

    public async Task<IEnumerable<Medicine>> GetLowStockMedicinesAsync(int threshold = 10)
    {
        return await _context.Medicines
            .Where(m => m.StockQuantity < threshold)
            .ToListAsync();
    }
    
    public async Task<Medicine?> GetByIdAsync(Guid id)
    {
        return await _context.Medicines.FindAsync(id);
    }
}
