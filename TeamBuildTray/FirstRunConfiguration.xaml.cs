using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;

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


        private bool ValidEntries()
        {
            int portNumber;
            if (int.TryParse(TextBoxPortNumber.Text, out portNumber))
            {
                if (portNumber <= 0)
                {
                    MessageBox.Show("Please enter a valid port number");
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid port number");
                return false;
            }

            if (String.IsNullOrEmpty(TextBoxServerName.Text))
            {
                MessageBox.Show("Please enter a server name");
                return false;
            }

            if (RadioButtonHttp.IsChecked.Value == RadioButtonHttps.IsChecked.Value)
            {
                MessageBox.Show("Please select a protocol");
                return false;
            }

            return true;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

            if (ValidEntries())
            {
                try
                {
                    string serverName = TextBoxServerName.Text;
                    int portNumber = int.Parse(TextBoxPortNumber.Text);

                    string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                    Collection<TeamProject> projects = new Collection<TeamProject>();
                    projects.Add(new TeamProject() { ProjectName = TextBoxProjectName.Text });

                    //Save the server list
                    List<TeamServer> servers = new List<TeamServer>() 
                                            { 
                                                new TeamServer() { Port = portNumber, ServerName = serverName, Protocol = protocol, Projects = projects} 
                                            };

                    XmlSerializer serializer = new XmlSerializer(typeof(List<TeamServer>));
                    FileStream fs = File.Open(TeamServer.ServerConfigurationPath, FileMode.Create, FileAccess.Write,
                                              FileShare.ReadWrite);
                    serializer.Serialize(fs, servers);
                    fs.Close();

                    DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to write values to the configuration file.  The exception is: " + ex.Message);
                    DialogResult = false;
                    this.Close();
                }
            }

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
