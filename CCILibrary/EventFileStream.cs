﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Collections;
using Event;
using EventDictionary;
using GroupVarDictionary;

namespace EventFile
{

    /// <summary>
    /// Class for reading Event files
    ///     1. Create an EventFileReader based on an input stream
    ///     2. Read individual records using <code>readEvent()</code>, until returns <code>null</code> for EOF
    ///     3. Or, use <code>foreach</code> iterator
    /// </summary>
    public sealed class EventFileReader : IDisposable, IEnumerable<InputEvent> {
        XmlReader xr;
        string nameSpace;

        /// <summary>
        /// General constructor for Event file reader:
        ///     positions stream to read first Event element in the file
        /// </summary>
        /// <param name="str">Event file stream to read</param>
        public EventFileReader(Stream str)
        {
            try {
                if (!str.CanRead) throw new IOException("Unable to read from stream");
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreWhitespace = true;
                settings.IgnoreComments = true;
                settings.IgnoreProcessingInstructions = true;
                xr = XmlReader.Create(str, settings);
                if (xr.MoveToContent() != XmlNodeType.Element) throw new XmlException("Not a valid Event file");
                nameSpace = xr.NamespaceURI; 
                xr.ReadStartElement("Events", nameSpace);
            } catch(Exception x) {
                throw new Exception("EventFileReader: " + x.Message);
            }
        }

        /// <summary>
        /// Reads single Event file record
        /// </summary>
        /// <returns> Returns InputRecord if record is valid; else returns null</returns>
        public InputEvent readEvent()
        {
            if (xr.Name != "Event") return null; // signal EOF?
            try
            {
                InputEvent ev = EventFactory.Instance().CreateInputEvent(xr["Name"]);
                xr.ReadStartElement("Event", nameSpace);
                xr.ReadStartElement("Index", nameSpace);
                ev.m_index = (uint)xr.ReadContentAsInt();
                xr.ReadEndElement(/* Index */);
                xr.ReadStartElement("GrayCode", nameSpace);
                ev.m_gc = (uint)xr.ReadContentAsInt();
                xr.ReadEndElement(/* GrayCode */);
                if (xr.Name == "ClockTime") //usual form for Events
                {
                    string t = xr.ReadElementString("ClockTime", nameSpace);
                    if (t.Contains(".")) //preferred, with decimal point
                        ev.m_time = Convert.ToDouble(t);
                    else //deprecated, no decimal point
                    {
                        int l = Math.Max(t.Length - 7, 1); //count in 100nsec intervals assumed
                        ev.m_time = Convert.ToDouble(t.Substring(0, l) + "." + t.Substring(l));
                    }
                    if (xr.Name == "EventTime") // optional; present with absolute timing, optional with relative
                        ev._eventTime = xr.ReadElementString("EventTime", nameSpace);
                }
                else if (xr.Name == "Time") //Time construct -- deprecated as of 11 Feb 2013
                {
                    string t = xr.ReadElementString(/* Time */); //not in nameSpace
                    if (t.Contains(".")) //new style
                        ev.m_time = System.Convert.ToDouble(t);
                    else //old, old style -- very deprecated!
                    {
                        int l = Math.Min(t.Length, 11);
                        ev.m_time = System.Convert.ToDouble(t.Substring(0, l) + "." + t.Substring(l));
                    }
                }
                bool isEmpty = xr.IsEmptyElement; // Use this to handle <GroupVars /> construct
                xr.ReadStartElement("GroupVars", nameSpace);
                if (!isEmpty)
                {
                    //the GVs in the EVT file should match the GVs defined in the HDR file in number, name, and order
                    int j = 0;
                    if(ev.EDE.GroupVars!=null)
                        foreach (GVEntry gve in ev.EDE.GroupVars)
                        {
                            if (xr.IsStartElement("GV", nameSpace))
                            {
                                string GVName = xr["Name"];
                                if (GVName == null)
                                    throw new Exception("GV " + gve.Name + " not found in Event " + ev.Name);
                                if (gve.Name != GVName)
                                    throw new Exception("Found GV named " + GVName + " in Event file which does not match Event " + ev.Name + " definition for GV " + gve.Name);
                                ev.GVValue[j++] = xr.ReadElementContentAsString("GV", nameSpace);
                            }
                            else
                                throw new Exception("Missing GV named " + gve.Name + " in Event file record for Event " + ev.Name);
                        }
                    xr.ReadEndElement(/* GroupVars */);
                }
                else
                {
                    if (ev.EDE.GroupVars != null && ev.EDE.GroupVars.Count != 0)
                        throw new Exception("No GVs found for Event " + ev.Name + " which defines " + ev.EDE.GroupVars.Count.ToString("0") + " GVs in the Header file");
                }
                if (xr.Name == "Ancillary")
                {
                    xr.ReadStartElement("Ancillary", nameSpace);
                    //do it this way to assure correct number of bytes
                    byte[] anc = Convert.FromBase64String(xr.ReadContentAsString());
                    if (anc.Length != ev.ancillary.Length)
                        throw new Exception("Ancillary data size mismatch in Event " + ev.Name);
                    for (int i = 0; i < ev.ancillary.Length; i++) ev.ancillary[i] = anc[i];
                    xr.ReadEndElement(/* Ancillary */);
                }
                else if (ev.ancillary != null && ev.ancillary.Length > 0)
                    throw new Exception("Ancillary data expected in Event " + ev.Name);
                xr.ReadEndElement(/* Event */);
                return ev;
            }
            catch (Exception e)
            {
                throw new Exception("InputEvent.readEvent: Error processing " + xr.NodeType.ToString() +
                    " named " + xr.Name + ": " + e.Message);
            }
        }

        public void Dispose() {
            this.Close();
        }

        public void Close() {
            xr.Close();
        }

        public IEnumerator<InputEvent> GetEnumerator()
        {
            return new EventFileIterator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EventFileIterator(this);
        }

    }

    /// <summary>
    /// Class EventFileIterator allows iteration through an EventFile
    ///     Generally used by <code>foreach</code> over an EventFileReader
    /// </summary>
    internal class EventFileIterator : IEnumerator<InputEvent>, IDisposable
    {
        private InputEvent current;
        private EventFileReader efr;

        internal EventFileIterator(EventFileReader e) { efr = e; }

        InputEvent IEnumerator<InputEvent>.Current
        {
            get { return current; }
        }

        void IDisposable.Dispose() { efr.Dispose(); }

        object IEnumerator.Current
        {
            get { return (Object)current; }
        }

        bool IEnumerator.MoveNext()
        {
            current = efr.readEvent();
            return current != null;
        }

        void IEnumerator.Reset()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Class for writing an Event file
    ///     1. Instantiate an <code>EventFileWriter</code> based on an output stream
    ///     2. Write events using <code>writeRecord</code> with an <code>OutputEvent</code> argument
    ///     3. <code>Close()</code> the <code>EventFileWriter</code> and the stream that it is based on
    /// </summary>
    public sealed class EventFileWriter : IDisposable
    {
        XmlWriter xw;
        bool strictGV;
        const string nameSpace = "http://www.zoomlenz.net/Event";

        /// <summary>
        /// General constructor for writing an Event file:
        ///     positions file to write first record
        /// </summary>
        /// <param name="stream">Stream to write OutputEvents to</param>
        /// <param name="strictGV">Enforce GV >= 0 rule</param>
        public EventFileWriter(Stream stream, bool strictGV = true)
        {
            try
            {
                if (!stream.CanWrite) throw new IOException("Unable to write to stream");
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = System.Text.Encoding.UTF8;
                xw = XmlWriter.Create(stream, settings);
                xw.WriteStartDocument();
                xw.WriteStartElement("Events", nameSpace);
                xw.WriteAttributeString("xmlns", nameSpace);
                xw.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                xw.WriteAttributeString("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance",
                    "http://www.zoomlenz.net http://www.zoomlenz.net/xml/Event.xsd");
            }
            catch (XmlException x)
            {
                throw new XmlException("EventFileWriter: " + x.Message);
            }
            this.strictGV = strictGV;
        }

        /// <summary>
        /// Writes single Event file record
        /// </summary>
        /// <param name="ev"><code>OuputEvent</code> to be written</param>
        public void writeRecord(OutputEvent ev)
        {
            try
            {
                xw.WriteStartElement("Event", nameSpace);
                xw.WriteAttributeString("Name", ev.Name);
                xw.WriteElementString("Index", nameSpace, ev.Index.ToString("0"));
                xw.WriteElementString("GrayCode", nameSpace, ev.GC.ToString("0"));
                xw.WriteStartElement("ClockTime", nameSpace);
                if (ev.HasRelativeTime) // relative, BDF-based clock
                {
                    xw.WriteString(ev.Time.ToString("0.0000000"));
                    xw.WriteEndElement(/* ClockTime */);
                    if (ev._eventTime != null) //string may be present, if the Event was created from an Absolute Event
                        xw.WriteElementString("EventTime", nameSpace, ev._eventTime);
                }
                else // Absolute clock
                {
                    xw.WriteString(ev.Time.ToString("00000000000.0000000"));
                    xw.WriteEndElement(/* ClockTime */);

                    // Absolute clocks always record EventTime in readable form
                    DateTime t = new DateTime((long)(ev.Time * 1E7));
                    if (t.Year < 1000) t = t.AddYears(1600); //convert to 0 year basis from 1600 basis
                    xw.WriteElementString("EventTime", nameSpace, t.ToString("d MMM yyyy HH:mm:ss.fffFF")); //enforce standard format
                }
                xw.WriteStartElement("GroupVars", nameSpace);
                if (ev.GVValue != null)
                    for (int j = 0; j < ev.GVValue.Length; j++)
                    {
                        xw.WriteStartElement("GV", nameSpace);
                        xw.WriteAttributeString("Name", ev.EDE.GroupVars[j].Name); //assure it matches HDR entry
                        string s = ev.GVValue[j];
                        if (strictGV && ev.EDE.GroupVars[j].ConvertGVValueStringToInteger(s) < 0)
                            throw new Exception($"In EventFileWriter.WriteRecord: Attempt to write invalid value of \"{s}\" to GV {ev.EDE.GroupVars[j].Name}");
                        xw.WriteValue(s);
                        xw.WriteEndElement(/* GV */);
                    }
                xw.WriteEndElement(/* GroupVars */);
                if (ev.ancillary != null && ev.ancillary.Length > 0)
                {
                    xw.WriteStartElement("Ancillary", nameSpace);
                    xw.WriteBase64(ev.ancillary, 0, ev.ancillary.Length);
                    xw.WriteEndElement(/* Ancillary */);
                }
                xw.WriteEndElement(/* Event */);
            }
            catch (Exception e)
            {
                throw new Exception("EventFileWriter.writeRecord: " + e.Message);
            }
        }

        public void Dispose() {
            this.Close();
        }

        public void Close() {
            xw.WriteEndDocument();
            xw.Close();
        }
    }

}