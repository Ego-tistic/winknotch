using System;
using System.Diagnostics;
using System.Collections.Generic;
using MemoryStream = System.IO.MemoryStream;
using System.Threading.Tasks;
using System.Windows.Shapes;
using Windows.Foundation;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: System.Reflection.AssemblyTitle("WinKnotch")]
[assembly: System.Reflection.AssemblyProduct("WinKnotch")]
[assembly: System.Reflection.AssemblyDescription("A customizable media island for Windows")]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.0.0")]

internal sealed class PlayerWindow : Window
{
    private const double SurfaceWidth = 444;
    private const double ClosedWidth = 264;
    private const double ClosedHeight = 37;
    private const double MediaWidth = 350;
    private const double MediaHeight = 213;
    private const double SideMediaWidth = 204;
    private const double SideMediaHeight = 278;
    private const double SideClosedWidth = ClosedHeight;
    private const double SideClosedHeight = ClosedWidth;
    private const double DetachDistance = 72;
    private const double StickyStretch = 41;
    private const double ClockWidth = 304;
    private const double ClockHeight = 254;
    private const double SideClockWidth = ClosedHeight;
    private const double SideClockHeight = 186;
    private const double TopClockWidth = 185;
    private const double SettingsWidth = 350;
    private const double SettingsHeight = 502;
    private const double OpenGap = 12;
    private const double TabGap = 44;
    private const double SideTabGap = 112;
    private const double SurfaceHeight = 546;
    private enum DockEdge { Top, Right, Bottom, Left, Floating }
    private enum PlacementMode { Locked, Edges, Anywhere }
    private readonly Canvas surface = new Canvas { ClipToBounds = true };
    private DockEdge edge = DockEdge.Top;
    private DockEdge lastDrawnEdge = DockEdge.Floating;
    private DockEdge lastDrawnCorner = DockEdge.Floating;
    private double lastDrawnTension = -1;
    private DockEdge rippleEdge = DockEdge.Top;
    private PlacementMode placementMode;
    private Rect monitor;
    private Point anchor;
    private Rect islandFrame;
    private Rect glassFrame;
    private Rect gapFrame;
    private bool hideAutomatically;
    private double sinkProgress;
    private double lastDockActivity;
    private bool dragArmed;
    private bool draggingIsland;
    private double dragPressStarted;
    private double suppressContextMenuUntil;
    private Point lastDragPointer;
    private double lastDragSample;
    private double dragEnergy;
    private double dragPhase;
    private Vector dragOffset;
    private double dragDepth;
    private DockEdge dragCorner = DockEdge.Floating;
    private double cornerDragDepth;
    private double tension;
    private double rippleStarted = double.NegativeInfinity;
    private bool rippleWasActive;
    private double attachStarted = double.NegativeInfinity;
    private bool attachWasActive;
    private DockEdge attachedEdge;
    private readonly AttachmentParticles attachmentParticles = new AttachmentParticles();
    private PlacementControls inlinePlacement;
    private PlacementControls windowPlacement;
    private readonly Border panel = new Border();
    private readonly Grid glassSurface = new Grid { IsHitTestVisible = false };
    private readonly Border glassTint = new Border();
    private readonly Image backdrop = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Border artwork = new Border { IsHitTestVisible = false };
    private readonly Image cover = new Image { Stretch = Stretch.UniformToFill };
    private readonly Image coverWash = new Image
    {
        Stretch = Stretch.UniformToFill, IsHitTestVisible = false, Opacity = 0,
        Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 30 }
    };
    private readonly TextBlock coverPlaceholder = new TextBlock { Text = "♪", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly BackdropLens lens = new BackdropLens();
    private string artworkKey;
    private Color? coverAccent;
    private double nextBackdropFrame;
    private IntPtr overlayHandle;
    private uint recoveryModifiers = 3;
    private uint recoveryKey = 0x49;
    private const int RecoveryHotkeyId = 0x4249;
    private bool recordingShortcut;
    private string shortcutError;
    private readonly Border recoveryFlash = new Border { Background = Brushes.White, Opacity = 0, IsHitTestVisible = false };
    private double revealUntil;
    private double nextFullscreenCheck;
    private readonly Path rim = new Path { IsHitTestVisible = false, StrokeThickness = 1 };
    private readonly EdgeGlare glare = new EdgeGlare();
    private const string SettingsKey = @"Software\BezelPlayer";
    private const string WindowsStartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "BezelPlayer";
    private bool startWithWindows;
    private string startupError;
    private Window settingsWindow;
    private Slider windowOpacity;
    private TextBlock windowOpacityLabel;
    private bool updatingOpacity;
    private bool lightTheme;
    private bool customTheme;
    private Color customThemeColor = Color.FromRgb(82, 109, 154);
    private ColorControls inlineColorControls, windowColorControls;
    private ComboBox windowThemeChoice;
    private bool updatingColor;
    private double glassOpacity = 0.92;
    private double islandScale = 1;
    private bool updatingSize;
    private static readonly string[] AlarmSounds = { "Soft chime", "Bubble", "Little bell", "Gentle pulse", "Silent" };
    private int alarmSound;
    private readonly System.Media.SoundPlayer alarmPlayer = new System.Media.SoundPlayer();
    private MemoryStream alarmAudio;
    private double alarmSoundEndsAt = double.NegativeInfinity;
    private bool alarmRinging;
    private bool controllerEnabled = true;
    private bool use24HourTime;
    private bool showBatteryPercentage;
    private double nextBatteryPoll;
    private readonly TextBlock batteryStatus = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(45, 0, 45, 18), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Grid compactBattery = CreateBatteryBadge();
    private readonly Grid expandedBattery = CreateBatteryBadge();
    private bool batteryAvailable;
    private double SideClockDisplayHeight { get { return use24HourTime ? 144 : SideClockHeight; } }
    private bool controllerMode;
    private IntPtr controllerPreviousWindow;
    private int controllerIndex = -1;
    private ushort controllerButtons;
    private double controllerChordAt = -1;
    private bool controllerChordUsed;
    private double nextControllerScan;
    private Button dismissAlarm;
    private Button compactDismiss;
    private readonly TextBlock compactTimer = Label(17, "#FFFFFF");
    private bool ringingWakeUp;
    private readonly Button previous;
    private readonly Button next;
    private readonly Button favorite;
    private bool favoriteBusy;
    private bool favoritePolling;
    private bool? favoriteState;
    private string favoriteIdentity;
    private double nextFavoriteRead;
    private readonly Dictionary<string, long> playingSince = new Dictionary<string, long>();
    private GlobalSystemMediaTransportControlsSessionManager manager;
    private GlobalSystemMediaTransportControlsSession session;
    private GlobalSystemMediaTransportControlsSession seekSession;
    private double seekStartValue;
    private long playbackOrder;
    private double lastPoll = -1;
    private double lastShapeProgress = -1;
    private double lastTabProgress = -1;
    private double tabProgress;
    private double lastTabScroll = Double.NegativeInfinity;
    private double lastTabScrollEvent = Double.NegativeInfinity;
    private int tabScrollDelta;
    private double openWidth = MediaWidth;
    private double openHeight = MediaHeight;
    private bool tabsOpen;
    private bool settingsOpen;
    private double measuredMediaWidth = 246;
    private int selectedTab;
    private int highlightedPage = -1;
    private int preferredPage = 1;
    private bool suppressHover;
    private bool outsideClickButtonDown;
    private double soundMenuClosedAt = double.NegativeInfinity;
    private double? pausedSince;
    private string pausedMediaKey;
    private readonly Border gapTarget = new Border { Background = Brush("#01000000"), Cursor = Cursors.Hand };
    private readonly Border tabShelf = new Border { Width = 273, Height = 35, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1) };
    private readonly StackPanel tabBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button[] tabs = new Button[3];
    private readonly StackPanel appsPage = new StackPanel { Margin = new Thickness(19, 17, 19, 44) };
    private readonly TextBox appSearch = new TextBox { Height = 29, Padding = new Thickness(7, 4, 7, 4) };
    private readonly WrapPanel appRows = new WrapPanel();
    private readonly StackPanel dockRows = new StackPanel
    {
        Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 16, 12, 16)
    };
    private readonly Grid dockCanvas = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Border dockShelf = new Border { CornerRadius = new CornerRadius(19), BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    private bool appsDock;
    private double dockScrollTarget;
    private ScrollViewer appScroll;
    private double appDrawerHeight = 354;
    private readonly HashSet<string> selectedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool showAppTitles = true;
    private bool editingApps;
    private bool appsGrid = true;
    private readonly StackPanel appLayoutOptions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 7), Visibility = Visibility.Collapsed };
    private readonly RadioButton appGridChoice = new RadioButton { Content = "Grid", Margin = new Thickness(0, 0, 18, 0) };
    private readonly RadioButton appListChoice = new RadioButton { Content = "List" };
    private readonly RadioButton appDockChoice = new RadioButton { Content = "Dock", Margin = new Thickness(18, 0, 0, 0) };
    private Button drawerGear;
    private Button drawerSearchButton;
    private bool drawerSearchOpen;
    private readonly CheckBox appTitlesToggle = new CheckBox { Content = "Show app titles", Margin = new Thickness(0, 6, 0, 5), Visibility = Visibility.Collapsed };
    private readonly TextBlock appStatus = Label(12, "#FFFFFF");
    private Button chooseAppsButton;
    private bool appsLoading;
    private readonly List<AppEntry> appEntries = new List<AppEntry>();
    private bool appsLoaded;
    private readonly TextBlock compactClock = Label(21, "#FFFFFF");
    private readonly TextBlock largeClock = Label(34, "#FFFFFF");
    private readonly TextBlock dateLabel = Label(13, "#B5B5BE");
    private readonly Stopwatch stopwatch = new Stopwatch();
    private readonly StackPanel clockTool = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock stopwatchDisplay = Label(24, "#FFFFFF");
    private readonly TextBlock alarmNotice = Label(13, "#FFFFFF");
    private int clockToolIndex;
    private DateTime? alarmAt;
    private DateTime? wakeUpAt;
    private readonly TextBlock alarmStatus = Label(12, "#FFFFFF");
    private readonly List<Button> clockButtons = new List<Button>();
    private readonly StackPanel clockPage = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 29, 16, 0) };
    private readonly ScrollViewer settingsPage = new ScrollViewer { Margin = new Thickness(18, 17, 18, 44), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private SettingsCard inlineAppearanceCard;
    private SettingsCard windowAppearanceCard;
    private readonly TextBlock opacityText = Label(12, "#FFFFFF");
    private readonly Slider inlineOpacity = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true };
    private Button themeButton;
    private Button tabsButton;
    private Button settingsButton;
    private bool updatingAppearance;
    private bool hasMedia;
    private double length;
    private double timelineStart;
    private double requestedSeekUntil;
    private double playbackCorrection;
    private double playbackRate = 1;
    private string playbackIdentity;
    private bool refreshing;
    private bool draggingTimeline;
    private bool commandPending;
    private readonly Grid collapsed = new Grid();
    private readonly ScrollingTitle sideMarquee = new ScrollingTitle { Width = 28, Height = 135, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 51, 0, 0), IsHitTestVisible = false };
    private readonly StackPanel sideWave = new StackPanel { Width = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 203, 0, 0), IsHitTestVisible = false };
    private readonly Border[] sideBars = new Border[12];
    private readonly StackPanel expanded = new StackPanel();
    private readonly TextBlock smallTrack = Label(12, "#F5F5F5");
    private readonly TextBlock smallTrackRepeat = Label(12, "#F5F5F5");
    private readonly Canvas marquee = new Canvas { ClipToBounds = true, Height = 20, VerticalAlignment = VerticalAlignment.Center };
    private double marqueeOffset;
    private double marqueeDelay;
    private double marqueeTextWidth;
    private readonly TextBlock title = Label(19, "#FFFFFF");
    private readonly TextBlock artist = Label(12, "#B5B5BE");
    private readonly TextBlock elapsed = Label(11, "#B5B5BE");
    private readonly TextBlock duration = Label(11, "#B5B5BE");
    private readonly Slider timeline = new Slider();
    private readonly Slider volumeSlider = new Slider { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 10, IsMoveToPointEnabled = true, IsEnabled = false };
    private readonly TextBlock speakerIcon = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Width = 24, VerticalAlignment = VerticalAlignment.Center };
    private readonly SystemAudioVolume systemVolume = new SystemAudioVolume();
    private bool updatingVolume;
    private readonly Button play;
    private readonly Border[] bars = new Border[12];
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private double lastFrame;
    private double lastHover = -1;
    private double progress;
    private Point cursor;
    private double position;
    private double waveAmplitude;

    private bool playing;
    private bool updatingTimeline;

    [STAThread]
    private static void Main()
    {
        new Application().Run(new PlayerWindow());
    }

    private PlayerWindow()
    {
        Title = "WinKnotch";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        SourceInitialized += delegate
        {
            overlayHandle = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(overlayHandle);
            source.AddHook(HandleWindowMessage);
            var mouse = new RawInputDevice { UsagePage = 1, Usage = 2, Flags = 0x100, Target = overlayHandle };
            RegisterRawInputDevices(new[] { mouse }, 1, (uint)Marshal.SizeOf(typeof(RawInputDevice)));
        };
        Topmost = true;
        Width = SurfaceWidth;
        Height = SurfaceHeight;
        monitor = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        anchor = new Point(monitor.Width / 2, 0);
        PositionWindow();
        surface.Width = SurfaceWidth;
        surface.Height = SurfaceHeight;
        Content = new Viewbox { Child = surface, Stretch = Stretch.Fill };
        PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs args)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0 || args.Delta == 0) return;
            args.Handled = true;
            double scrollNow = clock.Elapsed.TotalSeconds;
            if (scrollNow - lastTabScrollEvent > .3 || Math.Sign(tabScrollDelta) != Math.Sign(args.Delta)) tabScrollDelta = 0;
            lastTabScrollEvent = scrollNow;
            if (scrollNow - lastTabScroll < .4) { tabScrollDelta = 0; return; }
            tabScrollDelta += args.Delta;
            if (Math.Abs(tabScrollDelta) < 120) return;
            lastTabScroll = scrollNow;
            tabScrollDelta = 0;
            int current = CurrentPage(clock.Elapsed.TotalSeconds);
            int[] pages = { 0, 1, 3 };
            int index = Array.IndexOf(pages, current);
            if (index < 0) index = 0;
            index = (index + (args.Delta < 0 ? 1 : 2)) % pages.Length;
            settingsOpen = false;
            tabsOpen = true;
            SelectTab(pages[index]);
            args.Handled = true;
        };
        MouseWheel += delegate(object sender, MouseWheelEventArgs args)
        {
            if (args.Handled || args.Delta == 0 || settingsOpen || CurrentPage(clock.Elapsed.TotalSeconds) == 3) return;
            DependencyObject source = args.OriginalSource as DependencyObject;
            while (source != null && source != this)
            {
                if (source is Slider || source is TextBox || source is ComboBox || source is ScrollViewer) return;
                var contentSource = source as FrameworkContentElement;
                source = contentSource != null ? contentSource.Parent : VisualTreeHelper.GetParent(source);
            }
            RefreshVolume();
            if (!volumeSlider.IsEnabled) return;
            volumeSlider.Value = Math.Max(0, Math.Min(100, volumeSlider.Value + args.Delta / 120.0 * 5));
            args.Handled = true;
        };



        panel.ContextMenu = new ContextMenu();
        var quit = new MenuItem { Header = "Quit WinKnotch" };
        quit.Click += delegate { Close(); };
        panel.ContextMenu.Items.Add(quit);
        var layers = new Grid();
        glassSurface.Children.Add(backdrop);
        glassSurface.Children.Add(glassTint);
        coverWash.OpacityMask = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Colors.Black, 0),
                new GradientStop(Color.FromArgb(184, 0, 0, 0), 0.40),
                new GradientStop(Colors.Transparent, 0.94)
            }, new Point(0.04, 0.12), new Point(1, 0.92));
        glassSurface.Children.Add(coverWash);
        panel.Child = layers;
        surface.Children.Add(gapTarget);
        surface.Children.Add(glassSurface);
        surface.Children.Add(panel);
        surface.Children.Add(rim);
        surface.Children.Add(glare);
        attachmentParticles.Width = SurfaceWidth;
        attachmentParticles.Height = SurfaceHeight;
        surface.Children.Add(attachmentParticles);

        collapsed.Margin = new Thickness(62, 0, 29, 0);
        collapsed.ColumnDefinitions.Add(new ColumnDefinition());
        collapsed.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        marquee.Margin = new Thickness(0, 0, 9, 0);
        marquee.Children.Add(smallTrack);
        marquee.Children.Add(smallTrackRepeat);
        Canvas.SetTop(smallTrack, 2);
        Canvas.SetTop(smallTrackRepeat, 2);
        collapsed.Children.Add(marquee);
        var wave = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Height = 19 };
        Grid.SetColumn(wave, 1);
        collapsed.Children.Add(wave);
        for (int index = 0; index < bars.Length; index++)
        {
            var bar = new Border
            {
                Width = 2,
                Height = 3,
                CornerRadius = new CornerRadius(1),
                Background = Brushes.White,
                Margin = new Thickness(index == 0 ? 0 : 2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            bars[index] = bar;
            wave.Children.Add(bar);
        }
        layers.Children.Add(collapsed);
        for (int i = 0; i < sideBars.Length; i++)
        {
            sideBars[i] = new Border
            {
                Width = 4, Height = 2, Background = Brushes.White, CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 1.5)
            };
            sideWave.Children.Add(sideBars[i]);
        }
        layers.Children.Add(sideMarquee);
        layers.Children.Add(sideWave);

        expanded.Margin = new Thickness(19, 14, 19, 12);
        title.FontWeight = FontWeights.SemiBold;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(59, 0, 0, 0);
        artist.Margin = new Thickness(59, 3, 0, 0);
        expanded.Children.Add(title);
        expanded.Children.Add(artist);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 4)
        };
        previous = Control("Previous", "⏮", async delegate { await SendCommand(s => s.TrySkipPreviousAsync()); });
        controls.Children.Add(previous);
        play = Control("Play", "▶", async delegate { await SendCommand(s => playing ? s.TryPauseAsync() : s.TryPlayAsync()); });
        controls.Children.Add(play);
        next = Control("Next", "⏭", async delegate { await SendCommand(s => s.TrySkipNextAsync()); });
        controls.Children.Add(next);
        favorite = Control("Favorite track", "☆", ToggleFavorite);
        favorite.IsEnabled = false;
        ToolTipService.SetShowOnDisabled(favorite, true);
        controls.Children.Add(favorite);
        expanded.Children.Add(controls);

        timeline.Minimum = 0;
        timeline.IsMoveToPointEnabled = true;
        timeline.Template = CreateTimelineTemplate(true);
        timeline.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(delegate
        {
            draggingTimeline = true;
            seekSession = session;
            seekStartValue = timeline.Value;
        }));
        timeline.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async delegate(object sender, DragCompletedEventArgs args)
        {
            draggingTimeline = false;
            bool moved = Math.Abs(timeline.Value - seekStartValue) > 0.01;
            bool sameSession = seekSession == session;
            seekSession = null;
            if (!args.Canceled && sameSession && moved) await Seek(timeline.Value);
        }));
        timeline.ValueChanged += async delegate
        {
            if (updatingTimeline) return;
            elapsed.Text = FormatTime(timeline.Value);
            if (!draggingTimeline) await Seek(timeline.Value);
        };
        System.Windows.Automation.AutomationProperties.SetName(timeline, "Playback position");
        expanded.Children.Add(timeline);

        var times = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 2, 0, 0) };
        DockPanel.SetDock(duration, Dock.Right);
        times.Children.Add(elapsed);
        times.Children.Add(duration);
        expanded.Children.Add(times);
        var volumeRow = new DockPanel { Margin = new Thickness(3, 7, 3, 0) };
        volumeSlider.Template = CreateTimelineTemplate(true);
        AutomationProperties.SetName(volumeSlider, "Windows output volume");
        volumeSlider.ValueChanged += delegate
        {
            if (updatingVolume) return;
            UpdateSpeakerIcon(volumeSlider.Value);
            if (!systemVolume.TrySet((float)(volumeSlider.Value / 100))) RefreshVolume();
        };
        volumeRow.Children.Add(speakerIcon);
        volumeRow.Children.Add(volumeSlider);
        expanded.Children.Add(volumeRow);
        layers.Children.Add(expanded);
        var artworkLayers = new Grid();
        artworkLayers.Children.Add(coverPlaceholder);
        artworkLayers.Children.Add(cover);
        artwork.Child = artworkLayers;
        artwork.HorizontalAlignment = HorizontalAlignment.Left;
        artwork.VerticalAlignment = VerticalAlignment.Top;
        layers.Children.Add(artwork);
        InitializeTabs(layers);
        panel.PreviewMouseRightButtonDown += BeginIslandDrag;
        panel.PreviewMouseRightButtonUp += EndIslandDrag;
        panel.ContextMenuOpening += delegate(object sender, ContextMenuEventArgs args)
        {
            if (dragArmed || clock.Elapsed.TotalSeconds < suppressContextMenuUntil) args.Handled = true;
        };
        panel.LostMouseCapture += delegate { if (dragArmed && Mouse.Captured != panel) FinishIslandDrag(); };

        LoadSettings();
        ApplyIslandSize();
        PreviewKeyDown += CaptureRecoveryShortcut;
        Loaded += delegate { RegisterRecoveryShortcut(); };
        Closed += delegate { UnregisterHotKey(overlayHandle, RecoveryHotkeyId); };
        ApplyAppearance();
        ShowIdle("Waiting for playback");
        DrawFrame(0, 0);
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += delegate { Tick(); };
        Loaded += delegate { timer.Start(); };
        Closed += delegate
        {
            timer.Stop();
            alarmPlayer.Stop();
            alarmPlayer.Dispose();
            if (alarmAudio != null) alarmAudio.Dispose();
            lens.Dispose();
            systemVolume.Dispose();
            if (settingsWindow != null) settingsWindow.Close();
        };
        SystemEventsSubscribe();
    }

    private void SystemEventsSubscribe()
    {
        EventHandler changed = delegate { Dispatcher.BeginInvoke(new Action(RefreshMonitor)); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += changed;
        Closed += delegate { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= changed; };
    }

    private void PositionWindow()
    {
        double left = Clamp(anchor.X - Width / 2, monitor.Left, monitor.Right - Width);
        double top = Clamp(anchor.Y - Height / 2, monitor.Top, monitor.Bottom - Height);
        if (edge == DockEdge.Left) left = monitor.Left;
        if (edge == DockEdge.Right) left = monitor.Right - Width;
        if (edge == DockEdge.Top) top = monitor.Top;
        if (edge == DockEdge.Bottom) top = monitor.Bottom - Height;
        Left = Math.Round(left);
        Top = Math.Round(top);
    }

    private void RaiseOverFullscreenWindow()
    {
        if (controllerMode)
        {
            SetWindowPos(overlayHandle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
            return;
        }
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == overlayHandle) return;
        IntPtr display = MonitorFromWindow(foreground, 2);
        if (display != MonitorFromWindow(overlayHandle, 2)) return;
        var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
        NativeRect bounds;
        if (!GetMonitorInfo(display, ref info) || !GetWindowRect(foreground, out bounds)) return;
        const int tolerance = 3;
        if (bounds.Left <= info.Bounds.Left + tolerance && bounds.Top <= info.Bounds.Top + tolerance
            && bounds.Right >= info.Bounds.Right - tolerance && bounds.Bottom >= info.Bounds.Bottom - tolerance)
        {
            const uint noResize = 0x0001, noMove = 0x0002, noActivate = 0x0010, keepOwnerOrder = 0x0200;
            SetWindowPos(overlayHandle, new IntPtr(-1), 0, 0, 0, 0, noResize | noMove | noActivate | keepOwnerOrder);
        }
    }

    private static double Clamp(double value, double low, double high)
    {
        return Math.Max(low, Math.Min(Math.Max(low, high), value));
    }

    private Point ToDesktop(Point local)
    {
        return new Point(local.X + Left, local.Y + Top);
    }

    private Point IslandPoint(Point screen)
    {
        Point local = PointFromScreen(screen);
        return new Point(local.X / islandScale, local.Y / islandScale);
    }

    private void ApplyIslandSize()
    {
        Width = SurfaceWidth * islandScale;
        Height = SurfaceHeight * islandScale;
        PositionWindow();
    }

    private Rect MonitorAt(NativePoint point)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(MonitorFromPoint(point, 2), ref info)) return monitor;
        Point topLeft = ToDesktop(PointFromScreen(new Point(info.Bounds.Left, info.Bounds.Top)));
        Point bottomRight = ToDesktop(PointFromScreen(new Point(info.Bounds.Right, info.Bounds.Bottom)));
        return new Rect(topLeft, bottomRight);
    }

    private void RefreshMonitor()
    {
        NativePoint point;
        if (GetCursorPos(out point)) monitor = MonitorAt(point);
        anchor = new Point(Clamp(anchor.X, monitor.Left, monitor.Right), Clamp(anchor.Y, monitor.Top, monitor.Bottom));
        PositionWindow();
    }

    private void Recenter()
    {
        edge = DockEdge.Top;
        anchor = new Point(monitor.Left + monitor.Width / 2, monitor.Top);
        tension = sinkProgress = 0;
        rippleStarted = double.NegativeInfinity;
        attachStarted = double.NegativeInfinity;
        lastDockActivity = clock.Elapsed.TotalSeconds;
        PositionWindow();
    }

    private void BeginIslandDrag(object sender, MouseButtonEventArgs args)
    {
        if (placementMode == PlacementMode.Locked) return;
        dragPressStarted = clock.Elapsed.TotalSeconds;
        dragArmed = panel.CaptureMouse();
        if (dragArmed) args.Handled = true;
    }

    private Point CompactCenter()
    {
        bool showsClock = !playing && (preferredPage == 1 || ShouldShowClock(clock.Elapsed.TotalSeconds));
        double sideWidth = showsClock ? SideClockWidth : SideClosedWidth;
        switch (edge)
        {
            case DockEdge.Top: return new Point(anchor.X, monitor.Top + ClosedHeight / 2);
            case DockEdge.Bottom: return new Point(anchor.X, monitor.Bottom - ClosedHeight / 2);
            case DockEdge.Left: return new Point(monitor.Left + sideWidth / 2, anchor.Y);
            case DockEdge.Right: return new Point(monitor.Right - sideWidth / 2, anchor.Y);
            default: return anchor;
        }
    }

    private void EndIslandDrag(object sender, MouseButtonEventArgs args)
    {
        if (!dragArmed) return;
        bool wasDragging = draggingIsland;
        FinishIslandDrag();
        args.Handled = true;
        if (wasDragging) suppressContextMenuUntil = clock.Elapsed.TotalSeconds + 0.3;
        else
        {
            panel.ContextMenu.PlacementTarget = panel;
            panel.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            panel.ContextMenu.IsOpen = true;
        }
    }

    private void FinishIslandDrag()
    {
        dragArmed = false;
        if (Mouse.Captured == panel) panel.ReleaseMouseCapture();
        if (draggingIsland && placementMode == PlacementMode.Anywhere && edge == DockEdge.Floating)
        {
            NativePoint point;
            if (!SnapFloatingCorner() && GetCursorPos(out point))
            {
                Point release = ToDesktop(PointFromScreen(new Point(point.X, point.Y)));
                DockEdge nearest = NearestEdge(release, monitor);
                if (EdgeDistance(release, nearest, monitor) <= 18) edge = nearest;
            }
            if (edge != DockEdge.Floating)
            {
                attachedEdge = edge;
                attachStarted = clock.Elapsed.TotalSeconds;
                rippleStarted = double.NegativeInfinity;
            }
        }
        draggingIsland = false;
        lastDockActivity = clock.Elapsed.TotalSeconds;
        PositionWindow();
    }

    private bool SnapFloatingCorner()
    {
        const double reach = 12;
        DockEdge side = Left + islandFrame.Left * islandScale - monitor.Left <= reach ? DockEdge.Left
            : monitor.Right - (Left + islandFrame.Right * islandScale) <= reach ? DockEdge.Right : DockEdge.Floating;
        DockEdge vertical = Top + islandFrame.Top * islandScale - monitor.Top <= reach ? DockEdge.Top
            : monitor.Bottom - (Top + islandFrame.Bottom * islandScale) <= reach ? DockEdge.Bottom : DockEdge.Floating;
        if (side == DockEdge.Floating || vertical == DockEdge.Floating) return false;
        edge = rippleEdge == side ? side : vertical;
        anchor = new Point(
            side == DockEdge.Left ? monitor.Left + ClosedWidth / 2 : monitor.Right - ClosedWidth / 2,
            vertical == DockEdge.Top ? monitor.Top + SideClosedHeight / 2 : monitor.Bottom - SideClosedHeight / 2);
        return true;
    }

    private void DragIsland(NativePoint native, double now)
    {
        if (!dragArmed) return;
        Point pointer = ToDesktop(PointFromScreen(new Point(native.X, native.Y)));
        if (!draggingIsland)
        {
            if (now - dragPressStarted < 0.5) return;
            lastDragPointer = pointer;
            lastDragSample = now;
            dragOffset = CompactCenter() - pointer;
            dragDepth = EdgeDistance(pointer, edge, monitor);
            dragCorner = CornerEdge();
            cornerDragDepth = EdgeDistance(pointer, dragCorner, monitor);
            draggingIsland = true;
            tabsOpen = false;
            settingsOpen = false;
            lastHover = double.NegativeInfinity;
            return;
        }
        double seconds = Math.Max(0.016, now - lastDragSample);
        double speed = (pointer - lastDragPointer).Length / seconds;
        dragEnergy = Math.Max(dragEnergy, Math.Max(0, Math.Min(1, (speed - 650) / 720)));
        lastDragSample = now;
        lastDragPointer = pointer;
        draggingIsland = true;
        tabsOpen = false;
        settingsOpen = false;
        lastHover = double.NegativeInfinity;
        sinkProgress = 0;
        Rect nextMonitor = MonitorAt(native);
        bool changedMonitor = nextMonitor != monitor;
        monitor = nextMonitor;
        Point desired = pointer + dragOffset;
        DockEdge nearest = NearestEdge(pointer, monitor);
        if (placementMode == PlacementMode.Edges)
        {
            if (changedMonitor || edge == DockEdge.Floating
                || EdgeDistance(pointer, nearest, monitor) + 10 < EdgeDistance(pointer, edge, monitor))
                edge = nearest;
        }
        else if (edge != DockEdge.Floating)
        {
            double pull = Math.Max(0, EdgeDistance(pointer, edge, monitor) - dragDepth);
            double stretch = Math.Min(1, pull / DetachDistance);
            tension = StickyStretch * (1 - Math.Pow(1 - stretch, 0.78));
            if (!changedMonitor && pull >= DetachDistance && dragCorner != DockEdge.Floating
                && EdgeDistance(pointer, dragCorner, monitor) - cornerDragDepth < 28)
            {
                edge = dragCorner;
                dragDepth = cornerDragDepth;
                dragCorner = DockEdge.Floating;
                tension = 0;
            }
            else if (pull >= DetachDistance || changedMonitor)
            {
                rippleEdge = edge;
                rippleStarted = now;
                edge = DockEdge.Floating;
                tension = 0;
            }
        }
        anchor = new Point(Clamp(desired.X, monitor.Left, monitor.Right), Clamp(desired.Y, monitor.Top, monitor.Bottom));
        PositionWindow();
    }

    private static DockEdge NearestEdge(Point point, Rect bounds)
    {
        DockEdge nearest = DockEdge.Top;
        foreach (DockEdge candidate in new[] { DockEdge.Right, DockEdge.Bottom, DockEdge.Left })
            if (EdgeDistance(point, candidate, bounds) < EdgeDistance(point, nearest, bounds)) nearest = candidate;
        return nearest;
    }

    private static double EdgeDistance(Point point, DockEdge side, Rect bounds)
    {
        switch (side)
        {
            case DockEdge.Top: return Math.Abs(point.Y - bounds.Top);
            case DockEdge.Right: return Math.Abs(bounds.Right - point.X);
            case DockEdge.Bottom: return Math.Abs(bounds.Bottom - point.Y);
            case DockEdge.Left: return Math.Abs(point.X - bounds.Left);
            default: return 0;
        }
    }

    private void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        if (now >= nextBatteryPoll) { nextBatteryPoll = now + 5; RefreshBattery(); }
        PollController(now);
        double playbackDelta = Math.Max(0, now - lastFrame);
        double delta = Math.Min(playbackDelta, 0.05);
        if (appsDock && !editingApps && appScroll != null)
        {
            dockScrollTarget = Math.Max(0, Math.Min(appScroll.ScrollableWidth, dockScrollTarget));
            double offset = appScroll.HorizontalOffset;
            double next = offset + (dockScrollTarget - offset) * (1 - Math.Exp(-15 * delta));
            appScroll.ScrollToHorizontalOffset(Math.Abs(next - dockScrollTarget) < 0.25 ? dockScrollTarget : next);
        }
        lastFrame = now;

        int page = CurrentPage(now);
        if (highlightedPage != page) UpdateTabAppearance();
        if (page != 3 && editingApps) CloseDrawerSettings();
        double previousWidth = openWidth;
        double previousHeight = openHeight;
        NativePoint screenPoint;
        if (GetCursorPos(out screenPoint))
        {
            DragIsland(screenPoint, now);
            cursor = IslandPoint(new Point(screenPoint.X, screenPoint.Y));
            bool buttonDown = (tabsOpen || settingsOpen) && (GetAsyncKeyState(1) < 0 || GetAsyncKeyState(2) < 0 || GetAsyncKeyState(4) < 0);
            if (buttonDown && !outsideClickButtonDown) DismissTabsAt(cursor);
            outsideClickButtonDown = buttonDown;
            bool nearDock = hideAutomatically && edge != DockEdge.Floating && IsNearDock(cursor);
            bool overIsland = islandFrame.Contains(cursor) || gapFrame.Contains(cursor) || nearDock;
            if (overIsland || draggingIsland || tabsOpen || settingsOpen) lastDockActivity = now;
            if (!overIsland) suppressHover = false;
            else if (!suppressHover && sinkProgress < 0.18 && !draggingIsland && tension < 1)
                lastHover = now;
        }

        double targetWidth = IsSideDock ? SideMediaWidth : measuredMediaWidth;
        double targetHeight = IsSideDock ? SideMediaHeight : MediaHeight;
        openWidth = ResizeToward(openWidth, page == 3 ? (appsDock && !editingApps ? 420 : 340) : page == 1 ? (clockToolIndex >= 2 ? 304 : ClockWidth) : page == 2 ? SettingsWidth : targetWidth, delta);
        double clockHeight = alarmRinging ? 252 : clockToolIndex >= 2 ? 310 : clockToolIndex == 1 ? 218 : 184;
        openHeight = ResizeToward(openHeight, page == 3 ? appDrawerHeight : page == 1 ? clockHeight : page == 2 ? SettingsHeight : targetHeight, delta);
        dragEnergy *= Math.Exp(-4.6 * delta);
        if (!draggingIsland) tension = ResizeToward(tension, 0, delta);
        if (dragEnergy < 0.003) dragEnergy = 0;
        dragPhase = (dragPhase + (16 + 5 * dragEnergy) * delta) % (2 * Math.PI);

        bool hoverOpen = now - lastHover < 0.23 && now - attachStarted >= 0.52
            && (!alarmRinging || progress > 0.75);
        bool open = !draggingIsland && (controllerMode || now < revealUntil || tabsOpen || settingsOpen || hoverOpen
            || timeline.IsMouseCaptureWithin || volumeSlider.IsMouseCaptureWithin || panel.ContextMenu.IsOpen);
        progress = Math.Max(0, Math.Min(1, progress + delta * (open ? 2.65 : -3.2)));
        tabProgress = Math.Max(0, Math.Min(1, tabProgress + delta * (tabsOpen ? 5 : -5)));
        bool sink = hideAutomatically && edge != DockEdge.Floating && !dragArmed && !open && settingsWindow == null
            && progress <= 0.01 && now - lastDockActivity > 1.35;
        sinkProgress = Math.Max(0, Math.Min(1, sinkProgress + delta * (sink ? 3.0 : -4.7)));
        if (now - lastPoll >= 0.6)
        {
            lastPoll = now;
            RefreshMedia();
            if (!volumeSlider.IsMouseCaptureWithin) RefreshVolume();
        }
        if (playing && !draggingTimeline)
        {
            double correction = playbackCorrection * (1 - Math.Exp(-3 * playbackDelta));
            playbackCorrection -= correction;
            position = Math.Max(0, Math.Min(length, position + playbackDelta * playbackRate + correction));
        }
        CheckAlarm();
        if (alarmRinging && now >= alarmSoundEndsAt) DismissAlarm();
        if (clockToolIndex >= 2) RefreshAlarmStatus();
        if (clockToolIndex == 1) stopwatchDisplay.Text = stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.ff");
        if (!draggingTimeline) RefreshPosition();
        waveAmplitude += ((playing ? 1.0 : 0.0) - waveAmplitude) * Math.Min(1, delta * 12);
        DrawFrame(now, delta);
        if (now >= nextFullscreenCheck)
        {
            nextFullscreenCheck = now + (controllerMode ? 0.1 : 0.75);
            RaiseOverFullscreenWindow();
        }
        if (now >= nextBackdropFrame && sinkProgress < 0.99)
        {
            bool moving = (progress > 0 && progress < 1) || (tabProgress > 0 && tabProgress < 1)
                || openWidth != previousWidth || openHeight != previousHeight || dragEnergy > 0 || draggingIsland || tension > 0
                || (sinkProgress > 0 && sinkProgress < 1);
            nextBackdropFrame = now + (moving ? 0.025 : 0.060);
            Point origin = PointToScreen(new Point(0, 0));
            Point end = PointToScreen(new Point(Width, Height));
            lens.Update(backdrop, overlayHandle, origin, end,
                glassFrame, Smooth(progress));
        }
    }

    private void RefreshVolume()
    {
        float level;
        bool muted;
        bool available = systemVolume.TryRead(out level, out muted);
        updatingVolume = true;
        volumeSlider.IsEnabled = available;
        volumeSlider.Value = available && !muted ? Math.Round(level * 100) : 0;
        UpdateSpeakerIcon(volumeSlider.Value);
        if (!available) speakerIcon.ToolTip = "Windows output volume is unavailable";
        updatingVolume = false;
    }

    private void PollController(double now)
    {
        if (!controllerEnabled) { controllerMode = false; return; }
        ControllerState state;
        if (controllerIndex < 0)
        {
            if (now < nextControllerScan) return;
            nextControllerScan = now + 1;
            for (int i = 0; i < 4; i++)
                if (XInputGetState((uint)i, out state) == 0) { controllerIndex = i; controllerButtons = state.Buttons; break; }
            return;
        }
        if (XInputGetState((uint)controllerIndex, out state) != 0)
        {
            controllerIndex = -1;
            CloseControllerMode();
            controllerChordAt = -1;
            controllerChordUsed = false;
            controllerButtons = 0;
            return;
        }
        ushort pressed = (ushort)(state.Buttons & ~controllerButtons);
        controllerButtons = state.Buttons;
        bool chord = (state.Buttons & 0x30) == 0x30;
        if (!chord)
        {
            controllerChordAt = -1;
            controllerChordUsed = false;
        }
        else
        {
            if (controllerChordAt < 0) controllerChordAt = now;
            if (!controllerChordUsed && controllerMode)
            {
                controllerChordUsed = true;
                CloseControllerMode();
            }
            else if (!controllerChordUsed && now - controllerChordAt >= 0.6)
            {
                controllerChordUsed = true;
                controllerPreviousWindow = GetForegroundWindow();
                tabsOpen = settingsOpen = false;
                recordingShortcut = false;
                suppressHover = false;
                sinkProgress = 0;
                lastDockActivity = clock.Elapsed.TotalSeconds;
                Show();
                controllerMode = true;
                preferredPage = selectedTab = 0;
                Activate();
                SetForegroundWindow(overlayHandle);
                nextFullscreenCheck = 0;
            }
            return;
        }
        if (!controllerMode) return;
        if ((pressed & 0x2000) != 0) { CloseControllerMode(); return; }
        if ((pressed & 0x1000) != 0) { CloseControllerMode(); return; }
        if ((pressed & 0x4000) != 0 && play.IsEnabled)
            SendControllerCommand(s => playing ? s.TryPauseAsync() : s.TryPlayAsync());
        if ((pressed & 0x4) != 0 && previous.IsEnabled) SendControllerCommand(s => s.TrySkipPreviousAsync());
        if ((pressed & 0x8) != 0 && next.IsEnabled) SendControllerCommand(s => s.TrySkipNextAsync());
        if ((pressed & 3) != 0 && volumeSlider.IsEnabled)
            volumeSlider.Value = Math.Max(0, Math.Min(100, volumeSlider.Value + ((pressed & 1) != 0 ? 5 : -5)));
    }

    private async void SendControllerCommand(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        await SendCommand(command);
    }

    private void CloseControllerMode()
    {
        bool restoreGame = controllerMode && GetForegroundWindow() == overlayHandle;
        controllerMode = false;
        revealUntil = 0;
        tabsOpen = settingsOpen = false;
        lastHover = double.NegativeInfinity;
        suppressHover = true;
        if (restoreGame && controllerPreviousWindow != IntPtr.Zero && IsWindow(controllerPreviousWindow))
            SetForegroundWindow(controllerPreviousWindow);
        controllerPreviousWindow = IntPtr.Zero;
    }

    private void UpdateSpeakerIcon(double volume)
    {
        speakerIcon.Text = volume <= 0 ? "\uE74F" : volume < 34 ? "\uE993" : volume < 67 ? "\uE994" : "\uE995";
        speakerIcon.ToolTip = "Windows volume " + Math.Round(volume).ToString("0") + "%";
    }

    private void DrawFrame(double now, double delta)
    {
        double eased = Smooth(progress);
        bool compactIsClock = !playing && (preferredPage == 1 || ShouldShowClock(now));
        bool compactAlarm = alarmRinging || alarmAt.HasValue;
        double compactWidth = compactAlarm ? 210 : compactIsClock ? (IsSideDock ? SideClockWidth : TopClockWidth) : IsSideDock ? SideClosedWidth : ClosedWidth;
        bool batteryVisible = showBatteryPercentage && batteryAvailable;
        if (batteryVisible && !IsSideDock) compactWidth += 44;
        double width = compactWidth + (openWidth - compactWidth) * eased;
        double compactHeight = alarmRinging ? 78 : compactAlarm ? ClosedHeight : IsSideDock ? (compactIsClock ? SideClockDisplayHeight : SideClosedHeight) : ClosedHeight;
        if (batteryVisible && IsSideDock) compactHeight += 30;
        double height = compactHeight + (openHeight - compactHeight) * eased;
        double ripple = (now - rippleStarted) / 0.95;
        bool rippleActive = edge == DockEdge.Floating && ripple >= 0 && ripple < 1;
        double compression = Math.Pow(Clamp(tension / StickyStretch, 0, 1), 0.82);
        double verticalCompression = IsSideDock ? 1 : 0;
        if (rippleActive)
        {
            double recovery = Smooth(Clamp(ripple / 0.45, 0, 1));
            compression = 1 - recovery;
            if (rippleEdge == DockEdge.Left || rippleEdge == DockEdge.Right)
            {
                double sideWidth = compactIsClock ? SideClockWidth : SideClosedWidth;
                double sideHeight = compactIsClock ? SideClockDisplayHeight : SideClosedHeight;
                width = sideWidth + (width - sideWidth) * recovery;
                height = sideHeight + (height - sideHeight) * recovery;
                verticalCompression = 1 - recovery;
            }
        }
        width *= 1 - compression * (0.48 - 0.33 * verticalCompression);
        height *= 1 - compression * (0.15 + 0.33 * verticalCompression);
        double wobble = 0.15 * dragEnergy * Math.Sin(dragPhase);
        width *= 1 + wobble;
        height *= 1 - wobble;
        double attachment = (now - attachStarted) / 1.05;
        bool attachActive = edge == attachedEdge && attachment >= 0 && attachment < 1;
        DockEdge corner = CornerEdge();
        if (progress != lastShapeProgress || tabProgress != lastTabProgress || width != panel.Width || height != panel.Height
            || edge != lastDrawnEdge || corner != lastDrawnCorner || tension != lastDrawnTension
            || rippleActive || rippleWasActive || attachActive || attachWasActive)
        {
            lastShapeProgress = progress;
            lastTabProgress = tabProgress;
            lastDrawnEdge = edge;
            lastDrawnCorner = corner;
            lastDrawnTension = tension;
            Geometry shape = rippleActive ? RippleOutline(width, height, ripple, rippleEdge) : DockOutline(width, height, eased, true);
            if (attachActive) shape = RippleOutline(width, height, attachment, edge, shape, corner);
            rippleWasActive = rippleActive;
            attachWasActive = attachActive;
            panel.Width = width;
            panel.Height = height;
            panel.Clip = shape;
            double compactCover = IsSideDock ? 22 : 24;
            double coverSize = compactCover + (49 - compactCover) * eased;
            artwork.Width = artwork.Height = coverSize;
            double artworkLeft = IsSideDock ? (width - coverSize) / 2 : 29 - 10 * eased;
            artwork.Margin = new Thickness(artworkLeft, IsSideDock ? 22 - 2 * eased : 6 + 8 * eased, 0, 0);
            artwork.Clip = new RectangleGeometry(new Rect(0, 0, coverSize, coverSize), 5 + 4 * eased, 5 + 4 * eased);
            coverPlaceholder.FontSize = 15 + 10 * eased;
            rim.Data = progress == 0 && edge != DockEdge.Floating && tension <= 0.5 && corner == DockEdge.Floating && !attachActive
                ? DockOutline(width, height, eased, false) : shape;
            if (corner == DockEdge.Floating) rim.Clip = null;
            else
            {
                DockEdge primaryContact = tension < 0.5 ? edge : DockEdge.Floating;
                const double inset = 1.5;
                double left = primaryContact == DockEdge.Left || corner == DockEdge.Left ? inset : 0;
                double top = primaryContact == DockEdge.Top || corner == DockEdge.Top ? inset : 0;
                double right = primaryContact == DockEdge.Right || corner == DockEdge.Right ? inset : 0;
                double bottom = primaryContact == DockEdge.Bottom || corner == DockEdge.Bottom ? inset : 0;
                rim.Clip = new RectangleGeometry(new Rect(left, top, width - left - right, height - top - bottom));
            }
            rim.Opacity = 0.42 + eased * 0.48;
            glare.SetTrack(PathGeometry.CreateFromGeometry(shape), width, height);
            glare.Width = width;
            glare.Height = height;
            collapsed.Opacity = 1 - Math.Min(1, progress * 2.5);
            collapsed.IsHitTestVisible = false;
            collapsed.VerticalAlignment = VerticalAlignment.Top;
            collapsed.Height = ClosedHeight - 2;
            expanded.Opacity = Math.Max(0, (progress - 0.42) / 0.58);
            expanded.IsHitTestVisible = progress >= 0.76;
            expanded.Margin = IsSideDock ? new Thickness(14, 77, 14, 12) : new Thickness(19, 14, 19, 12);
            title.Margin = IsSideDock ? new Thickness(0) : new Thickness(59, 0, 0, 0);
            artist.Margin = IsSideDock ? new Thickness(0, 3, 0, 0) : new Thickness(59, 3, 0, 0);
            title.TextAlignment = artist.TextAlignment = IsSideDock ? TextAlignment.Center : TextAlignment.Left;
            artist.TextTrimming = TextTrimming.CharacterEllipsis;
            previous.Width = play.Width = next.Width = favorite.Width = IsSideDock ? 36 : 44;
            compactClock.FontSize = IsSideDock ? 19 : 21;
            compactClock.LineHeight = IsSideDock ? 21 : 28;
        }
        LayoutIsland(width, height, eased);
        double stretch = tension > 0 ? tension + CurrentGap() * eased : 0;
        LayoutGlass(stretch, eased);
        recoveryFlash.Width = glassFrame.Width;
        recoveryFlash.Height = glassFrame.Height;
        recoveryFlash.Clip = glassSurface.Clip;
        Place(recoveryFlash, glassFrame.X, glassFrame.Y);
        attachmentParticles.Frame(islandFrame, attachedEdge, attachActive ? (now - attachStarted) / 0.67 : 1);

        int page = CurrentPage(now);
        double compactOpacity = 1 - Math.Min(1, progress * 2.5);
        compactBattery.Visibility = expandedBattery.Visibility = batteryVisible ? Visibility.Visible : Visibility.Collapsed;
        compactBattery.Opacity = compactOpacity;
        compactBattery.HorizontalAlignment = IsSideDock ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        compactBattery.VerticalAlignment = IsSideDock ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        compactBattery.Margin = IsSideDock ? new Thickness(0, 0, 0, 10) : new Thickness(0, 10, 12, 0);
        compactClock.Margin = batteryVisible ? (IsSideDock ? new Thickness(0, 0, 0, 30) : new Thickness(0, 0, 44, 0)) : new Thickness(0);
        collapsed.Margin = new Thickness(62, 0, batteryVisible && !IsSideDock ? 73 : 29, 0);
        double pageOpacity = Math.Max(0, (progress - 0.42) / 0.58);
        double squeezedOpacity = 1 - 0.55 * compression;
        collapsed.Opacity = compactOpacity * squeezedOpacity;
        collapsed.Visibility = compactAlarm || compactIsClock || IsSideDock ? Visibility.Hidden : Visibility.Visible;
        sideMarquee.Visibility = sideWave.Visibility = IsSideDock && !compactAlarm && !compactIsClock && compactOpacity > 0 ? Visibility.Visible : Visibility.Hidden;
        sideMarquee.Opacity = sideWave.Opacity = compactOpacity * squeezedOpacity;
        compactClock.Opacity = compactIsClock ? compactOpacity * squeezedOpacity : 0;
        compactClock.Visibility = compactIsClock && !compactAlarm ? Visibility.Visible : Visibility.Hidden;
        compactTimer.Visibility = compactAlarm && compactOpacity > 0 ? Visibility.Visible : Visibility.Hidden;
        compactTimer.Opacity = compactOpacity;
        if (compactAlarm)
        {
            int seconds = alarmAt.HasValue ? Math.Max(0, (int)Math.Ceiling((alarmAt.Value - DateTime.Now).TotalSeconds)) : 0;
            compactTimer.Text = alarmRinging ? (ringingWakeUp ? "⏰ Wake-up alarm" : "⏳ Timer finished")
                : "⏳ " + TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
        compactDismiss.Visibility = alarmRinging && progress < 0.40 ? Visibility.Visible : Visibility.Hidden;
        compactDismiss.Opacity = compactOpacity;
        expanded.Visibility = page == 0 ? Visibility.Visible : Visibility.Hidden;
        clockPage.Visibility = page == 1 ? Visibility.Visible : Visibility.Hidden;
        settingsPage.Visibility = page == 2 ? Visibility.Visible : Visibility.Hidden;
        appsPage.Visibility = page == 3 ? Visibility.Visible : Visibility.Hidden;
        appsPage.Opacity = pageOpacity;
        appsPage.IsHitTestVisible = progress >= 0.76;
        clockPage.Opacity = settingsPage.Opacity = pageOpacity;
        batteryStatus.Opacity = pageOpacity;
        expandedBattery.Opacity = pageOpacity;
        clockPage.IsHitTestVisible = settingsPage.IsHitTestVisible = progress >= 0.76;
        artwork.Opacity = page == 0 && !compactIsClock ? 1
            : ((!compactIsClock ? compactOpacity : 0) + (page == 0 ? pageOpacity : 0));
        artwork.Opacity *= squeezedOpacity;
        if (compactAlarm) artwork.Opacity *= pageOpacity;
        artwork.Visibility = hasMedia && artwork.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        tabShelf.Opacity = Smooth(Math.Max(0, (tabProgress - 0.24) / 0.76)) * pageOpacity;
        tabShelf.Visibility = tabShelf.Opacity > 0 ? Visibility.Visible : Visibility.Hidden;
        tabShelf.IsHitTestVisible = tabsOpen && progress > 0.76 && tabProgress > 0.5;
        tabsButton.Opacity = settingsButton.Opacity = pageOpacity;
        tabsButton.Visibility = settingsButton.Visibility = pageOpacity > 0 ? Visibility.Visible : Visibility.Hidden;
        tabsButton.IsHitTestVisible = settingsButton.IsHitTestVisible = progress >= 0.88;
        gapTarget.Visibility = progress > 0.76 && !draggingIsland ? Visibility.Visible : Visibility.Hidden;
        if (compactIsClock || page == 1) UpdateClock();
        glare.Frame(new Point(cursor.X - Canvas.GetLeft(glare), cursor.Y - Canvas.GetTop(glare)), delta, Smooth(progress));
        coverWash.Visibility = page == 0 ? Visibility.Visible : Visibility.Hidden;
        coverWash.Opacity = 0.49 * Smooth(Math.Max(0, (progress - 0.4) / 0.6));
        if (progress < 0.4 && session != null)
        {
            marqueeDelay = Math.Max(0, marqueeDelay - delta);
            if (marqueeDelay == 0)
            {
                double repeatAt = Math.Max(marquee.ActualWidth + 24, marqueeTextWidth + 29);
                marqueeOffset = (marqueeOffset + 29 * delta) % repeatAt;
                Canvas.SetLeft(smallTrack, -marqueeOffset);
                Canvas.SetLeft(smallTrackRepeat, repeatAt - marqueeOffset);
                if (IsSideDock) sideMarquee.Advance(delta);
            }
        }

        for (int index = 0; index < bars.Length; index++)
        {
            double phase = now * (5.1 + index % 4 * 0.7) + index * 0.94;
            double beat = 0.5 + 0.5 * Math.Sin(phase);
            double extent = 3 + waveAmplitude * (2 + beat * beat * 13);
            bars[index].Height = extent;
            double sideBeat = 0.5 + 0.5 * Math.Sin(now * 5.6 - index * 0.79);
            sideBars[index].Width = 4 + waveAmplitude * (3 + sideBeat * sideBeat * 18);
        }
    }

    private double CurrentGap()
    {
        if (CornerEdge() != DockEdge.Floating) return 0;
        double expandedGap = IsSideDock ? SideTabGap : TabGap;
        return OpenGap + (expandedGap - OpenGap) * Smooth(tabProgress);
    }

    private bool IsSideDock { get { return edge == DockEdge.Left || edge == DockEdge.Right; } }

    private void LayoutIsland(double width, double height, double eased)
    {
        bool side = IsSideDock;
        double gap = CurrentGap() * eased;
        double sink = Smooth(sinkProgress) * ((side ? width : height) - 3);
        double inset = gap + tension - sink;
        double drift = 11 * dragEnergy * Math.Sin(dragPhase - 0.55);
        double x = Clamp((anchor.X - Left) / islandScale - width / 2 + (side ? 0 : drift), 0, SurfaceWidth - width);
        double y = Clamp((anchor.Y - Top) / islandScale - height / 2 + (side || edge == DockEdge.Floating ? drift : 0), 0, SurfaceHeight - height);
        switch (edge)
        {
            case DockEdge.Top: y = inset; break;
            case DockEdge.Bottom: y = SurfaceHeight - height - inset; break;
            case DockEdge.Left: x = inset; break;
            case DockEdge.Right: x = SurfaceWidth - width - inset; break;
            default: y = Clamp(y, gap, SurfaceHeight - height); break;
        }
        switch (CornerEdge())
        {
            case DockEdge.Left: x = 0; break;
            case DockEdge.Right: x = SurfaceWidth - width; break;
            case DockEdge.Top: y = 0; break;
            case DockEdge.Bottom: y = SurfaceHeight - height; break;
        }
        islandFrame = new Rect(x, y, width, height);
        Place(panel, x, y);
        Place(rim, x, y);
        Place(glare, x, y);

        if (side)
            gapFrame = new Rect(edge == DockEdge.Left ? x - gap : x + width, y, gap, height);
        else
            gapFrame = new Rect(x, edge == DockEdge.Bottom ? y + height : y - gap, width, gap);
        gapTarget.Width = gapFrame.Width;
        gapTarget.Height = gapFrame.Height;
        Place(gapTarget, gapFrame.X, gapFrame.Y);

        bool inCorner = CornerEdge() != DockEdge.Floating;
        bool verticalTabs = side && !inCorner;
        tabBar.Orientation = verticalTabs ? Orientation.Vertical : Orientation.Horizontal;
        tabShelf.Width = verticalTabs ? 104 : inCorner ? 211 : 254;
        tabShelf.Height = verticalTabs ? 125 : 41;
        foreach (var tab in tabs)
            if (tab != null)
            {
                tab.Width = verticalTabs ? 70 : inCorner ? 51 : 66;
                tab.Margin = new Thickness(8, 5, 8, 5);
            }
        if (inCorner)
            Place(tabShelf, x + (width - tabShelf.Width) / 2, y + height - 47);
        else if (side)
            Place(tabShelf, gapFrame.X + (gapFrame.Width - tabShelf.Width) / 2,
                Clamp(y + (height - tabShelf.Height) / 2, 0, SurfaceHeight - tabShelf.Height));
        else
            Place(tabShelf, Clamp(x + (width - tabShelf.Width) / 2, 0, SurfaceWidth - tabShelf.Width),
                gapFrame.Y + (gapFrame.Height - tabShelf.Height) / 2);
    }

    private void LayoutGlass(double stretch, double eased)
    {
        Geometry glassShape = panel.Clip;
        glassFrame = islandFrame;
        if (stretch > 0.5)
        {
            Geometry combined = CornerEdge() == DockEdge.Floating
                ? DockOutline(islandFrame.Width, islandFrame.Height, eased, true, stretch)
                : Geometry.Combine(glassShape, EdgeStretch(islandFrame.Width, islandFrame.Height, edge, stretch), GeometryCombineMode.Union, null);
            Rect bounds = combined.Bounds;
            glassFrame = new Rect(islandFrame.X + bounds.X, islandFrame.Y + bounds.Y, bounds.Width, bounds.Height);
            var shifted = new GeometryGroup { Transform = new TranslateTransform(-bounds.X, -bounds.Y) };
            shifted.Children.Add(combined);
            shifted.Freeze();
            glassShape = shifted;
            rim.Data = glassShape;
            rim.Clip = null;
            Place(rim, glassFrame.X, glassFrame.Y);
        }
        glassSurface.Width = glassFrame.Width;
        glassSurface.Height = glassFrame.Height;
        glassSurface.Clip = glassShape;
        Place(glassSurface, glassFrame.X, glassFrame.Y);
    }

    private static Geometry EdgeStretch(double width, double height, DockEdge side, double stretch)
    {
        bool vertical = side == DockEdge.Left || side == DockEdge.Right;
        double halfEdge = (vertical ? height : width) / 2;
        double progress = Math.Min(1, stretch / StickyStretch);
        double baseWidth = halfEdge * (0.92 - 0.70 * progress);
        double neck = halfEdge * (0.86 - 0.82 * progress);
        double contact = halfEdge * (0.94 - 0.927 * progress);
        double overlap = Math.Min(26, (vertical ? width : height) * 0.62);
        var bridge = new StreamGeometry();
        using (var path = bridge.Open())
        {
            path.BeginFigure(StretchPoint(width, height, side, -baseWidth, overlap), true, true);
            path.BezierTo(StretchPoint(width, height, side, -neck, 2),
                StretchPoint(width, height, side, -neck, -stretch * 0.66),
                StretchPoint(width, height, side, -contact, -stretch), true, false);
            path.LineTo(StretchPoint(width, height, side, contact, -stretch), true, false);
            path.BezierTo(StretchPoint(width, height, side, neck, -stretch * 0.66),
                StretchPoint(width, height, side, neck, 2),
                StretchPoint(width, height, side, baseWidth, overlap), true, false);
        }
        bridge.Freeze();
        return bridge;
    }

    private static Point StretchPoint(double width, double height, DockEdge side, double across, double inward)
    {
        switch (side)
        {
            case DockEdge.Left: return new Point(inward, height / 2 + across);
            case DockEdge.Right: return new Point(width - inward, height / 2 + across);
            case DockEdge.Bottom: return new Point(width / 2 + across, height - inward);
            default: return new Point(width / 2 + across, inward);
        }
    }

    private static void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }

    private bool IsNearDock(Point local)
    {
        if (edge == DockEdge.Top || edge == DockEdge.Bottom)
        {
            double line = edge == DockEdge.Top ? 0 : SurfaceHeight;
            return Math.Abs(local.Y - line) <= 7 && local.X >= islandFrame.Left && local.X <= islandFrame.Right;
        }
        double side = edge == DockEdge.Left ? 0 : SurfaceWidth;
        return Math.Abs(local.X - side) <= 7 && local.Y >= islandFrame.Top && local.Y <= islandFrame.Bottom;
    }

    private Geometry DockOutline(double width, double height, double eased, bool face, double stretch = 0)
    {
        double peel = Smooth(Math.Min(1, tension / StickyStretch));
        if (edge == DockEdge.Floating)
        {
            double radius = Math.Min(18, (Math.Min(width, height) - 1) / 2);
            return new RectangleGeometry(new Rect(0.5, 0.5, width - 1, height - 1), radius, radius);
        }
        DockEdge corner = CornerEdge();
        if (corner != DockEdge.Floating)
            return CornerOutline(width, height, corner, tension < 0.5 ? edge : DockEdge.Floating);
        var outline = (IsSideDock ? Outline(height, width, eased, face, peel, stretch) : Outline(width, height, eased, face, peel, stretch)).Clone();
        if (edge == DockEdge.Bottom)
            outline.Transform = new MatrixTransform(-1, 0, 0, -1, width, height);
        else if (edge == DockEdge.Left)
            outline.Transform = new MatrixTransform(0, -1, 1, 0, 0, height);
        else if (edge == DockEdge.Right)
            outline.Transform = new MatrixTransform(0, 1, -1, 0, width, 0);
        outline.Freeze();
        return outline;
    }

    private static Geometry RippleOutline(double width, double height, double phase, DockEdge releasedFrom,
        Geometry attachedShape = null, DockEdge otherEdge = DockEdge.Floating)
    {
        const int samples = 96;
        double strength = Math.Sin(Math.PI * Math.Pow(phase, 0.57)) * (1 - phase);
        double amplitude = Math.Min(15, height * 0.36) * strength;
        if (attachedShape == null) amplitude *= 0.48;
        double inset = 0.5 + amplitude;
        double radius = Math.Min(18, (Math.Min(width, height) - 2 * inset) / 2);
        Geometry baseShape = attachedShape ?? new RectangleGeometry(new Rect(inset, inset, width - 2 * inset, height - 2 * inset), radius, radius);
        var perimeter = PathGeometry.CreateFromGeometry(baseShape);
        var center = new Point(width / 2, height / 2);
        Vector release = releasedFrom == DockEdge.Bottom ? new Vector(0, 1)
            : releasedFrom == DockEdge.Left ? new Vector(-1, 0)
            : releasedFrom == DockEdge.Right ? new Vector(1, 0) : new Vector(0, -1);
        var points = new Point[samples];
        var normals = new Vector[samples];
        int origin = 0;
        double closest = double.NegativeInfinity;
        for (int i = 0; i < samples; i++)
        {
            Point tangent;
            perimeter.GetPointAtFractionLength((double)i / samples, out points[i], out tangent);
            var normal = new Vector(-tangent.Y, tangent.X);
            if (normal.LengthSquared > 0) normal.Normalize();
            if (normal * (points[i] - center) < 0) normal = -normal;
            normals[i] = normal;
            Vector fromCenter = points[i] - center;
            double alignment = fromCenter * release - 0.55 * Math.Abs(Vector.CrossProduct(fromCenter, release));
            if (alignment <= closest) continue;
            closest = alignment;
            origin = i;
        }
        var shape = new StreamGeometry();
        using (var path = shape.Open())
        {
            for (int i = 0; i < samples; i++)
            {
                double distance = Math.Abs(i - origin) / (double)samples;
                distance = Math.Min(distance, 1 - distance);
                double wave = distance - 0.66 * phase;
                double pulse = Math.Exp(-wave * wave / 0.036);
                double offset = amplitude * Math.Sin((wave * 4.5 + phase * 1.25) * 2 * Math.PI) * (0.35 + 0.65 * pulse);
                if (attachedShape != null)
                {
                    var frame = new Rect(0, 0, width, height);
                    double edgeDistance = EdgeDistance(points[i], releasedFrom, frame);
                    if (otherEdge != DockEdge.Floating) edgeDistance = Math.Min(edgeDistance, EdgeDistance(points[i], otherEdge, frame));
                    offset *= 0.36 * Smooth(Clamp((edgeDistance - 1) / 15, 0, 1));
                }
                Point point = points[i] + normals[i] * offset;
                if (attachedShape != null)
                    point = new Point(Clamp(point.X, 0, width), Clamp(point.Y, 0, height));
                if (i == 0) path.BeginFigure(point, true, true);
                else path.LineTo(point, true, true);
            }
        }
        shape.Freeze();
        return shape;
    }

    private static Geometry CornerOutline(double width, double height, DockEdge side, DockEdge primary)
    {
        bool left = side == DockEdge.Left || primary == DockEdge.Left;
        bool top = side == DockEdge.Top || primary == DockEdge.Top;
        bool right = side == DockEdge.Right || primary == DockEdge.Right;
        bool bottom = side == DockEdge.Bottom || primary == DockEdge.Bottom;
        double radius = Math.Min(17, Math.Min(width, height) / 2);
        double tl = left || top ? 0 : radius;
        double tr = right || top ? 0 : radius;
        double br = right || bottom ? 0 : radius;
        double bl = left || bottom ? 0 : radius;
        var shape = new StreamGeometry();
        using (var path = shape.Open())
        {
            path.BeginFigure(new Point(tl, 0), true, true);
            path.LineTo(new Point(width - tr, 0), true, false);
            path.QuadraticBezierTo(new Point(width, 0), new Point(width, tr), true, false);
            path.LineTo(new Point(width, height - br), true, false);
            path.QuadraticBezierTo(new Point(width, height), new Point(width - br, height), true, false);
            path.LineTo(new Point(bl, height), true, false);
            path.QuadraticBezierTo(new Point(0, height), new Point(0, height - bl), true, false);
            path.LineTo(new Point(0, tl), true, false);
            path.QuadraticBezierTo(new Point(0, 0), new Point(tl, 0), true, false);
        }
        shape.Freeze();
        return shape;
    }

    private DockEdge CornerEdge()
    {
        if (edge == DockEdge.Floating) return DockEdge.Floating;
        if (IsSideDock)
        {
            if (anchor.Y - monitor.Top <= SideClosedHeight / 2 + 8) return DockEdge.Top;
            if (monitor.Bottom - anchor.Y <= SideClosedHeight / 2 + 8) return DockEdge.Bottom;
        }
        else
        {
            if (anchor.X - monitor.Left <= ClosedWidth / 2 + 8) return DockEdge.Left;
            if (monitor.Right - anchor.X <= ClosedWidth / 2 + 8) return DockEdge.Right;
        }
        return DockEdge.Floating;
    }

    private int CurrentPage(double now)
    {
        return settingsOpen ? 2 : tabsOpen ? selectedTab : playing ? 0 : (preferredPage == 1 || ShouldShowClock(now) ? 1 : 0);
    }

    private static double ResizeToward(double current, double target, double delta)
    {
        if (Math.Abs(target - current) < 0.35) return target;
        return target + (current - target) * Math.Exp(-17 * delta);
    }

    private bool ShouldShowClock(double now)
    {
        return pausedSince.HasValue && now - pausedSince.Value >= 60;
    }

    private void InitializeTabs(Grid layers)
    {
        compactClock.HorizontalAlignment = HorizontalAlignment.Center;
        compactClock.TextAlignment = TextAlignment.Center;
        compactClock.VerticalAlignment = VerticalAlignment.Center;
        compactClock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        compactClock.FontWeight = FontWeights.SemiBold;
        compactClock.IsHitTestVisible = false;
        layers.Children.Add(compactClock);
        layers.Children.Add(compactBattery);
        compactTimer.HorizontalAlignment = HorizontalAlignment.Center;
        compactTimer.VerticalAlignment = VerticalAlignment.Top;
        compactTimer.Margin = new Thickness(0, 7, 0, 0);
        compactTimer.IsHitTestVisible = false;
        layers.Children.Add(compactTimer);
        compactDismiss = ClockToolButton("Dismiss ringing alarm", "Dismiss", DismissAlarm);
        compactDismiss.HorizontalAlignment = HorizontalAlignment.Center;
        compactDismiss.VerticalAlignment = VerticalAlignment.Bottom;
        compactDismiss.Margin = new Thickness(0, 0, 0, 8);
        compactDismiss.Visibility = Visibility.Hidden;
        layers.Children.Add(compactDismiss);
        largeClock.FontWeight = FontWeights.SemiBold;
        largeClock.TextAlignment = dateLabel.TextAlignment = TextAlignment.Center;
        dateLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        dateLabel.MaxWidth = ClockWidth - 24;
        dateLabel.Margin = new Thickness(0, 4, 0, 0);
        clockPage.Children.Add(largeClock);
        clockPage.Children.Add(dateLabel);
        var clockActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var item in new[] { Control("Stopwatch", "⏱ Stopwatch", OpenStopwatch),
            Control("Countdown", "⏳ Timer", OpenAlarm), Control("Wake-up alarm", "⏰ Wake-up", OpenWakeUp) })
        {
            item.Width = 73;
            item.Margin = new Thickness(8, 5, 8, 5);
            item.FontSize = 10;
            clockButtons.Add(item);
            clockActions.Children.Add(item);
        }
        clockPage.Children.Add(clockActions);
        alarmNotice.TextAlignment = TextAlignment.Center;
        alarmNotice.Margin = new Thickness(0, 7, 0, 0);
        alarmNotice.TextWrapping = TextWrapping.Wrap;
        clockPage.Children.Add(clockTool);
        clockPage.Children.Add(alarmNotice);
        dismissAlarm = ClockToolButton("Dismiss alarm", "Dismiss", DismissAlarm);
        dismissAlarm.Width = 150;
        dismissAlarm.Margin = new Thickness(0, 9, 0, 0);
        dismissAlarm.HorizontalAlignment = HorizontalAlignment.Center;
        dismissAlarm.Visibility = Visibility.Collapsed;
        clockPage.Children.Add(dismissAlarm);
        alarmNotice.MouseLeftButtonUp += delegate { if (alarmRinging) DismissAlarm(); else alarmNotice.Text = ""; };
        layers.Children.Add(clockPage);
        surface.Children.Add(recoveryFlash);

        themeButton = Control("Switch theme", "Dark", delegate
        {
            SelectTheme(customTheme ? 0 : lightTheme ? 2 : 1);
        });
        themeButton.FontSize = 13;
        themeButton.Width = 86;
        themeButton.HorizontalAlignment = HorizontalAlignment.Right;
        themeButton.Margin = new Thickness(8, 0, 0, 0);
        var settingsContent = new StackPanel();
        var settingsHeading = Label(20, "#FFFFFF");
        settingsHeading.Text = "Settings";
        settingsHeading.FontWeight = FontWeights.SemiBold;
        settingsHeading.Margin = new Thickness(3, 0, 0, 14);
        settingsHeading.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = opacityText });
        settingsContent.Children.Add(settingsHeading);
        inlineAppearanceCard = new SettingsCard("Appearance");
        var themeRow = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(themeButton, Dock.Right);
        themeRow.Children.Add(themeButton);
        var themeCaption = Label(12, "#FFFFFF");
        themeCaption.Text = "Theme";
        themeCaption.VerticalAlignment = VerticalAlignment.Center;
        themeCaption.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = opacityText });
        themeRow.Children.Add(themeCaption);
        inlineAppearanceCard.Body.Children.Add(themeRow);
        inlineColorControls = BuildColorControls();
        inlineAppearanceCard.Body.Children.Add(inlineColorControls.Root);
        opacityText.Margin = new Thickness(0, 12, 0, 5);
        inlineAppearanceCard.Body.Children.Add(opacityText);
        AutomationProperties.SetName(inlineOpacity, "Glass opacity");
        ConnectOpacitySlider(inlineOpacity);
        inlineAppearanceCard.Body.Children.Add(inlineOpacity);
        inlinePlacement = BuildPlacementSettings();
        inlineAppearanceCard.Body.Children.Add(inlinePlacement.Appearance);
        settingsContent.Children.Add(inlineAppearanceCard);
        settingsContent.Children.Add(inlinePlacement.Root);
        settingsPage.Content = settingsContent;
        settingsPage.Resources.Add(typeof(ScrollBar), CreateGlassScrollBarStyle());
        layers.Children.Add(settingsPage);
        AutomationProperties.SetName(appSearch, "Search apps");
        appSearch.ToolTip = "Search installed apps";
        appSearch.TextChanged += delegate { RenderApps(); };
        var drawerHeader = new DockPanel { LastChildFill = false };
        drawerGear = ClockToolButton("App drawer settings", "\uE9E9", delegate
        {
            SetDrawerEditing(!editingApps);
        });
        drawerGear.Width = 29;
        drawerGear.Height = 29;
        drawerGear.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        drawerGear.ToolTip = "Choose apps and title visibility";
        DockPanel.SetDock(drawerGear, Dock.Right);
        drawerHeader.Children.Add(drawerGear);
        var searchGroup = new StackPanel { Orientation = Orientation.Horizontal };
        drawerSearchButton = ClockToolButton("Search apps", "\uE721", ToggleAppSearch);
        drawerSearchButton.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        drawerSearchButton.Width = drawerSearchButton.Height = 29;
        drawerSearchButton.Margin = new Thickness(0, 0, 5, 0);
        searchGroup.Children.Add(drawerSearchButton);
        appSearch.Width = 0;
        appSearch.Opacity = 0;
        appSearch.IsHitTestVisible = false;
        searchGroup.Children.Add(appSearch);
        drawerHeader.Children.Add(searchGroup);
        appSearch.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape) return;
            if (drawerSearchOpen) ToggleAppSearch();
            args.Handled = true;
        };
        appsPage.Children.Add(drawerHeader);
        appTitlesToggle.IsChecked = showAppTitles;
        appTitlesToggle.Click += delegate
        {
            showAppTitles = appTitlesToggle.IsChecked == true;
            SaveSettings();
            RenderApps();
        };
        appsPage.Children.Add(appTitlesToggle);
        appGridChoice.IsChecked = appsGrid;
        appListChoice.IsChecked = !appsGrid;
        appLayoutOptions.Children.Add(appGridChoice);
        appLayoutOptions.Children.Add(appListChoice);
        appLayoutOptions.Children.Add(appDockChoice);
        appGridChoice.Checked += delegate { SetAppsLayout(true); };
        appListChoice.Checked += delegate { SetAppsLayout(false); };
        appDockChoice.Checked += delegate
        {
            if (updatingAppearance) return;
            appsDock = true;
            SaveSettings();
            RenderApps();
        };
        appsPage.Children.Add(appLayoutOptions);
        appStatus.Margin = new Thickness(0, 6, 0, 6);
        appsPage.Children.Add(appStatus);
        chooseAppsButton = ClockToolButton("Choose apps", "Choose apps", delegate { SetDrawerEditing(true); });
        chooseAppsButton.Width = 130;
        chooseAppsButton.Height = 32;
        chooseAppsButton.Visibility = Visibility.Collapsed;
        appsPage.Children.Add(chooseAppsButton);
        appScroll = new ScrollViewer
        {
            Height = 209, Content = appRows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        appScroll.Resources.Add(typeof(ScrollBar), CreateGlassScrollBarStyle());
        dockCanvas.Children.Add(dockRows);
        dockCanvas.SetBinding(FrameworkElement.MinWidthProperty,
            new System.Windows.Data.Binding("ViewportWidth") { Source = appScroll });
        dockCanvas.SetBinding(FrameworkElement.MinHeightProperty,
            new System.Windows.Data.Binding("ViewportHeight") { Source = appScroll });
        appScroll.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs args)
        {
            if (!appsDock || editingApps) return;
            dockScrollTarget = Math.Max(0, Math.Min(appScroll.ScrollableWidth, dockScrollTarget - args.Delta * 0.8));
            args.Handled = true;
        };
        dockShelf.Child = appScroll;
        appsPage.Children.Add(dockShelf);
        layers.Children.Add(appsPage);

        string[] names = { "Media", "Clock", "Apps" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var tab = Control(names[i], names[i], delegate { SelectTab(index == 2 ? 3 : index); });
            tab.Width = 66;
            tab.Height = 29;
            tab.FontSize = 12;
            tabs[i] = tab;
            tabBar.Children.Add(tab);
        }
        tabShelf.Child = tabBar;
        Canvas.SetLeft(tabShelf, (SurfaceWidth - tabShelf.Width) / 2);
        surface.Children.Add(tabShelf);
        tabsButton = AddFooterButton(layers, "Tabs", "☰", HorizontalAlignment.Left, ToggleTabs);
        expandedBattery.HorizontalAlignment = HorizontalAlignment.Center;
        expandedBattery.VerticalAlignment = VerticalAlignment.Bottom;
        expandedBattery.Margin = new Thickness(45, 0, 45, 13);
        expandedBattery.RenderTransformOrigin = new Point(0.5, 0.5);
        expandedBattery.RenderTransform = new ScaleTransform(1.15, 1.15);
        layers.Children.Add(expandedBattery);
        settingsButton = AddFooterButton(layers, "Settings", "⚙", HorizontalAlignment.Right, delegate
        {
            settingsOpen = !settingsOpen;
            tabsOpen = false;
            UpdateTabAppearance();
        });

        PreviewKeyDown += delegate(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape || !(tabsOpen || settingsOpen)) return;
            tabsOpen = settingsOpen = false;
            UpdateTabAppearance();
            args.Handled = true;
        };
    }

    private Button AddFooterButton(Grid layers, string name, string glyph, HorizontalAlignment alignment, Action onClick)
    {
        var button = Control(name, glyph, onClick);
        button.Width = 29;
        button.Height = 26;
        button.FontSize = 16;
        button.HorizontalAlignment = alignment;
        button.VerticalAlignment = VerticalAlignment.Bottom;
        button.Margin = new Thickness(11, 0, 11, 9);
        layers.Children.Add(button);
        return button;
    }

    private void ToggleTabs()
    {
        settingsOpen = false;
        tabsOpen = !tabsOpen;
        if (tabsOpen) selectedTab = playing ? 0 : preferredPage == 1 || ShouldShowClock(clock.Elapsed.TotalSeconds) ? 1 : 0;
        UpdateTabAppearance();
    }

    private void SelectTab(int index)
    {
        CloseDrawerSettings();
        selectedTab = index;
        if (index < 2) preferredPage = index;
        if (index == 3) LoadApps();
        UpdateTabAppearance();
    }

    private void CloseDrawerSettings()
    {
        editingApps = false;
        appTitlesToggle.Visibility = appLayoutOptions.Visibility = Visibility.Collapsed;
        RenderApps();
    }

    private void SetDrawerEditing(bool editing)
    {
        editingApps = editing;
        appTitlesToggle.Visibility = appLayoutOptions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        if (editing && !String.IsNullOrEmpty(appSearch.Text)) appSearch.Clear();
        RenderApps();
    }

    private void ToggleAppSearch()
    {
        drawerSearchOpen = !drawerSearchOpen;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        appSearch.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(
            drawerSearchOpen ? 205 : 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = easing });
        appSearch.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(
            drawerSearchOpen ? 1 : 0, TimeSpan.FromMilliseconds(180)));
        appSearch.IsHitTestVisible = drawerSearchOpen;
        if (drawerSearchOpen)
        {
            Activate();
            appSearch.Focus();
            appSearch.SelectAll();
        }
        else appSearch.Clear();
    }

    private void SetAppsLayout(bool grid)
    {
        if (updatingAppearance || (appsGrid == grid && !appsDock)) return;
        appsDock = false;
        appsGrid = grid;
        SaveSettings();
        RenderApps();
    }

    private sealed class AppEntry
    {
        public string Name;
        public string Target;
        public string Executable;
        public string AppId;
        public BitmapSource Icon;
    }

    private static bool WindowBelongsToApp(IntPtr window, AppEntry entry, Dictionary<uint, bool> cache)
    {
        uint processId;
        GetWindowThreadProcessId(window, out processId);
        bool known;
        if (cache.TryGetValue(processId, out known)) return known;
        cache[processId] = false;
        IntPtr process = OpenProcess(0x1000, false, processId);
        if (process == IntPtr.Zero) return false;
        try
        {
            if (!String.IsNullOrEmpty(entry.AppId))
            {
                uint length = 1024;
                var appId = new System.Text.StringBuilder((int)length);
                if (GetApplicationUserModelId(process, ref length, appId) == 0
                    && String.Equals(appId.ToString(), entry.AppId, StringComparison.OrdinalIgnoreCase)) { cache[processId] = true; return true; }
            }
            if (!String.IsNullOrEmpty(entry.Executable))
            {
                uint length = 32768;
                var path = new System.Text.StringBuilder((int)length);
                if (QueryFullProcessImageName(process, 0, path, ref length)
                    && String.Equals(System.IO.Path.GetFullPath(path.ToString()), System.IO.Path.GetFullPath(entry.Executable), StringComparison.OrdinalIgnoreCase)) { cache[processId] = true; return true; }
            }
            return false;
        }
        finally { CloseHandle(process); }
    }

    private static bool TryActivateRunningApp(AppEntry entry)
    {
        if (String.IsNullOrEmpty(entry.Executable) && String.IsNullOrEmpty(entry.AppId)) return false;
        IntPtr match = IntPtr.Zero;
        var cache = new Dictionary<uint, bool>();
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true;
            int cloaked;
            if (DwmGetWindowAttribute(window, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            bool belongs = WindowBelongsToApp(window, entry, cache);
            if (!belongs)
                EnumChildWindows(window, delegate(IntPtr child, IntPtr unused)
                {
                    belongs = WindowBelongsToApp(child, entry, cache);
                    return !belongs;
                }, IntPtr.Zero);
            if (!belongs) return true;
            match = window;
            return false;
        }, IntPtr.Zero);
        if (match == IntPtr.Zero) return false;
        if (IsIconic(match)) ShowWindowAsync(match, 9);
        IntPtr popup = GetLastActivePopup(match);
        if (popup != IntPtr.Zero && IsWindowVisible(popup)) match = popup;
        SetForegroundWindow(match);
        return true;
    }

    private async void LoadApps()
    {
        if (appsLoaded || appsLoading) return;
        appsLoading = true;
        appStatus.Text = "Loading apps…";
        var entries = await Task.Run(() =>
        {
            var found = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.CommonPrograms })
                ReadShortcuts(Environment.GetFolderPath(folder), found);
            object shell = null, apps = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                apps = ComCall(shell, "NameSpace", System.Reflection.BindingFlags.InvokeMethod, "shell:AppsFolder");
                if (apps != null)
                {
                    items = ComCall(apps, "Items", System.Reflection.BindingFlags.InvokeMethod);
                    int count = Convert.ToInt32(ComCall(items, "Count", System.Reflection.BindingFlags.GetProperty));
                    for (int i = 0; i < count; i++)
                    {
                        object item = ComCall(items, "Item", System.Reflection.BindingFlags.InvokeMethod, i);
                        try
                        {
                            string name = Convert.ToString(ComCall(item, "Name", System.Reflection.BindingFlags.GetProperty));
                            string path = Convert.ToString(ComCall(item, "Path", System.Reflection.BindingFlags.GetProperty));
                            if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(path))
                            {
                                AppEntry shortcut;
                                found.TryGetValue(name, out shortcut);
                                string executable = shortcut == null ? null : shortcut.Executable;
                                if (String.IsNullOrEmpty(executable))
                                {
                                    try
                                    {
                                        string targetPath = Convert.ToString(ComCall(item, "ExtendedProperty", System.Reflection.BindingFlags.InvokeMethod, "System.Link.TargetParsingPath"));
                                        if (String.Equals(System.IO.Path.GetExtension(targetPath), ".exe", StringComparison.OrdinalIgnoreCase)) executable = targetPath;
                                    }
                                    catch (Exception error) { Debug.WriteLine(error); }
                                }
                                found[name] = new AppEntry { Name = name, Target = @"shell:AppsFolder\" + path, Executable = executable, AppId = path };
                            }
                        }
                        catch (Exception error) { Debug.WriteLine(error); }
                        finally { if (item != null) Marshal.ReleaseComObject(item); }
                    }
                }
            }
            catch (Exception error) { Debug.WriteLine(error); }
            finally
            {
                foreach (object value in new[] { items, apps, shell })
                    if (value != null) Marshal.ReleaseComObject(value);
            }
            var result = new List<AppEntry>(found.Values);
            result.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
            foreach (var entry in result) entry.Icon = ReadAppIcon(entry.Target);
            return result;
        });
        appEntries.AddRange(entries);
        appsLoading = false;
        appsLoaded = true;
        RenderApps();
    }

    private static object ComCall(object target, string member, System.Reflection.BindingFlags operation, params object[] arguments)
    {
        return target.GetType().InvokeMember(member, operation, null, target, arguments);
    }

    private static void ReadShortcuts(string folder, Dictionary<string, AppEntry> entries)
    {
        if (String.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder)) return;
        try
        {
            foreach (string file in System.IO.Directory.GetFiles(folder))
            {
                string extension = System.IO.Path.GetExtension(file);
                if (!String.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(extension, ".appref-ms", StringComparison.OrdinalIgnoreCase)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                var entry = new AppEntry { Name = name, Target = file };
                if (String.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    object shell = null, shortcut = null;
                    try
                    {
                        shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                        shortcut = ComCall(shell, "CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, file);
                        string executable = Convert.ToString(ComCall(shortcut, "TargetPath", System.Reflection.BindingFlags.GetProperty));
                        if (String.Equals(System.IO.Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase)) entry.Executable = executable;
                    }
                    catch (Exception error) { Debug.WriteLine(error); }
                    finally
                    {
                        if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                        if (shell != null) Marshal.ReleaseComObject(shell);
                    }
                }
                entries[name] = entry;
            }
            foreach (string subfolder in System.IO.Directory.GetDirectories(folder))
            {
                if ((System.IO.File.GetAttributes(subfolder) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
                ReadShortcuts(subfolder, entries);
            }
        }
        catch (Exception error) { Debug.WriteLine(error); }
    }

    private void RenderApps()
    {
        appRows.Children.Clear();
        dockRows.Children.Clear();
        bool dock = appsDock && !editingApps;
        dockShelf.BorderThickness = new Thickness(dock ? 0.8 : 0);
        dockShelf.Background = dock ? Brush(lightTheme ? "#4594A0AE" : "#244E6178") : Brushes.Transparent;
        dockShelf.BorderBrush = Brush(lightTheme ? "#A6FFFFFF" : "#698EA7BF");
        if (appScroll != null)
        {
            appScroll.Content = dock ? (UIElement)dockCanvas : appRows;
            appScroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            appScroll.HorizontalScrollBarVisibility = dock ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            appScroll.VerticalScrollBarVisibility = dock ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            appScroll.CanContentScroll = false;
            if (dock) dockScrollTarget = 0;
        }
        string query = appSearch.Text.Trim();
        int count = 0;
        foreach (AppEntry entry in appEntries)
        {
            if (entry.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
            if (!editingApps && !selectedApps.Contains(entry.Target)) continue;
            count++;
            if (editingApps)
            {
                var selection = new CheckBox
                {
                    Width = 265, Margin = new Thickness(5, 5, 5, 5),
                    Foreground = title.Foreground, IsChecked = selectedApps.Contains(entry.Target),
                    Content = AppTile(entry, true)
                };
                AutomationProperties.SetName(selection, "Show " + entry.Name);
                selection.Click += delegate
                {
                    if (selection.IsChecked == true) selectedApps.Add(entry.Target); else selectedApps.Remove(entry.Target);
                    SaveSettings();
                };
                appRows.Children.Add(selection);
                continue;
            }
            var row = Control("Open " + entry.Name, entry.Name, delegate
            {
                try
                {
                    if (!TryActivateRunningApp(entry)) Process.Start(new ProcessStartInfo(entry.Target) { UseShellExecute = true });
                    tabsOpen = false;
                    revealUntil = 0;
                    lastHover = double.NegativeInfinity;
                    suppressHover = true;
                }
                catch (Exception error)
                {
                    appStatus.Text = "Couldn’t open " + entry.Name;
                    appStatus.ToolTip = error.Message;
                }
            });
            row.Width = dock ? 52 : appsGrid ? 76 : 245;
            row.Height = dock ? (showAppTitles ? 65 : 45) : appsGrid ? (showAppTitles ? 76 : 54) : 42;
            row.FontSize = 12;
            row.Foreground = title.Foreground;
            row.Margin = dock ? new Thickness(4, 2, 4, 2) : new Thickness(4, 3, 4, 3);
            row.ToolTip = entry.Name;
            row.Content = AppTile(entry, showAppTitles);
            if (dock)
            {
                row.Background = Brushes.Transparent;
                row.RenderTransformOrigin = new Point(0.5, 1);
                var transforms = new TransformGroup();
                transforms.Children.Add(new ScaleTransform(1, 1));
                transforms.Children.Add(new TranslateTransform(0, 0));
                row.RenderTransform = transforms;
                row.MouseEnter += delegate { AnimateDockApp(row, true); };
                row.MouseLeave += delegate { AnimateDockApp(row, false); };
                dockRows.Children.Add(row);
            }
            else appRows.Children.Add(row);
        }
        appStatus.Text = appsLoading ? "Loading apps…" : editingApps ? "Choose apps to show" : count == 0
            ? (!String.IsNullOrWhiteSpace(query) ? "No matching apps" : selectedApps.Count == 0 ? "No apps selected" : "Selected apps are unavailable")
            : count.ToString() + " apps";
        if (chooseAppsButton != null) chooseAppsButton.Visibility = appsLoaded && !editingApps && selectedApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (appScroll != null)
        {
            int rows = editingApps || !appsGrid ? count : (count + 2) / 3;
            double rowHeight = editingApps ? 40 : appsGrid ? (showAppTitles ? 82 : 60) : 48;
            appScroll.Height = dock ? (showAppTitles ? 109 : 89) : Math.Max(48, Math.Min(209, rows * rowHeight));
            double optionsHeight = editingApps ? 60 : 0;
            appDrawerHeight = 132 + appScroll.Height + optionsHeight + (chooseAppsButton != null && chooseAppsButton.Visibility == Visibility.Visible ? 36 : 0);
        }
    }

    private UIElement AppTile(AppEntry entry, bool showTitle)
    {
        bool gridTile = (appsGrid || appsDock) && !editingApps;
        var tile = new StackPanel { Orientation = gridTile ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (entry.Icon != null)
        {
            var logo = new Image { Source = entry.Icon, Width = appsDock && !editingApps ? 32 : gridTile ? 30 : 26, Height = appsDock && !editingApps ? 32 : gridTile ? 30 : 26, Margin = gridTile ? new Thickness(0, 0, 0, showTitle ? 6 : 0) : new Thickness(0, 0, showTitle ? 9 : 0, 0) };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            tile.Children.Add(logo);
        }
        else
        {
            var fallback = Label(22, lightTheme ? "#27303D" : "#E8EFFA");
            fallback.Text = "▣";
            fallback.Margin = new Thickness(0, 0, showTitle ? 9 : 0, 0);
            tile.Children.Add(fallback);
        }
        if (showTitle)
            tile.Children.Add(new TextBlock { Text = entry.Name, FontSize = appsDock && !editingApps ? 10 : 12, MaxWidth = appsDock && !editingApps ? 48 : gridTile ? 68 : 206, TextAlignment = gridTile ? TextAlignment.Center : TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        return tile;
    }

    private static void AnimateDockApp(Button button, bool raised)
    {
        var group = (TransformGroup)button.RenderTransform;
        var scale = (ScaleTransform)group.Children[0];
        var lift = (TranslateTransform)group.Children[1];
        Func<double, DoubleAnimation> motion = value => new DoubleAnimation(value, TimeSpan.FromMilliseconds(210))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, motion(raised ? 1.30 : 1));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, motion(raised ? 1.30 : 1));
        lift.BeginAnimation(TranslateTransform.YProperty, motion(raised ? -8 : 0));
        Panel.SetZIndex(button, raised ? 1 : 0);
    }

    private static BitmapSource ReadAppIcon(string target)
    {
        BitmapSource artwork = ReadLargeAppIcon(target);
        if (artwork != null) return artwork;
        IntPtr pidl = IntPtr.Zero;
        ShellFileInfo info = new ShellFileInfo();
        try
        {
            uint attributes;
            if (SHParseDisplayName(target, IntPtr.Zero, out pidl, 0, out attributes) < 0) return null;
            if (SHGetFileInfo(pidl, 0, ref info, (uint)Marshal.SizeOf(typeof(ShellFileInfo)), 0x108) == IntPtr.Zero || info.Icon == IntPtr.Zero)
                return null;
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) { Debug.WriteLine(error); return null; }
        finally
        {
            if (info.Icon != IntPtr.Zero) DestroyIcon(info.Icon);
            if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl);
        }
    }

    private static BitmapSource ReadLargeAppIcon(string target)
    {
        ShellImageFactory factory = null;
        IntPtr image = IntPtr.Zero;
        try
        {
            Guid iid = typeof(ShellImageFactory).GUID;
            if (SHCreateItemFromParsingName(target, IntPtr.Zero, ref iid, out factory) < 0) return null;
            if (factory.GetImage(new ShellImageSize { Width = 256, Height = 256 }, 0x5, out image) < 0 || image == IntPtr.Zero)
                return null;
            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(image, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) { Debug.WriteLine(error); return null; }
        finally
        {
            if (image != IntPtr.Zero) DeleteObject(image);
            if (factory != null) Marshal.ReleaseComObject(factory);
        }
    }

    private IntPtr HandleWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == RecoveryHotkeyId)
        {
            RecoverIsland();
            handled = true;
            return IntPtr.Zero;
        }
        if (message != 0x00FF || !(tabsOpen || settingsOpen)) return IntPtr.Zero;
        uint size = (uint)Marshal.SizeOf(typeof(RawMouseInput));
        RawMouseInput input;
        uint read = GetRawInputData(lParam, 0x10000003, out input, ref size, (uint)Marshal.SizeOf(typeof(RawInputHeader)));
        if (read != size || input.Header.Type != 0 || (input.Mouse.ButtonFlags & 0x0155) == 0)
            return IntPtr.Zero;
        NativePoint screenPoint;
        if (GetCursorPos(out screenPoint)) DismissTabsAt(IslandPoint(new Point(screenPoint.X, screenPoint.Y)));
        return IntPtr.Zero;
    }

    private void DismissTabsAt(Point cursorPosition)
    {
        if (clock.Elapsed.TotalSeconds - soundMenuClosedAt < 0.25
            || (inlinePlacement != null && inlinePlacement.Sound.IsDropDownOpen)
            || (windowPlacement != null && windowPlacement.Sound.IsDropDownOpen))
            return;
        Point panelPoint = new Point(cursorPosition.X - Canvas.GetLeft(panel), cursorPosition.Y - Canvas.GetTop(panel));
        var shelf = new Rect(Canvas.GetLeft(tabShelf), Canvas.GetTop(tabShelf), tabShelf.Width, tabShelf.Height);
        if (gapFrame.Contains(cursorPosition) || (tabsOpen && shelf.Contains(cursorPosition))) return;
        if (panel.Clip.FillContains(panelPoint)) return;
        tabsOpen = settingsOpen = false;
        UpdateTabAppearance();
        suppressHover = true;
        lastHover = double.NegativeInfinity;
    }

    private void UpdateClock()
    {
        DateTime local = DateTime.Now;
        string time = local.ToString(use24HourTime ? "HH:mm:ss" : "h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
        string compact = time;
        if (IsSideDock)
        {
            string digits = local.ToString(use24HourTime ? "HHmmss" : "hhmmss", System.Globalization.CultureInfo.InvariantCulture);
            compact = digits.Substring(0, 2) + "\n··\n" + digits.Substring(2, 2) + "\n··\n" + digits.Substring(4, 2);
            if (!use24HourTime) compact += "\n\n" + local.ToString("tt", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (compactClock.Text == compact && largeClock.Text == time) return;
        compactClock.Text = compact;
        largeClock.Text = time;
        dateLabel.Text = local.ToString("D");
    }

    private void PrepareClockTool(int index)
    {
        clockToolIndex = index;
        clockTool.Children.Clear();
        largeClock.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        dateLabel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button ClockToolButton(string name, string caption, Action action)
    {
        var button = Control(name, caption, action);
        button.Width = 98;
        button.FontSize = 12;
        button.Foreground = title.Foreground;
        button.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        return button;
    }

    private void OpenStopwatch()
    {
        if (alarmRinging) return;
        if (clockToolIndex == 1) { PrepareClockTool(0); return; }
        PrepareClockTool(1);
        stopwatchDisplay.TextAlignment = TextAlignment.Center;
        stopwatchDisplay.Foreground = title.Foreground;
        stopwatchDisplay.Text = stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.ff");
        clockTool.Children.Add(stopwatchDisplay);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 9, 0, 0) };
        Button start = null;
        start = ClockToolButton("Start or pause stopwatch", stopwatch.IsRunning ? "⏸ Pause" : "▶ Start", delegate
        {
            if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start();
            start.Content = stopwatch.IsRunning ? "⏸ Pause" : "▶ Start";
        });
        actions.Children.Add(start);
        actions.Children.Add(ClockToolButton("Reset stopwatch", "↺ Reset", delegate { stopwatch.Reset(); start.Content = "▶ Start"; }));
        clockTool.Children.Add(actions);
    }

    private void OpenAlarm()
    {
        OpenAlarmEditor(false);
    }

    private void OpenWakeUp()
    {
        OpenAlarmEditor(true);
    }

    private void OpenAlarmEditor(bool wakeUp)
    {
        if (alarmRinging) return;
        int index = wakeUp ? 3 : 2;
        if (clockToolIndex == index) { PrepareClockTool(0); return; }
        PrepareClockTool(index);
        DateTime initial = wakeUpAt ?? DateTime.Today.AddHours(7);
        int hour = wakeUp ? initial.Hour : 0;
        int minute = wakeUp ? initial.Minute : 0;
        int second = wakeUp ? initial.Second : 0;
        var inputRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var hourDigits = AlarmNumber();
        var secondDigits = AlarmNumber();
        var minuteDigits = AlarmNumber();
        Action refresh = delegate
        {
            hourDigits.Text = hour.ToString("00");
            minuteDigits.Text = minute.ToString("00");
            secondDigits.Text = second.ToString("00");
        };
        inputRow.Children.Add(AlarmDial("Hour", hourDigits, 23, delegate(int value) { hour = value; }, delegate(int change) { hour = (hour + change + 24) % 24; refresh(); }));
        var colon = Label(30, lightTheme ? "#687080" : "#9EA8B8");
        colon.Text = ":";
        colon.VerticalAlignment = VerticalAlignment.Center;
        colon.Margin = new Thickness(3, 0, 3, 0);
        inputRow.Children.Add(colon);
        inputRow.Children.Add(AlarmDial("Minute", minuteDigits, 59, delegate(int value) { minute = value; }, delegate(int change) { minute = (minute + change + 60) % 60; refresh(); }));
        var secondColon = Label(30, lightTheme ? "#687080" : "#9EA8B8");
        secondColon.Text = ":";
        secondColon.VerticalAlignment = VerticalAlignment.Center;
        secondColon.Margin = new Thickness(3, 0, 3, 0);
        inputRow.Children.Add(secondColon);
        inputRow.Children.Add(AlarmDial("Second", secondDigits, 59, delegate(int value) { second = value; }, delegate(int change) { second = (second + change + 60) % 60; refresh(); }));
        refresh();
        clockTool.Children.Add(inputRow);
        alarmStatus.Margin = new Thickness(0, 8, 0, 8);
        alarmStatus.TextAlignment = TextAlignment.Center;
        alarmStatus.Foreground = title.Foreground;
        RefreshAlarmStatus();
        clockTool.Children.Add(alarmStatus);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        actions.Children.Add(ClockToolButton(wakeUp ? "Set wake-up alarm" : "Set countdown", wakeUp ? "⏰ Set" : "⏳ Set", delegate
        {
            TimeSpan delay = new TimeSpan(hour, minute, second);
            if (!wakeUp && delay == TimeSpan.Zero)
            {
                alarmNotice.Text = "Enter a duration greater than zero.";
                return;
            }
            if (wakeUp)
            {
                DateTime now = DateTime.Now;
                DateTime next = now.Date.Add(delay);
                wakeUpAt = next <= now ? next.AddDays(1) : next;
            }
            else alarmAt = DateTime.Now.Add(delay);
            alarmNotice.Text = "";
            RefreshAlarmStatus();
        }));
        actions.Children.Add(ClockToolButton(wakeUp ? "Cancel wake-up alarm" : "Cancel countdown", "× Cancel", delegate
        {
            if (wakeUp) wakeUpAt = null; else alarmAt = null;
            RefreshAlarmStatus();
        }));
        clockTool.Children.Add(actions);
    }
    private TextBox AlarmNumber()
    {
        return new TextBox
        {
            FontSize = 32, Width = 53, MaxLength = 2, Padding = new Thickness(0),
            Foreground = Brush(lightTheme ? "#151A21" : "#F0F2F6"),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            CaretBrush = title.Foreground, TextAlignment = TextAlignment.Center
        };
    }

    private Border AlarmDial(string name, TextBox digits, int maximum, Action<int> setValue, Action<int> change)
    {
        AutomationProperties.SetName(digits, "Alarm " + name.ToLower());
        digits.MouseEnter += delegate
        {
            Activate();
            digits.Focus();
            digits.SelectAll();
        };
        digits.PreviewTextInput += delegate(object sender, TextCompositionEventArgs args)
        {
            string proposed = digits.Text.Remove(digits.SelectionStart, digits.SelectionLength).Insert(digits.SelectionStart, args.Text);
            int value;
            args.Handled = !Int32.TryParse(proposed, out value) || value < 0 || value > maximum || proposed.Length > 2;
        };
        DataObject.AddPastingHandler(digits, delegate(object sender, DataObjectPastingEventArgs args) { args.CancelCommand(); });
        digits.TextChanged += delegate
        {
            int value;
            if (String.IsNullOrEmpty(digits.Text)) setValue(0);
            else if (Int32.TryParse(digits.Text, out value) && value >= 0 && value <= maximum) setValue(value);
        };
        digits.LostKeyboardFocus += delegate
        {
            int value;
            if (!Int32.TryParse(digits.Text, out value)) value = 0;
            digits.Text = Math.Max(0, Math.Min(maximum, value)).ToString("00");
        };
        var content = new StackPanel { Margin = new Thickness(5, 3, 5, 5) };
        var up = ClockToolButton("Increase " + name.ToLower(), "⌃", delegate { change(1); });
        var down = ClockToolButton("Decrease " + name.ToLower(), "⌄", delegate { change(-1); });
        up.Width = down.Width = 53;
        up.Height = down.Height = 21;
        up.Background = down.Background = Brushes.Transparent;
        digits.TextAlignment = TextAlignment.Center;
        digits.FontFamily = new FontFamily("Segoe UI");
        digits.FontWeight = FontWeights.Light;
        var caption = Label(10, lightTheme ? "#515B68" : "#B2BDCB");
        caption.Text = name.ToUpperInvariant();
        caption.TextAlignment = TextAlignment.Center;
        content.Children.Add(up);
        content.Children.Add(digits);
        content.Children.Add(down);
        content.Children.Add(caption);
        var card = new Border
        {
            CornerRadius = new CornerRadius(16), Child = content,
            Background = Brush(lightTheme ? "#50FFFFFF" : "#251F3147"),
            BorderBrush = Brush(lightTheme ? "#B0FFFFFF" : "#4CABBCCF"),
            BorderThickness = new Thickness(1)
        };
        card.MouseWheel += delegate(object sender, MouseWheelEventArgs args) { change(args.Delta > 0 ? 1 : -1); args.Handled = true; };
        return card;
    }

    private void RefreshAlarmStatus()
    {
        if (clockToolIndex == 3)
        {
            alarmStatus.Text = wakeUpAt.HasValue
                ? "⏰ " + wakeUpAt.Value.ToString("ddd, HH:mm:ss") + " · Local time"
                : "Wake-up time · 24-hour clock";
            return;
        }
        if (!alarmAt.HasValue) { alarmStatus.Text = "Hours · Minutes · Seconds"; return; }
        TimeSpan remaining = alarmAt.Value - DateTime.Now;
        int seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        string countdown = seconds >= 3600
            ? (seconds / 3600).ToString() + "h " + ((seconds / 60) % 60).ToString("00") + "m " + (seconds % 60).ToString("00") + "s"
            : (seconds / 60).ToString() + "m " + (seconds % 60).ToString("00") + "s";
        alarmStatus.Text = "⏳ Rings in " + countdown;
    }

    private void CheckAlarm()
    {
        DateTime now = DateTime.Now;
        bool countdownDue = alarmAt.HasValue && now >= alarmAt.Value;
        bool wakeUpDue = wakeUpAt.HasValue && now >= wakeUpAt.Value;
        if (!countdownDue && !wakeUpDue) return;
        if (countdownDue) alarmAt = null;
        if (wakeUpDue) wakeUpAt = null;
        RefreshAlarmStatus();
        ringingWakeUp = wakeUpDue;
        StartAlarmSound();
        clockToolIndex = 0;
        clockTool.Children.Clear();
        largeClock.Visibility = dateLabel.Visibility = Visibility.Visible;
        preferredPage = selectedTab = 1;
        settingsOpen = false;
        tabsOpen = true;
        RecoverIsland();
        preferredPage = selectedTab = 1;
        tabsOpen = false;
        revealUntil = 0;
        lastHover = double.NegativeInfinity;
        suppressHover = true;
        alarmNotice.Text = wakeUpDue ? "⏰ Wake-up alarm" : "⏳ Timer finished";
        dismissAlarm.Visibility = Visibility.Visible;
    }

    private void PlayAlarmSound()
    {
        if (alarmRinging) return;
        LoadAlarmSound();
        if (alarmAudio != null) alarmPlayer.Play();
    }

    private void StartAlarmSound()
    {
        LoadAlarmSound();
        alarmRinging = true;
        alarmSoundEndsAt = clock.Elapsed.TotalSeconds + 180;
        if (alarmAudio != null) alarmPlayer.PlayLooping();
    }

    private void DismissAlarm()
    {
        alarmPlayer.Stop();
        alarmRinging = false;
        alarmSoundEndsAt = double.NegativeInfinity;
        alarmNotice.Text = "";
        dismissAlarm.Visibility = Visibility.Collapsed;
        compactDismiss.Visibility = Visibility.Hidden;
    }

    private void LoadAlarmSound()
    {
        alarmPlayer.Stop();
        alarmPlayer.Stream = null;
        if (alarmAudio != null) { alarmAudio.Dispose(); alarmAudio = null; }
        if (alarmSound == AlarmSounds.Length - 1) return;
        alarmAudio = CreateNotificationSound(alarmSound);
        alarmPlayer.Stream = alarmAudio;
    }

    private static MemoryStream CreateNotificationSound(int sound)
    {
        const int rate = 44100;
        double[] notes = sound == 0 ? new[] { 659.25, 880.0, 1318.51 }
            : sound == 1 ? new[] { 620.0, 920.0, 780.0 }
            : sound == 2 ? new[] { 1046.50, 1567.98 }
            : new[] { 523.25, 659.25, 523.25 };
        double spacing = sound == 2 ? 0.23 : sound == 3 ? 0.28 : 0.16;
        double decay = sound == 2 ? 0.32 : sound == 1 ? 0.075 : 0.19;
        int samples = (int)((spacing * (notes.Length - 1) + decay * 5) * rate);
        var stream = new MemoryStream(44 + samples * 2);
        var writer = new System.IO.BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * 2);
        for (int i = 0; i < samples; i++)
        {
            double time = (double)i / rate;
            double signal = 0;
            for (int note = 0; note < notes.Length; note++)
            {
                double age = time - note * spacing;
                if (age < 0) continue;
                double phase = 2 * Math.PI * notes[note] * age;
                if (sound == 1) phase += 2 * Math.PI * 14 * (1 - Math.Exp(-age * 24));
                double tone = Math.Sin(phase);
                if (sound == 2) tone += 0.23 * Math.Sin(phase * 2.76) * Math.Exp(-age * 7);
                double envelope = Math.Min(1, age / 0.009) * Math.Exp(-age / decay);
                double fadeOut = Math.Min(1, (samples - i - 1) / (rate * 0.025));
                signal += tone * envelope * fadeOut * 0.24;
            }
            writer.Write((short)(Math.Max(-0.85, Math.Min(0.85, signal)) * Int16.MaxValue));
        }
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private void UpdateTabAppearance()
    {
        highlightedPage = CurrentPage(clock.Elapsed.TotalSeconds);
        var foreground = title.Foreground;
        var normal = Brush(lightTheme ? "#140F172A" : "#29AFBFD1");
        var active = Brush(lightTheme ? "#280F172A" : "#63AFBFD1");
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].Foreground = foreground;
            bool selected = (i == 2 ? 3 : i) == highlightedPage;
            tabs[i].Background = selected ? active : normal;
            tabs[i].FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
        themeButton.Foreground = tabsButton.Foreground = settingsButton.Foreground = foreground;
        themeButton.Background = normal;
        tabsButton.Background = tabsOpen ? active : normal;
        settingsButton.Background = settingsOpen ? active : normal;
        tabShelf.Background = Brush(lightTheme ? "#FAF7F8FA" : "#F215181E");
        tabShelf.BorderBrush = Brush(lightTheme ? "#309199A8" : "#668E9BAA");
    }

    private async void RefreshMedia()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            if (manager == null)
                manager = await Complete(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());

            var current = manager.GetCurrentSession();
            var active = new Dictionary<string, long>();
            GlobalSystemMediaTransportControlsSession newest = null;
            long newestOrder = -1;
            long newOrder = ++playbackOrder;
            foreach (var candidate in manager.GetSessions())
            {
                if (candidate.GetPlaybackInfo().PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    continue;

                string id = candidate.SourceAppUserModelId;
                long order;
                if (!playingSince.TryGetValue(id, out order)) order = newOrder;
                active[id] = order;
                if (order > newestOrder || (order == newestOrder && candidate == current))
                {
                    newest = candidate;
                    newestOrder = order;
                }
            }
            playingSince.Clear();
            foreach (var entry in active) playingSince.Add(entry.Key, entry.Value);

            var selected = newest ?? current;
            if (selected == null)
            {
                session = null;
                ShowIdle("Play something in Apple Music, Spotify, or another player");
                return;
            }

            if (session != selected)
            {
                session = selected;
                requestedSeekUntil = 0;
            }
            var media = await Complete(selected.TryGetMediaPropertiesAsync());
            var info = selected.GetPlaybackInfo();
            var times = selected.GetTimelineProperties();
            playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            hasMedia = true;
            string trackTitle = media == null ? null : media.Title;
            string trackArtist = media == null ? null : media.Artist;
            if (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
            {
                string key = String.Join("\n", selected.SourceAppUserModelId, trackTitle, trackArtist);
                if (!pausedSince.HasValue || key != pausedMediaKey) pausedSince = clock.Elapsed.TotalSeconds;
                pausedMediaKey = key;
            }
            else pausedSince = null;
            SetMediaLabels(String.IsNullOrWhiteSpace(trackTitle) ? "Now playing" : trackTitle,
                String.IsNullOrWhiteSpace(trackArtist) ? SourceName(selected.SourceAppUserModelId) : trackArtist);
            RefreshFavorite(selected.SourceAppUserModelId, title.Text, artist.Text);
            SetMarquee(title.Text + "  ·  " + artist.Text, true);

            timelineStart = times.StartTime.TotalSeconds;
            length = Math.Max(0, times.EndTime.TotalSeconds - timelineStart);
            playbackRate = info.PlaybackRate ?? 1;
            string identity = selected.SourceAppUserModelId + "\n" + title.Text + "\n" + artist.Text + "\n" + length.ToString();
            bool changedTrack = playbackIdentity != identity;
            playbackIdentity = identity;
            if (!draggingTimeline && clock.Elapsed.TotalSeconds >= requestedSeekUntil)
            {
                double age = playing && times.LastUpdatedTime.Year >= 2000
                    ? Math.Max(0, (DateTimeOffset.UtcNow - times.LastUpdatedTime).TotalSeconds) : 0;
                double reported = Math.Max(0, Math.Min(length, times.Position.TotalSeconds - timelineStart + age * playbackRate));
                double difference = reported - position;
                if (changedTrack || !playing || Math.Abs(difference) > 2.5)
                {
                    position = reported;
                    playbackCorrection = 0;
                }
                else playbackCorrection = Math.Abs(difference) < 0.35 ? 0 : difference;
            }

            updatingTimeline = true;
            timeline.Maximum = length;
            updatingTimeline = false;
            duration.Text = length > 0 ? FormatTime(length) : "–:––";
            timeline.IsEnabled = length > 0 && (info.Controls.IsPlaybackPositionEnabled || IsAppleMusic(selected.SourceAppUserModelId));
            previous.IsEnabled = info.Controls.IsPreviousEnabled;
            next.IsEnabled = info.Controls.IsNextEnabled;
            play.IsEnabled = playing ? info.Controls.IsPauseEnabled : info.Controls.IsPlayEnabled;
            RefreshPlayButton();
            await RefreshArtwork(selected.SourceAppUserModelId, media);
        }
        catch (Exception error)
        {
            Debug.WriteLine(error);
            session = null;
            ShowIdle(manager == null ? "Windows media controls are unavailable" : "Waiting for playback");
        }
        finally
        {
            refreshing = false;
        }
    }

    private void ShowIdle(string message)
    {
        playing = false;
        hasMedia = false;
        pausedSince = null;
        position = 0;
        playbackCorrection = 0;
        playbackIdentity = null;
        length = 0;
        SetMediaLabels("Nothing playing", message);
        favoriteIdentity = null;
        favoriteState = null;
        UpdateFavorite(false);
        SetMarquee(title.Text, false);
        duration.Text = "–:––";
        timeline.IsEnabled = false;
        play.IsEnabled = false;
        previous.IsEnabled = false;
        next.IsEnabled = false;
        artworkKey = null;
        SetCover(null);
        RefreshPlayButton();
        RefreshPosition();
    }

    private void SetMediaLabels(string track, string subtitle)
    {
        if (title.Text == track && artist.Text == subtitle) return;
        title.Text = track;
        artist.Text = subtitle;
        measuredMediaWidth = Clamp(106 + Math.Max(TextWidth(title), TextWidth(artist)), 246, MediaWidth);
    }

    private static double TextWidth(TextBlock label)
    {
        var face = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch);
        var text = new FormattedText(label.Text, System.Globalization.CultureInfo.CurrentUICulture, label.FlowDirection,
            face, label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip);
        return text.Width;
    }

    private async Task RefreshArtwork(string source, GlobalSystemMediaTransportControlsSessionMediaProperties media)
    {
        string key = media == null ? null : String.Join("\n", source, media.Title, media.Artist, media.AlbumTitle);
        if (key != artworkKey)
        {
            artworkKey = key;
            SetCover(null);
            coverPlaceholder.Visibility = Visibility.Visible;
        }
        if (cover.Source != null || media == null || media.Thumbnail == null) return;
        try
        {
            using (var stream = await Complete(media.Thumbnail.OpenReadAsync()))
            {
                if (stream.Size == 0 || stream.Size > 12 * 1024 * 1024) return;
                using (var reader = new DataReader(stream))
                {
                    uint count = await Complete(reader.LoadAsync((uint)stream.Size));
                    var bytes = new byte[count];
                    reader.ReadBytes(bytes);
                    using (var imageStream = new MemoryStream(bytes))
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 144;
                        image.StreamSource = imageStream;
                        image.EndInit();
                        image.Freeze();
                        SetCover(image);
                        coverPlaceholder.Visibility = Visibility.Hidden;
                    }
                }
            }
        }
        catch (Exception error) { Debug.WriteLine(error); }
    }

    private void SetMarquee(string text, bool repeat)
    {
        if (smallTrack.Text == text && (smallTrackRepeat.Visibility == Visibility.Visible) == repeat) return;
        smallTrack.Text = smallTrackRepeat.Text = text;
        sideMarquee.SetTitle(title.Text, repeat);
        smallTrack.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        marqueeTextWidth = smallTrack.DesiredSize.Width;
        marqueeOffset = 0;
        marqueeDelay = 0.65;
        Canvas.SetLeft(smallTrack, 0);
        Canvas.SetLeft(smallTrackRepeat, marqueeTextWidth + Math.Max(29, marquee.ActualWidth));
        smallTrackRepeat.Visibility = repeat ? Visibility.Visible : Visibility.Hidden;
    }

    private void SetCover(BitmapSource image)
    {
        if (cover.Source == image) return;
        cover.Source = coverWash.Source = image;
        coverAccent = image == null ? (Color?)null : ReadCoverAccent(image);
        ApplyMediaAccent();
    }

    private static Color? ReadCoverAccent(BitmapSource source)
    {
        var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        var weight = new double[12];
        var red = new double[12];
        var green = new double[12];
        var blue = new double[12];
        for (int y = 1; y < image.PixelHeight; y += 3)
        {
            for (int x = 1; x < image.PixelWidth; x += 3)
            {
                int index = y * stride + x * 4;
                byte r = pixels[index + 2], g = pixels[index + 1], b = pixels[index];
                int high = Math.Max(r, Math.Max(g, b)), low = Math.Min(r, Math.Min(g, b));
                if (pixels[index + 3] < 140 || high < 36 || high - low < 20) continue;
                var color = System.Drawing.Color.FromArgb(r, g, b);
                int hue = (int)((color.GetHue() + 15) / 30) % weight.Length;
                double score = (high - low) * (0.5 + high / 255.0);
                weight[hue] += score;
                red[hue] += r * score;
                green[hue] += g * score;
                blue[hue] += b * score;
            }
        }
        int best = 0;
        for (int i = 1; i < weight.Length; i++)
            if (weight[i] > weight[best]) best = i;
        if (weight[best] == 0) return null;
        return Color.FromRgb((byte)(red[best] / weight[best]), (byte)(green[best] / weight[best]), (byte)(blue[best] / weight[best]));
    }

    private async Task Seek(double value)
    {
        var selected = session;
        if (selected == null || !timeline.IsEnabled) return;
        position = value;
        playbackCorrection = 0;
        requestedSeekUntil = clock.Elapsed.TotalSeconds + 1.2;
        try
        {
            double start = timelineStart;
            double durationSeconds = length;
            bool changed;
            if (IsAppleMusic(selected.SourceAppUserModelId) && !selected.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled)
                changed = await Task.Run(() => SeekAppleMusic(value / durationSeconds));
            else
                changed = await Complete(selected.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(start + value).Ticks));
            if (!changed && session == selected) requestedSeekUntil = 0;
        }
        catch (Exception error)
        {
            Debug.WriteLine(error);
            if (session == selected) requestedSeekUntil = 0;
        }
    }

    private static bool SeekAppleMusic(double fraction)
    {
        foreach (Process process in Process.GetProcessesByName("AppleMusic"))
        {
            using (process)
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                var window = AutomationElement.FromHandle(process.MainWindowHandle);
                var scrubber = window.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "LCDScrubber"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider)));
                object pattern;
                if (scrubber == null || !scrubber.TryGetCurrentPattern(RangeValuePattern.Pattern, out pattern)) continue;
                var range = (RangeValuePattern)pattern;
                var info = range.Current;
                if (info.IsReadOnly || info.Maximum <= info.Minimum) continue;
                range.SetValue(info.Minimum + (info.Maximum - info.Minimum) * Math.Max(0, Math.Min(1, fraction)));
                return true;
            }
        }
        return false;
    }

    private async void RefreshFavorite(string source, string track, string artistName)
    {
        string identity = source + "\n" + track + "\n" + artistName;
        if (favoriteIdentity != identity)
        {
            favoriteIdentity = identity;
            favoriteState = null;
            nextFavoriteRead = 0;
            UpdateFavorite(false);
        }
        if (favoriteBusy || favoritePolling || clock.Elapsed.TotalSeconds < nextFavoriteRead) return;
        favoritePolling = true;
        nextFavoriteRead = clock.Elapsed.TotalSeconds + 1.5;
        try
        {
            bool? state = await Task.Run(() => ReadFavorite(source, track, false));
            if (favoriteIdentity != identity || favoriteBusy) return;
            favoriteState = state;
            UpdateFavorite(state.HasValue);
        }
        finally { favoritePolling = false; }
    }

    private async void ToggleFavorite()
    {
        if (session == null || favoriteBusy) return;
        string identity = favoriteIdentity;
        string source = session.SourceAppUserModelId;
        string track = title.Text;
        favoriteBusy = true;
        UpdateFavorite(false);
        bool? state;
        try { state = await Task.Run(() => ReadFavorite(source, track, true)); }
        finally { favoriteBusy = false; }
        if (favoriteIdentity == identity)
        {
            favoriteState = state;
            UpdateFavorite(state.HasValue);
            nextFavoriteRead = 0;
        }
    }

    private void UpdateFavorite(bool available)
    {
        favorite.Content = favoriteState == true ? "★" : "☆";
        favorite.IsEnabled = available && !favoriteBusy;
        favorite.Opacity = favorite.IsEnabled ? 1 : 0.45;
        favorite.Foreground = favoriteState == true ? Brush(lightTheme ? "#8B610B" : "#FFD377") : title.Foreground;
        string name = favoriteState == true ? "Remove favorite" : "Favorite track";
        AutomationProperties.SetName(favorite, name);
        favorite.ToolTip = favorite.IsEnabled ? name : "Favorite control unavailable in the current player";
    }

    private static bool? ReadFavorite(string source, string track, bool toggle)
    {
        bool apple = IsAppleMusic(source);
        if (!apple && source.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) < 0) return null;
        foreach (var process in Process.GetProcessesByName(apple ? "AppleMusic" : "Spotify"))
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    var window = AutomationElement.FromHandle(process.MainWindowHandle);
                    var bar = window.FindFirst(TreeScope.Descendants, new PropertyCondition(
                        apple ? AutomationElement.AutomationIdProperty : AutomationElement.NameProperty,
                        apple ? "LCD" : "Now playing bar"));
                    if (bar == null) continue;
                    var currentTitle = bar.FindFirst(TreeScope.Descendants, new PropertyCondition(
                        apple ? AutomationElement.AutomationIdProperty : AutomationElement.NameProperty,
                        apple ? "ScrollingText" : track));
                    if (currentTitle == null || !String.Equals(currentTitle.Current.Name, track, StringComparison.OrdinalIgnoreCase)) continue;
                    var buttons = bar.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    bool? appleLibraryState = apple ? ReadAppleLibraryFavorite(window, bar, track) : null;
                    foreach (AutomationElement button in buttons)
                    {
                        string name = button.Current.Name;
                        bool marked = name == "Favorited" || name == "Favourited" || name == "Remove from Favorites"
                            || name == "Remove from Liked Songs";
                        bool unmarked = name == "Favorite" || name == "Favourite" || name == "Add to Liked Songs"
                            || name == "Save to Your Library";
                        bool playlist = !apple && name == "Add to playlist";
                        if (!marked && !unmarked && !playlist) continue;
                        object togglePattern;
                        if (button.TryGetCurrentPattern(TogglePattern.Pattern, out togglePattern))
                            marked = ((TogglePattern)togglePattern).Current.ToggleState == ToggleState.On;
                        else if (playlist)
                            marked = button.Current.ClassName.IndexOf("text-bright-accent", StringComparison.Ordinal) >= 0;
                        if (appleLibraryState.HasValue) marked = appleLibraryState.Value;
                        if (!toggle) return marked;
                        if (playlist && marked) return ChangeSpotifyLiked(window, button);
                        object invocation;
                        if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out invocation)) return null;
                        ((InvokePattern)invocation).Invoke();
                        System.Threading.Thread.Sleep(250);
                        return ReadFavorite(source, track, false);
                    }
                }
                catch (Exception error) { Debug.WriteLine(error); }
            }
        }
        return null;
    }

    private static bool? ReadAppleLibraryFavorite(AutomationElement window, AutomationElement bar, string track)
    {
        string performer = "";
        var metadata = bar.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "ScrollingText"));
        if (metadata.Count > 1)
        {
            string subtitle = metadata[1].Current.Name;
            int separator = subtitle.IndexOf(" — ", StringComparison.Ordinal);
            if (separator > 0) performer = subtitle.Substring(0, separator);
        }
        if (String.IsNullOrEmpty(performer)) return null;
        var rows = window.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        foreach (AutomationElement row in rows)
        {
            string name = row.Current.Name;
            if (!name.StartsWith("Track ", StringComparison.Ordinal)) continue;
            int numberEnd = name.IndexOf(' ', 6);
            if (numberEnd < 0) continue;
            string identity = track + " " + performer + " ";
            if (!name.Substring(numberEnd + 1).StartsWith(identity, StringComparison.OrdinalIgnoreCase)) continue;
            return name.IndexOf(" Favorited ", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf(" Favourited ", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        return null;
    }

    private static bool? ChangeSpotifyLiked(AutomationElement window, AutomationElement button)
    {
        object pattern;
        if (button.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out pattern))
            ((ExpandCollapsePattern)pattern).Expand();
        else if (button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            ((InvokePattern)pattern).Invoke();
        else return null;
        AutomationElement menu = null;
        for (int i = 0; i < 8; i++)
        {
            System.Threading.Thread.Sleep(70);
            menu = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Add to playlist menu"));
            if (menu != null && !menu.Current.IsOffscreen) break;
        }
        if (menu == null || menu.Current.IsOffscreen) return null;
        var parent = TreeWalker.RawViewWalker.GetParent(menu);
        AutomationElement cancel = null;
        for (int i = 0; i < 4 && parent != null; i++)
        {
            cancel = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Cancel"));
            if (cancel != null) break;
            parent = TreeWalker.RawViewWalker.GetParent(parent);
        }
        try
        {
            var liked = menu.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, "Liked Songs"),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)));
            if (liked == null || !liked.TryGetCurrentPattern(TogglePattern.Pattern, out pattern)) return null;
            var checkbox = (TogglePattern)pattern;
            bool wasLiked = checkbox.Current.ToggleState == ToggleState.On;
            checkbox.Toggle();
            System.Threading.Thread.Sleep(100);
            if (parent == null) return null;
            var save = parent.FindFirst(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.NameProperty, "Done"),
                new PropertyCondition(AutomationElement.NameProperty, "Save")));
            if (save == null || !save.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) return null;
            ((InvokePattern)pattern).Invoke();
            cancel = null;
            return !wasLiked;
        }
        finally
        {
            if (cancel != null && cancel.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                ((InvokePattern)pattern).Invoke();
        }
    }

    private async Task SendCommand(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> command)
    {
        var selected = session;
        if (selected == null || commandPending) return;
        commandPending = true;
        try { await Complete(command(selected)); }
        catch (Exception error) { Debug.WriteLine(error); }
        finally { commandPending = false; }
        RefreshMedia();
    }

    private static Task<T> Complete<T>(IAsyncOperation<T> operation)
    {
        var completion = new TaskCompletionSource<T>();
        operation.Completed = delegate(IAsyncOperation<T> result, AsyncStatus status)
        {
            try { completion.TrySetResult(result.GetResults()); }
            catch (Exception error) { completion.TrySetException(error); }
        };
        return completion.Task;
    }

    private static string SourceName(string id)
    {
        if (IsAppleMusic(id)) return "Apple Music";
        if (id.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0) return "Spotify";
        return "Windows media";
    }

    private static bool IsAppleMusic(string id)
    {
        return id.IndexOf("AppleMusic", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    private void RefreshPosition()
    {
        updatingTimeline = true;
        timeline.Value = position;
        updatingTimeline = false;
        elapsed.Text = FormatTime(position);
    }

    private void RefreshPlayButton()
    {
        SetControl(play, playing ? "Pause" : "Play", playing ? "⏸" : "▶");
    }

    private static Button Control(string name, string glyph, Action action)
    {
        var button = new Button
        {
            Width = 44, Height = 32, Margin = new Thickness(3, 0, 3, 0),
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 17, Foreground = Brushes.White, Background = Brush("#457F92AD"),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var background = new FrameworkElementFactory(typeof(Border));
        background.Name = "ButtonFace";
        background.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        background.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
        background.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1, 1));
        background.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
        });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        background.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = background };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.73));
        template.Triggers.Add(hover);
        button.Template = template;
        button.MouseEnter += delegate { MagnifyButtonGroup(button, true); };
        button.MouseLeave += delegate { MagnifyButtonGroup(button, false); };
        SetControl(button, name, glyph);
        button.Click += delegate { action(); };
        return button;
    }

    private static void MagnifyButtonGroup(Button hovered, bool active)
    {
        var root = Window.GetWindow(hovered);
        if (root == null || !hovered.IsVisible) return;
        Point origin = hovered.TranslatePoint(new Point(0, 0), root);
        Rect bounds = new Rect(origin, hovered.RenderSize);
        Action<DependencyObject> visit = null;
        visit = delegate(DependencyObject node)
        {
            var candidate = node as Button;
            if (candidate != null && candidate.IsVisible && candidate.Template != null)
            {
                var face = candidate.Template.FindName("ButtonFace", candidate) as Border;
                var scale = face == null ? null : face.RenderTransform as ScaleTransform;
                if (scale != null)
                {
                    if (scale.IsFrozen)
                    {
                        scale = scale.Clone();
                        face.RenderTransform = scale;
                    }
                    Point p = candidate.TranslatePoint(new Point(0, 0), root);
                    Rect other = new Rect(p, candidate.RenderSize);
                    double gapX = Math.Max(0, Math.Max(bounds.Left - other.Right, other.Left - bounds.Right));
                    double gapY = Math.Max(0, Math.Max(bounds.Top - other.Bottom, other.Top - bounds.Bottom));
                    bool sameRow = Math.Abs(bounds.Top + bounds.Height / 2 - other.Top - other.Height / 2) < 12;
                    bool sameColumn = Math.Abs(bounds.Left + bounds.Width / 2 - other.Left - other.Width / 2) < 12;
                    double target = active && candidate == hovered ? 1.22 :
                        active && ((sameRow && gapX < 22) || (sameColumn && gapY < 16)) ? 1.10 : 1;
                    foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                        scale.BeginAnimation(property, new DoubleAnimation(target, TimeSpan.FromMilliseconds(190))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) visit(VisualTreeHelper.GetChild(node, i));
        };
        visit(root);
    }

    private static BeginStoryboard ScaleAnimation(string target, double scale, int milliseconds)
    {
        var storyboard = new Storyboard();
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTargetName(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, property));
            storyboard.Children.Add(animation);
        }
        return new BeginStoryboard { Storyboard = storyboard };
    }

    private static void SetControl(Button button, string name, string glyph)
    {
        button.Content = glyph;
        button.ToolTip = name;
        System.Windows.Automation.AutomationProperties.SetName(button, name);
    }

    private static TextBlock Label(double size, string color)
    {
        return new TextBlock { FontSize = size, Foreground = Brush(color), FontFamily = new FontFamily("Segoe UI") };
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static string FormatTime(double seconds)
    {
        return TimeSpan.FromSeconds(Math.Floor(seconds)).ToString(@"m\:ss");
    }

    private static double Smooth(double value)
    {
        return value * value * (3 - 2 * value);
    }

    private void LoadSettings()
    {
        bool previous = updatingAppearance;
        updatingAppearance = true;
        try { LoadSettingsCore(); }
        finally { updatingAppearance = previous; }
    }

    private void LoadSettingsCore()
    {
        try
        {
            using (var startup = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WindowsStartupKey))
                startWithWindows = startup != null && startup.GetValue(StartupValueName) != null;
        }
        catch (Exception error) { Debug.WriteLine(error); }
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                if (key == null) return;
                string theme = key.GetValue("Theme") as string;
                customTheme = theme == "Custom";
                try { customThemeColor = (Color)ColorConverter.ConvertFromString((string)key.GetValue("CustomColor", "#526D9A")); }
                catch (FormatException) { customThemeColor = Color.FromRgb(82, 109, 154); }
                customThemeColor = Color.FromRgb(customThemeColor.R, customThemeColor.G, customThemeColor.B);
                lightTheme = theme == "Light" || customTheme && IsLightColor(customThemeColor);
                int opacity = (int)key.GetValue("Opacity", 92);
                glassOpacity = Math.Max(0, Math.Min(100, opacity)) / 100.0;
                int mode = (int)key.GetValue("PlacementMode", 0);
                placementMode = (PlacementMode)Math.Max(0, Math.Min(2, mode));
                hideAutomatically = (int)key.GetValue("HideAutomatically", 0) != 0;
                islandScale = Math.Max(60, Math.Min(150, (int)key.GetValue("IslandSize", 100))) / 100.0;
                alarmSound = Math.Max(0, Math.Min(AlarmSounds.Length - 1, (int)key.GetValue("AlarmSound", 0)));
                controllerEnabled = (int)key.GetValue("ControllerEnabled", 1) != 0;
                use24HourTime = (int)key.GetValue("Use24HourTime", 0) != 0;
                showBatteryPercentage = (int)key.GetValue("ShowBatteryPercentage", 0) != 0;
                showAppTitles = (int)key.GetValue("ShowAppTitles", 1) != 0;
                appsGrid = (int)key.GetValue("AppsGrid", 1) != 0;
                appsDock = (int)key.GetValue("AppsDock", 0) != 0;
                appGridChoice.IsChecked = appsGrid && !appsDock;
                appListChoice.IsChecked = !appsGrid && !appsDock;
                appDockChoice.IsChecked = appsDock;
                foreach (string app in key.GetValue("SelectedApps", new string[0]) as string[] ?? new string[0]) selectedApps.Add(app);
                appTitlesToggle.IsChecked = showAppTitles;
                recoveryModifiers = (uint)((int)key.GetValue("RecoveryModifiers", 3) & 15);
                recoveryKey = (uint)(int)key.GetValue("RecoveryKey", 0x49);
            }
        }
        catch (Exception error) { Debug.WriteLine(error); }
    }

    private void SaveSettings()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsKey))
            {
                key.SetValue("Theme", customTheme ? "Custom" : lightTheme ? "Light" : "Dark");
                key.SetValue("CustomColor", ColorHex(customThemeColor));
                key.SetValue("IslandSize", (int)Math.Round(islandScale * 100));
                key.SetValue("AlarmSound", alarmSound);
                key.SetValue("ControllerEnabled", controllerEnabled ? 1 : 0);
                key.SetValue("Use24HourTime", use24HourTime ? 1 : 0);
                key.SetValue("ShowBatteryPercentage", showBatteryPercentage ? 1 : 0);
                key.SetValue("ShowAppTitles", showAppTitles ? 1 : 0);
                key.SetValue("AppsGrid", appsGrid ? 1 : 0);
                key.SetValue("AppsDock", appsDock ? 1 : 0);
                var selected = new string[selectedApps.Count];
                selectedApps.CopyTo(selected);
                key.SetValue("SelectedApps", selected, Microsoft.Win32.RegistryValueKind.MultiString);
                key.SetValue("Opacity", (int)Math.Round(glassOpacity * 100));
                key.SetValue("PlacementMode", (int)placementMode);
                key.SetValue("HideAutomatically", hideAutomatically ? 1 : 0);
                key.SetValue("RecoveryModifiers", (int)recoveryModifiers);
                key.SetValue("RecoveryKey", (int)recoveryKey);
            }
        }
        catch (Exception error) { Debug.WriteLine(error); }
    }

    private void ApplyAppearance()
    {
        if (customTheme) lightTheme = IsLightColor(customThemeColor);
        var fill = customTheme
            ? Gradient(ColorHex(MixColor(customThemeColor, Colors.White, .12)), ColorHex(MixColor(customThemeColor, Colors.White, .05)), ColorHex(customThemeColor), ColorHex(MixColor(customThemeColor, Colors.Black, .08)))
            : lightTheme ? Gradient("#FFFFFF", "#FCFCFD", "#F0F1F4", "#E8EAF0")
            : Gradient("#191A1D", "#0D0E10", "#050506", "#141518");
        fill = fill.Clone();
        fill.Opacity = glassOpacity;
        panel.Background = Brushes.Transparent;
        panel.Cursor = placementMode == PlacementMode.Locked ? Cursors.Arrow : Cursors.SizeAll;
        glassTint.Background = fill;
        rim.Stroke = lightTheme
            ? Gradient("#FFFFFFFF", "#E8FFFFFF", "#55858B98", "#B0FFFFFF")
            : Gradient("#86FFFFFF", "#A4D7E5F4", "#3B8495A8", "#769EADBE");

        var primary = Brush(lightTheme ? "#101419" : "#F7F8FA");
        Resources["IslandControlInk"] = primary;
        if (!Resources.Contains(typeof(CheckBox)))
        {
            foreach (bool radio in new[] { false, true })
            {
                Type kind = radio ? typeof(RadioButton) : typeof(CheckBox);
                var style = new Style(kind);
                style.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, CreateChoiceTemplate(radio)));
                Resources[kind] = style;
            }
        }
        if (settingsWindow != null) settingsWindow.Resources["IslandControlInk"] = primary;
        if (settingsWindow != null)
        {
            settingsWindow.Resources[typeof(CheckBox)] = Resources[typeof(CheckBox)];
            settingsWindow.Resources[typeof(RadioButton)] = Resources[typeof(RadioButton)];
        }
        if (settingsWindow != null)
            settingsWindow.Background = customTheme ? new SolidColorBrush(customThemeColor) : Brush(lightTheme ? "#F2F3F6" : "#17181B");
        if (windowOpacityLabel != null) windowOpacityLabel.Foreground = primary;
        StyleSettingsCard(inlineAppearanceCard);
        StyleSettingsCard(windowAppearanceCard);
        batteryStatus.Foreground = primary;
        foreach (Grid badge in new[] { compactBattery, expandedBattery })
        {
            ((Border)badge.Children[0]).BorderBrush = primary;
            ((Border)badge.Children[1]).Background = primary;
            ((TextBlock)badge.Children[2]).Foreground = primary;
        }
        var secondary = Brush(customTheme ? (lightTheme ? "#252B35" : "#E5E9EF") : lightTheme ? "#525B6B" : "#BDC2C9");
        artwork.Background = Brush(lightTheme ? "#6C6E879B" : "#AA31343A");
        coverPlaceholder.Foreground = secondary;
        title.Foreground = smallTrack.Foreground = smallTrackRepeat.Foreground = primary;
        sideMarquee.SetInk(primary);
        artist.Foreground = elapsed.Foreground = duration.Foreground = secondary;
        foreach (var button in new[] { previous, play, next, favorite })
        {
            button.Foreground = primary;
            button.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        }
        timeline.Background = Brush(lightTheme ? "#C2C7D0" : "#65656B");
        timeline.Foreground = lightTheme ? Brush("#4C5B70") : Brushes.White;
        volumeSlider.Background = timeline.Background;
        volumeSlider.Foreground = timeline.Foreground;
        foreach (var slider in new[] { inlineOpacity, windowOpacity, inlinePlacement.Size, windowPlacement == null ? null : windowPlacement.Size,
            inlineColorControls == null ? null : inlineColorControls.Brightness, windowColorControls == null ? null : windowColorControls.Brightness })
        {
            if (slider == null) continue;
            if (slider.ReadLocalValue(System.Windows.Controls.Control.TemplateProperty) == DependencyProperty.UnsetValue)
                slider.Template = CreateTimelineTemplate();
            slider.Background = timeline.Background;
            slider.Foreground = timeline.Foreground;
        }
        speakerIcon.Foreground = secondary;
        compactClock.Foreground = largeClock.Foreground = opacityText.Foreground = primary;
        dateLabel.Foreground = secondary;
        appStatus.Foreground = secondary;
        appSearch.Foreground = primary;
        appSearch.Background = Brush(lightTheme ? "#CFFFFFFF" : "#D023262D");
        foreach (Control row in appRows.Children) row.Foreground = primary;
        drawerGear.Foreground = appTitlesToggle.Foreground = primary;
        drawerSearchButton.Foreground = primary;
        appGridChoice.Foreground = appListChoice.Foreground = appDockChoice.Foreground = primary;
        foreach (Button row in dockRows.Children) row.Foreground = primary;
        drawerGear.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        drawerSearchButton.Background = drawerGear.Background;
        stopwatchDisplay.Foreground = alarmStatus.Foreground = alarmNotice.Foreground = primary;
        dismissAlarm.Foreground = primary;
        dismissAlarm.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        compactTimer.Foreground = compactDismiss.Foreground = primary;
        compactDismiss.Background = dismissAlarm.Background;
        foreach (var button in clockButtons)
        {
            button.Foreground = primary;
            button.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        }
        themeButton.Content = customTheme ? "Custom" : lightTheme ? "Light" : "Dark";
        SyncColorControls(inlineColorControls, primary, secondary);
        SyncColorControls(windowColorControls, primary, secondary);
        UpdateGlassOpacity();
        updatingAppearance = true;
        if (windowThemeChoice != null) windowThemeChoice.SelectedIndex = customTheme ? 2 : lightTheme ? 1 : 0;
        ApplyPlacementAppearance(inlinePlacement, primary);
        ApplyPlacementAppearance(windowPlacement, primary);
        updatingAppearance = false;
        UpdateTabAppearance();
        ApplyMediaAccent();
        UpdateFavorite(favorite.IsEnabled);
    }

    private void ConnectOpacitySlider(Slider slider)
    {
        slider.ValueChanged += delegate
        {
            if (updatingOpacity) return;
            glassOpacity = slider.Value / 100;
            UpdateGlassOpacity();
            if (!slider.IsMouseCaptureWithin) SaveSettings();
        };
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(delegate { SaveSettings(); }));
    }

    private void UpdateGlassOpacity()
    {
        if (customTheme && lightTheme != IsLightColor(customThemeColor))
        {
            ApplyAppearance();
            return;
        }
        double percent = Math.Round(glassOpacity * 100);
        string text = "Glass opacity  " + percent.ToString("0") + "%";
        glassSurface.Opacity = glassOpacity;
        if (glassTint.Background != null) glassTint.Background.Opacity = glassOpacity;
        opacityText.Text = text;
        updatingOpacity = true;
        inlineOpacity.Value = percent;
        if (windowOpacity != null)
        {
            windowOpacity.Value = percent;
            windowOpacityLabel.Text = text;
        }
        updatingOpacity = false;
    }

    private void ApplyMediaAccent()
    {
        var accent = coverAccent ?? Colors.White;
        double blend = coverAccent.HasValue ? (lightTheme ? 0.23 : 0.32) : 0;
        int endpoint = lightTheme ? 0 : 255;
        var waveColor = new SolidColorBrush(Color.FromRgb(
            (byte)(accent.R + (endpoint - accent.R) * blend),
            (byte)(accent.G + (endpoint - accent.G) * blend),
            (byte)(accent.B + (endpoint - accent.B) * blend)));
        waveColor.Freeze();
        foreach (var bar in bars) bar.Background = waveColor;
        foreach (var bar in sideBars) bar.Background = waveColor;

    }

    private sealed class SettingsCard : Border
    {
        public readonly StackPanel Body = new StackPanel();
        public readonly TextBlock Heading = PlayerWindow.Label(12, "#FFFFFF");
        public SettingsCard(string title)
        {
            CornerRadius = new CornerRadius(12);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12);
            Margin = new Thickness(0, 0, 0, 10);
            Heading.Text = title;
            Heading.FontWeight = FontWeights.SemiBold;
            Heading.Margin = new Thickness(0, 0, 0, 10);
            var content = new StackPanel();
            content.Children.Add(Heading);
            content.Children.Add(Body);
            Child = content;
        }
    }

    private static string ColorHex(Color color) { return String.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B); }
    private static Color MixColor(Color color, Color tint, double amount)
    {
        return Color.FromRgb((byte)(color.R + (tint.R - color.R) * amount), (byte)(color.G + (tint.G - color.G) * amount), (byte)(color.B + (tint.B - color.B) * amount));
    }
    private bool IsLightColor(Color color)
    {
        // The tint and glass layer each apply opacity. Choose ink for the
        // resulting surface over a dark background, rather than the raw swatch.
        color = MixColor(Colors.Black, color, glassOpacity * glassOpacity);
        Func<byte, double> linear = channel => channel / 255.0 <= .04045 ? channel / 3294.6 : Math.Pow((channel / 255.0 + .055) / 1.055, 2.4);
        return .2126 * linear(color.R) + .7152 * linear(color.G) + .0722 * linear(color.B) > .179;
    }
    private void SelectTheme(int theme)
    {
        customTheme = theme == 2;
        lightTheme = theme == 1 || customTheme && IsLightColor(customThemeColor);
        ApplyAppearance();
        SaveSettings();
    }
    private sealed class ColorControls
    {
        public readonly StackPanel Root = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        public readonly ColorBlock Picker = new ColorBlock { Height = 115, Margin = new Thickness(0, 9, 0, 9) };
        public readonly Border Swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 8, 0), BorderThickness = new Thickness(1) };
        public readonly TextBlock Caption = PlayerWindow.Label(12, "#FFFFFF");
        public readonly TextBox Hex = new TextBox { Width = 85, Height = 26, Padding = new Thickness(5, 3, 5, 2), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center };
        public readonly TextBlock BrightnessLabel = PlayerWindow.Label(12, "#FFFFFF");
        public readonly Slider Brightness = new Slider { Minimum = 0, Maximum = 100, IsMoveToPointEnabled = true, Margin = new Thickness(0, 6, 0, 0) };
    }
    private ColorControls BuildColorControls()
    {
        var controls = new ColorControls();
        var row = new DockPanel { LastChildFill = true };
        controls.Caption.Text = "Custom color";
        controls.Caption.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(controls.Hex, Dock.Right);
        row.Children.Add(controls.Hex);
        DockPanel.SetDock(controls.Swatch, Dock.Left);
        row.Children.Add(controls.Swatch);
        row.Children.Add(controls.Caption);
        controls.Root.Children.Add(row);
        controls.Root.Children.Add(controls.Picker);
        controls.Root.Children.Add(controls.BrightnessLabel);
        controls.Root.Children.Add(controls.Brightness);
        AutomationProperties.SetName(controls.Picker, "Custom theme color block");
        AutomationProperties.SetName(controls.Hex, "Custom theme hex color");
        AutomationProperties.SetName(controls.Brightness, "Custom theme brightness");
        controls.Picker.Changed += delegate(Color color, bool committed)
        {
            if (updatingColor) return;
            customThemeColor = color;
            lightTheme = IsLightColor(color);
            ApplyAppearance();
            if (committed) SaveSettings();
        };
        controls.Brightness.ValueChanged += delegate
        {
            if (updatingColor) return;
            controls.Picker.SetBrightness(controls.Brightness.Value / 100);
            customThemeColor = controls.Picker.SelectedColor;
            lightTheme = IsLightColor(customThemeColor);
            ApplyAppearance();
            if (!controls.Brightness.IsMouseCaptureWithin) SaveSettings();
        };
        controls.Brightness.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(delegate { SaveSettings(); }));
        Action applyHex = delegate
        {
            string value = controls.Hex.Text.Trim();
            if (value.Length == 6) value = "#" + value;
            try
            {
                if (value.Length != 7 || value[0] != '#') throw new FormatException();
                var color = (Color)ColorConverter.ConvertFromString(value);
                customThemeColor = Color.FromRgb(color.R, color.G, color.B);
                lightTheme = IsLightColor(customThemeColor);
                controls.Hex.ToolTip = "Enter a color such as #5279C7";
                ApplyAppearance();
                SaveSettings();
            }
            catch (Exception error)
            {
                if (!(error is FormatException) && !(error is ArgumentException)) throw;
                controls.Hex.ToolTip = "Use a six-digit hex color, for example #5279C7";
                controls.Hex.Text = ColorHex(customThemeColor);
            }
        };
        controls.Hex.LostKeyboardFocus += delegate { applyHex(); };
        controls.Hex.KeyDown += delegate(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter) { applyHex(); args.Handled = true; }
            else if (args.Key == Key.Escape) { controls.Hex.Text = ColorHex(customThemeColor); args.Handled = true; }
        };
        return controls;
    }
    private void SyncColorControls(ColorControls controls, Brush primary, Brush secondary)
    {
        if (controls == null) return;
        updatingColor = true;
        try
        {
            controls.Root.Visibility = customTheme ? Visibility.Visible : Visibility.Collapsed;
            controls.Picker.SelectedColor = customThemeColor;
            controls.Brightness.Value = controls.Picker.Brightness * 100;
            controls.BrightnessLabel.Text = "Brightness  " + Math.Round(controls.Brightness.Value) + "%";
            controls.Caption.Foreground = controls.Hex.Foreground = primary;
            controls.BrightnessLabel.Foreground = secondary;
            controls.Swatch.Background = new SolidColorBrush(customThemeColor);
            controls.Swatch.BorderBrush = Brush(lightTheme ? "#33818A9B" : "#66FFFFFF");
            controls.Hex.Background = Brush(lightTheme ? "#90FFFFFF" : "#32000000");
            controls.Hex.BorderBrush = controls.Swatch.BorderBrush;
            if (!controls.Hex.IsKeyboardFocusWithin) controls.Hex.Text = ColorHex(customThemeColor);
        }
        finally { updatingColor = false; }
    }

    private sealed class ColorBlock : FrameworkElement
    {
        private double hue = .61, saturation = .47, brightness = .6;
        private BitmapSource colors;
        private double renderedBrightness = -1;
        public event Action<Color, bool> Changed;
        public double Brightness { get { return brightness; } }
        public ColorBlock() { Focusable = true; Cursor = Cursors.Cross; }
        public Color SelectedColor
        {
            get { return Hsv(hue, saturation, brightness); }
            set
            {
                double r = value.R / 255.0, g = value.G / 255.0, b = value.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
                brightness = max;
                if (max > 0) saturation = delta / max;
                if (delta > 0) hue = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) / 6 + 1) % 1;
                InvalidateVisual();
            }
        }
        public void SetBrightness(double value) { brightness = Math.Max(0, Math.Min(1, value)); InvalidateVisual(); }
        private static Color Hsv(double hue, double saturation, double value)
        {
            double h = (hue % 1) * 6, c = value * saturation, x = c * (1 - Math.Abs(h % 2 - 1)), m = value - c;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; } else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; } else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            const int width = 256, height = 100;
            if (colors == null || renderedBrightness != brightness)
            {
                var pixels = new byte[width * height * 4];
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    Color color = Hsv(x / (double)width, y / (double)(height - 1), brightness);
                    int offset = (y * width + x) * 4;
                    pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R; pixels[offset + 3] = 255;
                }
                colors = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                colors.Freeze();
                renderedBrightness = brightness;
            }
            drawing.PushClip(new RectangleGeometry(new Rect(RenderSize), 7, 7));
            drawing.DrawImage(colors, new Rect(RenderSize));
            drawing.Pop();
            var point = new Point(hue * ActualWidth, saturation * ActualHeight);
            drawing.DrawEllipse(null, new Pen(Brushes.Black, 4), point, 5, 5);
            drawing.DrawEllipse(null, new Pen(Brushes.White, 2), point, 5, 5);
            if (IsKeyboardFocused) drawing.DrawRoundedRectangle(null, new Pen(Brushes.White, 1), new Rect(RenderSize), 7, 7);
        }
        private void Pick(Point point, bool committed)
        {
            hue = Math.Max(0, Math.Min(.999999, point.X / Math.Max(1, ActualWidth)));
            saturation = Math.Max(0, Math.Min(1, point.Y / Math.Max(1, ActualHeight)));
            InvalidateVisual();
            if (Changed != null) Changed(SelectedColor, committed);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args) { base.OnMouseLeftButtonDown(args); Focus(); CaptureMouse(); Pick(args.GetPosition(this), false); args.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs args) { base.OnMouseMove(args); if (IsMouseCaptured) Pick(args.GetPosition(this), false); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args) { base.OnMouseLeftButtonUp(args); if (IsMouseCaptured) { Pick(args.GetPosition(this), true); ReleaseMouseCapture(); args.Handled = true; } }
        protected override void OnKeyDown(KeyEventArgs args)
        {
            base.OnKeyDown(args);
            if (args.Key == Key.Left) hue = Math.Max(0, hue - .01);
            else if (args.Key == Key.Right) hue = Math.Min(.999999, hue + .01);
            else if (args.Key == Key.Up) saturation = Math.Max(0, saturation - .02);
            else if (args.Key == Key.Down) saturation = Math.Min(1, saturation + .02);
            else return;
            InvalidateVisual();
            if (Changed != null) Changed(SelectedColor, true);
            args.Handled = true;
        }
    }

    private void StyleSettingsCard(SettingsCard card)
    {
        if (card == null) return;
        card.Background = Brush(lightTheme ? "#C0FFFFFF" : "#0DFFFFFF");
        card.BorderBrush = Brush(lightTheme ? "#15818A9B" : "#14FFFFFF");
        card.Heading.Foreground = Brush(lightTheme ? "#354150" : "#C3CEDD");
    }

    private sealed class PlacementControls
    {
        public readonly StackPanel Root = new StackPanel();
        public readonly StackPanel Appearance = new StackPanel();
        public readonly List<SettingsCard> Sections = new List<SettingsCard>();
        public readonly TextBlock Label = PlayerWindow.Label(12, "#FFFFFF");
        public readonly RadioButton[] Modes = new RadioButton[3];
        public readonly CheckBox Hide = new CheckBox { Content = "Hide automatically", VerticalAlignment = VerticalAlignment.Center };
        public readonly CheckBox Startup = new CheckBox { Content = "Start with Windows", Margin = new Thickness(0, 7, 0, 0) };
        public readonly CheckBox MilitaryTime = new CheckBox { Content = "24-hour time (military time)", Margin = new Thickness(0, 9, 0, 0) };
        public readonly CheckBox Battery = new CheckBox { Content = "Show battery percentage", Margin = new Thickness(0, 9, 0, 0), ToolTip = "Show a battery icon with percentage in both collapsed and expanded views. Hidden when no battery is detected." };
        public Button Recenter;
        public Button Shortcut;
        public readonly TextBlock SizeLabel = PlayerWindow.Label(12, "#FFFFFF");
        public readonly Slider Size = new Slider { Minimum = 60, Maximum = 150, TickFrequency = 5, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true };
        public readonly TextBlock SoundLabel = PlayerWindow.Label(12, "#FFFFFF");
        public readonly ComboBox Sound = new ComboBox { Width = 145, Height = 28 };
        public Button Preview;
        public readonly CheckBox Controller = new CheckBox { Content = "Controller access", Margin = new Thickness(0, 9, 0, 0), ToolTip = "Hold View + Menu for 0.6 seconds to open. Press View + Menu again to close. A returns to the game, X play/pause, D-pad left/right skip, up/down volume." };
    }

    private PlacementControls BuildPlacementSettings()
    {
        var view = new PlacementControls();
        view.Label.Text = "Drag placement";
        view.Root.Children.Add(view.Label);
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 10) };
        string[] names = { "Locked", "Edges", "Anywhere" };
        for (int i = 0; i < names.Length; i++)
        {
            var mode = (PlacementMode)i;
            var choice = new RadioButton { Content = names[i], Margin = new Thickness(0, 0, 13, 0) };
            choice.Checked += delegate
            {
                if (updatingAppearance) return;
                placementMode = mode;
                if (placementMode == PlacementMode.Edges && edge == DockEdge.Floating)
                {
                    edge = NearestEdge(anchor, monitor);
                    PositionWindow();
                }
                ApplyAppearance();
                SaveSettings();
            };
            view.Modes[i] = choice;
            choices.Children.Add(choice);
        }
        view.Root.Children.Add(choices);
        var actions = new DockPanel { LastChildFill = true };
        view.Recenter = Control("Re-center", "Re-center", Recenter);
        view.Recenter.Width = 80;
        view.Recenter.Height = 27;
        view.Recenter.FontSize = 12;
        view.Recenter.ToolTip = null;
        DockPanel.SetDock(view.Recenter, Dock.Right);
        actions.Children.Add(view.Recenter);
        view.Hide.Checked += delegate { SetAutoHide(true); };
        view.Hide.Unchecked += delegate { SetAutoHide(false); };
        actions.Children.Add(view.Hide);
        view.Root.Children.Add(actions);
        view.Startup.Checked += delegate { SetStartWithWindows(true); };
        view.Startup.Unchecked += delegate { SetStartWithWindows(false); };
        view.Root.Children.Add(view.Startup);
        view.Shortcut = Control("Recovery shortcut", "", delegate
        {
            recordingShortcut = true;
            Activate();
            view.Shortcut.Focus();
            ApplyAppearance();
        });
        view.Shortcut.Width = 228;
        view.Shortcut.FontSize = 12;
        view.Shortcut.Margin = new Thickness(0, 9, 0, 0);
        view.Root.Children.Add(view.Shortcut);
        view.SizeLabel.Margin = new Thickness(0, 9, 0, 4);
        view.Root.Children.Add(view.SizeLabel);
        view.Root.Children.Add(view.Size);
        AutomationProperties.SetName(view.Size, "Island size");
        view.Size.ValueChanged += delegate
        {
            if (updatingSize) return;
            islandScale = view.Size.Value / 100;
            ApplyIslandSize();
            ApplyAppearance();
            SaveSettings();
        };
        view.SoundLabel.Text = "Notification sound";
        view.SoundLabel.Margin = new Thickness(0, 10, 0, 5);
        view.Root.Children.Add(view.SoundLabel);
        var soundRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string sound in AlarmSounds) view.Sound.Items.Add(sound);
        view.Sound.SelectedIndex = alarmSound;
        view.Sound.DropDownClosed += delegate { soundMenuClosedAt = clock.Elapsed.TotalSeconds; };
        AutomationProperties.SetName(view.Sound, "Notification sound");
        view.Sound.SelectionChanged += delegate
        {
            if (updatingAppearance || view.Sound.SelectedIndex < 0) return;
            alarmSound = view.Sound.SelectedIndex;
            ApplyAppearance();
            SaveSettings();
        };
        soundRow.Children.Add(view.Sound);
        view.Preview = ClockToolButton("Preview notification sound", "♪ Preview", PlayAlarmSound);
        view.Preview.Width = 82;
        view.Preview.Height = 28;
        soundRow.Children.Add(view.Preview);
        view.Root.Children.Add(soundRow);
        view.Controller.Checked += delegate { SetControllerEnabled(true); };
        view.Controller.Unchecked += delegate { SetControllerEnabled(false); };
        view.Root.Children.Add(view.Controller);
        view.MilitaryTime.Checked += delegate { Set24HourTime(true); };
        view.MilitaryTime.Unchecked += delegate { Set24HourTime(false); };
        view.Root.Children.Add(view.MilitaryTime);
        RoutedEventHandler batteryChanged = delegate
        {
            if (updatingAppearance) return;
            showBatteryPercentage = view.Battery.IsChecked == true;
            RefreshBattery();
            ApplyAppearance();
            SaveSettings();
        };
        view.Battery.Checked += batteryChanged;
        view.Battery.Unchecked += batteryChanged;
        view.Root.Children.Add(view.Battery);
        view.Root.Children.Clear();
        view.Appearance.Children.Add(view.SizeLabel);
        view.Appearance.Children.Add(view.Size);
        var position = new SettingsCard("Position");
        position.Body.Children.Add(view.Label);
        position.Body.Children.Add(choices);
        position.Body.Children.Add(actions);
        var display = new SettingsCard("Clock & display");
        view.MilitaryTime.Content = "24-hour time";
        view.MilitaryTime.Margin = new Thickness(0);
        view.Battery.Margin = new Thickness(0, 10, 0, 0);
        display.Body.Children.Add(view.MilitaryTime);
        display.Body.Children.Add(view.Battery);
        var soundSection = new SettingsCard("Sound & input");
        view.SoundLabel.Text = "Alarm sound";
        view.SoundLabel.Margin = new Thickness(0, 0, 0, 7);
        soundSection.Body.Children.Add(view.SoundLabel);
        soundSection.Body.Children.Add(soundRow);
        view.Controller.Margin = new Thickness(0, 12, 0, 0);
        soundSection.Body.Children.Add(view.Controller);
        var system = new SettingsCard("System");
        view.Startup.Margin = new Thickness(0);
        view.Shortcut.Width = Double.NaN;
        view.Shortcut.HorizontalAlignment = HorizontalAlignment.Stretch;
        view.Shortcut.Margin = new Thickness(0, 12, 0, 0);
        system.Body.Children.Add(view.Startup);
        system.Body.Children.Add(view.Shortcut);
        view.Sections.AddRange(new[] { position, display, soundSection, system });
        foreach (var section in view.Sections) view.Root.Children.Add(section);
        return view;
    }

    private void RefreshBattery()
    {
        SystemPowerStatus status;
        bool available = GetSystemPowerStatus(out status) && status.BatteryFlag != 255 && (status.BatteryFlag & 128) == 0 && status.BatteryLifePercent <= 100;
        batteryAvailable = available;
        if (available)
        {
            foreach (Grid badge in new[] { compactBattery, expandedBattery })
            {
                ((TextBlock)badge.Children[2]).Text = status.BatteryLifePercent + "%";
                badge.ToolTip = status.ACLineStatus == 1 ? "Battery connected to power" : "Battery power";
            }
        }
    }

    private static Grid CreateBatteryBadge()
    {
        var badge = new Grid { Width = 32, Height = 17, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        badge.Children.Add(new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 3, 0) });
        badge.Children.Add(new Border { Background = Brushes.White, Width = 2, Height = 7, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
        badge.Children.Add(new TextBlock { Foreground = Brushes.White, FontSize = 8, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) });
        return badge;
    }

    private void Set24HourTime(bool enabled)
    {
        if (updatingAppearance) return;
        use24HourTime = enabled;
        UpdateClock();
        ApplyAppearance();
        SaveSettings();
    }

    private void SetStartWithWindows(bool enabled)
    {
        if (updatingAppearance) return;
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(WindowsStartupKey))
            {
                if (enabled)
                {
                    string command = "\"" + typeof(PlayerWindow).Assembly.Location + "\"";
                    key.SetValue(StartupValueName, command, Microsoft.Win32.RegistryValueKind.String);
                }
                else key.DeleteValue(StartupValueName, false);
            }
            startWithWindows = enabled;
            startupError = null;
        }
        catch (Exception error)
        {
            Debug.WriteLine(error);
            startupError = "Windows couldn’t update this startup setting.";
        }
        ApplyAppearance();
    }

    private void SetControllerEnabled(bool enabled)
    {
        if (updatingAppearance) return;
        controllerEnabled = enabled;
        if (!enabled) CloseControllerMode();
        ApplyAppearance();
        SaveSettings();
    }

    private string ShortcutName()
    {
        string name = "";
        if ((recoveryModifiers & 2) != 0) name += "Ctrl+";
        if ((recoveryModifiers & 1) != 0) name += "Alt+";
        if ((recoveryModifiers & 4) != 0) name += "Shift+";
        if ((recoveryModifiers & 8) != 0) name += "Win+";
        return name + KeyInterop.KeyFromVirtualKey((int)recoveryKey);
    }

    private bool RegisterRecoveryShortcut()
    {
        bool registered = RegisterHotKey(overlayHandle, RecoveryHotkeyId, recoveryModifiers | 0x4000, recoveryKey);
        shortcutError = registered ? null : "Shortcut unavailable. Choose another combination.";
        ApplyAppearance();
        return registered;
    }

    private void CaptureRecoveryShortcut(object sender, KeyEventArgs args)
    {
        if (!recordingShortcut) return;
        args.Handled = true;
        Key key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (key == Key.Escape) { recordingShortcut = false; ApplyAppearance(); return; }
        if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
            || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        {
            shortcutError = "Include Ctrl, Alt, or Windows with the key.";
            ApplyAppearance();
            return;
        }
        uint previousModifiers = recoveryModifiers, previousKey = recoveryKey;
        UnregisterHotKey(overlayHandle, RecoveryHotkeyId);
        recoveryModifiers = (uint)modifiers;
        recoveryKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (RegisterRecoveryShortcut())
        {
            recordingShortcut = false;
            SaveSettings();
        }
        else
        {
            recoveryModifiers = previousModifiers;
            recoveryKey = previousKey;
            RegisterRecoveryShortcut();
            shortcutError = "That shortcut is already in use. Try another.";
        }
        ApplyAppearance();
    }

    private void RecoverIsland()
    {
        if (dragArmed) FinishIslandDrag();
        NativePoint point;
        if (GetCursorPos(out point)) monitor = MonitorAt(point);
        tabsOpen = settingsOpen = false;
        recordingShortcut = false;
        suppressHover = false;
        Recenter();
        revealUntil = clock.Elapsed.TotalSeconds + 4;
        Show();
        SetWindowPos(overlayHandle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        var flash = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(0.9) };
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.12))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.36))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0.65, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.52))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.9))));
        recoveryFlash.BeginAnimation(UIElement.OpacityProperty, flash);
        UpdateTabAppearance();
    }

    private void SetAutoHide(bool enabled)
    {
        if (updatingAppearance) return;
        hideAutomatically = enabled;
        lastDockActivity = clock.Elapsed.TotalSeconds;
        ApplyAppearance();
        SaveSettings();
    }

    private void ApplyPlacementAppearance(PlacementControls view, Brush text)
    {
        if (view == null) return;
        foreach (var section in view.Sections) StyleSettingsCard(section);
        view.Label.Foreground = view.Hide.Foreground = view.Startup.Foreground = view.Recenter.Foreground = text;
        view.Recenter.Background = Brush(lightTheme ? "#150F172A" : "#32AFBFD1");
        view.Hide.IsChecked = hideAutomatically;
        view.Startup.IsChecked = startWithWindows;
        view.Startup.ToolTip = startupError ?? "Launch after you sign in to Windows";
        view.SizeLabel.Foreground = text;
        view.SoundLabel.Foreground = view.Preview.Foreground = text;
        view.Controller.Foreground = text;
        view.Controller.IsChecked = controllerEnabled;
        view.MilitaryTime.Foreground = text;
        view.MilitaryTime.IsChecked = use24HourTime;
        view.Battery.Foreground = text;
        view.Battery.IsChecked = showBatteryPercentage;
        view.Sound.SelectedIndex = alarmSound;
        view.Preview.Background = view.Recenter.Background;
        view.SizeLabel.Text = "Island size  " + Math.Round(islandScale * 100).ToString("0") + "%";
        updatingSize = true;
        view.Size.Value = islandScale * 100;
        updatingSize = false;
        view.Shortcut.Foreground = text;
        view.Shortcut.Background = view.Recenter.Background;
        view.Shortcut.Content = recordingShortcut ? "Press a shortcut… Esc cancels" : "Find island · " + ShortcutName();
        view.Shortcut.ToolTip = shortcutError ?? "Choose a shortcut to reveal and flash the island";
        for (int i = 0; i < view.Modes.Length; i++)
        {
            view.Modes[i].Foreground = text;
            view.Modes[i].IsChecked = (int)placementMode == i;
        }
    }

    private void OpenSettings()
    {
        if (settingsWindow != null)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new Window
        {
            Title = "WinKnotch Settings", Width = 378, Height = 631,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 13
        };
        var form = new StackPanel { Margin = new Thickness(24, 21, 24, 18) };
        var themeLabel = new TextBlock { Text = "Appearance", FontWeight = FontWeights.SemiBold };
        form.Children.Add(themeLabel);
        var themes = new ComboBox { Margin = new Thickness(0, 8, 0, 20), Height = 30 };
        themes.Items.Add("Dark");
        themes.Items.Add("Light");
        themes.Items.Add("Custom");
        themes.SelectedIndex = customTheme ? 2 : lightTheme ? 1 : 0;
        windowThemeChoice = themes;
        System.Windows.Automation.AutomationProperties.SetName(themes, "Appearance");
        form.Children.Add(themes);
        var opacityLabel = new TextBlock { FontWeight = FontWeights.SemiBold };
        themeLabel.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { Source = opacityText });
        windowOpacityLabel = opacityLabel;
        form.Children.Add(opacityLabel);
        var opacity = new Slider
        {
            Minimum = 0, Maximum = 100, Value = glassOpacity * 100,
            TickFrequency = 1, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true,
            Margin = new Thickness(0, 8, 0, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(opacity, "Glass opacity");
        windowOpacity = opacity;
        ConnectOpacitySlider(opacity);
        form.Children.Add(opacity);
        windowPlacement = BuildPlacementSettings();
        form.Children.Clear();
        windowAppearanceCard = new SettingsCard("Appearance");
        themeLabel.Text = "Theme";
        windowAppearanceCard.Body.Children.Add(themeLabel);
        themes.Margin = new Thickness(0, 7, 0, 12);
        windowAppearanceCard.Body.Children.Add(themes);
        windowColorControls = BuildColorControls();
        windowAppearanceCard.Body.Children.Add(windowColorControls.Root);
        windowAppearanceCard.Body.Children.Add(opacityLabel);
        windowAppearanceCard.Body.Children.Add(opacity);
        windowAppearanceCard.Body.Children.Add(windowPlacement.Appearance);
        form.Children.Add(windowAppearanceCard);
        form.Children.Add(windowPlacement.Root);

        Action update = delegate
        {
            settingsWindow.Background = Brush(lightTheme ? "#F3F6F9" : "#17181B");
            var color = Brush(lightTheme ? "#151A21" : "#F0F2F6");
            opacityLabel.Foreground = color;
            ApplyAppearance();
        };
        themes.SelectionChanged += delegate
        {
            if (updatingAppearance) return;
            customTheme = themes.SelectedIndex == 2;
            lightTheme = themes.SelectedIndex == 1 || customTheme && IsLightColor(customThemeColor);
            update();
            SaveSettings();
        };
        settingsWindow.Closed += delegate
        {
            SaveSettings();
            settingsWindow = null;
            windowPlacement = null;
            windowOpacity = null;
            windowOpacityLabel = null;
            windowAppearanceCard = null;
            windowColorControls = null;
            windowThemeChoice = null;
        };
        var settingsScroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        settingsScroll.Resources.Add(typeof(ScrollBar), CreateGlassScrollBarStyle());
        settingsWindow.Content = settingsScroll;
        update();
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private static Style CreateGlassScrollBarStyle()
    {
        const string markup = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
        <Setter Property='Width' Value='25'/>
    <Setter Property='Margin' Value='2,3,0,3'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='ScrollBar'>
                <Grid Background='Transparent'>
                    <Border Width='4' CornerRadius='2' Background='#65656B'/>
                    <Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'
                           Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}'
                           Value='{TemplateBinding Value}' ViewportSize='NaN'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'>
                                <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template>
                            </RepeatButton>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'>
                                <RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template>
                            </RepeatButton>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb Height='15' Width='25' Cursor='Hand' Background='Transparent'>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Ellipse x:Name='Glass' Height='15' Width='15' Fill='{DynamicResource IslandControlInk}'
                                                VerticalAlignment='Center' HorizontalAlignment='Center'
                                                RenderTransformOrigin='0.5,0.5'>
                                            <Ellipse.RenderTransform><ScaleTransform/></Ellipse.RenderTransform>
                                            <Ellipse.Effect><DropShadowEffect Color='Black' Opacity='0.2' BlurRadius='3' ShadowDepth='1'/></Ellipse.Effect>
                                        </Ellipse>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property='IsMouseOver' Value='True'>
                                                <Trigger.EnterActions><BeginStoryboard><Storyboard>
                                                    <DoubleAnimation Storyboard.TargetName='Glass' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)' To='1.25' Duration='0:0:0.18'/>
                                                    <DoubleAnimation Storyboard.TargetName='Glass' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)' To='1.25' Duration='0:0:0.18'/>
                                                </Storyboard></BeginStoryboard></Trigger.EnterActions>
                                                <Trigger.ExitActions><BeginStoryboard><Storyboard>
                                                    <DoubleAnimation Storyboard.TargetName='Glass' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)' To='1' Duration='0:0:0.15'/>
                                                    <DoubleAnimation Storyboard.TargetName='Glass' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)' To='1' Duration='0:0:0.15'/>
                                                </Storyboard></BeginStoryboard></Trigger.ExitActions>
                                            </Trigger>
                                            <Trigger Property='IsDragging' Value='True'>
                                                <Setter TargetName='Glass' Property='Opacity' Value='1'/>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
        return (Style)System.Windows.Markup.XamlReader.Parse(markup);
    }

    private static ControlTemplate CreateChoiceTemplate(bool radio)
    {
        string kind = radio ? "RadioButton" : "CheckBox";
        string mark = radio
            ? "<Ellipse x:Name='Mark' Width='7' Height='7' Fill='{TemplateBinding Foreground}' Visibility='Collapsed'/>"
            : "<Path x:Name='Mark' Data='M 3,8 L 6,11 L 12,4' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.8' Visibility='Collapsed'/>";
        string markup = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='" + kind + "'>"
            + "<Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='16'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>"
            + "<Grid Width='15' Height='15' VerticalAlignment='Center'><Border BorderBrush='{TemplateBinding Foreground}' BorderThickness='1' CornerRadius='" + (radio ? "8" : "3") + "'/>" + mark + "</Grid>"
            + "<ContentPresenter Grid.Column='1' Margin='8,0,0,0' VerticalAlignment='Center' RecognizesAccessKey='True'/></Grid>"
            + "<ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Mark' Property='Visibility' Value='Visible'/></Trigger>"
            + "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>"
            + "<Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Opacity' Value='0.7'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(markup);
    }

    private static ControlTemplate CreateTimelineTemplate(bool originalMusic = true)
    {
        const string markup = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='Slider'>
    <Grid Background='Transparent' Height='20' SnapsToDevicePixels='True'>
        <Border Margin='7,0' Height='6' CornerRadius='3' BorderThickness='0.7' BorderBrush='#8AFFFFFF'
                VerticalAlignment='Center' Background='{TemplateBinding Background}' />
        <Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}'
               Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
            <Track.DecreaseRepeatButton>
                <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
                    <RepeatButton.Template>
                        <ControlTemplate TargetType='RepeatButton'>
                            <Grid Background='Transparent'>
                                <Border Margin='7,0,0,0' Height='6' CornerRadius='3'
                                        VerticalAlignment='Center'
                                        Background='{Binding Foreground, RelativeSource={RelativeSource AncestorType=Slider}}' />
                            </Grid>
                        </ControlTemplate>
                    </RepeatButton.Template>
                </RepeatButton>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
                <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>
                    <RepeatButton.Template>
                        <ControlTemplate TargetType='RepeatButton'>
                            <Border Background='Transparent' />
                        </ControlTemplate>
                    </RepeatButton.Template>
                </RepeatButton>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
                <Thumb Width='19' Height='19' VerticalAlignment='Center' Cursor='Hand'>
                    <Thumb.Template>
                        <ControlTemplate TargetType='Thumb'>
                            <Ellipse x:Name='Knob' RenderTransformOrigin='0.5,0.5' Fill='{DynamicResource IslandControlInk}'
                                     Stroke='#CFFFFFFF' StrokeThickness='1'>
                                <Ellipse.RenderTransform><ScaleTransform /></Ellipse.RenderTransform>
                                <Ellipse.Effect><DropShadowEffect Color='Black' Opacity='0.2' BlurRadius='3' ShadowDepth='1'/></Ellipse.Effect>
                            </Ellipse>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsDragging' Value='True'>
                                    <Trigger.EnterActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)' To='1.42' Duration='0:0:0.225'>
                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut' /></DoubleAnimation.EasingFunction>
                                                </DoubleAnimation>
                                                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)' To='1.42' Duration='0:0:0.225'>
                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut' /></DoubleAnimation.EasingFunction>
                                                </DoubleAnimation>
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.EnterActions>
                                    <Trigger.ExitActions>
                                        <BeginStoryboard>
                                            <Storyboard>
                                                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)' To='1' Duration='0:0:0.165' />
                                                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)' To='1' Duration='0:0:0.165' />
                                            </Storyboard>
                                        </BeginStoryboard>
                                    </Trigger.ExitActions>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Thumb.Template>
                </Thumb>
            </Track.Thumb>
        </Track>
    </Grid>
    <ControlTemplate.Triggers>
        <Trigger Property='IsEnabled' Value='False'>
            <Setter TargetName='PART_Track' Property='Opacity' Value='0.45' />
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";
        string template = markup;
        if (originalMusic)
        {
            template = template.Replace("Width='19' Height='19'", "Width='15' Height='15'")
                .Replace("SnapsToDevicePixels='True'", "SnapsToDevicePixels='False'")
                .Replace("Height='6' CornerRadius='3' BorderThickness='0.7' BorderBrush='#8AFFFFFF'", "Height='4' CornerRadius='2'")
                .Replace("Height='6' CornerRadius='3'", "Height='4' CornerRadius='2'")
                .Replace("Stroke='#CFFFFFFF' StrokeThickness='1'", "Stroke='White' StrokeThickness='0'")
                .Replace("Color='#F2FFFFFF'", "Color='#FFFFFFFF'")
                .Replace("Color='#728FA9BD'", "Color='#FFFFFFFF'")
                .Replace("Color='#3577899B'", "Color='#FFFFFFFF'")
                .Replace("Color='#C7E8F4FF'", "Color='#FFFFFFFF'")
                .Replace("Color='#DB3C4855'", "Color='#FFFFFFFF'");
        }
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(template);
    }

    private sealed class SystemAudioVolume : IDisposable
    {
        private static readonly Guid EnumeratorClass = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private DeviceEnumerator enumerator;
        private EndpointVolume endpoint;
        private string deviceId;

        public bool TryRead(out float level, out bool muted)
        {
            level = 0;
            muted = false;
            try
            {
                RefreshEndpoint();
                Check(endpoint.GetMasterVolumeLevelScalar(out level));
                Check(endpoint.GetMute(out muted));
                return true;
            }
            catch (COMException)
            {
                ResetEndpoint();
                return false;
            }
        }

        public bool TrySet(float level)
        {
            if (endpoint == null) return false;
            try
            {
                Check(endpoint.SetMasterVolumeLevelScalar(level, IntPtr.Zero));
                bool muted;
                if (level > 0 && endpoint.GetMute(out muted) >= 0 && muted)
                    Check(endpoint.SetMute(false, IntPtr.Zero));
                return true;
            }
            catch (COMException)
            {
                ResetEndpoint();
                return false;
            }
        }

        private void RefreshEndpoint()
        {
            if (enumerator == null) enumerator = (DeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass));
            AudioDevice device;
            const int render = 0, console = 0;
            Check(enumerator.GetDefaultAudioEndpoint(render, console, out device));
            try
            {
                string id;
                Check(device.GetId(out id));
                if (endpoint != null && id == deviceId) return;
                ResetEndpoint();
                Guid iid = typeof(EndpointVolume).GUID;
                object activated;
                const int allContexts = 23;
                Check(device.Activate(ref iid, allContexts, IntPtr.Zero, out activated));
                endpoint = (EndpointVolume)activated;
                deviceId = id;
            }
            finally { Marshal.ReleaseComObject(device); }
        }

        private static void Check(int result)
        {
            if (result < 0) Marshal.ThrowExceptionForHR(result);
        }

        private void ResetEndpoint()
        {
            if (endpoint != null) Marshal.ReleaseComObject(endpoint);
            endpoint = null;
            deviceId = null;
        }

        public void Dispose()
        {
            ResetEndpoint();
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            enumerator = null;
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface DeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out AudioDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface AudioDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object activated);
            [PreserveSig] int OpenPropertyStore(int mode, out IntPtr store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface EndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        }
    }

    private sealed class BackdropLens : IDisposable
    {
        private const int TextureWidth = 374;
        private const int TextureHeight = 176;
        private readonly WriteableBitmap texture = new WriteableBitmap(TextureWidth, TextureHeight, 96, 96, PixelFormats.Bgr32, null);
        private readonly int[] output = new int[TextureWidth * TextureHeight];
        private System.Drawing.Bitmap capture;
        private System.Drawing.Graphics graphics;
        private int[] input;
        private int[] sumRed;
        private int[] sumGreen;
        private int[] sumBlue;

        public void Update(Image view, IntPtr window, Point origin, Point end, Rect frame, double expansion)
        {
            int width = Math.Max(1, (int)Math.Round(end.X - origin.X));
            int height = Math.Max(1, (int)Math.Round(end.Y - origin.Y));
            if (capture == null || capture.Width != width || capture.Height != height)
            {
                Dispose();
                capture = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                graphics = System.Drawing.Graphics.FromImage(capture);
                input = new int[width * height];
                int sumSize = (width + 1) * (height + 1);
                sumRed = new int[sumSize];
                sumGreen = new int[sumSize];
                sumBlue = new int[sumSize];
            }

            // Exclude the overlay while sampling so the lens cannot feed back into itself.
            if (!SetWindowDisplayAffinity(window, 0x11)) return;
            try
            {
                graphics.CopyFromScreen((int)Math.Round(origin.X), (int)Math.Round(origin.Y), 0, 0,
                    capture.Size, System.Drawing.CopyPixelOperation.SourceCopy);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                view.Source = null;
                return;
            }
            finally { SetWindowDisplayAffinity(window, 0); }

            var data = capture.LockBits(new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try { Marshal.Copy(data.Scan0, input, 0, input.Length); }
            finally { capture.UnlockBits(data); }
            BuildColorSums(width, height);

            double scaleX = width / SurfaceWidth;
            double scaleY = height / SurfaceHeight;
            double centerX = (frame.Left + frame.Width / 2) * scaleX;
            double centerY = (frame.Top + frame.Height / 2) * scaleY;
            double halfWidth = frame.Width / 2;
            double halfHeight = frame.Height / 2;
            double radius = 13 + 7 * expansion;
            double motion = Math.Sin(Math.PI * expansion);
            double bevelWidth = 9 + 14 * expansion;
            for (int y = 0; y < TextureHeight; y++)
            {
                double v = (y + 0.5) * 2 / TextureHeight - 1;
                double py = v * halfHeight;
                for (int x = 0; x < TextureWidth; x++)
                {
                    double u = (x + 0.5) * 2 / TextureWidth - 1;
                    double px = u * halfWidth;
                    double qx = Math.Abs(px) - halfWidth + radius;
                    double qy = Math.Abs(py) - halfHeight + radius;
                    double outsideX = Math.Max(0, qx);
                    double outsideY = Math.Max(0, qy);
                    double cornerDistance = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
                    double edgeDistance = radius - cornerDistance - Math.Min(0, Math.Max(qx, qy));
                    double bevel = Smooth(Clamp01(1 - edgeDistance / bevelWidth));
                    double normalX = Math.Sign(px) * (cornerDistance > 0 ? outsideX / cornerDistance : (qx > qy ? 1 : 0));
                    double normalY = Math.Sign(py) * (cornerDistance > 0 ? outsideY / cornerDistance : (qy >= qx ? 1 : 0));
                    // Sample the background at its original size and position.
                    int sx = (int)Clamp(Math.Round(centerX + px * scaleX), 0, width - 1);
                    int sy = (int)Clamp(Math.Round(centerY + py * scaleY), 0, height - 1);
                    int blur = Math.Max(1, (int)Math.Round((0.7 + 7.5 * motion + 2.7 * bevel) * scaleX));
                    int color = Diffuse(sx, sy, blur, width, height);
                    double underside = Smooth(Clamp01((v - 0.30 + 0.18 * u * u) / 0.70));
                    double edgeShade = 1 - 0.41 * bevel - 0.08 * underside * expansion;
                    double facingLight = Clamp01((-normalX - normalY) * 0.71);
                    double topFade = 1 - Math.Max(0, -normalY) * (1 - expansion);
                    double faceReflection = Smooth(Clamp01(1 - Math.Abs(v + 0.52 - 0.35 * u * u) / 0.46));
                    double reflectionTaper = 1 - 0.76 * u * u;
                    int reflection = (int)(27 * facingLight * topFade * bevel * bevel
                        + 14 * expansion * faceReflection * reflectionTaper * (1 - bevel));
                    output[y * TextureWidth + x] = Shade(color, edgeShade, reflection);
                }
            }

            texture.WritePixels(new Int32Rect(0, 0, TextureWidth, TextureHeight), output, TextureWidth * 4, 0);
            if (view.Source != texture) view.Source = texture;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        private static int Shade(int color, double shade, int reflection)
        {
            int red = Math.Min(255, (int)(((color >> 16) & 255) * shade) + reflection);
            int green = Math.Min(255, (int)(((color >> 8) & 255) * shade) + reflection);
            int blue = Math.Min(255, (int)((color & 255) * shade) + reflection);
            return (red << 16) | (green << 8) | blue;
        }

        private void BuildColorSums(int width, int height)
        {
            int stride = width + 1;
            for (int y = 0; y < height; y++)
            {
                int red = 0, green = 0, blue = 0;
                for (int x = 0; x < width; x++)
                {
                    int pixel = input[y * width + x];
                    red += (pixel >> 16) & 255;
                    green += (pixel >> 8) & 255;
                    blue += pixel & 255;
                    int index = (y + 1) * stride + x + 1;
                    sumRed[index] = red + sumRed[index - stride];
                    sumGreen[index] = green + sumGreen[index - stride];
                    sumBlue[index] = blue + sumBlue[index - stride];
                }
            }
        }

        private int Diffuse(int x, int y, int radius, int width, int height)
        {
            int left = Math.Max(0, x - radius);
            int right = Math.Min(width, x + radius + 1);
            int top = Math.Max(0, y - radius);
            int bottom = Math.Min(height, y + radius + 1);
            int count = (right - left) * (bottom - top);
            int stride = width + 1;
            int tl = top * stride + left, tr = top * stride + right;
            int bl = bottom * stride + left, br = bottom * stride + right;
            int red = (sumRed[br] - sumRed[tr] - sumRed[bl] + sumRed[tl]) / count;
            int green = (sumGreen[br] - sumGreen[tr] - sumGreen[bl] + sumGreen[tl]) / count;
            int blue = (sumBlue[br] - sumBlue[tr] - sumBlue[bl] + sumBlue[tl]) / count;
            return (red << 16) | (green << 8) | blue;
        }

        public void Dispose()
        {
            if (graphics != null) graphics.Dispose();
            if (capture != null) capture.Dispose();
            graphics = null;
            capture = null;
        }
    }

    private sealed class ScrollingTitle : Canvas
    {
        private readonly Canvas text = new Canvas();
        private readonly Canvas repeat = new Canvas();
        private Brush ink = Brushes.White;
        private double offset;
        private double distance;
        private bool scrolling;

        public ScrollingTitle()
        {
            Children.Add(text);
            Children.Add(repeat);
        }

        public void SetInk(Brush color)
        {
            ink = color;
            foreach (Canvas column in new[] { text, repeat })
                foreach (TextBlock letter in column.Children) letter.Foreground = color;
        }

        public void SetTitle(string title, bool scroll)
        {
            double contentHeight = 0;
            foreach (Canvas column in new[] { text, repeat })
            {
                column.Children.Clear();
                column.Width = Width;
                double y = 0;
                bool gap = false;
                var elements = System.Globalization.StringInfo.GetTextElementEnumerator(title.Trim());
                while (elements.MoveNext())
                {
                    string character = elements.GetTextElement();
                    if (String.IsNullOrWhiteSpace(character))
                    {
                        if (!gap) y += 6;
                        gap = true;
                        continue;
                    }
                    gap = false;
                    var letter = PlayerWindow.Label(11, "#FFFFFF");
                    letter.Foreground = ink;
                    letter.Text = character;
                    letter.Width = Width;
                    letter.TextAlignment = TextAlignment.Center;
                    letter.LineHeight = 12;
                    letter.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                    column.Children.Add(letter);
                    SetTop(letter, y);
                    y += 12;
                }
                column.Height = contentHeight = y;
            }
            distance = Math.Max(Height, contentHeight) + 12;
            scrolling = scroll;
            offset = 0;
            repeat.Visibility = scroll ? Visibility.Visible : Visibility.Hidden;
            SetTop(text, 0);
            SetTop(repeat, -distance);
        }

        public void Advance(double seconds)
        {
            if (!scrolling) return;
            offset = (offset + 23 * seconds) % distance;
            SetTop(text, offset);
            SetTop(repeat, offset - distance);
        }
    }
    private sealed class AttachmentParticles : FrameworkElement
    {
        private Rect origin;
        private DockEdge edge;
        private double phase = 1;

        public AttachmentParticles() { IsHitTestVisible = false; }

        public void Frame(Rect frame, DockEdge side, double nextPhase)
        {
            if (nextPhase >= 1 && phase >= 1) return;
            if (nextPhase < phase)
            {
                origin = frame;
                edge = side;
            }
            phase = nextPhase;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            if (phase >= 1) return;
            bool vertical = edge == DockEdge.Left || edge == DockEdge.Right;
            double outward = 1 - Math.Pow(1 - phase, 2);
            for (int i = 0; i < 12; i++)
            {
                int index = i / 2;
                double direction = i % 2 == 0 ? -1 : 1;
                double tangent = direction * ((vertical ? origin.Height : origin.Width) / 2 - 8
                    + outward * (15 + index * (vertical ? 3 : 5)));
                double inward = 3 + index * 1.8 + Math.Sin(phase * Math.PI * 0.72) * (3 + (index % 3) * 8);
                Point point = StretchPoint(origin.Width, origin.Height, edge, tangent, inward);
                point.Offset(origin.X, origin.Y);
                double opacity = Math.Min(1, phase * 12) * Math.Pow(1 - phase, 1.5) * (0.65 + (index % 3) * 0.15);
                double radius = (0.75 + (index % 4) * 0.28) * (1 - 0.33 * phase);
                drawing.PushOpacity(opacity);
                drawing.DrawEllipse(Brushes.White, null, point, radius, radius);
                drawing.Pop();
            }
        }
    }

    private sealed class EdgeGlare : FrameworkElement
    {
        private readonly Pen[] segments = new Pen[18];
        private readonly Vector[] directions = new Vector[160];
        private PathGeometry track;
        private Point center;
        private double location;
        private bool tracking;

        public EdgeGlare()
        {
            IsHitTestVisible = false;
            for (int i = 0; i < segments.Length; i++)
            {
                double fade = Math.Sin(Math.PI * (i + 1) / (segments.Length + 1));
                var tint = new SolidColorBrush(Color.FromArgb((byte)(144 * fade * fade), 232, 244, 255));
                var pen = new Pen(tint, 0.65 + 0.65 * fade) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                segments[i] = pen;
            }
        }

        public void SetTrack(PathGeometry path, double width, double height)
        {
            track = path;
            center = new Point(width / 2, height / 2);
            for (int i = 0; i < directions.Length; i++)
            {
                Point point, tangent;
                track.GetPointAtFractionLength((double)i / directions.Length, out point, out tangent);
                Vector direction = point - center;
                direction.Normalize();
                directions[i] = direction;
            }
        }

        public void Frame(Point cursor, double delta, double reveal)
        {
            double strength = Math.Max(0, (reveal - 0.25) / 0.75);
            if (strength == 0)
            {
                tracking = false;
                Opacity = 0;
                return;
            }

            Vector aim = cursor - center;
            double target = location;
            if (aim.LengthSquared > 16)
            {
                aim.Normalize();
                double best = -2;
                for (int i = 0; i < directions.Length; i++)
                {
                    double alignment = directions[i] * aim;
                    if (alignment <= best) continue;
                    best = alignment;
                    target = (double)i / directions.Length;
                }
            }

            double offset = target - location;
            offset -= Math.Round(offset);
            location = tracking ? location + offset * (1 - Math.Exp(-7.5 * delta)) : target;
            location -= Math.Floor(location);
            tracking = true;
            Opacity = strength;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            if (track == null || Opacity <= 0) return;
            DrawHighlight(drawing, location);
            drawing.PushOpacity(0.29);
            DrawHighlight(drawing, location + 0.5);
            drawing.Pop();
        }

        private void DrawHighlight(DrawingContext drawing, double head)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                Point from, to, tangent;
                double start = head + (i - segments.Length / 2.0) * 0.0035;
                double end = start + 0.0035;
                track.GetPointAtFractionLength(start - Math.Floor(start), out from, out tangent);
                track.GetPointAtFractionLength(end - Math.Floor(end), out to, out tangent);
                drawing.DrawLine(segments[i], from, to);
            }
        }
    }

    private static LinearGradientBrush Gradient(string top, string highlight, string middle, string bottom)
    {
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0.13, 1) };
        gradient.GradientStops.Add(new GradientStop(Brush(top).Color, 0));
        gradient.GradientStops.Add(new GradientStop(Brush(highlight).Color, 0.14));
        gradient.GradientStops.Add(new GradientStop(Brush(middle).Color, 0.58));
        gradient.GradientStops.Add(new GradientStop(Brush(bottom).Color, 1));
        gradient.Freeze();
        return gradient;
    }

    private static Geometry Outline(double width, double height, double progress, bool topEdge, double peel, double stretch = 0)
    {
        double shoulder = Math.Min(19, width * 0.23) * (1 - progress);
        double upper = Math.Min(19, width * 0.36) * progress;
        double lower = Math.Min(13 + 7 * progress, width * 0.40);
        double left = shoulder;
        double right = width - shoulder;
        double retreat = Math.Max(0, Math.Min(width * 0.43, width / 2 - upper - 4)) * Math.Pow(peel, 0.78);
        double depth = Math.Min(30, Math.Max(0, height - lower - 2) * 0.92) * peel;
        double curveDepth = Math.Max(16, depth + 1);
        Point start = new Point(upper + retreat, 0.5 + depth);
        Point end = new Point(width - upper - retreat, 0.5 + depth);
        double bend = Smooth(Math.Min(1, stretch / (StickyStretch * 0.86)));
        double sideInset = Math.Min(width * 0.11, height * 0.45) * bend;
        double roundedSpan = lower + Math.Min(width * 0.16, height * 0.95) * bend;
        double sideTop = Math.Max(upper, curveDepth);
        sideTop += (height * 0.60 - sideTop) * bend;
        double sideBottom = Math.Max(sideTop, height - lower - 5 * bend);
        double leftSide = left + 0.5 + sideInset;
        double rightSide = right - 0.5 - sideInset;
        double roundedBottom = height - 0.5 - Math.Min(6, height * 0.14) * bend;

        var geometry = new StreamGeometry();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(start, topEdge, topEdge);
            if (stretch > 0.5)
            {
                double middle = width / 2;
                double pull = Math.Min(1, stretch / StickyStretch);
                double join = Math.Min(middle - start.X, middle * (0.9 - 0.70 * pull));
                double contact = Math.Min(join * 0.9, middle * (0.86 - 0.845 * pull));
                double turn = join - contact;
                path.LineTo(new Point(middle - join, start.Y), true, true);
                path.BezierTo(new Point(middle - join + turn * 0.70, start.Y),
                    new Point(middle - contact - turn * 0.35, -stretch),
                    new Point(middle - contact, -stretch), true, true);
                path.LineTo(new Point(middle + contact, -stretch), true, true);
                path.BezierTo(new Point(middle + contact + turn * 0.35, -stretch),
                    new Point(middle + join - turn * 0.70, start.Y),
                    new Point(middle + join, start.Y), true, true);
                path.LineTo(end, true, true);
            }
            else path.LineTo(end, topEdge, false);
            path.BezierTo(new Point(width - 0.5 - retreat * 0.35 - sideInset, end.Y),
                new Point(rightSide + 0.5, upper * 0.35 + depth * 0.5 + 8 * bend),
                new Point(rightSide, sideTop), true, true);
            path.LineTo(new Point(rightSide, sideBottom), true, true);
            path.QuadraticBezierTo(new Point(rightSide, roundedBottom),
                new Point(right - roundedSpan, roundedBottom), true, true);
            if (bend > 0)
                path.BezierTo(new Point(width * 0.60, height - 0.5 + 2 * bend), new Point(width * 0.40, height - 0.5 + 2 * bend),
                    new Point(left + roundedSpan, roundedBottom), true, true);
            else path.LineTo(new Point(left + roundedSpan, roundedBottom), true, false);
            path.QuadraticBezierTo(new Point(leftSide, roundedBottom),
                new Point(leftSide, sideBottom), true, true);
            path.LineTo(new Point(leftSide, sideTop), true, true);
            path.BezierTo(new Point(leftSide - 0.5, upper * 0.35 + depth * 0.5 + 8 * bend),
                new Point(0.5 + retreat * 0.35 + sideInset, start.Y),
                start, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Bounds, WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);


    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref uint length, System.Text.StringBuilder appId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, System.Text.StringBuilder path, ref uint length);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr order, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type, Size;
        public IntPtr Device, Message;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct RawMouse
    {
        [FieldOffset(4)]
        public ushort ButtonFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouseInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, out RawMouseInput data, ref uint size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct ControllerState
    {
        public uint Packet;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint index, out ControllerState state);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint flags, out uint attributes);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, ref ShellFileInfo info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellImageSize { public int Width, Height; }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ShellImageFactory
    {
        [PreserveSig]
        int GetImage(ShellImageSize size, uint flags, out IntPtr bitmap);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out ShellImageFactory factory);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}
