using log4net;

namespace DataWarehousePower.Middleware;

public sealed class RequestExceptionLoggingMiddleware(RequestDelegate next)
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(RequestExceptionLoggingMiddleware));

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                throw;
            }

            string requestDescription = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
            Logger.Error($"Unhandled exception while processing {requestDescription}. TraceId: {context.TraceIdentifier}", ex);
            throw;
        }
    }
}