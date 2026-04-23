using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Enums;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services;

public partial class PrescriptionService : IPrescriptionService
{
    private readonly PharmacyDbContext _context;

    public PrescriptionService(PharmacyDbContext context)
    {
        _context = context;
    }

    public async Task<Prescription> CreatePrescriptionAsync(Prescription prescription)
    {
        if (string.IsNullOrWhiteSpace(prescription.DoctorLicense))
            throw new ArgumentException("Doctor license is required.");

        if (!DoctorLicenseRegex().IsMatch(prescription.DoctorLicense))
            throw new ArgumentException("Doctor license must match format LIC-####-#### (e.g. LIC-1234-5678).");

        prescription.IssuedDate = DateTime.UtcNow;
        prescription.ExpiresDate = prescription.IssuedDate.AddDays(30);
        prescription.Status = PrescriptionStatus.Active;

        _context.Prescriptions.Add(prescription);
        await _context.SaveChangesAsync();

        return prescription;
    }

    public async Task<Prescription?> GetPrescriptionByIdAsync(Guid id)
    {
        return await _context.Prescriptions
            .Include(p => p.Items)
            .ThenInclude(i => i.Medicine)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    [GeneratedRegex(@"^LIC-\d{4}-\d{4}$")]
    private static partial Regex DoctorLicenseRegex();
}
