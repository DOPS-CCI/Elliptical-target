using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace RTLibrary
{
    public class RTScreen : Grid
    {
        internal RTScreen()
        {
            this.MouseLeave += Screen_MouseLeave;
            this.Cursor = Cursors.None;
            this.Background = Brushes.Black;
        }

        /// <summary>
        /// Add a panel to the display queue
        /// </summary>
        /// <param name="panel">Panel to be added</param>
        /// <returns>Index of panel</returns>
        /// <remarks>Panel is not immediately visible until "shown"</remarks>
        public int AddPanel(Panel panel)
        {
            Grid.SetRow(panel, 0);
            Grid.SetColumn(panel, 0);
            panel.Visibility = Visibility.Collapsed;
            Children.Add(panel);
            return Children.Count - 1;
        }

        /// <summary>
        /// Display a particular panel
        /// </summary>
        /// <param name="panel">Panel to be displayed</param>
        public void ShowPanel(Panel panel)
        {
            panel.Visibility = Visibility.Visible;
        }


        /// <summary>
        /// Display a particular panel by its index
        /// </summary>
        /// <param name="p">Index of panel to be displayed</param>
        public void ShowPanel(int p)
        {
            Children[p].Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hide a panel from screen
        /// </summary>
        /// <param name="panel">Panel to be hidden</param>
        /// <exception cref="RTException">Panel not found</exception>
        public void HidePanel(Panel panel)
        {
            foreach (UIElement child in Children)
            {
                if (child == panel)
                {
                    panel.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            throw new RTException("In RTScreen.HidePanel: unable to find panel to hide");
        }

        /// <summary>
        /// Hide a panel from screen by index number
        /// </summary>
        /// <param name="p">Index of panel to be hidden</param>
        public void HidePanel(int p)
        {
            Children[p].Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Hide all registered panels
        /// </summary>
        public void HideAll()
        {
            foreach (UIElement child in Children)
                    child.Visibility = Visibility.Collapsed;
        }

        private void Screen_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RTDisplays.TechScreen.Status.Text = "Warning: cursor outside subject window";
        }
    }
}
