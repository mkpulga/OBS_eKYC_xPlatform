using Microsoft.Maui.Controls.Shapes;
using OBS.Core;

namespace OBS.Maui.Controls;

public sealed class DashboardIcon : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(string), typeof(DashboardIcon), "dashboard", propertyChanged: Refresh);
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(DashboardIcon), Color.FromArgb("#51535F"), propertyChanged: Refresh);
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public DashboardIcon()
    {
        WidthRequest = HeightRequest = 20;
        InputTransparent = true;
        UpdateIcon();
    }
    private static void Refresh(BindableObject bindable, object oldValue, object newValue) => ((DashboardIcon)bindable).UpdateIcon();
    private void UpdateIcon() => Content = new Microsoft.Maui.Controls.Shapes.Path
    {
        Data = new PathGeometryConverter().ConvertFromInvariantString(DashboardPresentation.IconPath(Kind)) as Geometry
            ?? throw new InvalidOperationException("Dashboard icon geometry is invalid."),
        Stroke = new SolidColorBrush(Tint), StrokeThickness = 1.6, Aspect = Stretch.Uniform,
        StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
    };
}
