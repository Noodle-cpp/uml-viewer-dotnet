using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using UmlViewer.App.Rendering;
using UmlViewer.Engine;

namespace UmlViewer.App;

public sealed class DetailWindow : Window
{
    private readonly DetailEngine.DetailModel _model;

    public DetailWindow(DetailEngine.DetailModel model)
    {
        _model = model;
        Title = model.ClassName;
        Width = DetailEngine.Width;
        Height = DetailEngine.Height;
        Background = new SolidColorBrush(ViewerTheme.Bg);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var grid = new Grid
        {
            Margin = new Thickness(DetailEngine.Pad),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        var header = new TextBlock
        {
            Text = _model.ClassName,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(ViewerTheme.Ink),
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var scroll = new ScrollViewer();
        Grid.SetRow(scroll, 1);
        var panel = new StackPanel { Spacing = 2 };
        foreach (var row in DetailEngine.BuildRows(_model))
        {
            panel.Children.Add(BuildRow(row));
        }

        var open = new Button
        {
            Content = "Open source file",
            Margin = new Thickness(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_model.File))
                SourceOpener.OpenFile(StartupOptions.MetricsRoot, _model.File, null);
        };
        panel.Children.Add(open);
        scroll.Content = panel;
        grid.Children.Add(scroll);
        return grid;
    }

    private Control BuildRow(DetailEngine.DetailRow row)
    {
        if (row.Kind is "heading")
        {
            return new TextBlock
            {
                Text = row.Text,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(ViewerTheme.Ink),
                Margin = new Thickness(0, 12, 0, 4),
            };
        }

        if (row.Kind is "col-header")
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,56,32,44,52,60,78"),
                Margin = new Thickness(0, 8, 0, 0),
            };
            g.Children.Add(Cell("Crap", 1, ViewerTheme.Gold, FontWeight.SemiBold));
            g.Children.Add(Cell("CC", 2, ViewerTheme.Gold, FontWeight.SemiBold));
            g.Children.Add(Cell("Cov", 3, ViewerTheme.Gold, FontWeight.SemiBold));
            g.Children.Add(Cell("killed", 4, ViewerTheme.Gold, FontWeight.SemiBold));
            g.Children.Add(Cell("survived", 5, ViewerTheme.Gold, FontWeight.SemiBold));
            g.Children.Add(Cell("uncovered", 6, ViewerTheme.Gold, FontWeight.SemiBold));
            return g;
        }

        if (row.Kind is "stats")
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,56,32,44,52,60,78"),
                Background = row.Private ? null : null,
            };
            g.Children.Add(Cell(row.Text, 0, ViewerTheme.Ink));
            g.Children.Add(Cell(row.Crap ?? row.MutNote ?? "", 1, ViewerTheme.Muted));
            g.Children.Add(Cell(row.Cc ?? "", 2, ViewerTheme.Muted));
            g.Children.Add(Cell(row.Cov ?? "", 3, ViewerTheme.Muted));
            g.Children.Add(Cell(row.Killed ?? "", 4, ViewerTheme.Muted));
            g.Children.Add(Cell(row.Survived ?? "", 5, ViewerTheme.Muted));
            g.Children.Add(Cell(row.Uncovered ?? "", 6, ViewerTheme.Muted));
            if (!string.IsNullOrEmpty(row.OpName))
            {
                g.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(g).Properties.IsLeftButtonPressed && _model.File != null)
                    {
                        SourceOpener.OpenFile(StartupOptions.MetricsRoot, _model.File, row.OpName);
                        e.Handled = true;
                    }
                };
                g.Cursor = new Cursor(StandardCursorType.Hand);
            }

            return g;
        }

        var muted = row.Kind is "crap" or "muted" or "rel";
        return new TextBlock
        {
            Text = row.Text,
            Foreground = new SolidColorBrush(muted ? ViewerTheme.Muted : ViewerTheme.Ink),
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static TextBlock Cell(string text, int col, Color color, FontWeight weight = FontWeight.Normal)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 13,
            FontWeight = weight,
            HorizontalAlignment = col == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            FontFamily = col == 0
                ? new FontFamily(FontFamily.Default.Name)
                : new FontFamily("Consolas,Courier New,monospace"),
        };
        Grid.SetColumn(tb, col);
        return tb;
    }
}
