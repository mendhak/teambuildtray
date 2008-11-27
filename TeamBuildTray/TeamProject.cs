using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Serialization;
using TeamBuildTray.TeamBuildService;
using System.Globalization;

namespace TeamBuildTray
{
    public class TeamProject
    {
        private readonly Collection<BuildAgent> buildAgents = new Collection<BuildAgent>();
        private readonly Dictionary<string, BuildDefinition> buildDefinitions = new Dictionary<string, BuildDefinition>();
        public string ProjectName { get; set; }

        [XmlIgnore]
        public Dictionary<string, BuildDefinition> BuildDefinitions
        {
            get { return buildDefinitions; }
        }

        [XmlIgnore]
        public Collection<BuildAgent> BuildAgents
        {
            get { return buildAgents; }
        }

        public Dictionary<DateTime, StatusMessage> GetAgentMessages(DateTime since)
        {
            var messages = new Dictionary<DateTime, StatusMessage>();
            foreach (BuildAgent agent in buildAgents)
            {
                int splitLocation = agent.StatusMessage.LastIndexOf(" on ", StringComparison.OrdinalIgnoreCase);
                string message = agent.StatusMessage.Substring(0, splitLocation);
                DateTime date = DateTime.Parse(agent.StatusMessage.Substring(splitLocation + 4), CultureInfo.CurrentCulture);

                if (date >= since)
                {
                    messages.Add(date, new StatusMessage {EventDate = date, Message = message});
                }
            }

            return messages;
        }
    }
}