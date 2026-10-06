namespace BrowserDesk.Models;

public sealed class Bookmark
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public BrowserKind Browser { get; set; } = BrowserKind.Chrome;
}

public enum BrowserKind
{
    Chrome,
    Firefox,
    Edge
}
