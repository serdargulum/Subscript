using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Application.Invoices;
using Subscript.Domain.Invoices;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class InvoicesController(InvoiceService invoiceService) : ControllerBase
{
    [HttpGet("invoices")]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] Guid? subscriptionId = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await invoiceService.ListAsync(
            page,
            pageSize,
            status,
            subscriptionId,
            includeDeleted,
            cancellationToken));

    [HttpGet("invoices/{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(
        Guid id,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await invoiceService.GetAsync(id, includeDeleted, cancellationToken));

    [HttpGet("subscriptions/{subscriptionId:guid}/invoices")]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> ListBySubscription(
        Guid subscriptionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
        => Ok(await invoiceService.ListBySubscriptionAsync(
            subscriptionId,
            page,
            pageSize,
            includeDeleted,
            cancellationToken));

    [HttpPost("invoices/{id:guid}/cancel")]
    public async Task<ActionResult<InvoiceDto>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await invoiceService.CancelAsync(id, cancellationToken));
}
