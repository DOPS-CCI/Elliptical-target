using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using EventDictionary;

namespace RTLibrary
{
    /// <summary>
    /// Class that embodies an awaited event; this is the event that is
    /// triggered by some real-world event, such as a mouse click or key press.
    /// Initially a "timeout" event is scheduled and then overridden if the
    /// triggering event occurs.
    /// </summary>
    public class RTAwaitEvent : RTEvent
    {

        internal RTEvent timeout;

        internal RTTrigger trigger;

        internal static uint nextUniqueID = 1;

        internal uint UniqueID { get; private set; }

        /// <summary>
        /// Object that triggered this awaited RTEvent
        /// </summary>
        public object TriggerSource { get; private set; }

        /// <summary>
        /// Arguments to the event delegate that triggered this awaited RTEvent
        /// </summary>
        public EventArgs TriggerArgs { get; private set; }

        /// <summary>
        /// COTR for Awaited Event
        /// </summary>
        /// <param name="awaitedEvent">Event to occur at Trigger</param>
        /// <param name="trigger">Trigger for the awaited event; if null use default Trigger</param>
        /// <param name="timeoutEvent">Event that occurs if wait times out; if null, wait is infinite</param>
        /// <remarks>Delay between Trigger and Event is always zero</remarks>
        /// <remarks>Timeout RTEvent delay is the maximum wait period before time out</remarks>
        public RTAwaitEvent(RTEvent awaitedEvent, RTTrigger trigger = null, RTEvent timeoutEvent = null)
        {
            this.EDE = awaitedEvent.EDE;
            this.Name = awaitedEvent.Name;
            this.clockRoutine = awaitedEvent.clockRoutine;
            this.uiRoutine = awaitedEvent.uiRoutine;
            this.UniqueID = 1;
            this.Delay = 0;

            if (trigger == null) this.trigger = new RTTrigger();
            else this.trigger = trigger;
            if (this.trigger.handler == null)
                this.trigger.handler = Trigger; //default handler for trigger

            if (timeoutEvent == null)
                timeout = InfiniteWait;
            else
            {
                timeout = timeoutEvent;
                if (string.IsNullOrEmpty(timeout.Name))
                    timeout.Name = "Timeout";
                //Wrap ClockRoutine inside routine to disable current await handler
                timeoutIMRoutine = timeout.clockRoutine;
                timeout.clockRoutine = timeoutIM;
            }

            IsAwaitEvent = true;
        }

        RTEvent.ClockRoutine timeoutIMRoutine;
        RTEvent timeoutIM()
        {
            trigger.removeHandler();
            return timeoutIMRoutine();
        }

        RTClock.awaitDelegate scheduleTriggeredEvent = RTClock.ScheduleTriggeredEvent;
        /// <summary>
        /// Trigger the awaited event; call this in triggering event handler to dispatch RTEvent at next
        /// RTClock tick, thus cancelling timeout Event
        /// </summary>
        /// <param name="sender">source of trigger</param>
        /// <param name="e">arguments from triggering event</param>
        public void Trigger(object sender = null, EventArgs e = null)
        {
            trigger.removeHandler();
            TriggerSource = sender;
            TriggerArgs = e;
            //Use high priority to assure that all "simultaneous" Triggers occur before the RTClock event
            //that will be scheduled by one of the Triggers; otherwise it seems that the MMTimer.OnTick
            //event has priority and may occur before all the Triggers have cleared
            RTClock.clockDispatcher.BeginInvoke(DispatcherPriority.Send, scheduleTriggeredEvent, this);
        }

        //NOTE: THIS IS RUN ON CLOCK THREAD IN OnTick
        internal RTEvent ScheduleTimeOut()
        {
            UniqueID = ++nextUniqueID;
            RTClock.uniqueAwaitID = UniqueID; //remember uniqueID
            trigger.addHandler();
            return timeout;
        }
    }
}
