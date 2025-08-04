using System;
using System.Threading;
using System.Windows.Threading;
using CCILibrary;
#if DIO
using MccDaq;
#endif
#if RTTrace || RTTraceUAId
using System.Diagnostics;
#endif

namespace RTLibrary
{
    public static class RTClock
    {
        const int RTTraceHistoryLength = 100;

        static RTTimer timer; //Main clocking device, based on Windows MultiMedia Timer
        static readonly object _lockEvent = new object(); //object used to lock pendingEvent and/or TimeIndex, CurrentTrial
        static ulong TimeIndex = 0; //Number of "ticks" since clock started

        internal volatile static RTTrial currentTrial = null; //current RTTrial being executed
        volatile static RTEvent pendingEvent = null; //the next RT Event to be performed
        private static RTEvent currentEvent; //RTEvent that is currently executing or last used to create Event

        private static Dispatcher main; //UI thread dispatcher that runs the user interface event queue
        internal static Dispatcher clockDispatcher { get; private set; }

        static internal GCFactory gc; //Gray code factory to create consecutive Gray codes for OutputEvents
#if RTTrace || RTTraceUAId
        static double SWClockRate = 1000.2500D;// 1000.0295D; //RTClock msec per Stopwatch sec
        static internal readonly Stopwatch stopwatch = new Stopwatch(); //High resolution timer
        static internal double SWfactor = SWClockRate / Stopwatch.Frequency; //microsec per Stopwatch.Tick
        static internal RTTraceClass trace = new RTTraceClass(RTTraceHistoryLength);
        static private double timingThreshold = 20D;
#endif

        #region Public interface
        /// <summary>
        /// Currently active trial; beware however, that this value may change asychronously after value obtained
        /// </summary>
        public static RTTrial CurrentTrial
        {
            get
            {
                lock (_lockEvent)
                    return currentTrial;
            }
        }

        /// <summary>
        /// Number of RTClock intervals (usually milliseconds) since the
        /// RTClock started, based on the Windows Multimedia Timer (MMTimer)
        /// </summary>
        public static ulong CurrentRTIndex
        {
            get
            {
                lock (_lockEvent)
                    return TimeIndex;
            }
        }

#if RTTrace || RTTraceUAId
        /// <summary>
        /// Elapsed time in microseconds since RTClock started, based on the
        /// system clock
        /// </summary>
        public static double CurrentCPUTime
        {
            get
            {
                return SWfactor * stopwatch.ElapsedTicks;
            }
        }

        static double baseline; //in msec
        /// <summary>
        /// Calculate correction factor to calibrate MMTimer clock against Stopwatch timer
        /// </summary>
        /// <param name="baselineLength">Length of timing baseline in seconds</param>
        public static void StandardizeClocks(int baselineLength)
        {
            int period = RTTimer.Capabilities.periodMax;
            if (baselineLength > 0 && baselineLength * 1000 < period)
                period = baselineLength * 1000; //in msec
            else
                baselineLength = period / 1000;
            baseline = baselineLength;
            timer = new RTTimer
            {
                Mode = RTTimerMode.OneShot,
                Period = period
            };
            timer.Tick += OnEndBaseline;
            stopwatch.Reset();
            timer.Start();
            stopwatch.Start();
            Thread.Sleep(baselineLength * 1001); //go quiescent
        }

        static void OnEndBaseline(object sender, EventArgs e) //at end of baseline period
        {
            stopwatch.Stop(); //contains number of stopwatch ticks in the baseline period
            RTClock.SWClockRate = 1000D * stopwatch.ElapsedTicks / (Stopwatch.Frequency * baseline); // = stopwatch msec per MMTimer sec
            RTClock.SWfactor = 1000D * baseline / stopwatch.ElapsedTicks; // = MMTimer msec per stopwatch tick
        }
#endif
        #endregion

        internal static void Start(int period = 1, int status = 16)
        {

#if RTTrace || RTTraceUAId
            SWfactor /= (double)period;
#endif
            if (status < 8 || status > 16) throw new RTException($"In RTClock.Start: invalid number of Status bits = {status}");
            gc = new GCFactory(status);
            main = Dispatcher.CurrentDispatcher; //this is run on UI thread
            timer = new RTTimer
            {
                Mode = RTTimerMode.Periodic,
                Period = period
            };
            timer.Started += OnStart;
            timer.Start();
#if RTTrace || RTTraceUAId
            stopwatch.Start();
            //Synch the clocks
            TimeIndex = (ulong)Math.Round(SWfactor * stopwatch.ElapsedTicks);
#endif
            timer.Tick += OnTick; //start clock here in case you did synch above
            // Clear out DIO 0 to synchronize with trials
            WriteStatusBytes(0);
#if RTTrace || RTTraceUAId
            trace.Display();
#endif
        }

        internal static void Stop()
        {
            timer.Stop();
#if RTTrace || RTTraceUAId
            trace.Write("RTClock stopped.");
            trace.Display();
#endif
        }

#if RTTrace || RTTraceUAId
        internal static void ExternalTrace(string desc)
        {
            ulong t;
            lock (_lockEvent) t = TimeIndex;

            trace.AddRec(desc, t);
        }
#endif

        internal delegate void awaitDelegate(RTAwaitEvent ev);
        internal static uint uniqueAwaitID = 0;
        //NOTE: THIS IS RUN ON CLOCK THREAD DISPATCHER
        /// <summary>
        /// Insert new RTEvent to execute at next clock tick in place of currently pending Event,
        /// thus triggering an awaited event
        /// </summary>
        /// <param name="ev">RTAwaitEvent to be realized</param>
        internal static void ScheduleTriggeredEvent(RTAwaitEvent ev)
        {
            //make sure pending event matches triggering event
            if (ev.UniqueID == uniqueAwaitID) 
            {
#if RTTraceUAId
                uint uaid = uniqueAwaitID;
#endif
                ev.Time = 0;
                pendingEvent = ev; //substitute awaited/triggered event for timeout event
#if RTTraceUAId
                trace.AddRec($"{ev.Name} sched immed on UAId {uaid:0}", TimeIndex);
#elif RTTrace
                trace.AddRec($"{ev.Name} sched immed", TimeIndex);
#endif
            }
#if RTTraceUAId
            else
                trace.AddRec($"{ev.Name} UAId {ev.UniqueID:0} != {uniqueAwaitID}", TimeIndex);
#endif
        }

        internal delegate void beginTrialDelegate(RTEvent ev, RTTrial trial);
        //NOTE: THIS IS RUN ON CLOCK THREAD DISPATCHER
        internal static void ScheduleBeginTrialEvent(RTEvent ev, RTTrial trial)
        {
            //only time we should schedule with pendingEvent null
            if (pendingEvent != null) //no event should pending
                throw new RTException($"In RTClock.ScheduleBeginTrialEvent: attempt to begin trial with an active trial ongoing; event = {pendingEvent.Name}");
            currentTrial = trial;
            if (ev.IsAwaitEvent)
            {
                ev = ((RTAwaitEvent)ev).ScheduleTimeOut(); //substitute timeout
#if RTTraceUAId
                trace.AddRec($"Set UAId = {uniqueAwaitID} in ScheduleBeginTrialEvent", TimeIndex);
#endif
            }
            ev.Time = TimeIndex + ev.Delay;
            pendingEvent = ev;
#if RTTrace || RTTraceUAId
            trace.AddRec($"{ev.Name} sched @ {ev.Time}", TimeIndex);
#endif
        }

        internal delegate void abortTrialDelegate(RTEvent ev, RTTrial trial);
        //NOTE: THIS IS RUN ON CLOCK THREAD DISPATCHER
        internal static void ScheduleAbortTrialEvent(RTEvent abort, RTTrial trial)
        {
            if (currentTrial == null) return; //no trial to abort
            if (trial != null && currentTrial != trial) return;
            abort.Time = 0; //schedule at next clock tick
            if (pendingEvent == null) return; //nothing to abort
            pendingEvent = abort;
#if RTTrace || RTTraceUAId
            trace.AddRec("Sched Abort immed", TimeIndex);
#endif
        }

        private static void OnStart(object sender, EventArgs e)
        {
            clockDispatcher = Dispatcher.CurrentDispatcher; //Capture clock dispatcher
            timer.Started -= OnStart; //Only execute once
        }

        // ********** Here's the guts of the whole set up **********
#region OnTick
        static void OnTick(object sender, EventArgs e)
        {
            lock (_lockEvent)
            {
                TimeIndex++;
#if RTTrace || RTTraceUAId
                double timingerror = Math.Abs(TimeIndex - CurrentCPUTime);
                if (timingerror > timingThreshold)
                {
                    trace.AddRec($"***Timing offset = {timingerror:0.000}", TimeIndex);
                    timingThreshold += timingerror;
                }
#endif
                if (pendingEvent == null) return; //currently in "idle" mode between trials
                if (TimeIndex < pendingEvent.Time) return; //not yet time for nextEvent

                //So, it's time for next Event to occur
                currentEvent = pendingEvent; //upgrade to current event status
                currentEvent.ClockIndex = TimeIndex;
#if RTTrace || RTTraceUAId
                trace.AddRec($"{currentEvent.Name} event", TimeIndex);
#endif
            }

            RTEventGV gv;
            //At this point, the RT Event will occur
            //if this is a Status-connected Event -> create RWNL Event
            if (currentEvent.EDE != null)
            {
                uint newGC = gc.NextGC(); //grab the next GC
                WriteStatusBytes(newGC); //and write it to DIO

                //Create temporary Event record
                gv = new RTEventGV(currentEvent, TimeIndex);

                //There has to be a record for all Status marks:
                //so enqueue it on Trial Event list
                currentTrial.EnqueueEvent(gv);
            }
            else
            {
                gv = new RTEventGV(currentEvent, TimeIndex);
            }

            //Now execute the "immediate" or clockRoutine, if any
            //The clockRoutine executes any extreme time-critical actions and
            //returns the next Event to be performed
            RTEvent eventToBeScheduled;
            lock (_lockEvent)
            {
                //get next RTEvent
                eventToBeScheduled = currentEvent.clockRoutine(); //run inside lock, so method can safely
                                                                  //read critical variables, avoid interlock
                if (eventToBeScheduled != null) //then this is not an abort or end trial
                {
                    if (eventToBeScheduled.IsAwaitEvent)
                    { //starting await,
                      //so substitute timeout Event for Event to be triggered and set uniqueID
                        eventToBeScheduled = ((RTAwaitEvent)eventToBeScheduled).ScheduleTimeOut();
#if RTTraceUAId
                        trace.AddRec($"Set UAId = {uniqueAwaitID} in RTClock.OnTick", TimeIndex);
#endif
                    }
                    else
                        uniqueAwaitID = 0; //indicate not an await event
                    eventToBeScheduled.Time = TimeIndex + eventToBeScheduled.Delay;
#if RTTrace || RTTraceUAId
                    trace.AddRec($"{currentEvent.Name} IM: {eventToBeScheduled.Name} sched @ {eventToBeScheduled.Time:0}", TimeIndex);
#endif
                }
                else
                    uniqueAwaitID = 0; //reset at end trial/abort
                pendingEvent = eventToBeScheduled; //schedule it
            } //end lock

            //Finally dispatch the UI routine asynchonously to the UI thread.
            //This can be used for changes in the UI or to execute more time-
            //intensive code, perhaps requiring I/O or a long computation
            if (currentEvent.uiRoutine != null) //now we can update the UI if needed on main thread
            {
                main.BeginInvoke(currentEvent.uiRoutine, gv);
#if RTTrace || RTTraceUAId
                //Indicate that Event will run
                trace.AddRec($"{currentEvent.Name} UI", TimeIndex);
#endif
            }
        }
#endregion

        static void WriteStatusBytes(uint newGC)
        {
#if DIO
            RTExperiment.DAQBoard.DOut(DigitalPortType.FirstPortA, (ushort)(newGC & 0xFF));
            RTExperiment.DAQBoard.DOut(DigitalPortType.FirstPortB, (ushort)(newGC >> (gc.Status - 8)));
#endif
#if RTTrace || RTTraceUAId
            trace.AddRec($"***DIO=>{newGC:X4}", TimeIndex);
#endif
        }
    }
}
