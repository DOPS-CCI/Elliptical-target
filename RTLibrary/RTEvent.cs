using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CCILibrary;
using Event;
using EventDictionary;

namespace RTLibrary
{
    /// <summary>
    /// Objectifies a real-time Event which may be incorporated into a Trial
    /// </summary>
    public class RTEvent
    {
        /// <summary>
        /// Delegate that executes the delayed code in an RTEvent; often processes the user interface (UI)
        /// </summary>
        /// <param name="ev"></param>
        public delegate void UIRoutine(RTEventGV ev);

        /// <summary>
        /// Delegate that executes the immediate processing of an RTEvent; this is "time critical" processing
        /// </summary>
        /// <returns>Next RTEvent to be scheduled</returns>
        public delegate RTEvent ClockRoutine(); //Delegate to be executed on the clock thread -- an "immediate" action

        /// <summary>
        /// Time delay in RTClock units for scheduling this event
        /// </summary>
        public uint Delay { get; set; }

        /// <summary>
        /// RTClock unit time that this event is scheduled (or last scheduled, if not the current event)
        /// </summary>
        public ulong Time { get; internal set; } //Clock time this event is scheduled to occur

        /// <summary>
        /// RTClock unit time that this event last occured
        /// </summary>
        public ulong ClockIndex { get; internal set; } //Clock time that this event did occur
        public EventDictionaryEntry EDE { get; protected set; } //Dictionary entry for this Event or null if none

        private protected string _name;
        /// <summary>
        /// Name by which this RTEvent is known
        /// </summary>
        public string Name { get { return _name; } internal set { _name = value; } } //Name of the Event associated
                                                                                     //with this RTEvent; if this RTEvent doesn't create an RWNL Event, then it is set to "Unknown"
        internal UIRoutine uiRoutine; //UI routine
        internal ClockRoutine clockRoutine; //Clock/immediate routine

        //indicates if the RTEvent is actually an RTAwaitEvent
        internal bool IsAwaitEvent = false;


        /// <summary>
        /// Static RTEvent that never occurs (~7 weeks) -- used for infinite waits
        /// </summary>
        public readonly static RTEvent InfiniteWait = new RTEvent("InfiniteTimeout", delay: uint.MaxValue /*7 weeks!*/, immediate: NullIM, null);

        private protected RTEvent() { }

        /// <summary>
        /// CTOR for a real-time Event, to be placed in next Event slot in the clocking routine
        /// </summary>
        /// <param name="EDE">EventDicationaryEntry for the actual Event; if null, then no Evednt will be created, but actions will be performed</param>
        /// <param name="delay">RTEvent scheduled to occur offset after the last RTEvent</param>
        /// <param name="immediate">Immediate action, performed synchronousy on clock thread</param>
        /// <param name="gui">"Delayed action, performed asynchronously on the UI thread</param>
        public RTEvent(EventDictionaryEntry EDE, uint delay, ClockRoutine immediate, UIRoutine gui)
        {
            clockRoutine = immediate ??
                throw new ArgumentException("In RTEvent.CTOR: immediate action cannot be null");
            this.EDE = EDE;
            if (EDE != null) _name = EDE.Name;
            else _name = "Unknown";
            Delay = delay;
            uiRoutine = gui;
        }

        /// <summary>
        /// CTOR for a real-time Event which does not have an associated RWNL Event creation when it occurs
        /// </summary>
        /// <param name="name">The name of this Event -- used in tracing</param>
        /// <param name="delay">Offset to the time this RTEvent is to occur after last RTEVent</param>
        /// <param name="immediate">The immediate action delegate</param>
        /// <param name="gui">The delayed action delegate</param>
        public RTEvent(string name, uint delay, ClockRoutine immediate, UIRoutine gui)
        {
            _name = name;
            clockRoutine = immediate ??
                throw new ArgumentException("In RTEvent.CTOR: immediate action cannot be null");
            Delay = delay;
            uiRoutine = gui;
        }

        /// <summary>
        /// Overrides object.ToString
        /// </summary>
        /// <returns>Name of RTEvent</returns>
        public override string ToString() => _name;

        private static RTEvent NullIM()
        {
            throw new RTException("In RTEvent.NullIM: this should never happen! 7 weeks sure seems like forever.");
        }

        public RTEvent simulateEvent()
        {
            Debug.WriteLine($"Delay = {Delay:0}; Event = {_name}");
            RTEvent nextEvent = clockRoutine();
            RTEventGV gv;
            if (EDE != null)
            {
                gv = new RTEventGV(this, RTClock.CurrentRTIndex);
                RTClock.CurrentTrial.EnqueueEvent(gv);
            }
            else
            {
                gv = new RTEventGV(this, RTClock.CurrentRTIndex);
            }
            uiRoutine?.Invoke(gv);
            return nextEvent;
        }
    }

    #region RTEventGV class
    /// <summary>
    /// Encapsulates information often needed in event UI routine,
    /// including space to store the associated GVs; also includes 
    /// timing information to ultimately create an Event file record;
    /// only created if there is an associated RWNL Event
    /// </summary>
    public class RTEventGV
    {
        /// <summary>
        /// RTEvent that is associated with this object
        /// </summary>
        public RTEvent ActualEvent { get; private set; }
        internal DateTime Time; //for use in creating Event file record
        internal int[] GVs; //storage fr GV values

        /// <summary>
        /// Returns number of GVs in this record
        /// </summary>
        public int Length { get { return GVs != null ? GVs.Length : 0; } }

        /// <summary>
        /// MMTimer-based time in designated "clicks", ususally milliseconds
        /// </summary>
        public ulong ClockTime { get; private set; }

        /// <summary>
        /// Name of the RWNL Event from the EDE
        /// </summary>
        public string EventName { get { return ActualEvent.Name; } }

        /// <summary>
        /// EDE of the RWNL Event associated with this object; cannot be
        /// null as RTREventGV only created after RTEvent with associated
        /// RWNL Event
        /// </summary>
        public EventDictionaryEntry EDE
        {
            get;
            internal set;
        }

        /// <summary>
        /// Get/set GV value based on zero-based index
        /// </summary>
        /// <param name="i">Index of GV</param>
        /// <returns>Returns reference to GV i to set or value of GV i</returns>
        public int this[int i]
        {
            get
            {
                return GVs[i];
            }
            set
            {
                GVs[i] = value;
            }
        }

        /// <summary>
        /// Get/set GV value based on GV name
        /// </summary>
        /// <param name="gvName">Name of GV to get or set</param>
        /// <returns>Returns reference to named GV to set or value of named GV</returns>
        public int this[string gvName]
        {
            get
            {
                try
                {
                    return GVs[EDE.GroupVars.FindIndex(g => g.Name == gvName)];
                }
                catch (NullReferenceException)
                {
                    throw new ArgumentException($"In RTEventGV[string].get: unable to find GV named {gvName}");
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"In RTEventGV[string].get: unable to find GV named {gvName}");
                }
            }
            set
            {
                try
                {
                    GVs[EDE.GroupVars.FindIndex(g => g.Name == gvName)] = value;
                }
                catch (NullReferenceException)
                {
                    throw new ArgumentException($"In RTEventGV[string].set: unable to find GV named {gvName}");
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentException($"In RTEventGV[string].set: unable to find GV named {gvName}");
                }
            }
        }

        /// <summary>
        /// Name of GV
        /// </summary>
        /// <param name="i">Index of the GV</param>
        /// <returns>Name of GV i</returns>
        public string GVName(int i)
        {
            return EDE.GroupVars[i].Name;
        }

        internal RTEventGV(RTEvent currentEvent, ulong actualTime)
        {
            Time = DateTime.Now;
            ActualEvent = currentEvent;
            ClockTime = actualTime;
            EDE = currentEvent.EDE;
            if (EDE?.GroupVars != null)
                GVs = new int[currentEvent.EDE.GroupVars.Count];
        }
    }
    #endregion
}
