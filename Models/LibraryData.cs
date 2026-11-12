namespace GameLibrary.Models;

public sealed class LibraryData
{
    public int Version { get; set; } = 1;
    public List<GameEntry> Games { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

public sealed class WindowSettings
{
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}
