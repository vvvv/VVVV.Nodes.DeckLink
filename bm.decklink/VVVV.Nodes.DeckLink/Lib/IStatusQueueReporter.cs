using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Used to report latency
    /// </summary>
    public interface IStatusQueueReporter
    {
        IReadOnlyList<TimeSpan> QueueData { get; }
    }
}
