using System.Windows;

namespace CircleTargetExperiment
{
    /// <summary>
    /// Interaction logic for Final_Stats.xaml
    /// </summary>
    public partial class FinalStats : Window
    {
        public FinalStats()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
