namespace GameLibrary.Models;

public sealed class AppSettings
{
    public BackgroundSettings Background { get; set; } = new();
}

public sealed class BackgroundSettings
{
    /// <summary>图片目录；目录内的 jpg / png / bmp / gif 会轮播。</summary>
    public string ImageDirectory { get; set; } = "";

    /// <summary>视频目录；优先播放目录内的视频，媒体播放失败时回退到图片。</summary>
    public string VideoDirectory { get; set; } = "";

    /// <summary>填充 / 适应 / 拉伸 / 平铺。</summary>
    public string DisplayMode { get; set; } = "填充";

    /// <summary>从背景图提取主色，用在高亮文字、选中框和环境光上。</summary>
    public bool DynamicColor { get; set; } = true;

    /// <summary>窗口失去焦点时暂停视频背景。</summary>
    public bool PauseOnDeactivated { get; set; } = true;
}
