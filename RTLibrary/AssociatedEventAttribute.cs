using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLibrary
{
    [AttributeUsage(AttributeTargets.Delegate | AttributeTargets.Method)]
    public sealed class AssociatedEventAttribute : Attribute
    {
        /// <summary>
        /// Name of Event that this RTEvent is associated
        /// </summary>
        public string EventName;

        /// <summary>
        /// Type of RTEvent delegate that this routine embodies
        /// </summary>
        public AssociatedEventType EventType;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Event">Event name</param>
        /// <param name="Type">Delegate type</param>
        public AssociatedEventAttribute(string Event, AssociatedEventType Type = AssociatedEventType.Unspecified)
        {
            EventName = Event;
            this.EventType = Type;
        }
    }


    /// <summary>
    /// Enumerates RTEvent delegate types
    /// </summary>
    public enum AssociatedEventType
    {
        /// <summary>
        /// Immediate clock delegate
        /// </summary>
        Immediate,
        /// <summary>
        /// Delayed or UI clock delegate
        /// </summary>
        Delayed_UI,
        /// <summary>
        /// Unspecified clock delegate
        /// </summary>
        Unspecified
    }

}
