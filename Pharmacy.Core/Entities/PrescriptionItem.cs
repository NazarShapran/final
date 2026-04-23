namespace Pharmacy.Core.Entities;

public class PrescriptionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    
    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    
    public string Dosage { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Instructions { get; set; } = string.Empty;
}
