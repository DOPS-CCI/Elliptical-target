using RTLibrary;
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

namespace CircleTargetExperiment
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SubjectPanel : Canvas
    {
        public double circleR;
        public Point cursor = new Point();
        public double radius;

        private double windowWidth;
        private double windowHeight;

        private System.Drawing.Point center;
        public SubjectPanel()
        {

            InitializeComponent();

            windowWidth = RTDisplays.SubjectDisplayRecord.Width;
            windowHeight = RTDisplays.SubjectDisplayRecord.Height;
            this.Cursor = System.Windows.Input.Cursors.None;
            SetGraphics();

        }

        double left;
        double top;
        private void SetGraphics()
        {
            circleR = Math.Min(windowWidth, windowHeight) / 2D - 20D;
            Circle.Width = circleR * 2D;
            Circle.Height = circleR * 2D;
            left = windowWidth / 2D - circleR;
            top = windowHeight / 2D - circleR;
            Canvas.SetLeft(Circle, left);
            Canvas.SetTop(Circle, top);
            Canvas.SetLeft(CrossHair, windowWidth / 2D);
            Canvas.SetTop(CrossHair, windowHeight / 2D);
            Visibility = Visibility.Hidden;

            center = new System.Drawing.Point((int)(RTDisplays.SubjectDisplayRecord.RawSize.Width / 2D + RTDisplays.SubjectDisplayRecord.RawLocation.X),
                (int)(RTDisplays.SubjectDisplayRecord.RawSize.Height / 2D + RTDisplays.SubjectDisplayRecord.RawLocation.Y));
        }

        private void Display_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //force cursor into Circle element and calculate radius, scaled to 1.0
            cursor = e.GetPosition(Circle);
            cursor.Offset(-circleR, -circleR);
            radius = Math.Sqrt(cursor.X * cursor.X + cursor.Y * cursor.Y) / circleR;
            if (radius > 1D)
            {
                cursor.X /= radius;
                cursor.Y /= radius;
                radius = 1D;
            }
            MoveCrossHair();
        }

        void MoveCrossHair()
        {
            Canvas.SetTop(CrossHair, cursor.Y + windowHeight / 2D);
            Canvas.SetLeft(CrossHair, cursor.X + windowWidth / 2D);
        }

        public void showTargetResponse(int trialNumber, Point target, Point response)
        {
            Canvas.SetLeft(Target, target.X * circleR + windowWidth / 2D);
            Canvas.SetTop(Target, -target.Y * circleR + windowHeight / 2D);
            Canvas.SetLeft(Response, response.X * circleR + windowWidth / 2D);
            Canvas.SetTop(Response, -response.Y * circleR + windowHeight / 2D);
            CrossHair.Visibility = Visibility.Collapsed;
            NTrial.Text = trialNumber.ToString("0");
            ANAMark.Visibility = Trial.Visibility = Response.Visibility = Target.Visibility = Visibility.Visible;
        }

        public void hideTargetResponse()
        {
            Visibility = Visibility.Collapsed;
            ANAMark.Visibility = Trial.Visibility = Response.Visibility = Target.Visibility = Visibility.Collapsed;
            CrossHair.Visibility = Visibility.Visible;
        }

        public void initializeCursor()
        {
            System.Windows.Forms.Cursor.Position = center;
        }
    }
}
