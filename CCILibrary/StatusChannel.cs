using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CCILibrary;
using Event;

namespace BDFEDFFileStream
{
    /// <summary>
    /// Class for processing Status channel
    /// </summary>
    public class StatusChannel : IEnumerable<GCTime>
    {
        internal List<GCTime> GCList = new List<GCTime>();
        /// <summary>
        /// List of System Events found in Status channel
        /// </summary>
        public List<SystemEvent> SystemEvents = new List<SystemEvent>();

        /// <summary>
        /// Constructor for StatusChannel object
        /// </summary>
        /// <param name="bdf">BDF/EDF file containing Stus channel</param>
        /// <param name="maskBits">Number of Status bits</param>
        /// <param name="hasSystemEvents">If true, check for System Events</param>
        public StatusChannel(IBDFEDFFileReader bdf, int maskBits, bool hasSystemEvents)
        {
            uint mask = 0xFFFFFFFF >> (32 - maskBits);
            double sampleTime = bdf.SampleTime(bdf.NumberOfChannels - 1);
            uint[] status = bdf.readAllStatus(); //read in complete Status channel
            GrayCode gc = new GrayCode(maskBits);
            GrayCode comp = firstValue = new GrayCode(status[0] & mask, maskBits); //contains previous GrayCode
            byte lastSE = 0; //previous System Event value
            for (int i = 0; i < status.Length; i++) //scan Status; start at 0 to pick up initial SE setting as Event
            {
                uint v = status[i];
                if (hasSystemEvents)
                {
                    byte s = (byte)(v >> 16); //reduce to System Event byte
                    if (s != lastSE) //only note changes => found change
                    {
                        lastSE = s;
                        SystemEvents.Add(new SystemEvent(s, (double)i * sampleTime));
                    }
                }
                uint c = v & mask; //mask out status bits

                if (c == comp.Value) continue; //no shange, keep looking

                gc.Value = c;
                int n = gc - comp; //subtract Gray codes to find how many Events occur at this exact time
                if (n > 0)
                {

                    double t = (double)i * sampleTime;
                    for (int k = 0; k < n; k++) //create an entry for every Event at this time
                        GCList.Add(new GCTime(++comp, t)); //this also sets comp to the right value
                }
                else //assume that this code is erroneous; skip it but use as basis for later codes
                    comp = gc;
            }
        }

        GrayCode firstValue;
        /// <summary>
        /// First Status value; should be zero
        /// </summary>
        public GrayCode FirstValue { get { return firstValue; } }

        /// <summary>
        /// Find all times where Status changes to particular GrayCode
        /// </summary>
        /// <param name="gc">Value to search for</param>
        /// <returns>Array of occurence times</returns>
        public double[] FindGCTime(GrayCode gc)
        {
            return FindGCTime((int)gc.Value);
        }

        /// <summary>
        /// Find all times when Status changes to particular value
        /// </summary>
        /// <param name="gc">Value to search for</param>
        /// <returns>Array of occurence times</returns>
        public double[] FindGCTime(int gc)
        {
            return GCList.FindAll(gct => gct.GC.Value == gc).Select(gct => gct.Time).ToArray();
        }

        /// <summary>
        /// Try to find Status mark before a particular relative time
        /// </summary>
        /// <param name="time">Index time</param>
        /// <param name="gc">Value Graycode found</param>
        /// <returns>True if a Status change was found</returns>
        public bool TryFindGCBefore(double time, out GrayCode gc)
        {
            gc = GCList.FindLast(gct => gct.Time < time).GC;
            return gc.Value != 0;
        }

        /// <summary>
        /// Try to find a Status mark at or following a particular relative time
        /// </summary>
        /// <param name="time">Index time</param>
        /// <param name="gc">Value of GraycCode found</param>
        /// <returns>True if a Status change was found</returns>
        public bool TryFindGCAtOrAfter(double time, out GrayCode gc)
        {
            gc = GCList.Find(gct => gct.Time >= time).GC;
            return gc.Value != 0;
        }

        public bool TryGetFirstGCTimeAtOrAfter(double time, out GCTime gct)
        {
            gct = GCList.Find(g => g.Time >= time);
            return gct.GC.Value != 0;
        }

        public bool TryGetFirstGCTimeAfter(double time, out GCTime gct)
        {
            gct = GCList.Find(g => g.Time > time);
            return gct.GC.Value != 0;
        }

        public bool TryFindGCTimeNearest(double time, out GCTime gct)
        {
            GCTime gct1 = GCList.FindLast(g => g.Time < time); //find closest before or at time
            if (gct1.GC.Value == 0) //case with no Events before
            {
                gct = GCList.Find(g => g.Time >= time);
                return gct.GC.Value != 0;
            }
            double d1 = time - gct1.Time;
            GCTime gct2 = GCList.Find(g => g.Time >= time && g.Time - time < d1); //find first after and closer
            gct = gct2.GC.Value != 0 ? gct2 : gct1;  //if it exists, return it; otherewise return the first
            return true;
        }

        /// <summary>
        /// Find all Status marks between two relative times
        /// </summary>
        /// <param name="start">Start time</param>
        /// <param name="end">End time</param>
        /// <returns>List of GCTimes meeting criteria</returns>
        public List<GCTime> FindMarks(double start = 0D, double end = double.MaxValue)
        {
            return GCList.FindAll(gct => gct.Time >= start && gct.Time < end);
        }


        /// <summary>
        /// Estimate absolute time of beginning of Status channel based on first suitable Event in List
        /// </summary>
        /// <param name="events">List of Events to search</param>
        /// <returns>Estimated absolute time of beginning of Status channel; returns null if no suitable Events found</returns>
        [Obsolete("Deprecated (renamed): use EstimateZeroAbsoluteTime")]
        public double? getFirstZeroTime(List<Event.Event> events)
        {
            return EstimateZeroAbsoluteTime(events);
        }

        /// <summary>
        /// Estimate absolute time of beginning of Status channel based on first suitable Event in List:
        /// must be absolute, covered, and have a Status mark
        /// </summary>
        /// <param name="events">List of Events to search</param>
        /// <returns>Estimated absolute time of beginning of Status channel; returns null if no suitable Event found</returns>
        public double? EstimateZeroAbsoluteTime(List<Event.Event> events)
        {
            foreach (Event.Event ev in events)
            {
                if (ev.HasAbsoluteTime && ev.IsCovered)
                {
                    GCTime g = GCList.Find(gct => gct.GC.Value == (uint)ev.GC);
                    if (g.Time > 0) return ev.Time - g.Time;
                }
            }
            return null;
        }

        /// <summary>
        /// Overrides object.String; retuns string of Status evens found 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("Events: ");
            bool first = true;
            foreach (GCTime gct in GCList)
            {
                sb.Append((first ? "" : ", ") + gct.ToString());
                first = false;
            }
            sb.Append(Environment.NewLine + "System Events: ");
            first = true;
            foreach (SystemEvent se in SystemEvents)
            {
                sb.Append((first ? "" : ", ") + se.ToString());
                first = false;
            }
            return sb.ToString();
        }

        public IEnumerator<GCTime> GetEnumerator()
        {
            return GCList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)(GCList.GetEnumerator());
        }
    }

    public struct GCTime
    {
        public GrayCode GC;
        public double Time;

        internal GCTime(GrayCode gc, double time)
        {
            GC = gc;
            Time = time;
        }

        public override string ToString()
        {
            return $"GC={GC.Value:0} t={Time:0.000}";
        }
    }

    public struct SystemEvent
    {
        public StatusByte Code;
        public double Time;

        internal SystemEvent(byte code, double time)
        {
            Code._code = code;
            Time = time;
        }

        public override string ToString()
        {
            return "Code=" + Code._code.ToString("0") + " t=" + Time.ToString("0.000");
        }
    }

    public struct StatusByte
    {
        internal byte _code;

        public StatusByte(byte code)
        {
            _code = code;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if ((_code & (byte)Codes.MK2) > 0) sb.Append("MK2 product" + Environment.NewLine);
            else sb.Append("MK1 product" + Environment.NewLine);
            sb.Append("Speed mode = " + decodeSpeedBits().ToString("0") + Environment.NewLine);
            if ((_code & (byte)Codes.NewEpoch) > 0) sb.Append("*New epoch*" + Environment.NewLine);
            if ((_code & (byte)Codes.CMSInRange) > 0) sb.Append("CMS in range" + Environment.NewLine);
            else sb.Append("CMS out of range" + Environment.NewLine);
            if ((_code & (byte)Codes.BatteryLow) > 0) sb.Append("Battery low" + Environment.NewLine);
            else sb.Append("Battery OK" + Environment.NewLine);

            return sb.ToString();
        }

        private byte decodeSpeedBits()
        {
            return (byte)((_code & (byte)(Codes.StatusBit0 | Codes.StatusBit1 | Codes.StatusBit2)) >> 1 |
                (_code & (byte)Codes.StatusBit3) >> 2);
        }

        [Flags]
        public enum Codes : byte
        {
            NewEpoch = 0x01,
            StatusBit0 = 0x02,
            StatusBit1 = 0x04,
            StatusBit2 = 0x08,
            CMSInRange = 0x10,
            StatusBit3 = 0x20,
            BatteryLow = 0x40,
            MK2 = 0x80
        }
    }
}
