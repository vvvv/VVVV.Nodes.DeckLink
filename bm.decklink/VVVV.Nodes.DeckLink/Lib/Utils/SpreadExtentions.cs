using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;

namespace VVVV.DeckLink.Utils
{
    /// <summary>
    /// Small extension methods for spread
    /// </summary>
    public static class SpreadExtensions
    {
        /// <summary>
        /// Returns a default value if spread is 0 slices or input is null
        /// </summary>
        /// <typeparam name="T">Type names</typeparam>
        /// <param name="index">Slice index</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>Value if spread is > 0 and slice is not null, default otherwise</returns>
        public static T DefaultIfNilOrNull<T>(this ISpread<T> spread, int index,  T defaultValue) where T : class
        {
            return spread.SliceCount == 0 ? defaultValue :
                spread[index] != null ? spread[index] : defaultValue;

        }
    }
}
