using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ProfessionalMinesweeper.Desktop;

public sealed class AboutView : StackPanel
{
    public const string AuthorUrl = "https://space.bilibili.com/672619350";
    public event Action? OpenAuthorRequested;
    public AboutView()
    {
        Margin = new Thickness(24);
        Spacing = 16;
        var assembly = typeof(AboutView).Assembly.GetName().Name;
        Bitmap Load(string name)
        {
            using var stream = AssetLoader.Open(new Uri($"avares://{assembly}/Assets/{name}"));
            return new Bitmap(stream);
        }
        Children.Add(new TextBlock { Text = "Professional Minesweeper", FontSize = 24, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        var author = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
        author.Children.Add(new Border { CornerRadius = new CornerRadius(18), ClipToBounds = true,
            Child = new Image { Source = Load("author.png"), Width = 36, Height = 36, Stretch = Stretch.UniformToFill } });
        author.Children.Add(new TextBlock { Text = "作者：Infinite75", FontSize = 18,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        Children.Add(author);
        Children.Add(new TextBlock { Text = "本项目是对原版 Professional Minesweeper（profmine.exe）的复刻。棋盘与预设的研究基于此前对原游戏的逆向分析；本项目不是原作者的官方版本。", TextWrapping = TextWrapping.Wrap });
        Children.Add(new TextBlock { Text = "开源许可：GPL-3.0-only", FontWeight = FontWeight.Bold });
        var link = new Avalonia.Controls.Button { Content = "B站主页 · Infinite75" };
        link.Click += (_, _) => OpenAuthorRequested?.Invoke();
        Children.Add(link);
        Children.Add(new TextBlock { Text = AuthorUrl, TextWrapping = TextWrapping.Wrap });
        Children.Add(new TextBlock { Text = "技术栈\nC# · .NET 10 · Avalonia UI 12\n独立棋盘拓扑与扫雷引擎\nPC：Windows Native AOT · Skia/ANGLE 静态链接\nAndroid：.NET for Android · Mono AOT · Skia", TextWrapping = TextWrapping.Wrap });
        Children.Add(new TextBlock { Text = "喜欢这个项目？感谢支持作者", FontWeight = FontWeight.Bold });
        Children.Add(new Image { Source = Load("support.png"), MaxWidth = 280, MaxHeight = 360, Stretch = Stretch.Uniform,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
    }
}
