using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ConceptJewelleryPro.WPF.Controls;

public sealed partial class ToastNotification : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ToastNotification),
            new PropertyMetadata(string.Empty, OnMessageChanged));

    public static readonly DependencyProperty ToastTypeProperty =
        DependencyProperty.Register(nameof(ToastType), typeof(ToastType), typeof(ToastNotification),
            new PropertyMetadata(ToastType.Info, OnToastTypeChanged));

    public static readonly DependencyProperty ToastBorderBrushProperty =
        DependencyProperty.Register(nameof(ToastBorderBrush), typeof(Brush), typeof(ToastNotification),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty IconBackgroundProperty =
        DependencyProperty.Register(nameof(IconBackground), typeof(Brush), typeof(ToastNotification),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty IconForegroundProperty =
        DependencyProperty.Register(nameof(IconForeground), typeof(Brush), typeof(ToastNotification),
            new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty AutoCloseDelayProperty =
        DependencyProperty.Register(nameof(AutoCloseDelay), typeof(int), typeof(ToastNotification),
            new PropertyMetadata(6000));

    private readonly DispatcherTimer _autoCloseTimer;
    private Storyboard? _showStoryboard;
    private Storyboard? _hideStoryboard;
    private bool _isHiding;

    public ToastNotification()
    {
        InitializeComponent();

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoCloseDelay)
        };
        _autoCloseTimer.Tick += (_, _) => Hide();

        Loaded += (_, _) =>
        {
            if (!double.IsNaN(Height))
            {
                Height = double.NaN;
            }
            UpdateAppearance(ToastType);

            if (FindResource("ShowToastStoryboard") is Storyboard showSb)
            {
                _showStoryboard = showSb.Clone();
            }
            if (FindResource("HideToastStoryboard") is Storyboard hideSb)
            {
                _hideStoryboard = hideSb.Clone();
                _hideStoryboard.Completed += OnHideStoryboardCompleted;
            }
        };
    }

    private void OnHideStoryboardCompleted(object? sender, EventArgs e)
    {
        if (_isHiding)
        {
            Visibility = Visibility.Collapsed;
            _isHiding = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ToastType ToastType
    {
        get => (ToastType)GetValue(ToastTypeProperty);
        set => SetValue(ToastTypeProperty, value);
    }

    public Brush ToastBorderBrush
    {
        get => (Brush)GetValue(ToastBorderBrushProperty);
        set => SetValue(ToastBorderBrushProperty, value);
    }

    public Brush IconBackground
    {
        get => (Brush)GetValue(IconBackgroundProperty);
        set => SetValue(IconBackgroundProperty, value);
    }

    public Brush IconForeground
    {
        get => (Brush)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    public int AutoCloseDelay
    {
        get => (int)GetValue(AutoCloseDelayProperty);
        set
        {
            SetValue(AutoCloseDelayProperty, value);
            _autoCloseTimer.Interval = TimeSpan.FromMilliseconds(value);
        }
    }

    public event EventHandler? Closed;

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToastNotification toast)
        {
            var msg = e.NewValue as string;
            if (!string.IsNullOrWhiteSpace(msg))
            {
                toast.Show();
            }
            else
            {
                toast.Hide();
            }
        }
    }

    private static void OnToastTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToastNotification toast)
        {
            toast.UpdateAppearance((ToastType)e.NewValue);
        }
    }

    private void UpdateAppearance(ToastType type)
    {
        switch (type)
        {
            case ToastType.Success:
                ToastBorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                IconBackground = new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9));
                IconForeground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                IconText.Text = "\uE73E"; // Checkmark
                break;

            case ToastType.Error:
                ToastBorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x45));
                IconBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xC9, 0xC9));
                IconForeground = new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x45));
                IconText.Text = "\uE711"; // X/Clear
                break;

            case ToastType.Warning:
                ToastBorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8F, 0x00));
                IconBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0));
                IconForeground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8F, 0x00));
                IconText.Text = "\uE7BA"; // Warning
                break;

            case ToastType.Info:
            default:
                ToastBorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                IconBackground = new SolidColorBrush(Color.FromRgb(0xBB, 0xDE, 0xFB));
                IconForeground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
                IconText.Text = "\uE709"; // Info
                break;
        }
    }

    public void Show()
    {
        _isHiding = false;
        _hideStoryboard?.Stop(this);

        if (!double.IsNaN(Height))
        {
            Height = double.NaN;
        }
        UpdateAppearance(ToastType);

        Visibility = Visibility.Visible;
        Opacity = 1.0;

        _autoCloseTimer.Stop();
        // Allow error messages to be visible for 8 seconds so user can read it comfortably
        var delay = ToastType == ToastType.Error ? Math.Max(AutoCloseDelay, 8000) : AutoCloseDelay;
        _autoCloseTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _autoCloseTimer.Start();

        if (_showStoryboard != null)
        {
            _showStoryboard.Begin(this);
        }
        else if (FindResource("ShowToastStoryboard") is Storyboard sb)
        {
            sb.Begin(this);
        }
    }

    public void Hide()
    {
        _autoCloseTimer.Stop();
        _isHiding = true;

        if (_hideStoryboard != null)
        {
            _hideStoryboard.Begin(this);
        }
        else if (FindResource("HideToastStoryboard") is Storyboard sb)
        {
            sb.Completed += (_, _) =>
            {
                if (_isHiding)
                {
                    Visibility = Visibility.Collapsed;
                    _isHiding = false;
                    Closed?.Invoke(this, EventArgs.Empty);
                }
            };
            sb.Begin(this);
        }
        else
        {
            Visibility = Visibility.Collapsed;
            _isHiding = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}

public enum ToastType
{
    Info,
    Success,
    Error,
    Warning
}