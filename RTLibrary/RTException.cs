using System;
#if !RTTrace
using System.Windows;
#endif
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLibrary
{
    public class RTException : Exception
    {
        /// <summary>
        /// Constructor; if RTTrace is defined also places message in trace message queue and flushes queue
        /// </summary>
        /// <param name="message">Error message to be displayed</param>
        public RTException(string message)
            : base(message)
        {
#if RTTrace || RTTraceUAId
            RTClock.ExternalTrace($"***** RT exception: {message} *****");
            RTClock.trace.Display();
#endif
        }
    }
}
