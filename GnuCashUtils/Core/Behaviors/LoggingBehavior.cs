using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using SerilogTimings.Extensions;

namespace GnuCashUtils.Core.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : notnull
{
    private static readonly ILogger _log = Log.ForContext(Constants.SourceContextPropertyName,
        typeof(LoggingBehavior<,>).FullName);

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        using var context = LogContext.PushProperty("MediatrRequest", requestName);
        using var op = _log.BeginOperation("Processing {Request}", requestName);

        try
        {
            var result = await next();
            op.Complete();
            return result;
        }
        catch (Exception ex)
        {
            op.SetException(ex);
            throw;
        }
    }
}