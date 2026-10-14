using OBS.Core;

namespace OBS.Maui.Controls;

public sealed class DashboardActivityDrawable : IDrawable
{
    public DashboardPeriod Period { get; set; } = DashboardPeriod.Year;
    public bool Dark { get; set; }
    public void Draw(ICanvas canvas, RectF bounds)
    {
        var left = 40f;
        var top = 12f;
        var width = Math.Max(0, bounds.Width - left - 8);
        var height = Math.Max(0, bounds.Height - top - 36);
        var maximum = DashboardSample.AxisMaximum(Period);
        canvas.FontSize = bounds.Width < 420 ? 9 : 10;
        canvas.FontColor = Color.FromArgb(Dark ? "#C5C8DA" : "#444653");
        canvas.StrokeColor = Color.FromArgb(Dark ? "#3A3D53" : "#DCDCE7");
        canvas.StrokeSize = 1;
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = top + height * tick / 4;
            canvas.DrawLine(left, y, left + width, y);
            canvas.DrawString((maximum * (4 - tick) / 4).ToString(), 0, y - 8, 28, 16, HorizontalAlignment.Left, VerticalAlignment.Center);
        }
        var series = DashboardSample.Activity(Period);
        var slot = width / series.Count;
        var barWidth = Math.Min(42, slot * .4f);
        for (var i = 0; i < series.Count; i++)
        {
            var point = series[i];
            var x = left + slot * (i + .5f) - barWidth / 2;
            var completed = height * point.Completed / maximum;
            var progress = height * point.InProgress / maximum;
            var baseline = top + height;
            canvas.FillColor = Color.FromArgb("#8E58FA");
            canvas.FillRoundedRectangle(x, baseline - completed - progress, barWidth, progress + completed, 5);
            canvas.FillColor = Color.FromArgb("#42D1B0");
            canvas.FillRoundedRectangle(x, baseline - completed, barWidth, completed, 5);
            canvas.FillRectangle(x, baseline - Math.Min(6, completed), barWidth, Math.Min(6, completed));
            canvas.DrawString(point.Label, left + slot * i, baseline + 8, slot, 28, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}

public sealed class DashboardReleaseDrawable : IDrawable
{
    public bool Dark { get; set; }
    public void Draw(ICanvas canvas, RectF bounds)
    {
        var size = Math.Min(bounds.Width, bounds.Height) - 16;
        var x = (bounds.Width - size) / 2;
        var y = (bounds.Height - size) / 2;
        canvas.StrokeSize = 6;
        canvas.StrokeColor = Color.FromArgb(Dark ? "#3A3D53" : "#E0DFE9");
        canvas.DrawEllipse(x, y, size, size);
        canvas.StrokeColor = Color.FromArgb("#9057FF");
        canvas.StrokeLineCap = LineCap.Round;
        var path = new PathF();
        for (var step = 0; step <= DashboardSample.ReleasePercent; step++)
        {
            var angle = (step * 3.6 - 90) * Math.PI / 180;
            var px = x + size / 2 + size / 2 * (float)Math.Cos(angle);
            var py = y + size / 2 + size / 2 * (float)Math.Sin(angle);
            if (step == 0) path.MoveTo(px, py); else path.LineTo(px, py);
        }
        canvas.DrawPath(path);
    }
}
