using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using TeamBuildTray.Resources;
using System.Windows.Controls;

namespace TeamBuildTray
{
    /// <summary>
    /// Interaction logic for FirstRunConfiguration.xaml
    /// </summary>
    public partial class FirstRunConfiguration
    {
        /// <summary>
        /// Specifies whether this is first run configuration or a reconfiguration.
        /// </summary>
        public bool Reconfigure
        {
            get;
            set;
        }

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
            return ValidEntries(true);
        }

        private bool ValidEntries(bool checkProjects)
        {
            if (checkProjects)
            {
                if (!ProjectsSelected())
                {
                    MessageBox.Show("Please select at least one project.", ResourcesMain.MainWindow_Title,
                                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
            }

            int portNumber;
            if (int.TryParse(TextBoxPortNumber.Text, out portNumber))
            {
                if (portNumber <= 0)
                {
                    MessageBox.Show("Please enter a valid port number", ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid port number", ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            if (String.IsNullOrEmpty(TextBoxServerName.Text))
            {
                MessageBox.Show("Please enter a server name", ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            if (RadioButtonHttp.IsChecked.Value == RadioButtonHttps.IsChecked.Value)
            {
                MessageBox.Show("Please select a protocol", ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }

            return true;
        }

        private bool ProjectsSelected()
        {
            foreach (CheckBox checkBox in ListBoxProjects.Items)
            {
                if ((checkBox.IsChecked.HasValue) && (checkBox.IsChecked.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

            bool validated = ValidEntries();

            if (validated && (ProjectsSelected()))
            {
                try
                {
                    string serverName = TextBoxServerName.Text;
                    int portNumber = Int32.Parse(TextBoxPortNumber.Text, CultureInfo.InvariantCulture);

                    string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                    Collection<TeamProject> projects = new Collection<TeamProject>();
                    foreach (CheckBox checkBox in ListBoxProjects.Items)
                    {
                        if ((checkBox.IsChecked.HasValue) && (checkBox.IsChecked.Value))
                        {
                            projects.Add(new TeamProject { ProjectName = checkBox.Content.ToString() });
                        }
                    }

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
                    MessageBox.Show("Unable to write values to the configuration file.  The exception is: " + ex.Message, ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    DialogResult = false;
                    Close();
                }
            }
            else
            {
                //Combobox validation needs to occur in the save event, not in validate. Here it is:
                if (validated && (ProjectsSelected()))
                {
                    MessageBox.Show("Please select a project name", ResourcesMain.MainWindow_Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
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

        private void TextBoxPortNumber_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBoxPortNumber.SelectAll();
        }

        private void TextBoxServerName_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBoxServerName.SelectAll();
        }

        private void FirstRunConfigurationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Reconfigure)
            {
                List<TeamServer> teamServers = TeamServer.GetTeamServerList();
                PopulateFields(teamServers);
            }
        }


        private void PopulateFields(IList<TeamServer> teamServers)
        {
            //Currently, we only deal with the first server.  To deal with multiple, the form layout will need to change.
            if (teamServers.Count >= 1)
            {
                TeamServer currentTeamServer = teamServers[0];
                TextBoxPortNumber.Text = currentTeamServer.Port.ToString(CultureInfo.CurrentCulture);
                TextBoxServerName.Text = currentTeamServer.ServerName;

                if (currentTeamServer.Protocol == "http")
                {
                    RadioButtonHttp.IsChecked = true;
                    RadioButtonHttps.IsChecked = false;
                }
                else
                {
                    RadioButtonHttps.IsChecked = true;
                    RadioButtonHttp.IsChecked = false;
                }

                //Manually add the project name to the combobox.
                foreach (var project in currentTeamServer.Projects)
                {
                    ListBoxProjects.Items.Add(new CheckBox
                                                  {
                                                      Content = project.ProjectName,
                                                      IsChecked = true
                                                  });
                }

                LabelWindowTitle.Content = "Change Team Build Server";
            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //Reset  configurationChanged = false;
            ListBoxProjects.Items.Clear();

            if (ValidEntries(false))
            {
                string serverName = TextBoxServerName.Text;
                int portNumber = Int32.Parse(TextBoxPortNumber.Text, CultureInfo.InvariantCulture);
                string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                ListBoxProjects.Items.Clear();
                Cursor = Cursors.Wait;
                var projectList = TeamServer.GetProjectList(protocol, serverName, portNumber);
                Cursor = Cursors.Arrow;

                foreach (var project in projectList)
                {
                    CheckBox checkBox = new CheckBox();
                    checkBox.Content = project.Name;
                    ListBoxProjects.Items.Add(checkBox);
                }
            }
        }


    }
}
