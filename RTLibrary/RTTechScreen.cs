using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Interaction logic for RTTechDisplay.xaml
    /// </summary>
    public partial class RTTechScreen : RTScreen
    {
        public delegate void Command(ModifierKeys modifier);
        internal Command[] Commands;
        internal string KeyCodes;

        internal RTTechScreen()
        {
            InitializeComponent();

            Status.Text = "OK";
        }

        public void AddKeyCodes(string keyCodes, Command[] commands)
        {
            if (commands?.Length != keyCodes?.Length)
                throw new ArgumentException("In RTTechDisplay.AddKeyCodes: keyCodes length does not match commands length.");
            KeyCodes = keyCodes;
            if (KeyCodes != null)
                KeyCodes.ToUpper();
            Commands = commands;
        }

        public void ClearKeyCodes()
        {
            KeyCodes = null;
            Commands = null;
        }

        public void TechMessage(string message)
        {
            Mess.Text = message;
        }

        private void Display_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            FrameworkElement w = (FrameworkElement)sender;
            w.Cursor = System.Windows.Input.Cursors.None;
            Status.Text = "Warning: cursor moved onto tech screen";
        }
    }
}
