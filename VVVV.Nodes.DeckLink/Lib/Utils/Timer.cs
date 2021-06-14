using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.Decklink.Utils
{
    /// <summary>
    /// Simple stopwatch timer, that gets elapsed time since started
    /// </summary>
    public static class Timer
    {
        private static Stopwatch watch = Stopwatch.StartNew();

        /// <summary>
        /// Elapsed since library was loaded
        /// </summary>
        public static TimeSpan Elapsed
        {
            get
            {
                return watch.Elapsed;
            }
        }
    }
}
