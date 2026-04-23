using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces;

public interface ISaleService
{
    Task<Sale> ProcessSaleAsync(Guid? prescriptionId, IEnumerable<SaleItemRequest> items);
    Task<IEnumerable<Sale>> GetSalesHistoryAsync();
}

public class SaleItemRequest
{
    public Guid MedicineId { get; set; }
    public int Quantity { get; set; }
}
