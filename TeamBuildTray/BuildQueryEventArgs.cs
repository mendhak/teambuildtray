using System;
using System.Collections.ObjectModel;
using Clyde.Rbi.TeamBuildTray.TeamBuildService;

namespace Clyde.Rbi.TeamBuildTray
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public sealed class BuildQueryEventArgs : EventArgs
    {
        public ReadOnlyCollection<BuildQueryResult> BuildQueryResults { get; set; }
        public ReadOnlyCollection<BuildQueueQueryResult> BuildQueueQueryResults { get; set; }
    }
}