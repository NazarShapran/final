namespace Pharmacy.Core.Entities;

public class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
