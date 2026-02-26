using System;
using Splat;

namespace GnuCashUtils.Core;

public static class ServiceLocatorExtensions
{
    extension(IReadonlyDependencyResolver locator)
    {
        public T GetRequiredService<T>()
        {
            return locator.GetService<T>() ?? throw new InvalidOperationException($"Service of type {typeof(T)} not found.");
        }
    }
    
}
