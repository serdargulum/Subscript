using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Common;
using Subscript.Application.Payments;
using Subscript.Domain.Payments;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class PaymentsController(PaymentService paymentService) : ControllerBase
{
    [HttpPost("payments")]
    public async Task<ActionResult<PaymentDto>> Record(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await paymentService.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListByInvoice), new { invoiceId = created.InvoiceId }, created);
    }

    [HttpGet("payments")]
    public async Task<ActionResult<PagedResult<PaymentDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] Guid? invoiceId = null,
        [FromQuery] PaymentStatus? status = null,
        CancellationToken cancellationToken = default)
        => Ok(await paymentService.ListAsync(page, pageSize, invoiceId, status, cancellationToken));

    [HttpGet("invoices/{invoiceId:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByInvoice(
        Guid invoiceId,
        CancellationToken cancellationToken)
        => Ok(await paymentService.ListByInvoiceAsync(invoiceId, cancellationToken));
}
