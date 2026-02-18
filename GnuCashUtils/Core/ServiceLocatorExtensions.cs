using System;
using Splat;

namespace GnuCashUtils.Core;

public static class ServiceLocatorExtensions
{
    public static T GetRequiredService<T>(this IReadonlyDependencyResolver locator)
    {
        return locator.GetService<T>() ?? throw new InvalidOperationException($"Service of type {typeof(T)} not found.");
    }
    
}
