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

namespace RTLibrary
{
    /// <summary>
    /// Interaction logic for RTMainWindow.xaml
    /// </summary>
    public partial class RTMainWindow : Window
    {
        internal RTMainWindow()
        {
            InitializeComponent();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //ignore mouse down not over subject display
            e.Handled = !RTDisplays.SubjectScreen.IsMouseOver;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //all KeyDown events get routed to current tech screen
            if (Keyboard.FocusedElement.GetType() == typeof(TextBox))
                return; //let TextBoxes on tech screen handle own input
            e.Handled = true; //otherwise we handle it here
            RTTechScreen tech = RTDisplays.TechScreen; //by executing the associated Command
            if (tech.KeyCodes == null) return; //if any
            string keyString = e.Key.ToString();
            if (keyString.Length != 1) return; //only letters allowed, even digits are 2 chars
            int key = tech.KeyCodes.IndexOf(keyString);
            if (key < 0) return;
            tech.Commands[key]?.Invoke(Keyboard.Modifiers);
        }
    }
}
