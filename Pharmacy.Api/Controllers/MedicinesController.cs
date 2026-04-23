using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MedicinesController : ControllerBase
{
    private readonly IMedicineService _medicineService;

    public MedicinesController(IMedicineService medicineService)
    {
        _medicineService = medicineService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Medicine>>> Get([FromQuery] string? category, [FromQuery] bool? requiresPrescription, [FromQuery] bool? inStock)
    {
        var medicines = await _medicineService.GetMedicinesAsync(category, requiresPrescription, inStock);
        return Ok(medicines);
    }

    [HttpPost]
    public async Task<ActionResult<Medicine>> Post(Medicine medicine)
    {
        var created = await _medicineService.AddMedicineAsync(medicine);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Medicine>> Put(Guid id, Medicine medicine)
    {
        var updated = await _medicineService.UpdateMedicineAsync(id, medicine);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetExpiring([FromQuery] int days = 30)
    {
        return Ok(await _medicineService.GetExpiringMedicinesAsync(days));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IEnumerable<Medicine>>> GetLowStock()
    {
        return Ok(await _medicineService.GetLowStockMedicinesAsync());
    }
}
