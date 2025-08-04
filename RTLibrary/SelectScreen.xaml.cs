using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RTLibrary
{
    /// <summary>
    /// Interaction logic for SelectScreen.xaml
    /// </summary>
    /// <remarks>We'd like to enforce ther being only one screen
    /// per display with each screen ocurring only once. We make 
    /// an initial assignment of screens to displays, unless one 
    /// has already been made</remarks>
    internal partial class SelectScreen : Window
    {
        private readonly string[] screenTypes; //names of screens to be assigned
        private int[] screenAssignment; //[i]==display to which screen i assigned, -1 if unassigned
        private int[] displayAssignment; //[i]==screen to which display i assigned, -1 if unassigned
        int nScreens;
        int nDisplays;
        RTDisplays RTD;

        internal SelectScreen(RTDisplays displays, bool subjectScreen = true, bool agentScreen = false)
        {
            RTD = displays;

            screenTypes = new string[displays.NScreens];
            screenTypes[0] = "Technician";

            nScreens = displays.NScreens;
            nDisplays = RTDisplays.NDisplays;

            if (nScreens > nDisplays)
                throw new RTException($"In RTLibrary.SelectScreen: more screens requested ({nScreens:0}) than displays available ({RTDisplays.NDisplays:0})");

            InitializeComponent();

            //assign screen names
            int ii = 1;
            if (subjectScreen) screenTypes[ii++] = "Subject";
            if (agentScreen) screenTypes[ii] = "Agent";

            //2D maps
            screenAssignment = new int[nScreens];
            for (int i = 0; i < nScreens; i++) screenAssignment[i] = -1;
            displayAssignment = new int[nDisplays];
            for (int i = 0; i < nDisplays; i++) displayAssignment[i] = -1;

            //make initial assignments, based on map in RTDisplays, correcting errors
            for (int i = 0; i < nDisplays; i++)
                for (int j = 0; j < nScreens; j++)
                    if (displays.screenMap[i, j]) //then screen defaulted to this display
                        if (displayAssignment[i] == -1 && screenAssignment[j] == -1) //then screen not previously assigned
                        {
                            screenAssignment[j] = i;
                            displayAssignment[i] = j;
                        }
                        else //display can only be assigned to one screen
                            displays.screenMap[i, j] = false; //modify RTDisplay map
            //Now make sure each screen is assigned to a display
            for (int i = 0; i < screenTypes.Length; i++)
                if (screenAssignment[i] == -1) //then we need to arbitrarily assign screen to "empty" display
                    for (int j = 0; j < nDisplays; j++) //find "empty" display
                        if (displayAssignment[j] == -1)
                        {
                            screenAssignment[i] = j;
                            displayAssignment[j] = i;
                            displays.screenMap[j, i] = true;
                            break;
                        }

            //Calculate scaling factor to display screen icons
            double scale = Math.Min(
                ScreenDiagram.Width / (RTDisplays.displayXMax - RTDisplays.displayXMin),
                ScreenDiagram.Height / (RTDisplays.displayYMax - RTDisplays.displayYMin));

            //Create window
            for (int iDisplay = 0; iDisplay < nDisplays; iDisplay++)
            {
                //create icon for each available display
                Grid display = new Grid
                {
                    Tag = iDisplay,
                    ToolTip = $"Size: {RTDisplays.Displays[iDisplay].Width:0} x {RTDisplays.Displays[iDisplay].Height:0}" +
                        (RTDisplays.Displays[iDisplay].Primary ? "\nPRIMARY" : "")
                };
                Canvas.SetLeft(display, (RTDisplays.Displays[iDisplay].Left - RTDisplays.displayXMin) * scale);
                Canvas.SetTop(display, (RTDisplays.Displays[iDisplay].Top - RTDisplays.displayYMin) * scale);
                Rectangle rect = new Rectangle
                {
                    Width = RTDisplays.Displays[iDisplay].Width * scale,
                    Height = RTDisplays.Displays[iDisplay].Height * scale
                };
                TextBlock title = new TextBlock
                {
                    Text = RTDisplays.Displays[iDisplay].Name
                };
                StackPanel sp = new StackPanel()
                {
                    Tag = iDisplay
                };

                if (displays.screenMap[iDisplay, 0]) //then tech display on PRIMARY
                {
                    sp.Children.Add(new TextBlock { Text = screenTypes[0], FontWeight = FontWeights.Regular });
                }
                else
                {
                    for (int iScreen = 1; iScreen < nScreens; iScreen++)
                    {
                        RadioButton rb = new RadioButton()
                        {
                            Tag = iScreen,
                            Content = screenTypes[iScreen],
                            IsChecked = displays.screenMap[iDisplay, iScreen],
                        };
                        sp.Children.Add(rb);
                    }
                }
                display.Children.Add(rect);
                display.Children.Add(title);
                display.Children.Add(sp);
                ScreenDiagram.Children.Add(display);
            }
        }

        private void Screen_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Rectangle r = (Rectangle)sender;
            r.Stroke = Brushes.Red;
        }

        private void Screen_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Rectangle r = (Rectangle)sender;
            r.Stroke = Brushes.Green;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void RadioButton_Clicked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = (RadioButton)sender;
            int newScrn = (int)rb.Tag; //screen number
            int newDsp = (int)((StackPanel)rb.Parent).Tag; //display number
            if (RTD.screenMap[newDsp, newScrn]) return; //no change
            int oldDsp = screenAssignment[newScrn];
            int oldScrn = displayAssignment[newDsp];
            displayAssignment[newDsp] = newScrn;
            screenAssignment[newScrn] = newDsp;
            RTD.screenMap[newDsp, newScrn] = true;
            RTD.screenMap[oldDsp, newScrn] = false;
            if ((displayAssignment[oldDsp] = oldScrn) != -1) //then move displaced screen to oldDsp
            {
                screenAssignment[oldScrn] = oldDsp;
                RTD.screenMap[oldDsp, oldScrn] = true;
                RTD.screenMap[newDsp, oldScrn] = false;
            }

            for (int dsp = 0; dsp < nDisplays; dsp++)
            {
                UIElementCollection uiec = ((Grid)ScreenDiagram.Children[dsp]).Children;
                foreach (UIElement uie in uiec)
                    if (uie.GetType() == typeof(StackPanel))
                    {
                        int ndsp = (int)((FrameworkElement)uie).Tag;
                        foreach (UIElement el in ((StackPanel)uie).Children)
                            if (el.GetType() == typeof(RadioButton))
                            {
                                int nscrn = (int)((FrameworkElement)el).Tag;
                                ((RadioButton)el).IsChecked =
                                    RTD.screenMap[ndsp, nscrn];
                            }
                    }
            }
        }
    }
}
