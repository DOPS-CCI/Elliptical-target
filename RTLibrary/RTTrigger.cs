using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RTLibrary
{
    /// <summary>
    /// The trigger for a particular RTAwatEvent
    /// </summary>
    public class RTTrigger
    {
        internal UIElement element;
        internal RoutedEvent eventType;
        internal RoutedEventHandler handler;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element">UIElement on which the event occurs</param>
        /// <param name="type">Type of Routed Event</param>
        /// <param name="handler">Event handler delegate; if null, then default Trigger is used</param>
        public RTTrigger(UIElement element, RoutedEvent type, RoutedEventHandler handler = null)
        {
            this.element = element;
            eventType = type;
            this.handler = handler;
        }

        /// <summary>
        /// Default constructor: defines MouseDown trigger anywhere on SubjectScreen with default handler
        /// </summary>
        public RTTrigger()
        {
            element = RTDisplays.SubjectScreen;
            if (element == null)
                throw new RTException("In RTTrigger() COTR: SubjectScreen object not yet defined.");
            eventType = UIElement.MouseDownEvent;
        }

        internal void removeHandler()
        {
            element.RemoveHandler(eventType, handler);
        }

        internal void addHandler()
        {
            element.AddHandler(eventType, handler);
        }
    }
}
