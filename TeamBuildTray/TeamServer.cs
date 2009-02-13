using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ServiceModel;
using TeamBuildTray.TeamBuildService;
using System.Threading;
using System.IO;
using TeamBuildTray.CommonStructureService;
using System.Xml.Serialization;

namespace TeamBuildTray
{
    public class TeamServer : IDisposable
    {
        private Timer queryTimer;
        public static readonly int IntervalTimeInSeconds = 5;
        private bool disposed;


        public string ServerName
        {
            get;
            set;
        }

        public int Port
        {
            get;
            set;
        }

        public string Protocol
        {
            get;
            set;
        }


        public Collection<TeamProject> Projects
        {
            get;
            set;
        }

        public static string ApplicationConfigurationPath
        {
            get
            {
                string applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamBuildTray");
                if (!Directory.Exists(applicationDataPath))
                {
                    Directory.CreateDirectory(applicationDataPath);
                }

                return applicationDataPath;
            }
        }

        public static string ServerConfigurationPath
        {
            get
            {
                return Path.Combine(ApplicationConfigurationPath, "servers.xml");
            }
        }

        public static string BuildListConfigurationPath
        {
            get
            {
                return Path.Combine(ApplicationConfigurationPath, "hiddenbuilds.xml");
            }
        }


        public BuildDefinition GetDefinitionByUri(string uri)
        {
            foreach (TeamProject project in Projects)
            {
                if (project.BuildDefinitions.ContainsKey(uri))
                {
                    return project.BuildDefinitions[uri];
                }
            }

            return null;
        }

        void QueryTimer_Elapsed(object sender)
        {
            if (Monitor.TryEnter(Projects))
            {
                try
                {
                    foreach (TeamProject project in Projects)
                    {
                        //Fire update event
                        if (OnProjectsUpdated != null)
                        {
                            OnProjectsUpdated(project, QueryBuilds(project.BuildDefinitions.Values));
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(Projects);
                }
            }
        }

        public IconColour GetServerStatus(bool refreshServerList)
        {
            IconColour colour = IconColour.Grey;

            if (refreshServerList)
            {
                //Connect to the server and get a build list
                colour = GetBuildList();
            }

            queryTimer = new Timer(QueryTimer_Elapsed, null, new TimeSpan(0), new TimeSpan(0, 0, IntervalTimeInSeconds));

            return colour;
        }

        private IconColour GetBuildList()
        {
            var soapClient = new BuildServiceSoapClient(GetBinding(Protocol, "BuildServiceSoap"), GetBuildEndpointAddress());


            bool projectsFound = false;
            try
            {
                foreach (TeamProject project in Projects)
                {
                    BuildGroupItemSpec spec = new BuildDefinitionSpec
                    {
                        FullPath =
                            String.Format(CultureInfo.InvariantCulture, "\\{0}\\*",
                                          project.ProjectName)
                    };

                    BuildGroupQueryResult[] buildGroupQueryResults = soapClient.QueryBuildGroups(new[] { spec });

                    foreach (BuildGroupQueryResult result in buildGroupQueryResults)
                    {
                        //Add the build agents
                        project.BuildAgents.Clear();
                        foreach (BuildAgent agent in result.Agents)
                        {
                            project.BuildAgents.Add(agent);
                        }

                        //Add the build definitions
                        project.BuildDefinitions.Clear();
                        foreach (BuildDefinition definition in result.Definitions)
                        {
                            project.BuildDefinitions.Add(definition.Uri, definition);
                        }

                        //Fire update event
                        if (OnProjectsUpdated != null)
                        {
                            OnProjectsUpdated(project, QueryBuilds(project.BuildDefinitions.Values));
                        }

                        if (project.BuildDefinitions.Count > 0)
                        {
                            projectsFound = true;
                        }
                    }
                }

                if (projectsFound)
                {
                    return IconColour.Green;
                }
                return IconColour.Grey;
            }
            catch
            {
                return IconColour.Red;
            }
        }

        public event EventHandler<BuildQueryEventArgs> OnProjectsUpdated;


        private static EndpointAddress GetProjectListEndpointAddress(string serverName, string port, string protocol)
        {
            return new EndpointAddress(new Uri(protocol + "://" +
                serverName +
                ":" + port +
                "/services/v1.0/commonstructureservice.asmx"));
        }

        private EndpointAddress GetBuildEndpointAddress()
        {
            return new EndpointAddress(new Uri(Protocol + "://" + ServerName + ":" + Port + "/build/v2.0/buildservice.asmx"));
        }

        public QueuedBuild GetBuildStatus(int id)
        {
            using (BuildServiceSoapClient soapClient = new BuildServiceSoapClient(GetBinding(Protocol, "BuildServiceSoap"), GetBuildEndpointAddress()))
            {
                var buildResut = soapClient.QueryBuildQueueById(new[] {id}, QueryOptions.All).Builds[0];

                try
                {
                    soapClient.Close();
                }
                catch (Exception)
                {
                    soapClient.Abort();
                }

                return buildResut;
            }
        }

        private BuildQueryEventArgs QueryBuilds(IEnumerable<BuildDefinition> buildDefinitions)
        {
            using (BuildServiceSoapClient soapClient = new BuildServiceSoapClient(GetBinding(Protocol, "BuildServiceSoap"), GetBuildEndpointAddress()))
            {
                List<BuildDetailSpec> buildDetailSpecs = new List<BuildDetailSpec>();
                List<BuildQueueSpec> buildQueueSpecs = new List<BuildQueueSpec>();

                //Do queries
                foreach (BuildDefinition definition in buildDefinitions)
                {
                    BuildDetailSpec buildDetailSpec = new BuildDetailSpec
                                                          {
                                                              BuildNumber = "*",
                                                              DefinitionPath = definition.FullPath,
                                                              DefinitionSpec =
                                                                  new BuildDefinitionSpec { FullPath = definition.FullPath },
                                                              MaxBuildsPerDefinition = 1,
                                                              MinChangedTime = DateTime.MinValue,
                                                              MinFinishTime = DateTime.MinValue,
                                                              QueryOrder = BuildQueryOrder.FinishTimeDescending
                                                          };

                    buildDetailSpecs.Add(buildDetailSpec);
                }

                //Generate agent specs
                foreach (TeamProject project in Projects)
                {
                    foreach (BuildAgent agent in project.BuildAgents)
                    {
                        buildQueueSpecs.Add(new BuildQueueSpec
                                                {
                                                    AgentSpec = new BuildAgentSpec
                                                                    {
                                                                        FullPath = agent.FullPath,
                                                                        MachineName = agent.MachineName,
                                                                        Port = agent.Port
                                                                    },
                                                    CompletedAge = 300,
                                                    DefinitionSpec = new BuildDefinitionSpec
                                                                         {
                                                                             FullPath =
                                                                                 "\\" + project.ProjectName + "\\*"
                                                                         },
                                                    Options = QueryOptions.All,
                                                    StatusFlags =
                                                        QueueStatus.Completed | QueueStatus.InProgress |
                                                        QueueStatus.Queued
                                                });
                    }
                }

                try
                {
                    ReadOnlyCollection<BuildQueueQueryResult> queueResults =
                        new List<BuildQueueQueryResult>(soapClient.QueryBuildQueue(buildQueueSpecs.ToArray())).
                            AsReadOnly();
                    ReadOnlyCollection<BuildQueryResult> buildResults =
                        new List<BuildQueryResult>(soapClient.QueryBuilds(buildDetailSpecs.ToArray())).AsReadOnly();

                    try
                    {
                        soapClient.Close();
                    }
                    catch (Exception)
                    {
                        soapClient.Abort();
                    }

                    return new BuildQueryEventArgs
                               {
                                   BuildQueryResults = buildResults,
                                   BuildQueueQueryResults = queueResults
                               };

                }
                catch
                {
                    return null;
                }

            }

        }

        public void QueueBuild(string agentUri, string buildUri, string dropLocation)
        {
            BuildServiceSoapClient soapClient = new BuildServiceSoapClient(GetBinding(Protocol, "BuildServiceSoap"), GetBuildEndpointAddress());


            BuildRequest request = new BuildRequest
            {
                BuildAgentUri = agentUri,
                BuildDefinitionUri = buildUri,
                DropLocation = dropLocation
            };

            soapClient.QueueBuild(request, QueueOptions.None);
        }

        public static ReadOnlyCollection<ProjectInfo> GetProjectList(string protocol, string serverName, int port)
        {
            try
            {


                ClassificationSoapClient soapClient = new ClassificationSoapClient(GetBinding(protocol, "ClassificationSoap"),
                                                                                    GetProjectListEndpointAddress(serverName, port.ToString(CultureInfo.InvariantCulture), protocol));


                return soapClient.ListProjects().AsReadOnly();
            }
            catch
            {
                return new List<ProjectInfo>().AsReadOnly();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    queryTimer.Dispose();
                }

                disposed = true;
            }
        }


        /// <summary>
        /// Creates and returns a BasicHttpBinding object with a security mode and end point configuration name specified.
        /// </summary>
        /// <param name="protocol"></param>
        /// <param name="endpointConfigurationName"></param>
        /// <returns></returns>
        private static BasicHttpBinding GetBinding(string protocol, string endpointConfigurationName)
        {
            BasicHttpBinding basicBinding = new BasicHttpBinding();

            if (String.Compare(protocol, "https", StringComparison.OrdinalIgnoreCase) == 0)
            {
                basicBinding.Security.Mode = BasicHttpSecurityMode.Transport;
                basicBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Ntlm;

            }
            else
            {
                basicBinding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
                basicBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Ntlm;
            }

            basicBinding.Name = endpointConfigurationName;

            return basicBinding;
        }


        /// <summary>
        /// Gets a list of configured team servers from the servers XML file.
        /// </summary>
        /// <returns></returns>
        public static List<TeamServer> GetTeamServerList()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<TeamServer>));
            FileStream fs = File.OpenRead(ServerConfigurationPath);
            List<TeamServer> teamServers = serializer.Deserialize(fs) as List<TeamServer>;
            fs.Close();
            return teamServers;
        }


        /// <summary>
        /// Gets a list of the hidden builds from the hidden builds XML file.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetHiddenBuilds()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
            FileStream fs = File.OpenRead(BuildListConfigurationPath);
            List<string> hiddenFields = serializer.Deserialize(fs) as List<string>;
            fs.Close();
            return hiddenFields;
        }

    }
}
