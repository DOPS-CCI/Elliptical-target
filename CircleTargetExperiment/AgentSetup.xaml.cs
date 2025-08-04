using System;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

namespace CircleTargetExperiment
{
    /// <summary>
    /// Interaction logic for AgentSetup.xaml
    /// </summary>
    public partial class AgentSetup : Window
    {
        public AgentSetup()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((Button)sender).Name == "OK";
            this.Close();
        }

        private void AgentPresent_Check(object sender, RoutedEventArgs e)
        {
                checkError();
        }
 
        private void Prob_Initialized(object sender, EventArgs e)
        {
            Prob.Text = MyApp.agentProb.ToString("0.000");
        }

        private void checkError()
        {
            if (!IsInitialized) return;
            bool isenabled = (double)BaselineDelay.Tag >= 0D;
            isenabled &= (double)BaselineLength.Tag > 0D;
            isenabled &= (double)TargetSelectMinDelay.Tag >= 0D;
            isenabled &= (double)TargetSelectMaxDelay.Tag >= 0D;
            if (isenabled) isenabled = (double)TargetSelectMaxDelay.Tag >= (double)TargetSelectMinDelay.Tag;
            isenabled &= (double)MinPermitDelay.Tag >= 0D;
            isenabled &= (double)FeedbackDelay.Tag >= 0D;
            isenabled &= (double)FeedbackLength.Tag >= 0D;
            isenabled &= (uint)NumberOfTrialsInRun.Tag > 0;
            if (isenabled && (bool)AgentPresent.IsChecked) isenabled = (double)Prob.Tag > 0D && (double)Prob.Tag <= 1D;
            OK.IsEnabled = isenabled;
        }

        private void TextBox_Initialized(object sender, EventArgs e)
        {
            TextBox tb = (TextBox)sender;
            uint msec = (uint)typeof(MyApp).GetField(tb.Name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            tb.Text = ((double)msec / 1000D).ToString("0.000");
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            try
            {
                tb.Tag = Convert.ToDouble(tb.Text);
            }
            catch (FormatException)
            {
                tb.Tag = -1D;
            }
            checkError();
        }

        private void NumberOfTrials_Initialized(object sender, EventArgs e)
        {
            NumberOfTrialsInRun.Text = MyApp.totalTrialsInRun.ToString("0");
        }

        private void NumberOfTrials_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                NumberOfTrialsInRun.Tag = Convert.ToUInt32(NumberOfTrialsInRun.Text);
            }
            catch (OverflowException)
            {
                NumberOfTrialsInRun.Tag = 0U;
            }
            catch (FormatException)
            {
                NumberOfTrialsInRun.Tag = 0U;
            }
            checkError();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MyApp.BaselineDelay = (uint)((double)BaselineDelay.Tag * 1000D);
            MyApp.BaselineLength = (uint)((double)BaselineLength.Tag * 1000D);
            MyApp.TargetSelectMinDelay = (uint)((double)TargetSelectMinDelay.Tag * 1000D);
            MyApp.TargetSelectMaxDelay = (uint)((double)TargetSelectMaxDelay.Tag * 1000D);
            MyApp.MinPermitDelay = (uint)((double)MinPermitDelay.Tag * 1000D);
            MyApp.FeedbackDelay = (uint)((double)FeedbackDelay.Tag * 1000D);
            MyApp.FeedbackLength = (uint)((double)FeedbackLength.Tag * 1000D);
            MyApp.agentProb = (double)Prob.Tag;
            MyApp.totalTrialsInRun = (uint)NumberOfTrialsInRun.Tag;
        }
    }
}
