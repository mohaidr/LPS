using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.DIExtensions
{
    public static class ServiceProviderExtensions
    {
        public static T ResolveWith<T>(this IServiceProvider provider, params object[] parameters) where T : class =>
            ActivatorUtilities.CreateInstance<T>(provider, parameters);
    }
}
