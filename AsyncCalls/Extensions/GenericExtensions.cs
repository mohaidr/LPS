using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncCalls.Extensions
{
    public static class GenericExtensions
    {
        public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this Dictionary<TKey, TValue> toclone)
        {
            return toclone.ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}
