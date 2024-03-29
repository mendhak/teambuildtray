﻿using TeamBuildTray.TeamBuildService;

namespace TeamBuildTray
{
    public static class BuildDefinitionExtension
    {
        public static string GetFriendlyName(this BuildGroupItem definition)
        {
            return GetFriendlyNameFromUri(definition.FullPath);
        }

        public static string GetFriendlyNameFromUri(string uri)
        {
            var splitDefinitionPath = uri.Split('\\');
            return splitDefinitionPath[splitDefinitionPath.Length - 1];
        }
    }
}
