using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLibrary
{
    class RTEndTrialEvent : RTEvent
    {
        private RTTrial EndingTrial;
        /// <summary>
        /// Normal end-of-trial Event; invokes TrialCleanup in UI to allow processing of GVs, etc.
        /// </summary>
        /// <param name="delay"></param>
        internal RTEndTrialEvent(uint delay = 0)
        {

            Delay = delay;
            clockRoutine = endTrialIM;
            uiRoutine = endTrialUI;
            _name = "EndTrial";
        }

        private RTEvent endTrialIM()
        {
            EndingTrial = RTClock.CurrentTrial; //remember which Trial is ending
            EndingTrial._completedTrialNumber = EndingTrial._currentTrialNumber;
            RTClock.currentTrial = null; //mark termination of current trial
            return null; //indicate no further events to schedule in this trial
        }

        private void endTrialUI(RTEventGV _)
        {
            EndingTrial.experiment.TransferEventsToExperiment(EndingTrial.TrialEventFileList);
            EndingTrial.TrialCleanup?.Invoke(EndingTrial);
            EndingTrial.TrialEventFileList.Clear(); //don't clear until Cleanup has a chance at the Event records
#if RTTrace || RTTraceUAId
            RTClock.trace.Display();
#endif
        }
    }
}
