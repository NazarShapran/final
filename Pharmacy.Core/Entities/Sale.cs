namespace Pharmacy.Core.Entities;

public class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    
    public decimal TotalAmount { get; set; }
    public DateTime SoldAt { get; set; } = DateTime.UtcNow;
}
