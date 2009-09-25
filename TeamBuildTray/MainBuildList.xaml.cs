using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using TeamBuildTray.TeamBuildService;
using System.Globalization;
using TeamBuildTray.Resources;
using System.Reflection;

namespace TeamBuildTray
{
    public partial class MainBuildList
    {
        private readonly Dictionary<string, DateTime> buildUpdates = new Dictionary<string, DateTime>();
        private ObservableCollection<BuildDetail> buildContentView;
        private readonly List<BuildDetail> buildContent = new List<BuildDetail>();
        StatusMessage lastStatusMessage = new StatusMessage { Message = "" };
        private bool exitButtonClicked;
        private readonly List<int> buildIdsAlertedInProgress = new List<int>();
        private readonly Dictionary<int, string> buildIdUris = new Dictionary<int, string>();
        private readonly List<int> buildIdsAlertedQueued = new List<int>();
        private readonly List<int> buildIdsAlertedDone = new List<int>();
        private IconColour currentIconColour = IconColour.Grey;
        private List<TeamServer> servers;
        private static List<string> hiddenFields;
        private bool showConfiguration;
        private readonly Queue<StatusMessage> statusMessages = new Queue<StatusMessage>(20);

        internal static List<string> HiddenBuilds
        {
            get
            {
                if (hiddenFields == null)
                {
                    LoadHiddenBuilds();
                }

                return hiddenFields;
            }
        }

        public MainBuildList()
        {

            InitializeComponent();
            SetIcon(IconColour.Grey);

            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

            //Set the main title
            LabelMainTitle.Content = ResourcesMain.MainWindow_Title;

            ButtonConfigure.ToolTip = ResourcesMain.MainWindow_ConfigureTooltip;
            ButtonClose.ToolTip = ResourcesMain.MainWindow_CloseTooltip;

            LoadConfiguration(false);
        }



        /// <summary>
        /// A collection of StatusMessages that the main window can add to.
        /// </summary>
        public ObservableCollection<BuildDetail> BuildContent
        {
            get
            {
                if (buildContentView == null)
                {
                    buildContentView = new ObservableCollection<BuildDetail>();
                }
                return buildContentView;
            }
        }

        private void LoadConfiguration(bool reconfigure)
        {
            if (reconfigure)
            {
                buildUpdates.Clear();
                buildContent.Clear();
                lastStatusMessage = new StatusMessage { Message = "" };
                buildIdsAlertedInProgress.Clear();
                buildIdUris.Clear();
                buildIdsAlertedQueued.Clear();
                buildIdsAlertedDone.Clear();
                currentIconColour = IconColour.Grey;
                statusMessages.Clear();
                buildContentView.Clear();
                foreach (var server in servers)
                {
                    server.Dispose();
                }
            }

            StatusMessage initializing = new StatusMessage { Message = "Initializing..." };
            MessageWindow.Show(initializing, 3000);

            if (File.Exists(TeamServer.ServerConfigurationPath))
            {
                try
                {
                    servers = GetServersFromConfigurationFile();

                    if (servers == null || servers.Count == 0)
                    {
                        RunConfigurationWindow();
                        servers = GetServersFromConfigurationFile();
                    }
                }
                catch (Exception)
                {
                    servers = new List<TeamServer>();
                }
            }
            else
            {
                RunConfigurationWindow();
                servers = GetServersFromConfigurationFile();
            }


            //Add version as menu item
            if (!reconfigure)
            {
                MenuItem versionMenuItem = new MenuItem
                                               {
                                                   Header =
                                                       "Version : " + Assembly.GetExecutingAssembly().GetName().Version
                                               };
                NotifyIconMainIcon.ContextMenu.Items.Insert(0, versionMenuItem);
                NotifyIconMainIcon.ContextMenu.Items.Insert(1, new Separator());

                //Add Reconfigure option into menu
                MenuItem reconfigureMenuItem = new MenuItem { Header = "Change Servers" };
                reconfigureMenuItem.Click += reconfigureMenuItem_Click;
                NotifyIconMainIcon.ContextMenu.Items.Insert(2, reconfigureMenuItem);
            }

            //Attach to server events
            foreach (TeamServer server in servers)
            {
                server.OnProjectsUpdated += Server_OnProjectsUpdated;
            }

            //Open xml file of builds to hide
            LoadHiddenBuilds();

            InitializeServers();
        }

        private void reconfigureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FirstRunConfiguration firstRun = new FirstRunConfiguration { Reconfigure = true };
            bool? result = firstRun.ShowDialog();

            if ((result.HasValue) && (result.Value))
            {
                LoadConfiguration(true);
            }
        }

        /// <summary>
        /// Runs the first-run configuration window which lets the user specify a server to connect to.
        /// </summary>
        private void RunConfigurationWindow()
        {
            FirstRunConfiguration firstRun = new FirstRunConfiguration();
            bool? firstRunHasRun = firstRun.ShowDialog();

            //If the user pressed cancel or closed the window, don't let them continue.
            if (firstRunHasRun.HasValue && firstRunHasRun.Value == false)
            {
                Close();
                Environment.Exit(0);
            }
        }


        /// <summary>
        /// Opens the servers.xml file and gets the team servers out.
        /// </summary>
        /// <returns></returns>
        private static List<TeamServer> GetServersFromConfigurationFile()
        {
            return TeamServer.GetTeamServerList();
        }

        private static void LoadHiddenBuilds()
        {
            //Load hidden builds
            if (File.Exists(TeamServer.BuildListConfigurationPath))
            {
                try
                {
                    hiddenFields = TeamServer.GetHiddenBuilds() ?? new List<string>();
                }
                catch (Exception)
                {
                    hiddenFields = new List<string>();
                }
            }
            else
            {
                hiddenFields = new List<string>();
            }
        }

        private void Server_OnProjectsUpdated(object sender, BuildQueryEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    TeamProject project = sender as TeamProject;
                    if (project != null)
                    {
                        lock (buildContent)
                        {
                            //Get current build item list
                            Dictionary<string, BuildDetail> currentBuilds = new Dictionary<string, BuildDetail>();
                            foreach (BuildDetail item in buildContent)
                            {
                                currentBuilds.Add(item.BuildDefinitionUri, item);
                            }
                            //Get updated builds
                            var query = from buildQueryItem in e.BuildQueryResults
                                        where buildQueryItem.Builds.Count() > 0
                                        orderby buildQueryItem.Builds[0].BuildNumber
                                        select buildQueryItem;

                            foreach (BuildQueryResult buildQuery in query)
                            {
                                foreach (BuildDetail item in buildQuery.Builds)
                                {
                                    //If the item doesnt exist or needs updating
                                    if ((currentBuilds.ContainsKey(item.BuildDefinitionUri) && (buildUpdates[item.BuildDefinitionUri] < item.LastChangedOn))
                                        || (!currentBuilds.ContainsKey(item.BuildDefinitionUri)))
                                    {
                                        //Update the last time
                                        if (buildUpdates.ContainsKey(item.BuildDefinitionUri))
                                        {
                                            buildUpdates[item.BuildDefinitionUri] = item.LastChangedOn;
                                        }
                                        else
                                        {
                                            buildUpdates.Add(item.BuildDefinitionUri, item.LastChangedOn);
                                        }

                                        //Add if doesn't exist
                                        if (!currentBuilds.ContainsKey(item.BuildDefinitionUri))
                                        {
                                            currentBuilds.Add(item.BuildDefinitionUri, item);
                                            buildContent.Add(item);

                                            //Add to view if not hidden
                                            if (!HiddenBuilds.Contains(item.BuildDefinitionUri))
                                            {
                                                buildContentView.Add(item);
                                            }
                                        }
                                        else //Update the item
                                        {
                                            currentBuilds[item.BuildDefinitionUri].Status = item.Status;
                                            currentBuilds[item.BuildDefinitionUri].RequestedFor = item.RequestedFor;
                                            currentBuilds[item.BuildDefinitionUri].LogLocation = item.LogLocation;
                                        }

                                        //If the icon is green and a build is failing, set it to red, only if visible
                                        if (!HiddenBuilds.Contains(item.BuildDefinitionUri))
                                        {
                                            if ((currentIconColour != IconColour.Amber) &&
                                                (item.Status == BuildStatus.Failed))
                                            {
                                                SetIcon(IconColour.Red);
                                            }
                                            else if (currentIconColour == IconColour.Grey)
                                            {
                                                SetIcon(IconColour.Green);
                                            }
                                        }

                                        //Update the name to a friendly name
                                        foreach (TeamServer server in servers)
                                        {
                                            string buildName = server.GetDefinitionByUri(item.BuildDefinitionUri).GetFriendlyName();
                                            if (!String.IsNullOrEmpty(buildName))
                                            {
                                                currentBuilds[item.BuildDefinitionUri].BuildNumber = buildName;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //Send alerts for changes
                        AlertChanges(e);
                    }
                }));
        }

        private void AlertChanges(BuildQueryEventArgs buildQueryEventArgs)
        {
            bool iconChanged = false;
            IconColour mainIconColour = IconColour.Green;

            //Cleanup history
            CleanupIds();

            //Find in progress builds
            foreach (BuildQueueQueryResult queueResult in buildQueryEventArgs.BuildQueueQueryResults)
            {
                //Check for cancelled builds
                IconColour? iconColour = CheckCancelledBuilds(queueResult);
                if (iconColour.HasValue)
                {
                    mainIconColour = iconColour.Value;
                    iconChanged = true;
                }

                //Loop through builds with active histories
                foreach (QueuedBuild build in queueResult.Builds.OrderBy(item => item.Id))
                {
                    //Check if build is hidden
                    if (HiddenBuilds.Contains(build.BuildDefinitionUri))
                    {
                        continue;
                    }

                    //Get the friendly names
                    string buildName = GetFriendlyName(build.BuildDefinitionUri);


                    //Adding builds while the tray is running can cause it to fail, only builds which have atleast 1 successfull build will be displayed.
                    if (!String.IsNullOrEmpty(buildName))
                    {
                        //Check if this is an "In Progress" status and has not been displayed before
                        if ((!buildIdsAlertedInProgress.Contains(build.Id)) && (build.Status == QueueStatus.InProgress))
                        {
                            StatusMessage message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now,
                                                            BuildStatus = IconColour.Amber,
                                                            Message =
                                                                String.Format(CultureInfo.CurrentUICulture,
                                                                              ResourcesMain.NotifierWindow_InProgress,
                                                                              build.RequestedBy, buildName)
                                                        };

                            statusMessages.Enqueue(message);
                            mainIconColour = IconColour.Amber;
                            iconChanged = true;
                            buildIdsAlertedInProgress.Add(build.Id);
                            buildIdUris.Add(build.Id, build.BuildDefinitionUri);
                            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title + " - Building";


                            UpdateMainWindowItem(build.BuildDefinitionUri, BuildStatus.InProgress, build.RequestedBy);
                        } //Check if this is an "Queued" status and has not been displayed before
                        else if ((!buildIdsAlertedQueued.Contains(build.Id)) && (build.Status == QueueStatus.Queued))
                        {
                            StatusMessage message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now,
                                                            BuildStatus = IconColour.Amber,
                                                            Message =
                                                                String.Format(CultureInfo.CurrentUICulture,
                                                                              ResourcesMain.NotifierWindow_Queued,
                                                                              build.RequestedBy, buildName)
                                                        };
                            statusMessages.Enqueue(message);
                            buildIdsAlertedQueued.Add(build.Id);
                        }//Check if this is an "Completed" status and has not been displayed before
                        else if ((!buildIdsAlertedDone.Contains(build.Id)) && (build.Status == QueueStatus.Completed))
                        {
                            StatusMessage message = new StatusMessage
                                                        {
                                                            EventDate = DateTime.Now
                                                        };

                            buildIdsAlertedInProgress.Remove(build.Id);
                            buildIdUris.Remove(build.Id);

                            //Get the status from the build log
                            foreach (BuildDetail item in buildContent)
                            {
                                if (item.BuildDefinitionUri == build.BuildDefinitionUri)
                                {
                                    message.BuildStatus = item.Status == BuildStatus.Failed
                                                              ? IconColour.Red
                                                              : IconColour.Green;
                                    message.Message = item.Status == BuildStatus.Failed
                                                          ? String.Format(CultureInfo.CurrentUICulture,
                                                                          ResourcesMain.NotifierWindow_FailedBuild,
                                                                          build.RequestedFor, buildName)
                                                          :
                                                              String.Format(CultureInfo.CurrentUICulture,
                                                                            ResourcesMain.NotifierWindow_BuildPassed,
                                                                            buildName);
                                    message.HyperlinkUri = new Uri(item.LogLocation);
                                    mainIconColour = message.BuildStatus;
                                    iconChanged = true;
                                    break;
                                }
                            }

                            NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

                            statusMessages.Enqueue(message);
                            buildIdsAlertedDone.Add(build.Id);

                        }
                    }
                }
            }

            //Only pop up if new messages
            if (statusMessages.Count > 0)
            {
                lastStatusMessage = statusMessages.Dequeue();
                MessageWindow.Show(lastStatusMessage, 3000);
            }
            //Only update the main icon if its a valid status change
            if (iconChanged)
            {
                SetIcon(mainIconColour);
            }
        }

        private string GetFriendlyName(string buildDefinitionUri)
        {
            string buildName = String.Empty;
            foreach (TeamServer server in servers)
            {
                BuildDefinition definition = server.GetDefinitionByUri(buildDefinitionUri);
                if (definition != null)
                {
                    buildName = definition.GetFriendlyName();
                    if (!String.IsNullOrEmpty(buildName))
                    {
                        break;
                    }
                }
            }

            return buildName;
        }

        private IconColour? CheckCancelledBuilds(BuildQueueQueryResult result)
        {
            IconColour? returnColour = null;

            var buildIdHistories = from build in result.Builds select build.Id;
            List<int> cancelledBuilds = new List<int>();
            foreach (int buildId in buildIdsAlertedInProgress)
            {
                if (!buildIdHistories.Contains(buildId))
                {
                    //Build was cancelled
                    cancelledBuilds.Add(buildId);
                }
            }

            foreach (int buildId in cancelledBuilds)
            {
                buildIdsAlertedInProgress.Remove(buildId);
                string uri = buildIdUris[buildId];
                buildIdUris.Remove(buildId);

                StatusMessage message = new StatusMessage
                {
                    EventDate = DateTime.Now,
                    BuildStatus = IconColour.Green,
                    Message = String.Format(CultureInfo.CurrentUICulture, ResourcesMain.NotifierWindow_Stopped, GetFriendlyName(uri))
                };

                statusMessages.Enqueue(message);
                returnColour = IconColour.Red;
                NotifyIconMainIcon.Text = ResourcesMain.MainWindow_Title;

                UpdateMainWindowItem(uri, BuildStatus.Failed, String.Empty);
            }

            return returnColour;
        }

        /// <summary>
        /// Cleans up the already done queues to save memory
        /// </summary>
        private void CleanupIds()
        {
            lock (buildIdsAlertedDone)
            {
                while (buildIdsAlertedDone.Count > 50)
                {
                    buildIdsAlertedDone.RemoveAt(0);
                }
            }
            lock (buildIdsAlertedInProgress)
            {
                while (buildIdsAlertedInProgress.Count > 50)
                {
                    buildIdsAlertedInProgress.RemoveAt(0);
                }
            }
            lock (buildIdsAlertedQueued)
            {
                while (buildIdsAlertedQueued.Count > 50)
                {
                    buildIdsAlertedQueued.RemoveAt(0);
                }
            }
        }

        private void UpdateMainWindowItem(string buildDefinitionUri, BuildStatus status, string requestedBy)
        {
            foreach (BuildDetail build in buildContent)
            {
                if (build.BuildDefinitionUri == buildDefinitionUri)
                {
                    build.Status = status;
                    if (!String.IsNullOrEmpty(requestedBy))
                    {
                        build.RequestedFor = requestedBy;
                    }
                    break;
                }
            }
        }

        private void InitializeServers()
        {
            IconColour iconColour = IconColour.Grey;

            foreach (TeamServer server in servers)
            {
                IconColour serverStatus = server.GetServerStatus(true);
                if (serverStatus < iconColour)
                {
                    iconColour = serverStatus;
                }
            }

            switch (iconColour)
            {
                case IconColour.Grey:
                    MessageWindow.Show(new StatusMessage { BuildStatus = IconColour.Grey, Message = ResourcesMain.NotifierWindow_NoDefinitions }, 3000);
                    //notifierWindow.Notify();
                    break;
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        internal void SetIcon(IconColour iconColour)
        {
            currentIconColour = iconColour;
            Uri iconUri = new Uri("pack://application:,,,/" + iconColour + ".ico", UriKind.RelativeOrAbsolute);
            NotifyIconMainIcon.Icon = BitmapFrame.Create(iconUri);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!exitButtonClicked)
            {
                // Don't close, just Hide.
                e.Cancel = true;
                // Trying to hide
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                                           (DispatcherOperationCallback)delegate
                                                                                             {
                                                                                                 Hide();
                                                                                                 return null;
                                                                                             }, null);
                return;
            }

            // Actually closing window.
            NotifyIconMainIcon.Visibility = Visibility.Collapsed;

            //Save the hidden builds list
            XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
            FileStream fs = File.Open(TeamServer.BuildListConfigurationPath, FileMode.Create, FileAccess.Write,
                                      FileShare.ReadWrite);
            serializer.Serialize(fs, HiddenBuilds);
            fs.Close();
        }

        private void NotifyIconMainIcon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Show();
                Activate();
            }
        }

        private void NotifyIconOpen_Click(object sender, RoutedEventArgs e)
        {
            Show();
            Activate();
        }

        private void NotifyIconOpenNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(lastStatusMessage.Message))
            {
                MessageWindow.Show(lastStatusMessage, 3000);
            }

        }

        private void NotifyIconExit_Click(object sender, RoutedEventArgs e)
        {
            //Cleare up servers
            foreach (TeamServer server in servers)
            {
                server.Dispose();
            }

            // Close this window.
            exitButtonClicked = true;
            Close();
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;

            if (border != null) border.Background = new SolidColorBrush(Color.FromArgb(31, 0, 0, 0));
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            Border border = sender as Border;

            if (border != null) border.Background = null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BorderMenuForceBuild_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                BuildDetail detail = menuItem.Tag as BuildDetail;
                if (detail != null)
                {
                    foreach (TeamServer server in servers)
                    {
                        if (server.GetDefinitionByUri(detail.BuildDefinitionUri) != null)
                        {
                            //extract the drop location
                            server.QueueBuild(detail.BuildAgentUri, detail.BuildDefinitionUri);
                            break;
                        }
                    }
                }
            }
        }

        private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                lock (HiddenBuilds)
                {
                    BuildDetail detail = checkBox.Tag as BuildDetail;
                    if (detail != null)
                    {
                        if ((checkBox.IsChecked.HasValue) && (checkBox.IsChecked.Value))
                        {
                            if (HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
                                HiddenBuilds.Remove(detail.BuildDefinitionUri);
                            }
                        }
                        else
                        {
                            if (!HiddenBuilds.Contains(detail.BuildDefinitionUri))
                            {
                                HiddenBuilds.Add(detail.BuildDefinitionUri);
                            }
                        }
                    }
                }
            }
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            showConfiguration = !showConfiguration;
            if (showConfiguration)
            {
                ScrollViewerBuildList.ContentTemplate = FindResource("BuildContentTemplateConfigure") as DataTemplate;

                buildContentView.Clear();
                foreach (BuildDetail detail in buildContent)
                {
                    buildContentView.Add(detail);
                }
            }
            else
            {
                ScrollViewerBuildList.ContentTemplate = FindResource("BuildContentTemplate") as DataTemplate;

                foreach (BuildDetail detail in new List<BuildDetail>(buildContentView))
                {
                    if (HiddenBuilds.Contains(detail.BuildDefinitionUri))
                    {
                        buildContentView.Remove(detail);
                    }
                }
            }
        }
    }
}
