using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace TeamBuildTray
{
    /// <summary>
    /// Interaction logic for FirstRunConfiguration.xaml
    /// </summary>
    public partial class FirstRunConfiguration
    {
        private bool projectListCached;

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
            if ((ValidEntries()) && (ComboBoxProjects.SelectedValue != null))
            {
                try
                {
                    string serverName = TextBoxServerName.Text;
                    int portNumber = Int32.Parse(TextBoxPortNumber.Text, CultureInfo.InvariantCulture);

                    string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                    Collection<TeamProject> projects = new Collection<TeamProject>();
                    projects.Add(new TeamProject { ProjectName = ComboBoxProjects.SelectedValue.ToString() });

                    //Save the server list
                    List<TeamServer> servers = new List<TeamServer>
                                                   { 
                                                new TeamServer { Port = portNumber, ServerName = serverName, Protocol = protocol, Projects = projects} 
                                            };

                    XmlSerializer serializer = new XmlSerializer(typeof(List<TeamServer>));
                    FileStream fs = File.Open(TeamServer.ServerConfigurationPath, FileMode.Create, FileAccess.Write,
                                              FileShare.ReadWrite);
                    serializer.Serialize(fs, servers);
                    fs.Close();

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to write values to the configuration file.  The exception is: " + ex.Message);
                    DialogResult = false;
                    Close();
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

        private void ComboBoxProjects_DropDownOpened(object sender, EventArgs e)
        {
            if (ValidEntries())
            {
                string serverName = TextBoxServerName.Text;
                int portNumber = Int32.Parse(TextBoxPortNumber.Text, CultureInfo.InvariantCulture);
                string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                if (!projectListCached)
                {
                    ComboBoxProjects.Items.Clear();
                    var projectList = TeamServer.GetProjectList(protocol, serverName, portNumber);
                    foreach (var project in projectList)
                    {
                        ComboBoxProjects.Items.Add(project.Name);
                    }
                    projectListCached = true;
                }
            }
        }

        private void ServerValuesChanged(object sender, EventArgs e)
        {
            projectListCached = false;
        }

        private void TextBoxPortNumber_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBoxPortNumber.SelectAll();
        }

        private void TextBoxServerName_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBoxServerName.SelectAll();
        }
    }
}
