using Microsoft.Extensions.DependencyInjection;
using Subscript.Application.Billing;
using Subscript.Application.Customers;
using Subscript.Application.Invoices;
using Subscript.Application.Payments;
using Subscript.Application.Plans;
using Subscript.Application.Subscriptions;

namespace Subscript.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CustomerService>();
        services.AddScoped<PlanService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<BillingService>();
        return services;
    }
}
