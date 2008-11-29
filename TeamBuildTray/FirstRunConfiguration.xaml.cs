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
        private bool configurationChanged;


        /// <summary>
        /// Specifies whether this is first run configuration or a reconfiguration.
        /// </summary>
        public bool ReConfigure
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

            if (configurationChanged)
            {
                MessageBox.Show("Please select a project name.");
                return false;
            }

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

            bool validated = ValidEntries();

            if (validated && (ComboBoxProjects.SelectedValue != null))
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
            else
            {
                //Combobox validation needs to occur in the save event, not in validate. Here it is:
                if (validated && ComboBoxProjects.SelectedValue == null)
                {
                    MessageBox.Show("Please select a project name");
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
            //Reset  configurationChanged = false;
            configurationChanged = false;
            ComboBoxProjects.Items.Clear();

            if (ValidEntries())
            {

                string serverName = TextBoxServerName.Text;
                int portNumber = Int32.Parse(TextBoxPortNumber.Text, CultureInfo.InvariantCulture);
                string protocol = (RadioButtonHttps.IsChecked.HasValue && RadioButtonHttps.IsChecked.Value) ? "https" : "http";

                if (!projectListCached)
                {
                    ComboBoxProjects.Items.Clear();
                    this.Cursor = Cursors.Wait;
                    var projectList = TeamServer.GetProjectList(protocol, serverName, portNumber);
                    this.Cursor = Cursors.Arrow;

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

        private void FirstRunConfigurationWindow_Loaded(object sender, RoutedEventArgs e)
        {


            if (ReConfigure)
            {
                List<TeamServer> teamServers = TeamServer.GetTeamServerList();
                PopulateFields(teamServers);
            }

            //Also, reset configurationChanged to false.
            configurationChanged = false;
        }


        private void PopulateFields(List<TeamServer> teamServers)
        {
            //Currently, we only deal with the first server.  To deal with multiple, the form layout will need to change.
            if (teamServers.Count >= 1)
            {
                TeamServer currentTeamServer = teamServers[0];
                TextBoxPortNumber.Text = currentTeamServer.Port.ToString();
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
                ComboBoxProjects.Items.Add(currentTeamServer.Projects[0].ProjectName);
                ComboBoxProjects.Text = currentTeamServer.Projects[0].ProjectName;

                LabelWindowTitle.Content = "Change Team Build Server";
            }
        }

        
        private void ServerValuesChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            configurationChanged = true;
        }

        private void CheckboxServerValuesChanged(object sender, RoutedEventArgs e)
        {
            configurationChanged = true;
        }

       
    }
}
