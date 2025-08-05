using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using CCILibrary;
using Event;
using EventDictionary;
using GroupVarDictionary;
using EventFile;
using HeaderFileStream;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs.Controls;
#if DIO
using MccDaq;
#endif

namespace RTLibrary
{
    /// <summary>
    /// Embodies the experiment being run in current session
    /// </summary>
    public sealed class RTExperiment
    {
        /// <summary>
        /// Title of the current experiment
        /// </summary>
        public readonly string Title;

        /// <summary>
        /// Software version of current experiment
        /// </summary>
        public readonly string SoftwareVersion;

        /// <summary>
        /// Two letter code assigned to this experiment; used in file naming
        /// </summary>
        public string ExperimentDesignCode { get; }
        /// <summary>
        /// Full name of dataset; calculated from design code, subject number and
        /// date and time
        /// </summary>
        public string RWNLName { get; }
        /// <summary>
        /// Active displays in experiment
        /// </summary>
        public RTDisplays Displays { get; }
#if RTTrace || RTTraceUAId
        /// <summary>
        /// Tracing object
        /// </summary>
        public RTTraceClass Trace { get; }
#endif

        /// <summary>
        /// Delegate run at the end of the current experiment, before RWNL dataset finalized
        /// </summary>
        public delegate void CleanUpExperiment();

        /// <summary>
        /// Delegate run at the end of the current experiment, after dataset finalized
        /// </summary>
        public delegate void Epilogue();

        /// <summary>
        /// Header record for this RTExperiment
        /// </summary>
        public Header.Header DatasetHeader { get { return header; } }

        internal Header.Header header;
#if DIO
        static internal MccBoard DAQBoard = new MccBoard(0);
#endif
        readonly bool askOther = false;
        readonly bool askDisplay = true;
        readonly bool subjectDisplay = true;
        readonly bool agentDisplay = false;
        internal static string subjectDisplayName;
        internal static string agentDisplayName;
        readonly CleanUpExperiment cleanup;  //Experiment cleanup delegate
        readonly Epilogue epilogue; //Experiment epilogue delegate

        readonly List<string> otherNames;

        //Variables to track Events created during the experiment
        private readonly List<RTEventGV> EventFileList = new List<RTEventGV>(); //List of Events that have been recorded
        private readonly List<int> trialIndex = new List<int>(); //index from trial number (zero-based) to index in EventFileList of

        #region Constructor
        /// <summary>
        /// Constructor for RTExperiment; performs necessary housekeeping and builds the HDR file
        /// record outlined in the parameter; organizes display screens
        /// </summary>
        /// <param name="XMLFileName">XML file which outlines HDR and other initialization information</param>
        /// <param name="agentPresent">true if Agent present</param>
        /// <param name="cleanup">Delegate to perform cleanup before writing RWNL dataset</param>
        /// <param name="epilogue">Epilogue delegate performed after RWNL dataet finalized</param>
        public RTExperiment(
            string XMLFileName,
            bool agentPresent = false,
            CleanUpExperiment cleanup = null,
            Epilogue epilogue = null)
        {
#if DIO
            ErrorInfo ei = DAQBoard.DConfigPort(DigitalPortType.FirstPortA, DigitalPortDirection.DigitalOut);
            if (ei.Value != 0)
                throw new RTException($"DAQ board port A config error: {ei.Message}");
            ei = DAQBoard.DConfigPort(DigitalPortType.FirstPortB, DigitalPortDirection.DigitalOut);
            if (ei.Value != 0)
                throw new RTException($"DAQ board port B config error: {ei.Message}");
#endif
            trialIndex.Add(0); //initialize Trial Event index

            this.cleanup = cleanup;
            this.epilogue = epilogue;

            string nameSpace;
            XmlReader xr = null;
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    IgnoreWhitespace = true,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true
                };
                xr = XmlReader.Create(new FileStream(RTApplication.DataDirectory + XMLFileName, FileMode.Open, FileAccess.Read), settings);
                if (xr.MoveToContent() != XmlNodeType.Element)
                    throw new XmlException("input stream not a valid experiment descriptor file");
                nameSpace = xr.NamespaceURI;
                xr.ReadStartElement("Experiment", nameSpace);

                header = new Header.Header();
                header.Title = Title = xr.ReadElementContentAsString("Title", nameSpace);

                SoftwareVersion = xr.ReadElementContentAsString("SoftwareVersion", nameSpace);
                if(!string.IsNullOrEmpty(RTApplication.Version)) SoftwareVersion = RTApplication.Version; //use full version, if deployed
                header.SoftwareVersion = SoftwareVersion;

                header.LongDescription = xr.ReadElementContentAsString("LongDescription", nameSpace);
                header.Experimenter = new List<string>();
                while (xr.Name == "Experimenter")
                    header.Experimenter.Add(xr.ReadElementContentAsString("Experimenter", nameSpace));
                ExperimentDesignCode = xr.ReadElementContentAsString("ExperimentCode", nameSpace);
                int clockPeriod = xr.ReadElementContentAsInt("RTClockPeriod", nameSpace);
                header.Status = xr.ReadElementContentAsInt("Status", nameSpace);

                RTClock.Start(clockPeriod, header.Status); //now we can start the clock!
#if RTTrace || RTTraceUAId
                Trace = RTClock.trace; //open tracing
#endif

                if (xr.Name == "Other")
                {
                    header.OtherExperimentInfo = new Dictionary<string, string>();
                    do
                    {
                        header.OtherExperimentInfo.Add(xr["Name"],
                            xr.ReadElementContentAsString("Other", nameSpace));
                    } while (xr.Name == "Other");
                }

                if (xr.Name == "AskAgent") //indicate that agent is always going to be present
                {
                    agentPresent = true;
                    xr.ReadElementContentAsString();
                }

                if (xr.Name == "AskSessionOther")
                {
                    askOther = true;
                    otherNames = new List<string>(1);
                    do
                    {
                        otherNames.Add(xr["Name"]);
                        xr.ReadElementContentAsString();
                    } while (xr.Name == "AskSessionOther");
                }

                #region DisplayInfo
                //NOTE: If DisplayConfiguration is not present, then the default configuration is:
                //  subject display is on, agent display is off and configuaration is asked.
                //If Subject sub-element is present, subject display is on and its name may be given;
                //  this will be the default for its selection if the "Ask" setting is true.
                //If Agent sub-element is present, agent display will be on if the element has the
                //  Always attribute set to "Yes"; otherwise its state is conditioned on whether an
                //  agent is physically present during the experiment as indicated by the parameter
                //  in the constructor agentPresent.
                //If DisplayConfiguration is present and Type attribute == "Ask", we ask for configuarion,
                //  with the initial settings as indicated by the selected Display names; otherwise
                //  the Display names should be given.
                //If either Subject or Agent sub-elements fail to indicate a display name, the
                //  configuration will be asked, even if not explicitly indicated.
                if (xr.Name == "DisplayConfiguration")
                {
                    askDisplay = xr["Type"] == "Ask";
                    subjectDisplay = false;
                    agentDisplay = false;
                    xr.ReadStartElement("DisplayConfiguration", nameSpace);
                    do
                    {
                        switch (xr.Name)
                        {
                            case "Subject":
                                subjectDisplay = true;
                                subjectDisplayName = xr.ReadElementContentAsString();
                                askDisplay |= string.IsNullOrEmpty(subjectDisplayName);
                                break;
                            case "Agent":
                                if (xr["Always"] == "Yes") agentDisplay = true; //always show agent display
                                else agentDisplay = agentPresent; //conditional display -- only if agent physically present
                                agentDisplayName = xr.ReadElementContentAsString();
                                askDisplay |= string.IsNullOrEmpty(agentDisplayName);
                                break;
                            case "Technician": //Technician optional, but has no effect -- always present on Primary Display
                                xr.ReadElementContentAsString();
                                break;
                            default:
                                throw new RTException("In RTExperiment COTR: Invalid display type in XML configuration file");
                        }
                    } while (xr.NodeType != XmlNodeType.EndElement);
                    xr.ReadEndElement(/* DisplayConfiguration */);
                }
                else //default configuration
                {
                    subjectDisplay = true;
                    agentDisplay = agentPresent; //show screen only if agent present
                    askDisplay = true;
                }
                #endregion

                xr.ReadStartElement("Structure", nameSpace);
                if (xr.Name == "GroupVar")
                {
                    header.GroupVars = new GroupVarDictionary.GroupVarDictionary();
                    do
                    {
                        xr.ReadStartElement(/* GroupVar */);
                        GroupVarDictionary.GVEntry gve = new GVEntry();
                        xr.ReadStartElement("Name", nameSpace);
                        string name = xr.ReadContentAsString();
                        if (name.Length > 24)
                            throw new Exception("name too long for GV " + name);
                        xr.ReadEndElement(/* Name */);
                        gve.Description = xr.ReadElementContentAsString("Description", nameSpace);
                        if (xr.Name == "GV")
                        {
                            gve.GVValueDictionary = new Dictionary<string, int>();
                            do
                            {
                                string key = xr["Desc", nameSpace];
                                xr.ReadStartElement("GV", nameSpace);
                                int val = xr.ReadContentAsInt();
                                if (val > 0)
                                    gve.GVValueDictionary.Add(key, val);
                                else
                                    throw new Exception("invalid value for GV " + name);
                                xr.ReadEndElement(/* GV */);
                            }
                            while (xr.Name == "GV");
                        }
                        header.GroupVars.Add(name, gve);
                        xr.ReadEndElement(/* GroupVar */);
                    } while (xr.Name == "GroupVar");
                }

                if (xr.Name == "Event")
                {
                    header.Events = new EventDictionary.EventDictionary(header.Status);
                    do
                    {
                        EventDictionaryEntry ede = new EventDictionaryEntry();
                        if (xr.MoveToAttribute("Type"))
                            ede.Intrinsic = xr.ReadContentAsString() != "Extrinsic";
                        //else Type is intrinsic by default
                        xr.ReadStartElement(/* Event */);
                        xr.ReadStartElement("Name", nameSpace);
                        string name = xr.ReadContentAsString();
                        xr.ReadEndElement(/* Event */);
                        xr.ReadStartElement("Description", nameSpace);
                        ede.Description = xr.ReadContentAsString();
                        xr.ReadEndElement(/* Description */);
                        if (ede.IsExtrinsic)
                        {
                            xr.ReadStartElement("Channel", nameSpace);
                            ede.channelName = xr.ReadContentAsString();
                            xr.ReadEndElement(/* Channel */);
                            xr.ReadStartElement("Edge", nameSpace);
                            ede.rise = xr.ReadContentAsString() == "rising";
                            xr.ReadEndElement(/* Edge */);
                            ede.location = xr.Name == "Location" && (xr.ReadElementContentAsString() == "after"); //leads by default
                            ede.channelMax = xr.Name == "Max" ? xr.ReadElementContentAsDouble() : 0D; //zero by default
                            ede.channelMin = xr.Name == "Min" ? xr.ReadElementContentAsDouble() : 0D; //zero by default
                            if (ede.channelMax < ede.channelMin)
                                throw new Exception("invalid max/min signal values in extrinsic Event " + name);
                            //Note: Max and Min are optional; if neither is specified, 0.0 will always be used as threshold
                        }
                        if (xr.Name == "GroupVar")
                        {
                            ede.GroupVars = new List<GVEntry>();
                            do
                            {
                                string gvName = xr["Name"];
                                bool isEmpty = xr.IsEmptyElement;
                                xr.ReadStartElement(/* GroupVar */);
                                if (header.GroupVars.TryGetValue(gvName, out GVEntry gve))
                                    ede.GroupVars.Add(gve);
                                else throw new Exception("invalid GroupVar " + gvName + " in Event " + name);
                                if (!isEmpty) xr.ReadEndElement(/* GroupVar */);
                            } while (xr.Name == "GroupVar");
                        }
                        if (xr.Name == "Ancillary")
                        {
                            xr.ReadStartElement("Ancillary", nameSpace);
                            ede.ancillarySize = xr.ReadContentAsInt();
                            xr.ReadEndElement(/* Ancillary */);
                        }
                        header.Events.Add(name, ede);
                        xr.ReadEndElement(/* Event */);
                    } while (xr.Name == "Event");
                }
                xr.ReadEndElement(/*Structure*/);
                xr.ReadEndElement(/*Experiment*/);
                xr.Close();
            }
            catch (XmlException e)
            {
                XmlNodeType nodeType = xr.NodeType;
                string name = xr.Name;
                throw new Exception("RTExperiment.CTOR: Error processing XML file node " + nodeType.ToString() +
                    " named " + name + ": " + e.Message);
            }
            catch (Exception x)
            {
                // re-throw exceptions with source method label
                throw new Exception("RTExperiment.CTOR: " + x.Message);
            }

            DateTime now = DateTime.Now;
            header.Date = now.ToString("dd MMM yyyy");
            header.Time = now.ToString("HH:mm");
            AskHeaderInfoWindow ask = new AskHeaderInfoWindow();
            if (agentPresent)
            {
                ask.AgentBlock.Height = GridLength.Auto;
                ask.AgentNumber.IsEnabled = true;
                ask.AgentNumber.Tag = null;
            }
            if (askOther)
            {
                foreach (string name in otherNames)
                {
                    System.Windows.Controls.GroupBox gb = new System.Windows.Controls.GroupBox
                    {
                        Header = "Session Info: " + name
                    };
                    System.Windows.Controls.TextBox tb = new System.Windows.Controls.TextBox();
                    gb.Content = tb;
                    ask.OtherPanels.Children.Add(gb);
                }
            }
            bool result = (bool)ask.ShowDialog();
            if (!result)
                Environment.Exit(-1); //negative return code to avoid writing RWNL dataset
            header.Subject = Convert.ToInt32(ask.SubjectNumber.Text);
            if (agentPresent) header.Agent = Convert.ToInt32(ask.AgentNumber.Text);
            header.Technician = new List<string>(1); //must be at least one
            string[] techs = ask.Technicians.Text.Split(',');
            foreach (string tech in techs)
                header.Technician.Add(tech.Trim());
            if (askOther)
            {
                header.OtherSessionInfo = new Dictionary<string, string>();
                int i = 0;
                foreach (System.Windows.Controls.GroupBox item in ask.OtherPanels.Children)
                {
                    System.Windows.Controls.TextBox tb = (System.Windows.Controls.TextBox)item.Content;
                    header.OtherSessionInfo.Add(otherNames[i++], tb.Text);
                }
            }

            //Configure display screens
            if (askDisplay)
                Displays = new RTDisplays(subjectDisplay, agentDisplay);
            else
                Displays = new RTDisplays(subjectDisplayName, agentDisplayName);

            //Create dataset name
            RWNLName = $"S{header.Subject:0000}-{ExperimentDesignCode}-{now:yyyyMMdd-HHmm}";

        }
        #endregion

        /// <summary>
        /// Automatic creation of RTEvent
        /// </summary>
        /// <param name="EventName">Name of the RTEvent</param>
        /// <param name="EventDelay">Event delay after scheduled time</param>
        /// <returns>Created RTEvent</returns>
        /// <exception cref="RTException">Unable to find immediate delegate for this RTEvent</exception>
        public RTEvent InstantiateRTEvent(string EventName, uint EventDelay = 0)
        {
            RTEvent.ClockRoutine immediate = null;
            RTEvent.UIRoutine gui = null;
            object app = System.Windows.Application.Current;
            IEnumerable<MethodInfo> methods = app.GetType().GetTypeInfo().DeclaredMethods;
            //
            // Find delegates associated with this RTEvent
            //  2 choices: name routines with IM and UI appended to Event name
            //      or use the custom attribute AssociatedEvent to mark the routines for the Event
            //
            foreach (MethodInfo method in methods)
            {
                if (method.Name.StartsWith(EventName)) //?use delegate naming convention
                {
                    if (method.Name == EventName + "IM")
                    {
                        if (method.ReturnType != typeof(RTEvent))
                            throw new RTException($"In RTExperiment.InstantiateRTEvent: invalid return Type for {method.Name}");
                        if (method.GetParameters().Length > 0)
                            throw new RTException($"In RTExperiment.InstantiateRTEvent: non-empty parameter list for {method.Name}");
                        immediate = (RTEvent.ClockRoutine)method.CreateDelegate(typeof(RTEvent.ClockRoutine), app);
                    }
                    else if (method.Name == EventName + "UI")
                    {
                        if (method.ReturnType != typeof(void))
                            throw new RTException($"In RTExperiment.InstantiateRTEvent: non-void return for {method.Name}");
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(RTEventGV))
                            throw new RTException($"In RTExperiment.InstantiateRTEvent: invalid parameter for {method.Name}");
                        gui = (RTEvent.UIRoutine)method.CreateDelegate(typeof(RTEvent.UIRoutine), app);
                    }
                }
                else //?use custom attribute
                {
                    AssociatedEventAttribute att = method.GetCustomAttributes<AssociatedEventAttribute>().FirstOrDefault();
                    if (att != null && att.EventName == EventName)
                        if (att.EventType == AssociatedEventType.Immediate)
                        {
                            if (method.ReturnType != typeof(RTEvent))
                                throw new RTException($"In RTExperiment.InstantiateRTEvent: invalid return Type for immediate routine {method.Name}");
                            if (method.GetParameters().Length > 0)
                                throw new RTException($"In RTExperiment.InstantiateRTEvent: non-empty parameter list for immediate routine {method.Name}");
                            immediate = (RTEvent.ClockRoutine)method.CreateDelegate(typeof(RTEvent.ClockRoutine), app);
                        }
                        else
                        if (att.EventType == AssociatedEventType.Delayed_UI)
                        {
                            if (method.ReturnType != typeof(void))
                                throw new RTException($"In RTExperiment.InstantiateRTEvent: non-void return for delayed routine {method.Name}");
                            ParameterInfo[] parameters = method.GetParameters();
                            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(RTEventGV))
                                throw new RTException($"In RTExperiment.InstantiateRTEvent: invalid parameter for delayed routine {method.Name}");
                            gui = (RTEvent.UIRoutine)method.CreateDelegate(typeof(RTEvent.UIRoutine), app);
                        }
                }
                if (immediate != null && gui != null) break; //found both
            }
            if (immediate == null) //immediate required; delayed is optional
                throw new RTException(
                    $"In RTExperiment.InstantiateRTEvent: Unable to find ClockRoutine (immediate delegate) for Event {EventName}");

            //
            // Determine if Event is RWNL Event by searching for EDE, and create correct type of RTEvent
            //
            if (DatasetHeader.Events.TryGetValue(EventName, out EventDictionaryEntry EDE))
                return new RTEvent(EDE, EventDelay, immediate, gui);
            else
                return new RTEvent(EventName, EventDelay, immediate, gui);
        }

        #region Prior Event access
        /// <summary>
        /// Retrieve an Event that has occured in the past
        /// </summary>
        /// <param name="n">
        /// Index of Event; if negative, count back from most recent Event with -1 being the last
        /// Event; if positive, use the index of the Event, i.e. 0 is the first Event that occurred
        /// </param>
        /// <returns>The referenced Event as an OutputEvent record</returns>
        /// <exception cref="ArgumentException">Invalid event index</exception>
        public RTEventGV GetPastGVsByIndex(int n)
        {
            int i = (n < 0 ? EventFileList.Count + n : n);
            if (i < 0 || i >= EventFileList.Count)
                throw new ArgumentException("In RTExperiment.GetPastEventByIndex: invalid index");
            return EventFileList[i];
        }

        /// <summary>
        /// Try to retrieve an Event that has occured in the past
        /// </summary>
        /// <param name="n">
        /// Index of Event; if negative, count back from most recent Event with -1 being the last
        /// Event; if positive, use the index of the Event, i.e. 0 is the first Event that occurred
        /// </param>
        /// <param name="ev">The referenced Event as an Output Event record</param>
        /// <returns>True if referenced Event is present, false otherwise</returns>
        public bool TryGetPastGVsByIndex(int n, out RTEventGV ev)
        {
            int i = n + (n < 0 ? EventFileList.Count : 0);
            if (i < 0 || i >= EventFileList.Count) { ev = null;  return false; }
            ev = EventFileList[i];
            return true;
        }

        internal void TransferEventsToExperiment(List<RTEventGV> events)
        {
            foreach (RTEventGV gv in events)
                EventFileList.Add(gv);
            trialIndex.Add(EventFileList.Count);
        }

        /// <summary>
        /// Try to get OutputEvent by trial number and Event name
        /// </summary>
        /// <param name="trial">Trial number including aborted trials (zero-based); if negative, refer
        /// to past trial with -1 being last trial, -2 next to last, etc.</param>
        /// <param name="eventName">Name of the OutputEvent to return</param>
        /// <param name="ev">Returned OutputEvent</param>
        /// <returns>True if found, false otherwise</returns>
        /// <exception cref="ArgumentException">Invalid trial number</exception>
        public bool TryGetEventByName(int trial, string eventName, out RTEventGV ev)
        {
            int nTrials = trialIndex.Count - 1;
            int tIndex = trial + (trial < 0 ? nTrials : 0);
            if (tIndex < 0 || tIndex >= nTrials)
                throw new ArgumentException($"In RTExperiment.TryGetEventByName: invalid trial number {tIndex:0}");
            int eLimit = trialIndex[tIndex + 1];
            for (int eIndex = trialIndex[tIndex]; eIndex < eLimit; eIndex++)
            {
                RTEventGV e = EventFileList[eIndex];
                if (e.EDE.Name == eventName) { ev = e; return true; }
            }
            ev = null;
            return false;
        }

        /// <summary>
        /// Set GV value by trial number, Event name, and GV name
        /// </summary>
        /// <param name="trial">Trial number including aborted trials (zero-based); if negative, refers
        /// to past trial with -1 being last trial, -2 next to last, etc.</param>
        /// <param name="eventName">Name of the Event</param>
        /// <param name="gv">Name of GV to set</param>
        /// <param name="val">Integer value of GV</param>
        /// <exception cref="ArgumentException">Invalid trial number or Event name</exception>
        public void SetGV(int trial, string eventName, string gv, int val)
        {
            int nTrials = trialIndex.Count - 1;
            int tIndex = trial + (trial < 0 ? nTrials : 0);
            if (tIndex < 0 || tIndex >= nTrials)
                throw new ArgumentException($"In RTExperiment.SetGV: invalid trial number {tIndex:0}");
            //tIndex is zero-based index of the trial
            int eLimit = trialIndex[tIndex + 1];
            for (int eIndex = trialIndex[tIndex]; eIndex < eLimit; eIndex++)
            {
                RTEventGV e = EventFileList[eIndex];
                if (e.EDE.Name == eventName) //found the event
                {
                    e[gv] = val;
                    return;
                }
            }
            throw new ArgumentException($"In RTExperiment.SetGV: unable to locate Event {eventName} in trial number {tIndex:0}");
        }

        /// <summary>
        /// Set GV value by trial number, Event name, and GV name
        /// </summary>
        /// <param name="trial">Trial number including aborted trials (zero-based); if negative, refers
        /// to past trial with -1 being last trial, -2 next to last, etc.</param>
        /// <param name="eventName">Name of the Event</param>
        /// <param name="gv">Name of GV to set</param>
        /// <param name="val">Integer value of GV</param>
        /// <returns>True if found, false otherwise</returns>
        public bool TrySetGV(int trial, string eventName, string gv, int val)
        {
            int nTrials = trialIndex.Count - 1;
            int tIndex = trial + (trial < 0 ? nTrials : 0);
            if (tIndex < 0 || tIndex >= nTrials)
                throw new ArgumentException($"In RTExperiment.SetGV: invalid trial number {tIndex:0}");
            //tIndex is zero-based index of the trial
            int eLimit = trialIndex[tIndex + 1];
            for (int eIndex = trialIndex[tIndex]; eIndex < eLimit; eIndex++)
            {
                RTEventGV e = EventFileList[eIndex];
                if (e.EDE.Name == eventName) //found the event
                {
                    e[gv] = val;
                    return true;
                }
            }

            return false;
        }
        #endregion

        bool RWNLFileWritten = false;
        public void FinalizeRWNLDataset(string sessionComment = null)
        {
            if (RWNLFileWritten) return;
            RWNLFileWritten = true; //write once only

            //Establish final location of RWNL dataset
            CommonOpenFileDialog dlg = new CommonOpenFileDialog()
            {
                InitialDirectory = Properties.Settings.Default.LastFolderRWNL,
                IsFolderPicker = true,
                Title = "Locate RWNL directory site",
                DefaultFileName = Path.GetFileName(Properties.Settings.Default.LastFolderRWNL)
            };
        reAskFolder: if (dlg.ShowDialog() == CommonFileDialogResult.Cancel)
            {
                if (MessageBoxResult.Yes == MessageBox.Show(
                    "WARNING: no folder selected for RWNL dataset. Are you sure you do not want to save this dataset?",
                    "Warning: no folder selected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No)) return;
                goto reAskFolder; //OK I used a goto: so sue me
            }

            string RWNLDirectory = dlg.FileName;
            dlg.Dispose();

            Properties.Settings.Default.LastFolderRWNL = RWNLDirectory;
            DirectoryInfo d = Directory.CreateDirectory(Path.Combine(RWNLDirectory, RWNLName));

            //Locate and move BDF file
            dlg = new CommonOpenFileDialog()
            {
                Title = "Copy/move BDF file into RWNL dataset",
                InitialDirectory = Properties.Settings.Default.LastFolderBDF
            };
            dlg.Filters.Add(new CommonFileDialogFilter("BDF files", "*.bdf"));
            CommonFileDialogCheckBox copyFile = new CommonFileDialogCheckBox("copy", "Copy file", true);
            copyFile.IsProminent = true;
            dlg.Controls.Add(copyFile);
        reAskBDF: if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Properties.Settings.Default.LastFolderBDF =
                    System.IO.Path.GetDirectoryName(dlg.FileName);
                if (((CommonFileDialogCheckBox)dlg.Controls["copy"]).IsChecked)
                    File.Copy(dlg.FileName, Path.Combine(d.FullName, RWNLName + ".bdf"));
                else
                    File.Move(dlg.FileName, Path.Combine(d.FullName, RWNLName + ".bdf"));
            }
            else
            {
                if (MessageBoxResult.Cancel == MessageBox.Show(
                    "WARNING: failure to move BDF file will result in incomplete RWNL dataset. OK to accept.",
                    "Warning: missing BDF",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel))
                    goto reAskBDF; //OK I used a goto: so sue me
            }
            dlg.Dispose();

            //Locate and move ETR file
            dlg = new CommonOpenFileDialog()
            {
                Title = "Copy ETR file into RWNL dataset",
                InitialDirectory = Properties.Settings.Default.LastFolderETR
            };
            dlg.Filters.Add(new CommonFileDialogFilter("ETR files", "*.etr"));
            copyFile = new CommonFileDialogCheckBox("copy", "Copy file", true);
            copyFile.IsProminent = true;
            dlg.Controls.Add(copyFile);
        reAskETR: if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Properties.Settings.Default.LastFolderETR =
                    System.IO.Path.GetDirectoryName(dlg.FileName);
                if (((CommonFileDialogCheckBox)dlg.Controls["copy"]).IsChecked)
                    File.Copy(dlg.FileName, Path.Combine(d.FullName, RWNLName + ".etr"));
                else
                    File.Move(dlg.FileName, Path.Combine(d.FullName, RWNLName + ".etr"));
            }
            else
            {
                if (MessageBoxResult.Cancel == System.Windows.MessageBox.Show(
                    "WARNING: failure to move ETR file will result in incomplete RWNL dataset. OK to accept.",
                    "Warning: missing ETR",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel))
                    goto reAskETR; //OK I used a goto: so sue me
            }
            dlg.Dispose();

            //Create EVT file
            EventFileWriter efw = new EventFileWriter(
                new FileStream(Path.Combine(d.FullName, RWNLName + ".evt"),
                    FileMode.Create, FileAccess.Write), false);
            EventFactory ef = EventFactory.Instance(DatasetHeader.Events);
            foreach (RTEventGV gv in EventFileList)
            {
                EventDictionaryEntry ede = gv.EDE;
                OutputEvent oe = ef.CreateOutputEvent(ede);
                oe.SetTime(gv.Time); //set correct time

                //Carry across any GV values, converted to strings
                if (ede.GroupVars != null)
                {
                    oe.GVValue = new string[ede.GroupVars.Count]; //not created in OE constructor
                    int i = 0;
                    foreach (GVEntry gve in ede.GroupVars)
                    {
                        oe.GVValue[i] = gve.ConvertGVValueIntegerToString(gv[i]);
                        i++;
                    }
                }
                efw.writeRecord(oe);
            }
            efw.Close();

            //Complete HDR
            header.BDFFile = RWNLName + ".bdf";
            header.ElectrodeFile = RWNLName + ".etr";
            header.EventFile = RWNLName + ".evt";

            //Ask for any final comments, if possible
            AskFinalCommentWindow ask = new AskFinalCommentWindow();
            if (!ask.Dispatcher.HasShutdownStarted)
            {
                StringBuilder sb = new StringBuilder(sessionComment);
                bool? result = ask.ShowDialog();
                if ((bool)result)
                {
                    if (sb.Length != 0 && !String.IsNullOrEmpty(ask.Comment.Text))
                        sb.Append(Environment.NewLine);
                    sb.Append(ask.Comment.Text);
                }
                header.Comment = sb.ToString();
            }
            else
                header.Comment = "Session terminated abnormally; part of the dataset may be lost.";

            //Write HDR
            _ = new HeaderFileWriter(
                new FileStream(Path.Combine(d.FullName, RWNLName + ".hdr"),
                    FileMode.Create, FileAccess.Write), header);

        }

        /// <summary>
        /// End current experiment; closes master window; stops the clock;
        /// Runs cleanup delegate, finalizes RWNL dataset and runs epilogue if <code>code</code> is >= 0;
        /// Exits the application.
        /// </summary>
        /// <param name="code">if less than 0  skips writing RWNL dataset and epilogue; also used as  "exit code" for the application</param>
        public void End(int code = 0, string sessionComment = null)
        {
            RTClock.Stop(); //stop clock; this eliminates the chance that further Events can occur; cannot restart clock

            cleanup?.Invoke(); //Run cleanup before displays are closed; note: cannot schedule any Events; any timing will depend on how long
            //it takes to complete the following before the Universal Window is closed

            if (code >= 0) //run only if "normal" ending
            {
                FinalizeRWNLDataset(sessionComment);
                epilogue?.Invoke(); //this is after everything is finalized
            }

            RTDisplays.UniversalWindow?.Close();
            Environment.Exit(code);
        }
    }
}
