using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Application.Subscriptions;
using Subscript.Domain.Subscriptions;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class SubscriptionsController(SubscriptionService subscriptionService) : ControllerBase
{
    [HttpPost("subscriptions")]
    public async Task<ActionResult<SubscriptionDto>> Create(
        [FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await subscriptionService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("subscriptions/{id:guid}")]
    public async Task<ActionResult<SubscriptionDto>> Get(
        Guid id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await subscriptionService.GetAsync(id, includeDeleted, cancellationToken));

    [HttpGet("subscriptions")]
    public async Task<ActionResult<PagedResult<SubscriptionDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] SubscriptionStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await subscriptionService.ListAsync(
            page,
            pageSize,
            status,
            customerId,
            includeDeleted,
            cancellationToken));

    [HttpGet("customers/{customerId:guid}/subscriptions")]
    public async Task<ActionResult<PagedResult<SubscriptionDto>>> ListByCustomer(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await subscriptionService.ListByCustomerAsync(
            customerId,
            page,
            pageSize,
            includeDeleted,
            cancellationToken));

    [HttpPost("subscriptions/{id:guid}/cancel")]
    public async Task<ActionResult<SubscriptionDto>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await subscriptionService.CancelAsync(id, cancellationToken));

    [HttpPost("subscriptions/{id:guid}/change-plan")]
    public async Task<ActionResult<SubscriptionDto>> ChangePlan(
        Guid id,
        [FromBody] ChangePlanRequest request,
        CancellationToken cancellationToken)
        => Ok(await subscriptionService.ChangePlanAsync(id, request, cancellationToken));

    [HttpDelete("subscriptions/{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        await subscriptionService.SoftDeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
