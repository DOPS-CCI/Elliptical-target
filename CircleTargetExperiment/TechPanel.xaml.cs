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
    /// Interaction logic for TechDisplay.xaml
    /// </summary>
    public partial class TechPanel : StackPanel
    {
        MyApp application;
        public TechPanel(MyApp app)
        {
            application = app;
            InitializeComponent();
        }

        public void EnableKey(TextBlock key)
        {
            key.Foreground = Brushes.White;
        }

        public void DisableKey(TextBlock key)
        {
            key.Foreground = Brushes.DarkGray;
        }
    }
}
