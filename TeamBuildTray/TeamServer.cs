using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ServiceModel;
using Clyde.Rbi.TeamBuildTray.TeamBuildService;
using System.Threading;
using System.Reflection;
using System.IO;

namespace Clyde.Rbi.TeamBuildTray
{
    public class TeamServer
    {
        private Collection<TeamProject> projects = new Collection<TeamProject>();
        public string ServerName { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }

        public static readonly int IntervalTimeInSeconds = 5;
        private Timer queryTimer;

        public Collection<TeamProject> Projects
        {
            get { return projects; }
            set { projects = value; }
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
            foreach (TeamProject project in projects)
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
            if (Monitor.TryEnter(projects))
            {
                try
                {
                    foreach (TeamProject project in projects)
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
                    Monitor.Exit(projects);
                }
            }
        }

        public IconColour GetServerStatus(bool refreshServerList)
        {
            IconColour colour = IconColour.Grey;

            if (refreshServerList)
            {
                //Connect to the server and get a build lsit
                colour = GetBuildList();
            }

            queryTimer = new Timer(QueryTimer_Elapsed, null, new TimeSpan(0), new TimeSpan(0, 0, IntervalTimeInSeconds));

            return colour;
        }

        private IconColour GetBuildList()
        {
            var soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetEndpointAddress());
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

        private EndpointAddress GetEndpointAddress()
        {
            return new EndpointAddress(new Uri(Protocol + "://" + ServerName + ":" + Port + "/build/v2.0/buildservice.asmx"));
        }

        private BuildQueryEventArgs QueryBuilds(IEnumerable<BuildDefinition> buildDefinitions)
        {
            var soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetEndpointAddress());
            var buildDetailSpecs = new List<BuildDetailSpec>();
            var buildQueueSpecs = new List<BuildQueueSpec>();

            //Do queries
            foreach (BuildDefinition definition in buildDefinitions)
            {
                var buildDetailSpec = new BuildDetailSpec
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
            foreach (TeamProject project in projects)
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
                var queueResults =
                    new List<BuildQueueQueryResult>(soapClient.QueryBuildQueue(buildQueueSpecs.ToArray())).AsReadOnly();
                var buildResults =
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
            var soapClient = new BuildServiceSoapClient("BuildServiceSoap", GetEndpointAddress());
            BuildRequest request = new BuildRequest
            {
                BuildAgentUri = agentUri,
                BuildDefinitionUri = buildUri,
                DropLocation = dropLocation
            };

            soapClient.QueueBuild(request, QueueOptions.None);
        }
    }
}