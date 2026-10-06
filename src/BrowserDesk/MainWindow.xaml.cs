using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BrowserDesk.Models;
using BrowserDesk.Services;

namespace BrowserDesk;

public partial class MainWindow : Window
{
    private readonly BrowserService _browserService = new();
    private readonly BookmarkService _bookmarkService = new();
    private readonly ObservableCollection<Bookmark> _bookmarks;
    private BrowserKind _selectedBrowser = BrowserKind.Chrome;

    public MainWindow()
    {
        InitializeComponent();

        _bookmarks = new ObservableCollection<Bookmark>(_bookmarkService.Load());
        BookmarksItems.ItemsSource = _bookmarks;

        Loaded += (_, _) => RefreshBrowserStatus();
        Closed += (_, _) => BrowserHost.CloseHostedWindow();
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
    }

    private string GetStatus(BrowserKind browser)
    {
        var version = _browserService.GetVersionText(browser);
        return version == "نصب نیست" ? "نصب نیست" : $"Version {version}";
    }

    private void BrowserChoice_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio ||
            !Enum.TryParse<BrowserKind>(radio.Tag?.ToString(), out var browser))
            return;

        _selectedBrowser = browser;

        if (CurrentBrowserText is not null)
        {
            CurrentBrowserText.Text = browser switch
            {
                BrowserKind.Chrome => "مرورگر انتخاب‌شده: Google Chrome",
                BrowserKind.Firefox => "مرورگر انتخاب‌شده: Mozilla Firefox",
                BrowserKind.Edge => "مرورگر انتخاب‌شده: Microsoft Edge",
                _ => "مرورگر انتخاب‌شده"
            };
        }
    }

    private async void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Bookmark bookmark })
            await OpenUrlAsync(bookmark.Url);
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
        await OpenUrlAsync(AddressBox.Text);
    }

    private async void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        await OpenUrlAsync(AddressBox.Text);
    }

    private async Task OpenUrlAsync(string rawUrl)
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

        try
        {
            CurrentSiteText.Text = "در حال باز کردن...";
            BrowserHost.Visibility = Visibility.Visible;
            BrowserHost.UpdateLayout();

            var hwnd = await _browserService.OpenEmbeddedWindowAsync(_selectedBrowser, url);

            BrowserHost.Attach(hwnd);
            EmptyState.Visibility = Visibility.Collapsed;
            AddressBox.Text = url;
            CurrentSiteText.Text = new Uri(url).Host;
        }
        catch (Exception ex)
        {
            if (!BrowserHost.HasBrowser)
            {
                BrowserHost.Visibility = Visibility.Hidden;
                EmptyState.Visibility = Visibility.Visible;
            }

            CurrentSiteText.Text = "باز کردن سایت ناموفق بود";

            MessageBox.Show(
                ex.Message,
                "خطا در اجرای مرورگر",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ToggleBookmarkEditor_Click(object sender, RoutedEventArgs e)
    {
        BookmarkEditorPanel.Visibility =
            BookmarkEditorPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        var name = BookmarkNameBox.Text.Trim();
        var url = NormalizeUrl(BookmarkUrlBox.Text);

        if (string.IsNullOrWhiteSpace(name) || url is null)
        {
            MessageBox.Show(
                "نام و آدرس معتبر وارد کنید.",
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
        BookmarkEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void SaveBookmarks()
    {
        _bookmarkService.Save(_bookmarks);
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
}
