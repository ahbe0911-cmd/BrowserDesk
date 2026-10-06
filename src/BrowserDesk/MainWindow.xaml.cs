using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BrowserDesk.Controls;
using BrowserDesk.Models;
using BrowserDesk.Services;

namespace BrowserDesk;

public partial class MainWindow : Window
{
    private readonly BrowserService _browserService = new();
    private readonly BookmarkService _bookmarkService = new();
    private readonly ObservableCollection<Bookmark> _bookmarks;
    private readonly List<BrowserTabSession> _tabs = [];

    private BrowserKind _selectedBrowser = BrowserKind.Chrome;

    public MainWindow()
    {
        InitializeComponent();

        _bookmarks = new ObservableCollection<Bookmark>(_bookmarkService.Load());
        BookmarksItems.ItemsSource = _bookmarks;

        Loaded += (_, _) => RefreshBrowserStatus();
        Closed += (_, _) => CloseAllTabs();
    }

    private void RefreshBrowserStatus()
    {
        ChromeVersionText.Text = GetStatus(BrowserKind.Chrome);
        FirefoxVersionText.Text = GetStatus(BrowserKind.Firefox);
        EdgeVersionText.Text = GetStatus(BrowserKind.Edge);

        ChromeChoice.IsEnabled = _browserService.IsInstalled(BrowserKind.Chrome);
        FirefoxChoice.IsEnabled = _browserService.IsInstalled(BrowserKind.Firefox);
        EdgeChoice.IsEnabled = _browserService.IsInstalled(BrowserKind.Edge);

        if (!ChromeChoice.IsEnabled)
        {
            if (FirefoxChoice.IsEnabled)
                FirefoxChoice.IsChecked = true;
            else if (EdgeChoice.IsEnabled)
                EdgeChoice.IsChecked = true;
        }

        UpdateTabCount();
    }

    private string GetStatus(BrowserKind browser)
    {
        var version = _browserService.GetVersionText(browser);
        return version == "نصب نیست" ? "نصب نیست" : $"v {version}";
    }

    private void BrowserChoice_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio &&
            Enum.TryParse<BrowserKind>(radio.Tag?.ToString(), out var browser))
        {
            _selectedBrowser = browser;
        }
    }

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Bookmark bookmark })
            await OpenNewTabAsync(bookmark.Url, bookmark.Name);
    }

    private void Bookmark_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { DataContext: Bookmark bookmark })
            return;

        e.Handled = true;

        var result = MessageBox.Show(
            $"بوک‌مارک «{bookmark.Name}» حذف شود؟",
            "حذف بوک‌مارک",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _bookmarks.Remove(bookmark);
        SaveBookmarks();
    }

    private async void OpenAddress_Click(object sender, RoutedEventArgs e)
    {
        await OpenNewTabAsync(AddressBox.Text);
    }

    private async void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        await OpenNewTabAsync(AddressBox.Text);
    }

    private async Task OpenNewTabAsync(string rawUrl, string? title = null)
    {
        var url = NormalizeUrl(rawUrl);
        if (url is null)
        {
            MessageBox.Show(
                "آدرس سایت معتبر نیست.",
                "BrowserDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!_browserService.IsInstalled(_selectedBrowser))
        {
            MessageBox.Show(
                "مرورگر انتخاب‌شده روی ویندوز نصب نیست.",
                "BrowserDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var displayTitle = string.IsNullOrWhiteSpace(title)
            ? GetHostTitle(url)
            : title.Trim();

        var host = new EmbeddedBrowserHost();
        var tab = new TabItem
        {
            Content = host,
            Padding = new Thickness(0),
            Background = Brushes.White
        };

        var session = new BrowserTabSession
        {
            Browser = _selectedBrowser,
            Url = url,
            Title = displayTitle,
            Host = host,
            Tab = tab
        };

        tab.Tag = session;
        tab.Header = BuildTabHeader(session);

        _tabs.Add(session);
        BrowserTabs.Items.Add(tab);
        BrowserTabs.SelectedItem = tab;

        EmptyState.Visibility = Visibility.Collapsed;
        CurrentSiteText.Text = $"در حال باز کردن {displayTitle} ...";
        AddressBox.Text = url;
        UpdateTabCount();

        try
        {
            BrowserTabs.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            var hwnd = await _browserService.OpenEmbeddedWindowAsync(session.Browser, url);
            session.Host.Attach(hwnd);

            CurrentSiteText.Text =
                $"{displayTitle}  •  {GetBrowserName(session.Browser)}";
        }
        catch (Exception ex)
        {
            CloseTab(session, closeWindow: false);

            MessageBox.Show(
                ex.Message,
                "خطا در اجرای مرورگر",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private object BuildTabHeader(BrowserTabSession session)
    {
        var badge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(7),
            Background = GetBrowserBadgeBackground(session.Browser),
            Margin = new Thickness(0, 0, 7, 0),
            Child = new TextBlock
            {
                Text = GetBrowserLetter(session.Browser),
                Foreground = GetBrowserBadgeForeground(session.Browser),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var title = new TextBlock
        {
            Text = session.Title,
            MaxWidth = 150,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FlowDirection = FlowDirection.RightToLeft
        };

        var close = new Button
        {
            Content = "×",
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(7, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)FindResource("MutedBrush"),
            Cursor = Cursors.Hand,
            FontSize = 15
        };

        close.Click += (_, e) =>
        {
            e.Handled = true;
            CloseTab(session);
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Thickness(2, 1, 2, 1)
        };

        panel.Children.Add(badge);
        panel.Children.Add(title);
        panel.Children.Add(close);

        return panel;
    }

    private void CloseTab(BrowserTabSession session, bool closeWindow = true)
    {
        if (!_tabs.Contains(session))
            return;

        if (closeWindow)
            session.Host.CloseHostedWindow();

        _tabs.Remove(session);
        BrowserTabs.Items.Remove(session.Tab);

        if (_tabs.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            CurrentSiteText.Text = "آماده";
            AddressBox.Text = "https://";
        }

        UpdateTabCount();
    }

    private void CloseAllTabs()
    {
        foreach (var tab in _tabs.ToArray())
            tab.Host.CloseHostedWindow();

        _tabs.Clear();
    }

    private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BrowserTabs.SelectedItem is not TabItem { Tag: BrowserTabSession session })
            return;

        AddressBox.Text = session.Url;
        CurrentSiteText.Text =
            $"{session.Title}  •  {GetBrowserName(session.Browser)}";
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        var name = BookmarkNameBox.Text.Trim();
        var url = NormalizeUrl(BookmarkUrlBox.Text);

        if (string.IsNullOrWhiteSpace(name) || url is null)
        {
            MessageBox.Show(
                "نام سایت و آدرس معتبر وارد کنید.",
                "BrowserDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _bookmarks.Add(new Bookmark
        {
            Name = name,
            Url = url,
            Browser = _selectedBrowser
        });

        SaveBookmarks();

        BookmarkNameBox.Clear();
        BookmarkUrlBox.Text = "https://";
    }

    private void SaveBookmarks()
    {
        _bookmarkService.Save(_bookmarks);
    }

    private void UpdateTabCount()
    {
        TabCountText.Text = $"{ToPersianDigits(_tabs.Count)} تب باز";
    }

    private static string GetHostTitle(string url)
    {
        try
        {
            var host = new Uri(url).Host;
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? host[4..]
                : host;
        }
        catch
        {
            return "سایت جدید";
        }
    }

    private static string GetBrowserName(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "Chrome",
        BrowserKind.Firefox => "Firefox",
        BrowserKind.Edge => "Edge",
        _ => browser.ToString()
    };

    private static string GetBrowserLetter(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => "C",
        BrowserKind.Firefox => "F",
        BrowserKind.Edge => "E",
        _ => "B"
    };

    private static Brush GetBrowserBadgeBackground(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => new SolidColorBrush(Color.FromRgb(232, 240, 254)),
        BrowserKind.Firefox => new SolidColorBrush(Color.FromRgb(255, 241, 232)),
        BrowserKind.Edge => new SolidColorBrush(Color.FromRgb(231, 248, 246)),
        _ => Brushes.Gainsboro
    };

    private static Brush GetBrowserBadgeForeground(BrowserKind browser) => browser switch
    {
        BrowserKind.Chrome => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
        BrowserKind.Firefox => new SolidColorBrush(Color.FromRgb(234, 88, 12)),
        BrowserKind.Edge => new SolidColorBrush(Color.FromRgb(15, 118, 110)),
        _ => Brushes.SlateGray
    };

    private static string ToPersianDigits(int value)
    {
        return value.ToString()
            .Replace('0', '۰')
            .Replace('1', '۱')
            .Replace('2', '۲')
            .Replace('3', '۳')
            .Replace('4', '۴')
            .Replace('5', '۵')
            .Replace('6', '۶')
            .Replace('7', '۷')
            .Replace('8', '۸')
            .Replace('9', '۹');
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    private sealed class BrowserTabSession
    {
        public required BrowserKind Browser { get; init; }
        public required string Url { get; init; }
        public required string Title { get; init; }
        public required EmbeddedBrowserHost Host { get; init; }
        public required TabItem Tab { get; init; }
    }
}
