using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Subscript.Application.Billing;

namespace Subscript.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/billing")]
public sealed class BillingController(BillingService billingService) : ControllerBase
{
    /// <summary>
    /// Manually triggers due-subscription billing. Useful for local/dev until the RabbitMQ scheduler is enabled.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<object>> Run(CancellationToken cancellationToken)
    {
        var processed = await billingService.ProcessDueSubscriptionsAsync(cancellationToken);
        return Ok(new { processed });
    }
}
