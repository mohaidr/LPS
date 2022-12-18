using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsyncCalls.Extensions
{
    public static class GenericExtensions
    {
        public static void Clone<T1, T2>(this Dictionary<T1, T2> toclone) where T1 : class where T2 : class
        {
            toclone.ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}
