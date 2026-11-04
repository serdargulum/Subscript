using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Application.Plans;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/plans")]
public sealed class PlansController(PlanService planService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlanDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await planService.ListAsync(page, pageSize, includeDeleted, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanDto>> Get(
        Guid id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await planService.GetAsync(id, includeDeleted, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PlanDto>> Create(
        [FromBody] CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var created = await planService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlanDto>> Update(
        Guid id,
        [FromBody] UpdatePlanRequest request,
        CancellationToken cancellationToken)
        => Ok(await planService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await planService.SoftDeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
