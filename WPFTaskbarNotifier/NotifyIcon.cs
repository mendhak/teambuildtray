using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;


/// Used with permission from Mariano Omar Rodriguez
/// http://weblogs.asp.net/marianor/archive/2007/10/15/a-wpf-wrapper-around-windows-form-notifyicon.aspx

namespace WPFTaskbarNotifier
{
    [ContentProperty("Text")]
    [DefaultEvent("MouseDoubleClick")]
    public class NotifyIcon : FrameworkElement, IAddChild
    {
        #region Events

        public new static readonly RoutedEvent MouseDownEvent = EventManager.RegisterRoutedEvent(
            "MouseDown", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(NotifyIcon));

        public new static readonly RoutedEvent MouseUpEvent = EventManager.RegisterRoutedEvent(
            "MouseUp", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(NotifyIcon));

        public static readonly RoutedEvent MouseClickEvent = EventManager.RegisterRoutedEvent(
            "MouseClick", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(NotifyIcon));

        public static readonly RoutedEvent MouseDoubleClickEvent = EventManager.RegisterRoutedEvent(
            "MouseDoubleClick", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(NotifyIcon));

        public new static readonly RoutedEvent MouseMoveEvent = EventManager.RegisterRoutedEvent(
            "MouseMove", RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(NotifyIcon));
        #endregion

        #region Dependency properties

        public static readonly DependencyProperty BalloonTipIconProperty =
            DependencyProperty.Register("BalloonTipIcon", typeof(BalloonTipIcon), typeof(NotifyIcon));

        public static readonly DependencyProperty BalloonTipTextProperty =
            DependencyProperty.Register("BalloonTipText", typeof(string), typeof(NotifyIcon));

        public static readonly DependencyProperty BalloonTipTitleProperty =
            DependencyProperty.Register("BalloonTipTitle", typeof(string), typeof(NotifyIcon));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(ImageSource), typeof(NotifyIcon));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(NotifyIcon));

        #endregion

        Forms.NotifyIcon notifyIcon;
        bool initialized;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            InitializeNotifyIcon();
            Dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
        }

        private void OnDispatcherShutdownStarted(object sender, EventArgs e)
        {
            notifyIcon.Dispose();
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new Forms.NotifyIcon { Text = Text, Icon = FromImageSource(Icon), Visible = FromVisibility(Visibility) };

            notifyIcon.MouseDown += OnMouseDown;
            notifyIcon.MouseUp += OnMouseUp;
            notifyIcon.MouseClick += OnMouseClick;
            notifyIcon.MouseDoubleClick += OnMouseDoubleClick;
            notifyIcon.MouseMove += OnMouseMove;

            initialized = true;
        }

        private void OnMouseMove(object sender, Forms.MouseEventArgs e)
        {
            OnRaiseEvent(MouseMoveEvent, new MouseButtonEventArgs(
                InputManager.Current.PrimaryMouseDevice, 0, MouseButton.Left));
        }

        private void OnMouseDown(object sender, Forms.MouseEventArgs e)
        {
            OnRaiseEvent(MouseDownEvent, new MouseButtonEventArgs(
                InputManager.Current.PrimaryMouseDevice, 0, ToMouseButton(e.Button)));
        }

        private void OnMouseUp(object sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                ShowContextMenu();
            }
            OnRaiseEvent(MouseUpEvent, new MouseButtonEventArgs(
                InputManager.Current.PrimaryMouseDevice, 0, ToMouseButton(e.Button)));
        }

        private void ShowContextMenu()
        {
            if (ContextMenu != null)
            {
                ContextMenuService.SetPlacement(ContextMenu, PlacementMode.MousePoint);
                ContextMenu.IsOpen = true;
            }
        }

        private void OnMouseDoubleClick(object sender, Forms.MouseEventArgs e)
        {
            OnRaiseEvent(MouseDoubleClickEvent, new MouseButtonEventArgs(
                InputManager.Current.PrimaryMouseDevice, 0, ToMouseButton(e.Button)));
        }

        private void OnMouseClick(object sender, Forms.MouseEventArgs e)
        {
            OnRaiseEvent(MouseClickEvent, new MouseButtonEventArgs(
                InputManager.Current.PrimaryMouseDevice, 0, ToMouseButton(e.Button)));
        }

        private void OnRaiseEvent(RoutedEvent handler, RoutedEventArgs e)
        {
            e.RoutedEvent = handler;
            RaiseEvent(e);
        }

        public BalloonTipIcon BalloonTipIcon
        {
            get { return (BalloonTipIcon)GetValue(BalloonTipIconProperty); }
            set { SetValue(BalloonTipIconProperty, value); }
        }

        public string BalloonTipText
        {
            get { return (string)GetValue(BalloonTipTextProperty); }
            set { SetValue(BalloonTipTextProperty, value); }
        }

        public string BalloonTipTitle
        {
            get { return (string)GetValue(BalloonTipTitleProperty); }
            set { SetValue(BalloonTipTitleProperty, value); }
        }

        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (initialized)
            {
                switch (e.Property.Name)
                {
                    case "Icon":
                        notifyIcon.Icon = FromImageSource(Icon);
                        break;
                    case "Text":
                        notifyIcon.Text = Text;
                        break;
                    case "Visibility":
                        notifyIcon.Visible = FromVisibility(Visibility);
                        break;
                }
            }
        }

        public void ShowBalloonTip(int timeout)
        {
            notifyIcon.BalloonTipTitle = BalloonTipTitle;
            notifyIcon.BalloonTipText = BalloonTipText;
            notifyIcon.BalloonTipIcon = (Forms.ToolTipIcon)BalloonTipIcon;
            notifyIcon.ShowBalloonTip(timeout);
        }

        public void ShowBalloonTip(int timeout, string tipTitle, string tipText, BalloonTipIcon tipIcon)
        {
            notifyIcon.ShowBalloonTip(timeout, tipTitle, tipText, (Forms.ToolTipIcon)tipIcon);
        }

        public event MouseButtonEventHandler MouseClick
        {
            add { AddHandler(MouseClickEvent, value); }
            remove { RemoveHandler(MouseClickEvent, value); }
        }

        public event MouseButtonEventHandler MouseDoubleClick
        {
            add { AddHandler(MouseDoubleClickEvent, value); }
            remove { RemoveHandler(MouseDoubleClickEvent, value); }
        }

        public new event MouseButtonEventHandler MouseDown
        {
            add { AddHandler(MouseDownEvent, value); }
            remove { RemoveHandler(MouseDownEvent, value); }
        }

        public new event MouseButtonEventHandler MouseUp
        {
            add { AddHandler(MouseUpEvent, value); }
            remove { RemoveHandler(MouseUpEvent, value); }
        }

        public new event MouseButtonEventHandler MouseMove
        {
            add { AddHandler(MouseMoveEvent, value); }
            remove { RemoveHandler(MouseMoveEvent, value); }
        }

        #region IAddChild Members

        void IAddChild.AddChild(object value)
        {
            throw new InvalidOperationException();
        }

        void IAddChild.AddText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            Text = text;
        }

        #endregion

        #region Conversion members

        private static Drawing.Icon FromImageSource(ImageSource icon)
        {
            if (icon == null)
            {
                return null;
            }
            Uri iconUri = new Uri(icon.ToString());
            return new Drawing.Icon(Application.GetContentStream(iconUri).Stream);
        }

        private static bool FromVisibility(Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        private static MouseButton ToMouseButton(Forms.MouseButtons button)
        {
            switch (button)
            {
                case Forms.MouseButtons.Left:
                    return MouseButton.Left;
                case Forms.MouseButtons.Right:
                    return MouseButton.Right;
                case Forms.MouseButtons.Middle:
                    return MouseButton.Middle;
                case Forms.MouseButtons.XButton1:
                    return MouseButton.XButton1;
                case Forms.MouseButtons.XButton2:
                    return MouseButton.XButton2;
            }
            throw new InvalidOperationException();
        }

        #endregion
    }
}