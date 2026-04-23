using Pharmacy.Core.Enums;

namespace Pharmacy.Core.Entities;

public class Prescription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatientName { get; set; } = string.Empty;
    public string PatientPhone { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorLicense { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiresDate { get; set; }
    public PrescriptionStatus Status { get; set; }
    
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}
