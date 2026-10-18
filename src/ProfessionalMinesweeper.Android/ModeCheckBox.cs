using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProfessionalMinesweeper.Android;

public sealed class ModeCheckBox : Avalonia.Controls.CheckBox
{
    public ModeCheckBox(string label)
    {
        Content = label;
        MinHeight = 44;
        Template = new FuncControlTemplate<ModeCheckBox>((owner, scope) =>
        {
            var accent = new SolidColorBrush(Color.Parse("#287CA7"));
            var tick = new TextBlock { Text = "✓", FontSize = 16, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var box = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(2), BorderBrush = accent,
                VerticalAlignment = VerticalAlignment.Center, Child = tick };
            var text = new TextBlock { Text = label, FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(text, 1);
            row.Children.Add(box); row.Children.Add(text);
            void Update()
            {
                box.Background = owner.IsChecked == true ? accent : Brushes.White;
                tick.IsVisible = owner.IsChecked == true;
            }
            owner.IsCheckedChanged += (_, _) => Update();
            Update();
            return new Border { Background = Brushes.Transparent, Padding = new Thickness(4, 0), Child = row };
        });
    }
}
