using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces;

public interface IMedicineService
{
    Task<IEnumerable<Medicine>> GetMedicinesAsync(string? category, bool? requiresPrescription, bool? inStock);
    Task<Medicine> AddMedicineAsync(Medicine medicine);
    Task<Medicine?> UpdateMedicineAsync(Guid id, Medicine medicine);
    Task<IEnumerable<Medicine>> GetExpiringMedicinesAsync(int days);
    Task<IEnumerable<Medicine>> GetLowStockMedicinesAsync(int threshold = 10);
    Task<Medicine?> GetByIdAsync(Guid id);
}
