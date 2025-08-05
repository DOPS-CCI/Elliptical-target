using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RTLibrary;

namespace CircleTargetExperiment
{
    /// <summary>
    /// Interaction logic for AgentPanel.xaml
    /// </summary>
    public partial class AgentPanel : Canvas
    {
        private readonly double windowWidth;
        private readonly double windowHeight;

        public AgentPanel(RTScreen agentScreen)
        {
            InitializeComponent();

            windowWidth = agentScreen.Width;
            windowHeight = agentScreen.Height;
            SetGraphics();

        }

        double left;
        double top;
        private double circleR;

        private void SetGraphics()
        {
            circleR = Math.Min(windowWidth, windowHeight) / 2D - 20D;
            Circle.Width = circleR * 2D;
            Circle.Height = circleR * 2D;
            left = windowWidth / 2D - circleR; //screen-centered coordinates
            top = windowHeight / 2D - circleR;
            Canvas.SetLeft(Circle, left);
            Canvas.SetTop(Circle, top);
            left += circleR; //circle-centered coordinates
            top += circleR;
            Visibility = Visibility.Collapsed;
        }

        internal void MoveTarget(double targetX, double targetY)
        {
            Canvas.SetLeft(Target, left + targetX * circleR);
            Canvas.SetTop(Target, top - targetY * circleR);
        }

        internal void ShowResponse(double responseX, double responseY)
        {
            Canvas.SetLeft(Response, left + responseX * circleR);
            Canvas.SetTop(Response, top - responseY * circleR);
            Response.Visibility = Visibility.Visible;
        }

        internal void Reset()
        {
            Response.Visibility = Visibility.Collapsed;
        }
    }
}
