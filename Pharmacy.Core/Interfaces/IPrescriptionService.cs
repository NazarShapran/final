using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Interfaces;

public interface IPrescriptionService
{
    Task<Prescription> CreatePrescriptionAsync(Prescription prescription);
    Task<Prescription?> GetPrescriptionByIdAsync(Guid id);
}
