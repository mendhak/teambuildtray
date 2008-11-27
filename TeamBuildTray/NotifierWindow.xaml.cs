using System;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using Clyde.Rbi.TeamBuildTray.Resources;
using System.Diagnostics;

namespace Clyde.Rbi.TeamBuildTray
{
    public partial class NotifierWindow
    {
        private readonly object lockObject = new object();

        private ObservableCollection<StatusMessage> notifyContent;

        public NotifierWindow()
        {
            InitializeComponent();

            // Insert code required on object creation below this point.
            LabelWindowTitle.Content = ResourcesMain.NotifierWindow_Title;
        }

        /// <summary>
        /// A collection of StatusMessages that the main window can add to.
        /// </summary>
        public ObservableCollection<StatusMessage> NotifyContent
        {
            get
            {
                lock (lockObject)
                {
                    if (notifyContent == null)
                    {
                        // Not yet created.
                        // Create it.
                        notifyContent = new ObservableCollection<StatusMessage>();
                    }
                }

                return notifyContent;
            }
        }

        private void Item_Click(object sender, EventArgs e)
        {
            var hyperlink = sender as Hyperlink;

            if (hyperlink == null)
                return;

            var statusMessage = hyperlink.Tag as StatusMessage;
            if ((statusMessage != null) && (statusMessage.HyperlinkUri != null))
            {
                Process.Start(statusMessage.HyperlinkUri.ToString());
            }
        }

        internal void AddContent(StatusMessage message)
        {
            //Remove old messages
            lock (notifyContent)
            {
                while (notifyContent.Count > 0)
                {
                    notifyContent.RemoveAt(0);
                }
            }

            notifyContent.Add(message);
        }
    }
}