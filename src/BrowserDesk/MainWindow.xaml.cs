using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using BrowserDesk.Models;
using BrowserDesk.Services;

namespace BrowserDesk;

public partial class MainWindow : Window
{
    private readonly BrowserService _browserService = new();
    private readonly BookmarkService _bookmarkService = new();
    private readonly ObservableCollection<Bookmark> _bookmarks;

    public MainWindow()
    {
        InitializeComponent();

        _bookmarks = new ObservableCollection<Bookmark>(_bookmarkService.Load());
        BookmarksGrid.ItemsSource = _bookmarks;
        QuickBrowserCombo.SelectedIndex = 0;
        BookmarkBrowserCombo.SelectedIndex = 0;

        Loaded += (_, _) => RefreshBrowserStatus();
    }

    private void RefreshBrowserStatus()
    {
        BrowserStatusText.Text =
            $"Chrome: {_browserService.GetVersionText(BrowserKind.Chrome)}\n" +
            $"Firefox: {_browserService.GetVersionText(BrowserKind.Firefox)}\n" +
            $"Edge: {_browserService.GetVersionText(BrowserKind.Edge)}";
    }

    private async void OpenQuick_Click(object sender, RoutedEventArgs e)
    {
        await OpenUrlAsync(QuickUrlBox.Text, GetSelectedBrowser(QuickBrowserCombo));
    }

    private async void BookmarksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BookmarksGrid.SelectedItem is Bookmark bookmark)
            await OpenUrlAsync(bookmark.Url, bookmark.Browser);
    }

    private async Task OpenUrlAsync(string rawUrl, BrowserKind browser)
    {
        var url = NormalizeUrl(rawUrl);
        if (url is null)
        {
            MessageBox.Show("آدرس سایت معتبر نیست.", "BrowserDesk",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (DockManagerRightCheckBox.IsChecked == true)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                _browserService.TileRight(hwnd);
            }

            await _browserService.OpenAsync(
                browser,
                url,
                TileLeftCheckBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطا در اجرای مرورگر",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddBookmark_Click(object sender, RoutedEventArgs e)
    {
        var name = BookmarkNameBox.Text.Trim();
        var url = NormalizeUrl(BookmarkUrlBox.Text);

        if (string.IsNullOrWhiteSpace(name) || url is null)
        {
            MessageBox.Show("نام و آدرس معتبر وارد کنید.", "BrowserDesk",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _bookmarks.Add(new Bookmark
        {
            Name = name,
            Url = url,
            Browser = GetSelectedBrowser(BookmarkBrowserCombo)
        });

        SaveBookmarks();
        BookmarkNameBox.Clear();
        BookmarkUrlBox.Text = "https://";
    }

    private void DeleteBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarksGrid.SelectedItem is not Bookmark bookmark)
            return;

        _bookmarks.Remove(bookmark);
        SaveBookmarks();
    }

    private void SaveBookmarks()
    {
        _bookmarkService.Save(_bookmarks);
    }

    private static BrowserKind GetSelectedBrowser(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<BrowserKind>(item.Tag?.ToString(), out var browser))
            return browser;

        return BrowserKind.Chrome;
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
