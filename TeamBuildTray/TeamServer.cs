using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ServiceModel;
using TeamBuildTray.TeamBuildService;
using System.Threading;
using System.Reflection;
using System.IO;
using TeamBuildTray.CommonStructureService;

namespace TeamBuildTray
{
    public class TeamServer
    {
        private Timer queryTimer;
        public static readonly int IntervalTimeInSeconds = 5;


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


        public static string ServerConfigurationPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "servers.xml");
            }
        }

        public static string BuildListConfigurationPath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "hiddenbuilds.xml");
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
            var soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetBuildEndpointAddress());
            if (String.Compare(Protocol, "https", StringComparison.OrdinalIgnoreCase) == 0)
            {
                ((BasicHttpBinding)soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.Transport;
            }
            else
            {
                ((BasicHttpBinding)soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
            }

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

        private EndpointAddress GetBuildEndpointAddress()
        {
            return new EndpointAddress(new Uri(Protocol + "://" + ServerName + ":" + Port + "/build/v2.0/buildservice.asmx"));
        }

        private BuildQueryEventArgs QueryBuilds(IEnumerable<BuildDefinition> buildDefinitions)
        {
            BuildServiceSoapClient soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetBuildEndpointAddress());
            if (String.Compare(Protocol, "https", StringComparison.OrdinalIgnoreCase) == 0)
            {
                ((BasicHttpBinding)soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.Transport;
            }
            else
            {
                ((BasicHttpBinding)soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
            }

            List<BuildDetailSpec> buildDetailSpecs = new List<BuildDetailSpec>();
            List<BuildQueueSpec> buildQueueSpecs = new List<BuildQueueSpec>();

            //Do queries
            foreach (BuildDefinition definition in buildDefinitions)
            {
                BuildDetailSpec buildDetailSpec = new BuildDetailSpec
                {
                    BuildNumber = "*",
                    DefinitionPath = definition.FullPath,
                    DefinitionSpec = new BuildDefinitionSpec { FullPath = definition.FullPath },
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
                            FullPath = "\\" + project.ProjectName + "\\*"
                        },
                        Options = QueryOptions.All,
                        StatusFlags = QueueStatus.Completed | QueueStatus.InProgress | QueueStatus.Queued
                    });
                }
            }

            try
            {
                ReadOnlyCollection<BuildQueueQueryResult> queueResults =
                    new List<BuildQueueQueryResult>(soapClient.QueryBuildQueue(buildQueueSpecs.ToArray())).AsReadOnly();
                ReadOnlyCollection<BuildQueryResult> buildResults =
                    new List<BuildQueryResult>(soapClient.QueryBuilds(buildDetailSpecs.ToArray())).AsReadOnly();

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

        public void QueueBuild(string agentUri, string buildUri, string dropLocation)
        {
            BuildServiceSoapClient soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetBuildEndpointAddress());
            BuildRequest request = new BuildRequest
            {
                BuildAgentUri = agentUri,
                BuildDefinitionUri = buildUri,
                DropLocation = dropLocation
            };

            soapClient.QueueBuild(request, QueueOptions.None);
        }

        public static List<ProjectInfo> GetProjectList(string protocol, string serverName, int port)
        {
            try
            {
                ClassificationSoapClient soapClient = new ClassificationSoapClient("ClassificationSoap",
                                                                                   new EndpointAddress(
                                                                                       new Uri(protocol + "://" +
                                                                                               serverName +
                                                                                               ":" + port +
                                                                                               "/services/v1.0/commonstructureservice.asmx")));

                if (String.Compare(protocol, "https", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ((BasicHttpBinding) soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.Transport;
                    
                }
                else
                {
                    ((BasicHttpBinding)soapClient.Endpoint.Binding).Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
                }

                return soapClient.ListProjects();
            }
            catch
            {
                return new List<ProjectInfo>();
            }
        }
    }
}