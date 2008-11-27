using System;
using System.Collections.ObjectModel;
using TeamBuildTray.TeamBuildService;

namespace TeamBuildTray
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