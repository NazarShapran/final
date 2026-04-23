using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptionService;

    public PrescriptionsController(IPrescriptionService prescriptionService)
    {
        _prescriptionService = prescriptionService;
    }

    [HttpPost]
    public async Task<ActionResult<Prescription>> Post(Prescription prescription)
    {
        try
        {
            var created = await _prescriptionService.CreatePrescriptionAsync(prescription);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Prescription>> Get(Guid id)
    {
        var prescription = await _prescriptionService.GetPrescriptionByIdAsync(id);
        if (prescription == null) return NotFound();
        return Ok(prescription);
    }
}
