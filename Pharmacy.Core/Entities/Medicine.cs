using Pharmacy.Core.Enums;

namespace Pharmacy.Core.Entities;

public class Medicine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool RequiresPrescription { get; set; }
    public Category Category { get; set; }
}
