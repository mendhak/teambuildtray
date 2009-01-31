// MessageWindow Class
// William Eddins - http://ascendedguard.com
// http://ascendedguard.com/2007/08/example-message-window.html
//
// A window for displaying a short message on the screen, with
// intentions of no interaction, and disappearing after a given time.
// 8/1/07 - Initial Version
// 8/13/07 - Show added to imitate MessageBox.
//         - Loaded event removed, replaced with an invoke, which allows for changing the left and top manually.
//         - Removed the need for private Timer variables, removed DoneClosingTimer (replaced with Storyboard.Completed)

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Timers;

namespace TeamBuildTray
{
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>

    public partial class MessageWindow : Window
    {
        //Delegate used for invoking anonymous functions
        delegate void VoidDelegate();

        public MessageWindow(String message, double duration)
        {
            InitializeComponent();

            //Message to be displayed in the window
            Message.Content = message;

            //Begin closing the window after the specified duration has elapsed.
            Timer closeTimer = new System.Timers.Timer(duration);
            closeTimer.Elapsed += new System.Timers.ElapsedEventHandler(closeTimer_Elapsed);
            closeTimer.Start();

            //This cannot be in the constructor directly, because the ActualWidth of items
            //does not get set until AFTER the constructor. Since I couldn't get the Initialize
            //event to fire, this method seems to work good.
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new VoidDelegate(delegate
            {
                this.Width = Message.ActualWidth + 50;

                //Set the default placement to the bottom right corner, above the Taskbar.
                this.Left = System.Windows.SystemParameters.WorkArea.Right - this.Width - 20;
                this.Top = System.Windows.SystemParameters.WorkArea.Bottom - this.Height - 20;
            }));
        }

        void closeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ((Timer)sender).Stop();

            //We must begin the storyboard on the main window thread.
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new VoidDelegate(delegate
            {
                Storyboard story = (Storyboard)this.FindResource("FadeAway");
                story.Completed += new EventHandler(story_Completed);
                this.BeginStoryboard(story);
            }));
        }

        /// <summary>
        /// Closes the window after we're done fading out.
        /// </summary>
        void story_Completed(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new VoidDelegate(Close));
        }

        #region Static Functions (Show)

        /// <summary>
        /// Creates and shows a MessageWindow.
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="duration">Amount of time, in milliseconds, to show the window</param>
        public static void Show(String message, double duration)
        {
            MessageWindow w = new MessageWindow(message, duration);
            w.Show();
        }

        /// <summary>
        /// Shows a message window, focusing on the parent after creation.
        /// </summary>
        /// <param name="message">Message to display</param>
        /// <param name="duration">Amount of time, in milliseconds, to show the window</param>
        /// <param name="parent">Window to send focus to</param>
        public static void Show(String message, double duration, Window parent)
        {
            MessageWindow w = new MessageWindow(message, duration);
            w.Show();
            parent.Focus();
        }

        #endregion
    }

}
