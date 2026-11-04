using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Application.Customers;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/customers")]
public sealed class CustomersController(CustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await customerService.ListAsync(page, pageSize, search, includeDeleted, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Get(
        Guid id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await customerService.GetAsync(id, includeDeleted, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var created = await customerService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
        => Ok(await customerService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await customerService.SoftDeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
