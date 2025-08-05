using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RTLibrary
{
    public class RTDisplays
    {

        /// <summary>
        /// Number of displays available in system
        /// </summary>
        public static int NDisplays { get; private set; }

        /// <summary>
        /// Panel in which the Agent screen is drawn
        /// </summary>
        public RTScreen AgentScreen
        {
            get
            {
                return AgentDisplayRecord.Screen;
            }
        }

        /// <summary>
        /// Number of screens active in experiment
        /// </summary>
        public int NScreens { get; private set; }

        #region Convenience static properties
        public static Record TechDisplayRecord { get; private set; }
        public static Record SubjectDisplayRecord { get; private set; }
        public static Record AgentDisplayRecord { get; private set; }

        /// <summary>
        /// Panel in which the Tech display is drawn
        /// </summary>
        public static RTTechScreen TechScreen
        {
            get
            {
                return (RTTechScreen)TechDisplayRecord.Screen;
            }
        }

        /// <summary>
        /// Panel in which the Subject display is drawn
        /// </summary>
        public static RTSubjectScreen SubjectScreen
        {
            get
            {
                return (RTSubjectScreen)SubjectDisplayRecord.Screen;
            }
        }
        #endregion

        internal bool[,] screenMap; //[i,j] == true if display i is used for screen j

        internal static Record[] Displays; //list of available displays and their parameters
        //display limits within display-space, based on all available displays
        internal static double displayXMin = double.MaxValue;
        internal static double displayXMax = double.MinValue;
        internal static double displayYMin = double.MaxValue;
        internal static double displayYMax = double.MinValue;

        internal static RTMainWindow UniversalWindow; //universal window that contains all screens

        static RTDisplays() //initialize display Records
        {
#if SCREENTEST
            Displays = new RTDisplays.Record[]
            {
                new Record()
                {
                    Name = "Display 1",
                    Location = new System.Drawing.Point(0,0),
                    Size = new System.Drawing.Size(1280,720),
                    Scale = 1D,
                    Primary = false
                },
                new Record()
                {
                    Name = "Display 2",
                    Location = new System.Drawing.Point(1280,0),
                    Size = new System.Drawing.Size(1280,720),
                    Scale = 1D,
                    Primary = true
                },
                new Record()
                {
                    Name = "Display 3",
                    Location = new System.Drawing.Point(1280,720),
                    Size = new System.Drawing.Size(1280,720),
                    Scale = 1D,
                    Primary = false
                },
                new Record()
                {
                    Name = "Display 4",
                    Location = new System.Drawing.Point(1280,-768),
                    Size = new System.Drawing.Size(1024,768),
                    Scale = 1D,
                    Primary = false
                }
            };
            NDisplays = Displays.Length;
            //Calculate Screen extents
            for (int i = 0; i < NDisplays; i++)
            {
                Record s = Displays[i];
                displayXMin = Math.Min(displayXMin, s.Left);
                displayXMax = Math.Max(displayYMax, s.Left + s.Width);
                displayYMin = Math.Min(displayYMin, s.Top);
                displayYMax = Math.Max(displayXMax, s.Top + s.Height);
            }
#else
            //Find all available displays
            System.Windows.Forms.Screen[] disps = System.Windows.Forms.Screen.AllScreens;
            NDisplays = disps.Length;
            Displays = new RTDisplays.Record[NDisplays];

            //calculate primary window scaling factor
            for (int i = 0; i < NDisplays; i++)
                if (disps[i].Primary)
                {
                    Record.PrimaryDisplayScaleFactor = disps[i].Bounds.Width / SystemParameters.PrimaryScreenWidth;
                    break;
                }
            //Gather Screen information and determine extent of display space
            for (int i = 0; i < NDisplays; i++)
            {
                System.Windows.Forms.Screen sc = disps[i];
                Record s = new Record
                {
                    Primary = sc.Primary,
                    Name = sc.DeviceName,
                    Location = sc.Bounds.Location,
                    Scale = DisplaySettings.DisplayScale(sc),
                    Size = sc.Bounds.Size
                };

                displayXMin = Math.Min(displayXMin, s.Left);
                displayXMax = Math.Max(displayXMax, s.Left + s.Width);
                displayYMin = Math.Min(displayYMin, s.Top);
                displayYMax = Math.Max(displayYMax, s.Top + s.Height);

                Displays[i] = s;
            }
#endif
                }

        internal RTDisplays(bool subjectScreen = true, bool agentScreen = false)
        {
            NScreens = 1 + (subjectScreen ? 1 : 0) + (agentScreen ? 1 : 0);
            screenMap = new bool[NDisplays, NScreens];

            //Gather Screen information
            for (int i = 0; i < NDisplays; i++)
            {
                if (Displays[i].Primary) screenMap[i, 0] = true; //default primary screen to tech
                if (Displays[i].Name == RTExperiment.subjectDisplayName)
                    screenMap[i, 1] = true;
                if (Displays[i].Name == RTExperiment.agentDisplayName)
                    screenMap[i, subjectScreen ? 2 : 1] = true;
            }

            //Ask for screen-display designation to create screen-display map
            SelectScreen ss = new SelectScreen(this, subjectScreen, agentScreen);
            ss.ShowDialog();
            if (!(bool)ss.DialogResult) Environment.Exit(-1);

            assignScreensToDisplays();
        }

        public RTDisplays(string subjectScreenName, string agentScreenName)
        {
            NScreens = 1 + (string.IsNullOrEmpty(subjectScreenName) ? 0 : 1) +
                (string.IsNullOrEmpty(agentScreenName) ? 0 : 1);
            screenMap = new bool[NDisplays, NScreens];

            //Gather Screen information
            for (int i = 0; i < NDisplays; i++)
            {
                RTDisplays.Record r = Displays[i];
                if (r.Primary) screenMap[i, 0] = true; //always assign primary screen to tech
                else if (r.Name == subjectScreenName)
                    screenMap[i, 1] = true;
                else if (r.Name == agentScreenName)
                    screenMap[i, string.IsNullOrEmpty(subjectScreenName) ? 1 : 2] = true;
            }

            assignScreensToDisplays();
        }

        /// <summary>
        /// Show and activate all screens
        /// </summary>
        public void BeginDisplays()
        {
            UniversalWindow.Show();
            UniversalWindow.Activate();
        }

        private double activeXMax = double.MinValue; //screen area based on active screens only
        private double activeYMax = double.MinValue;
        private double activeXMin = double.MaxValue;
        private double activeYMin = double.MaxValue;
        private void assignScreensToDisplays()
        {
            //Find extent of screens actually being used and check for scale compatability
            for (int i = 0; i < NDisplays; i++)
                for (int j = 0; j < NScreens; j++)
                    if (screenMap[i, j])
                    {
                        Record r = Displays[i];

                        if (r.Scale > Record.PrimaryDisplayScaleFactor) //incompatable scales => cannot cover display space with universal window
                            throw new RTException($"In RTDisplay COTR: secondary display {r.Name} has scale factor greater than primary display");

                        activeXMax = Math.Max(r.Left + r.Width, activeXMax);
                        activeXMin = Math.Min(r.Left, activeXMin);
                        activeYMax = Math.Max(r.Top + r.Height, activeYMax);
                        activeYMin = Math.Min(r.Top, activeYMin);
                    }
            UniversalWindow = new RTMainWindow()
            {
                Width = activeXMax - activeXMin,
                Height = activeYMax - activeYMin,
#if SCREENTEST
                Left = 0,
                Top = 0
#else
                Left = activeXMin,
                Top = activeYMin
#endif
            };

            //Create display RTScreen for tech
            for (int d = 0; d < NDisplays; d++)
                if (screenMap[d, 0])
                {
                    TechDisplayRecord = addScreenToDisplay(new RTTechScreen(), d);
                    break;
                }
            int k = 1;
            if (NScreens > k)
                for (int d = 0; d < NDisplays; d++)
                    if (screenMap[d, k])
                    {
                        SubjectDisplayRecord = addScreenToDisplay(new RTSubjectScreen(), d);
                        k++;
                        break;
                    }
            if (NScreens > k)
            {
                int d = 0;
                for (; d < NDisplays; d++)
                    if (screenMap[d, k])
                    {
                        AgentDisplayRecord = addScreenToDisplay(new RTScreen(), d); //plain vanilla screens only for agents
                        break;
                    }
                if (d >= NDisplays) //didn't find display
                    throw new RTException($"In RTDisplays COTR: unable to find agent display name.");
            }
        }

        private Record addScreenToDisplay(RTScreen screen, int display)
        {
            Record rec = Displays[display]; //get display record
            screen.Width = rec.Width; //set size of screen to fill display
            screen.Height = rec.Height;
            Canvas.SetLeft(screen, rec.Left - activeXMin); //set location of screen in universal window
            Canvas.SetTop(screen, rec.Top - activeYMin);
            rec.Screen = screen; //assign screen to display
            UniversalWindow.ExtendedCanvas.Children.Add(screen);
            return rec;
        }

        /// <summary>
        /// Encapsulates display information and screen mapping
        /// </summary>
        public class Record
        {
            public static double PrimaryDisplayScaleFactor = 1D;

            internal System.Drawing.Point Location; //raw display location
            internal System.Drawing.Size Size; //raw display size
            internal bool Primary; //is this display primary?
            internal double Scale = 1D; //display scaling, as set in system Settings

            internal RTScreen Screen { get; set; }

            /// <summary>
            /// Name of display as assigned by system
            /// </summary>
            public string Name { get; internal set; }

            /// <summary>
            /// X coordinate of the upper left hand corner of display in display-space
            /// coordinates
            /// </summary>
            public double Left { get {
                    return (double)Location.X / ScreenScale; } }

            /// <summary>
            /// Y coordinate of the upper left hand corner of display in display-space
            /// coordinates
            /// </summary>
            public double Top { get { return (double)Location.Y / ScreenScale; } }

            /// <summary>
            /// Width of the display
            /// </summary>
            public double Width { get { return (double)Size.Width / ScreenScale; } }

            /// <summary>
            /// Height of the display
            /// </summary>
            public double Height { get { return (double)Size.Height / ScreenScale; } }

            /// <summary>
            /// Scale of screen used for drawing
            /// </summary>
            double ScreenScale => PrimaryDisplayScaleFactor * PrimaryDisplayScaleFactor / Scale;

            public System.Drawing.Point RawLocation { get { return Location; } }

            public System.Drawing.Size RawSize { get { return Size; } }
        }

#if SCREENTEST
        public static class TestScreens
        {
            public static RTDisplays.Record getScreen(int i) => Displays[i];
        }
#endif
    }
}
