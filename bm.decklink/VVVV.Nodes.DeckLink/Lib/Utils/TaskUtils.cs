using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink.Utils
{
    /// <summary>
    /// Simple static class to run routines in a synchornous way, but using a waitable async task.
    /// This is used in order to use a MTA thread for decklink object creation, if creating decklink objects using
    /// 4v main thread, they are set as STA and access is then restricted to that thread
    /// </summary>
    public static class TaskUtils
    {
        /// <summary>
        /// Runs a delegate and waits for completion.
        /// Throws exception if any occures
        /// </summary>
        /// <param name="action">Action to run</param>
        public static void RunSync(Action action)
        {
            Task t = Task.Run(action);
            Task.WaitAll(t);            
        }

        /// <summary>
        /// Runs a delegate and waits for completion.
        /// Silently ignores exception
        /// </summary>
        /// <param name="action">Action to run</param>
        public static void RunSyncAndIgnoreException(Action action)
        {
            try
            {
                RunSync(action);
            }
            catch
            {

            }
        }
    }
}
