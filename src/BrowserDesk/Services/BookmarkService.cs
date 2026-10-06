using System.IO;
using System.Text.Json;
using BrowserDesk.Models;

namespace BrowserDesk.Services;

public sealed class BookmarkService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public BookmarkService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BrowserDesk");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "bookmarks.json");
    }

    public List<Bookmark> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return
                [
                    new Bookmark { Name = "Google", Url = "https://www.google.com", Browser = BrowserKind.Chrome },
                    new Bookmark { Name = "GitHub", Url = "https://github.com", Browser = BrowserKind.Edge }
                ];
            }

            return JsonSerializer.Deserialize<List<Bookmark>>(
                File.ReadAllText(_filePath), Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<Bookmark> bookmarks)
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(bookmarks, Options));
    }
}
