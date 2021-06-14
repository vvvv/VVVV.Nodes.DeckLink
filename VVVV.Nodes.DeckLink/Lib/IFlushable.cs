using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Specific interface in case we want to flush pending frame queue
    /// </summary>
    public interface IFlushable
    {
        /// <summary>
        /// Flush data
        /// </summary>
        void Flush();
    }
}
