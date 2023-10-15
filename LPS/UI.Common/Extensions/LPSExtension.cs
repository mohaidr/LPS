using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace LPS.UI.Common.Extensions
{
    public static class LPSExtension
    {
        public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this Dictionary<TKey, TValue> toclone)
        {
            return toclone.ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}
