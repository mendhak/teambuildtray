using System.Windows;
using System.Windows.Input;

namespace Clyde.Rbi.TeamBuildTray
{
    /// <summary>
    /// Interaction logic for FirstRunConfiguration.xaml
    /// </summary>
    public partial class FirstRunConfiguration : Window
    {
        public FirstRunConfiguration()
        {
            InitializeComponent();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
