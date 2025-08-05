using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using CCILibrary;
using Event;
using EventDictionary;
namespace RTLibrary
{
    public class RTTrial
    {
        //NB: this must be protected to limit access to list by the "general public"
        //This way access to the Experiment level List is public and permitted at any time,
        //because the Adds only occcur after a trial is over. See endTrialUI below
        internal readonly List<RTEventGV> TrialEventFileList = new List<RTEventGV>();

        /// <summary>
        ///Number of completed RWNL Events
        /// </summary>
        public int EventCount { get { return TrialEventFileList.Count; } }

        internal readonly RTExperiment experiment;
        RTEvent FirstEvent;

        internal int _currentTrialNumber = 0;
        /// <summary>
        /// Number of currently executing trial
        /// </summary>
        public int CurrentTrialNumber { get { return _currentTrialNumber; } }

        internal int _completedTrialNumber = 0;
        /// <summary>
        /// Last completed trial number
        /// </summary>
        public int CompletedTrialNumber { get { return _completedTrialNumber; } }

        /// <summary>
        /// Delegate run at the end of the trial
        /// </summary>
        /// <param name="trial"></param>
        public delegate void TrialCleanupRoutine(RTTrial trial);
        internal readonly TrialCleanupRoutine TrialCleanup;

        /// <summary>
        /// Delegate to be executed if the trial is aborted
        /// </summary>
        /// <param name="reason"></param>
        public delegate void PostAbortRoutine(int reason); //Delegate to be executed if the trial is aborted
        internal readonly PostAbortRoutine PostAbortCleanup;

        /// <summary>
        /// COTR of RTTrial trial object, associating this trial with an experiment
        /// </summary>
        /// <param name="rte">Experiment that this trial type is part of</param>
        /// <param name="firstEvent">First RTEvent in the trial</param>
        /// <param name="cleanup">Cleanup to be performed at the end of each trial</param>
        /// <param name="abort">UI routine to be run in case of aborted trial</param>
        public RTTrial(RTExperiment rte, RTEvent firstEvent, TrialCleanupRoutine cleanup, PostAbortRoutine abort = null)
        {
            experiment = rte;
            FirstEvent = firstEvent;
            TrialCleanup = cleanup;
            PostAbortCleanup = abort;
        }

        readonly RTClock.beginTrialDelegate begin = RTClock.ScheduleBeginTrialEvent;
        /// <summary>
        /// Initiate a trial by scheduling first RTEvent after a delay
        /// </summary>
        /// <param name="delay">Time delay to first event in trail</param>
        public void Begin(uint? delay = null)
        {
            _currentTrialNumber = _completedTrialNumber + 1;
            if (delay != null)
                FirstEvent.Delay = (uint)delay;
            RTClock.clockDispatcher.BeginInvoke(DispatcherPriority.Send, begin, FirstEvent, this); //run RTClock.ScheduleBeginTrialEvent on Clock thread
        }

        /// <summary>
        /// For testing purposes; simulates the beginning of a trial
        /// </summary>
        /// <param name="delay">Nominal delay to first event</param>
        /// <returns>Simulation of first event</returns>
        public RTEvent SimulateBegin(uint delay = 0)
        {
            _currentTrialNumber = _completedTrialNumber + 1;
            FirstEvent.Delay = delay;
            RTClock.currentTrial = this;
            return FirstEvent.simulateEvent();
        }

        #region GV editing
        /// <summary>
        /// Get event record by index in trial
        /// </summary>
        /// <param name="index">index of </param>
        /// <returns></returns>
        public RTEventGV GetEventGVByIndex(int index)
        {
            int i = index + (index < 0 ? TrialEventFileList.Count : 0);
            return TrialEventFileList[i];
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public RTEventGV GetEventGVByName(string eventName)
        {
            return TrialEventFileList.Find(g => g.EDE.Name == eventName);
        }

        public void SetGV(string eventName, string gv, int val)
        {
            GetEventGVByName(eventName)[gv] = val;
        }

        public int GetGV(string eventName, string gv)
        {
            return GetEventGVByName(eventName)[gv];
        }

        public void SetGV(int index, string gv, int val)
        {
            int i = index + (index < 0 ? TrialEventFileList.Count : 0);
            TrialEventFileList[i][gv] = val;
        }

        public int GetGV(int index, string gv)
        {
            int i = index + (index < 0 ? TrialEventFileList.Count : 0);
            return TrialEventFileList[i][gv];
        }

        public void SetAllGVByName(string gvName, int val)
        {
            foreach (RTEventGV gv in TrialEventFileList)
                for (int i = 0; i < gv.GVs.Length; i++)
                    if (gv.GVName(i) == gvName)
                    {
                        gv[i] = val;
                        break;
                    }
        }
        #endregion

        /// <summary>
        /// Returns the RTEvent to be scheduled to mark the end of a trial
        /// </summary>
        /// <param name="delay">Delay until end of trial RTEvent occurs</param>
        /// <returns>RTEvent to mark end of trial</returns>
        /// <remarks>Generally this is invoked in the IM routine of the last "real" event in the trial</remarks>
        public RTEvent End(uint delay = 0)
        {
            return new RTEndTrialEvent(delay);
        }

        static RTClock.abortTrialDelegate abort = RTClock.ScheduleAbortTrialEvent;
        /// <summary>
        /// Abort this specific trial; if trial not current, ignores
        /// <param name="reason">Indicate reason of abort; passed to PostAbortCleanup routine</param>
        /// </summary>
        public RTEvent Abort(int reason)
        {
            RTClock.clockDispatcher.BeginInvoke(DispatcherPriority.Send, abort, new RTAbortTrialEvent(reason), this);
            return null; //signal Abort underway
        }

        /// <summary>
        /// Abort current trial in progress
        /// <param name="reason">Indicate reason of abort; passed to PostAbortCleanup routine</param>
        /// </summary>
        public static RTEvent AbortAny(int reason)
        {
            RTClock.clockDispatcher.BeginInvoke(DispatcherPriority.Send, abort, new RTAbortTrialEvent(reason), null);
            //null indicates no matching trial check
            return null; //signal Abort underway
        }

        #region private and internal
        internal void EnqueueEvent(RTEventGV gv)
        {
            TrialEventFileList.Add(gv);
        }

        internal void NullOutEventsAndTransfer()
        {
            if (TrialEventFileList.Count != 0)
            {
                EventDictionaryEntry nullEDE;
                if (!experiment.header.Events.ContainsKey("Null"))
                {
                    nullEDE = new EventDictionaryEntry
                    {
                        Description = "Null Event (auto-created)"
                    };
                    experiment.header.Events.Add("Null", nullEDE);
                }
                else
                    nullEDE = experiment.header.Events["Null"];
                foreach (RTEventGV gv in TrialEventFileList)
                {
                    gv.EDE = nullEDE;
                    gv.GVs = null;
                }
            }
            experiment.TransferEventsToExperiment(TrialEventFileList); //must do even if no Events to update trial number
            TrialEventFileList.Clear();
        }
        #endregion
    }
}
