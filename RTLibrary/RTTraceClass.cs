using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLibrary
{
    /// <summary>
    /// Class for managing tracing during the running of an RTExperiment.
    /// Only instantiated by the RTExperiment object; reference is available 
    /// through its property <code>Trace</code>. 
    /// </summary>
#if RTTrace || RTTraceUAId
    public class RTTraceClass
    {
        readonly Record[] recordLoop;
        int current = 0;
        readonly int NEntries;
        static readonly object _lock = new object();

        internal RTTraceClass(int nEntries)
        {
            NEntries = nEntries;
            recordLoop = new Record[nEntries];
        }

        internal void AddRec(string desc, ulong RTTicks)
        {
            lock (_lock)
            {
                recordLoop[current].SWTicks = RTClock.stopwatch.ElapsedTicks;
                recordLoop[current].RTClockTime = RTTicks;
                recordLoop[current].Description = desc;
                if (++current >= NEntries) current = 0;
            }
        }

    #region Public interface
        //NOTE: THIS ROUTINE MAY BE RUN ON UI THREAD

        /// <summary>
        /// Write an informational message to the trace queue including
        /// elapsed times (MMTimer- and system clock-based)
        /// </summary>
        /// <param name="message">Message to be written</param>
        public void Write(string message)
        {
            ulong t = RTClock.CurrentRTIndex; //avoid potential interlock
            lock (_lock)
            {
                recordLoop[current].SWTicks = RTClock.stopwatch.ElapsedTicks;
                recordLoop[current].RTClockTime = t;
                recordLoop[current].Description = message;
                if (++current >= NEntries) current = 0;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                current = 0;
            }
        }

        /// <summary>
        /// Write out all recorded entries since last Display
        /// </summary>
        public void Display()
        {
            int start = 0;
            //check if we've overfilled queue
            int check = (current + 1) % NEntries;
            if (recordLoop[check].Description != null) start = check;
            ulong CT0 = recordLoop[start].RTClockTime;
            long SW0 = recordLoop[start].SWTicks;
            Trace.WriteLine("");
            for (int r = start;
                recordLoop[r].Description != null;
                )
            {
                Record rec = recordLoop[r];
                ulong CT1 = rec.RTClockTime - CT0;
                if (rec.SWTicks == -1) //special entry, no stopwatch ticks
                    Trace.WriteLine(
                        $"{String.Empty,10} {CT1,6:0} {rec.RTClockTime,8:0} {rec.Description}");
                else
                {
                    double SW1 = (rec.SWTicks - SW0) * RTClock.SWfactor;
                    Trace.WriteLine(
                        $"{SW1,10:0.000} {CT1,6:0} {rec.SWTicks * RTClock.SWfactor,12:0.000} {rec.RTClockTime,8:0} {rec.Description}");
                }
                r = (++r) % NEntries;
                if (r == start) break;
            }
            //empty queue
            current = 0;
            for (int i = 0; i < NEntries; i++) recordLoop[i].Description = null;
        }
    }
    #endregion

    struct Record
    {
        internal string Description;
        internal ulong RTClockTime;
        internal long SWTicks;
    }
#endif
}