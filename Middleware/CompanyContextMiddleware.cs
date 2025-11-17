using ShiftManager.Services;

namespace ShiftManager.Middleware;

/// <summary>
/// Middleware that ensures CompanyContext is populated early in the request pipeline.
/// This allows downstream middleware and handlers to access the company context.
/// Multitenancy Phase 2: Context infrastructure
/// </summary>
public class CompanyContextMiddleware
{
    private readonly RequestDelegate _next;

    public CompanyContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICompanyContext companyContext)
    {
        // Force resolution of CompanyContext early in the pipeline
        // The property access will trigger claim resolution and cache it in HttpContext.Items
        _ = companyContext.CompanyId;

        // Continue to the next middleware
        await _next(context);
    }
}