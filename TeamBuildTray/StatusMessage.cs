using System;

namespace Clyde.Rbi.TeamBuildTray
{
    public class StatusMessage
    {
        public IconColour BuildStatus { get; set; }
        public DateTime EventDate { get; set; }
        public string Message { get; set; }
        public Uri HyperlinkUri { get; set; }

        public string EventDateString
        {
            get
            {
                return EventDate.ToLongTimeString();
            }
        }

        public StatusMessage()
        {
            EventDate = DateTime.Now;
        }
    }
}
