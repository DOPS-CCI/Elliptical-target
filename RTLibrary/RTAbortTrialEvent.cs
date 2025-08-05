using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLibrary
{
    public class RTAbortTrialEvent : RTLibrary.RTEvent
    {
        /// <summary>
        /// Used to indicate the cause of the abort
        /// </summary>
        public int Reason { get; }

        private RTTrial AbortedTrial;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reason">May be used to indicate reason for abort; default = 0</param>
        public RTAbortTrialEvent(int reason = 0)
        {
            Reason = reason;
            Delay = 0; //all aborts are immediate to avoid "dueling aborts"
            clockRoutine = abortTrialIM;
            uiRoutine = cleanupAbortedTrialUI;
            _name = "AbortTrial";
        }

        private RTEvent abortTrialIM()
        {
            //set currentTrial to null and remember its former value for UI routine
            AbortedTrial = RTClock.currentTrial;
            AbortedTrial._currentTrialNumber = AbortedTrial._completedTrialNumber; //reset to last completed
            RTClock.currentTrial = null;
            return null; //to indicate trial is over
        }

        private void cleanupAbortedTrialUI(RTEventGV _)
        {
            AbortedTrial.NullOutEventsAndTransfer();
            AbortedTrial.PostAbortCleanup?.Invoke(Reason); //perform any abort cleanup; may depend on reason
#if RTTrace || RTTraceUAId
            RTClock.trace.Display();
#endif
        }
    }
}
