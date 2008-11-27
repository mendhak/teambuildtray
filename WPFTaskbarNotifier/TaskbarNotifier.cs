using System;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Windows.Forms;

[assembly:CLSCompliant(true)]
namespace WPFTaskbarNotifier
{
    /// <summary>
    /// A window that slides up from the bottom right corner of the desktop to
    /// notify the user of some event.
    /// </summary>
    public class TaskbarNotifier : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Internal states.
        /// </summary>
        private enum DisplayStates
        {
            Opening,
            Opened,
            Hiding,
            Hidden
        }

        private DispatcherTimer stayOpenTimer;
        private Storyboard storyboard; 
        private DoubleAnimation animation;

        private double hiddenTop;
        private double openedTop;
        private EventHandler arrivedHidden;
        private EventHandler arrivedOpened;

        public TaskbarNotifier()
        {
            Loaded += TaskbarNotifier_Loaded;
        }

        private void TaskbarNotifier_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial settings based on the current screen working area.
            SetInitialLocations(false);

            // Start the window in the Hidden state.
            DisplayState = DisplayStates.Hidden;

            // Prepare the timer for how long the window should stay open.
            stayOpenTimer = new DispatcherTimer();
            stayOpenTimer.Interval = TimeSpan.FromMilliseconds(stayOpenMilliseconds);
            stayOpenTimer.Tick += stayOpenTimer_Elapsed;

            // Prepare the animation to change the Top property.
            animation = new DoubleAnimation();
            Storyboard.SetTargetProperty(animation, new PropertyPath(TopProperty));
            storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.FillBehavior = FillBehavior.Stop;

            // Create the event handlers for when the animation finishes.
            arrivedHidden = Storyboard_ArrivedHidden;
            arrivedOpened = Storyboard_ArrivedOpened;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // For lack of a better way, bring the notifier window to the top whenever
            // Top changes.  Let me know if you have a better way.
            if (e.Property.Name == "Top")
            {
                if (((double)e.NewValue != (double)e.OldValue) && ((double)e.OldValue != hiddenTop))
                {
                    BringToTop();
                }
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            // No title bar or resize border.
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            // Don't show in taskbar.
            ShowInTaskbar = false;

            base.OnInitialized(e);
        }

        private int openingMilliseconds = 1000;
        /// <summary>
        /// The time the TaskbarNotifier window should take to open in milliseconds.
        /// </summary>
        public int OpeningMilliseconds
        {
            get { return openingMilliseconds; }
            set
            {
                openingMilliseconds = value;
                OnPropertyChanged("OpeningMilliseconds");
            }
        }

        private int hidingMilliseconds = 1000;
        /// <summary>
        /// The time the TaskbarNotifier window should take to hide in milliseconds.
        /// </summary>
        public int HidingMilliseconds
        {
            get { return hidingMilliseconds; }
            set
            {
                hidingMilliseconds = value;
                OnPropertyChanged("HidingMilliseconds");
            }
        }

        private int stayOpenMilliseconds = 1000;
        /// <summary>
        /// The time the TaskbarNotifier window should stay open in milliseconds.
        /// </summary>
        public int StayOpenMilliseconds
        {
            get { return stayOpenMilliseconds; }
            set
            {
                stayOpenMilliseconds = value;
                if (stayOpenTimer != null)
                    stayOpenTimer.Interval = TimeSpan.FromMilliseconds(stayOpenMilliseconds);
                OnPropertyChanged("StayOpenMilliseconds");
            }
        }

        private int leftOffset;
        /// <summary>
        /// The space, if any, between the left side of the TaskNotifer window and the right side of the screen.
        /// </summary>
        public int LeftOffset
        {
            get { return leftOffset; }
            set
            {
                leftOffset = value;
                OnPropertyChanged("LeftOffset");
            }
        }

        private DisplayStates displayState;
        /// <summary>
        /// The current DisplayState
        /// </summary>
        private DisplayStates DisplayState
        {
            get
            {
                return displayState;
            }
            set
            {
                if (value != displayState)
                {
                    displayState = value;

                    // Handle the new state.
                    OnDisplayStateChanged();
                }
            }
        }

        private void SetInitialLocations(bool showOpened)
        {
            // Determine screen working area.
            System.Drawing.Rectangle workingArea = new System.Drawing.Rectangle((int)Left, (int)Top, (int)ActualWidth, (int)ActualHeight);
            workingArea = Screen.GetWorkingArea(workingArea);

            // Initialize the window location to the bottom right corner.
            Left = workingArea.Right - ActualWidth - leftOffset;

            // Set the opened and hidden locations.
            hiddenTop = workingArea.Bottom;
            openedTop = workingArea.Bottom - ActualHeight;

            // Set Top based on whether opened or hidden is desired
            if (showOpened)
                Top = openedTop;
            else
                Top = hiddenTop;
        }

        private void BringToTop()
        {
            // Bring this window to the top without making it active.
            Topmost = true;
            Topmost = false;
        }

        private void OnDisplayStateChanged()
        {
            // The display state has changed.

            // Unless the stortboard as already been created, nothing can be done yet.
            if (storyboard == null)
                return;

            // Stop the current animation.
            storyboard.Stop(this);

            // Since the storyboard is reused for opening and closing, both possible
            // completed event handlers need to be removed.  It is not a problem if
            // either of them was not previously set.
            storyboard.Completed -= arrivedHidden;
            storyboard.Completed -= arrivedOpened;

            if (displayState != DisplayStates.Hidden)
            {
                // Unless the window has just arrived at the hidden state, it must be
                // moving, and should be shown.
                BringToTop();
            }

            if (displayState == DisplayStates.Opened)
            {
                // The window has just arrived at the opened state.

                // Because the inital settings of this TaskNotifier depend on the screen's working area,
                // it is best to reset these occasionally in case the screen size has been adjusted.
                SetInitialLocations(true);

                if (!IsMouseOver)
                {
                    // The mouse is not within the window, so start the countdown to hide it.
                    stayOpenTimer.Stop();
                    stayOpenTimer.Start();
                }
            }
            else if (displayState == DisplayStates.Opening)
            {
                // The window should start opening.

                // Make the window visible.
                Visibility = Visibility.Visible;
                BringToTop();

                // Because the window may already be partially open, the rate at which
                // it opens may be a fraction of the normal rate.
                // This must be calculated.
                int milliseconds = CalculateMillseconds(openingMilliseconds, openedTop);

                if (milliseconds < 1)
                {
                    // This window must already be open.
                    DisplayState = DisplayStates.Opened;
                    return;
                }

                // Reconfigure the animation.
                animation.To = openedTop;
                animation.Duration = new Duration(new TimeSpan(0, 0, 0, 0, milliseconds));

                // Set the specific completed event handler.
                storyboard.Completed += arrivedOpened;

                // Start the animation.
                storyboard.Begin(this, true);
            }
            else if (displayState == DisplayStates.Hiding)
            {
                // The window should start hiding.

                // Because the window may already be partially hidden, the rate at which
                // it hides may be a fraction of the normal rate.
                // This must be calculated.
                int milliseconds = CalculateMillseconds(hidingMilliseconds, hiddenTop);

                if (milliseconds < 1)
                {
                    // This window must already be hidden.
                    DisplayState = DisplayStates.Hidden;
                    return;
                }

                // Reconfigure the animation.
                animation.To = hiddenTop;
                animation.Duration = new Duration(new TimeSpan(0, 0, 0, 0, milliseconds));

                // Set the specific completed event handler.
                storyboard.Completed += arrivedHidden;

                // Start the animation.
                storyboard.Begin(this, true);
            }
            else if (displayState == DisplayStates.Hidden)
            {
                // Ensure the window is in the hidden position.
                SetInitialLocations(false);

                // Hide the window.
                Visibility = Visibility.Hidden;
            }
        }

        private int CalculateMillseconds(int totalMillsecondsNormally, double destination)
        {
            if (Top == destination)
            {
                // The window is already at its destination.  Nothing to do.
                return 0;
            }

            double distanceRemaining = Math.Abs(Top - destination);
            double percentDone = distanceRemaining / ActualHeight;

            // Determine the percentage of normal milliseconds that are actually required.
            return (int)(totalMillsecondsNormally * percentDone);
        }

        protected virtual void Storyboard_ArrivedHidden(object sender, EventArgs e)
        {
            // Setting the display state will result in any needed actions.
            DisplayState = DisplayStates.Hidden;
        }

        protected virtual void Storyboard_ArrivedOpened(object sender, EventArgs e)
        {
            // Setting the display state will result in any needed actions.
            DisplayState = DisplayStates.Opened;
        }

        private void stayOpenTimer_Elapsed(Object sender, EventArgs args)
        {
            // Stop the timer because this should not be an ongoing event.
            stayOpenTimer.Stop();

            if (!IsMouseOver)
            {
                // Only start closing the window if the mouse is not over it.
                DisplayState = DisplayStates.Hiding;
            }
        }

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            if (DisplayState == DisplayStates.Opened)
            {
                // When the user mouses over and the window is already open, it should stay open.
                // Stop the timer that would have otherwise hidden it.
                stayOpenTimer.Stop();
            }
            else if ((DisplayState == DisplayStates.Hidden) ||
                     (DisplayState == DisplayStates.Hiding))
            {
                // When the user mouses over and the window is hidden or hiding, it should open. 
                DisplayState = DisplayStates.Opening;
            }

            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            if (DisplayState == DisplayStates.Opened)
            {
                // When the user moves the mouse out of the window, the timer to hide the window
                // should be started.
                stayOpenTimer.Stop();
                stayOpenTimer.Start();
            }

            base.OnMouseEnter(e);
        }

        public void Notify()
        {
            if (DisplayState == DisplayStates.Opened)
            {
                // The window is already open, and should now remain open for another count.
                stayOpenTimer.Stop();
                stayOpenTimer.Start();
            }
            else
            {
                DisplayState = DisplayStates.Opening;
            }
        }

        /// <summary>
        /// Force the window to immediately move to the hidden state.
        /// </summary>
        public void ForceHidden()
        {
            DisplayState = DisplayStates.Hidden;
        }

        #region <<< INotifyPropertyChanged Members >>>

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

    }
}