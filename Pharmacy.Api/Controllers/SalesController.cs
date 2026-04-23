using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISaleService _saleService;

    public SalesController(ISaleService saleService)
    {
        _saleService = saleService;
    }

    [HttpPost]
    public async Task<ActionResult<Sale>> Post([FromBody] SaleRequest request)
    {
        try
        {
            var sale = await _saleService.ProcessSaleAsync(request.PrescriptionId, request.Items);
            return Ok(sale);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Sale>>> Get()
    {
        var sales = await _saleService.GetSalesHistoryAsync();
        return Ok(sales);
    }
}

public class SaleRequest
{
    public Guid? PrescriptionId { get; set; }
    public List<SaleItemRequest> Items { get; set; } = new();
}
