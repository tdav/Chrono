using Chrono.Services;

namespace Chrono.Views;

/// <summary>
/// Рисует 12-часовой циферблат дня по готовым срезам <see cref="DialLayout"/>.
/// Углы — как в DialLayout: 0° — «12 часов», по часовой стрелке. Радиус обода один и тот же для рисования и для касания.
/// </summary>
public sealed class DialDrawable : IDrawable
{
    private static readonly Color Rim = Color.FromArgb("#B8B0A6");
    private static readonly Color Hole = Color.FromArgb("#F5F2EC");
    private static readonly Color HoleEdge = Color.FromArgb("#E4DED3");
    private static readonly Color Ink = Color.FromArgb("#2E3136");
    private static readonly Color Arrow = Color.FromArgb("#B5482F");

    public IReadOnlyList<DialSlice> Slices { get; set; } = [];

    /// <summary>Id выбранной записи: её срез обведён, остальные бледнее.</summary>
    public Guid? SelectedId { get; set; }

    /// <summary>Угол стрелки «сейчас»; null — стрелки нет (показана не текущая половина суток).</summary>
    public double? ArrowAngle { get; set; }

    /// <summary>Радиус обода: вписанный в область круг с запасом под обводку.</summary>
    public static float RadiusFor(double width, double height) => (float)(Math.Min(width, height) / 2 - 4);

    /// <summary>Касание → (угол, доля радиуса обода). Угол 0° — вверх, по часовой.</summary>
    public static (double Angle, double RadiusFraction) ToPolar(Point touch, double width, double height)
    {
        var dx = touch.X - width / 2;
        var dy = touch.Y - height / 2;
        var angle = Math.Atan2(dx, -dy) * 180 / Math.PI;
        return ((angle + 360) % 360, Math.Sqrt(dx * dx + dy * dy) / RadiusFor(width, height));
    }

    private static PointF At(float cx, float cy, float radius, double angle)
    {
        var rad = angle * Math.PI / 180;
        return new PointF(cx + radius * (float)Math.Sin(rad), cy - radius * (float)Math.Cos(rad));
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var cx = dirtyRect.Width / 2;
        var cy = dirtyRect.Height / 2;
        var r = RadiusFor(dirtyRect.Width, dirtyRect.Height);

        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, cy, r);

        // Выбранный срез рисуется последним, чтобы его обводку не перекрывали соседи.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var slice in this.Slices)
            {
                var selected = this.SelectedId == slice.Entry.Alarm.Id;
                if (selected == (pass == 0))
                {
                    continue;
                }

                var dim = this.SelectedId is not null && !selected;
                var color = Color.FromArgb(DialLayout.Palette[slice.ColorIndex]);

                if (slice.IsTick)
                {
                    canvas.StrokeLineCap = LineCap.Butt;
                    if (selected)
                    {
                        canvas.StrokeColor = Ink;
                        canvas.StrokeSize = 8;
                        canvas.DrawLine(At(cx, cy, r * 0.84f, slice.StartAngle), At(cx, cy, r, slice.StartAngle));
                    }

                    // Как у секторов: выполненная — 35 %, выключенная — бледная тонкая линия, остальные бледнеют при выборе другой записи.
                    var alpha = slice.Entry.IsMuted ? 0.45f : slice.Entry.IsDone ? 0.35f : 0.9f;
                    canvas.StrokeColor = color.WithAlpha(dim ? Math.Min(alpha, 0.35f) : alpha);
                    canvas.StrokeSize = slice.Entry.IsMuted ? 2 : 4;
                    canvas.DrawLine(At(cx, cy, r * 0.86f, slice.StartAngle), At(cx, cy, r, slice.StartAngle));
                    continue;
                }

                // Кольцо от радиуса центрального круга до обода: ломаная с шагом ~3°.
                var path = new PathF();
                var steps = Math.Max(2, (int)Math.Ceiling(slice.SweepAngle / 3));
                for (var i = 0; i <= steps; i++)
                {
                    var p = At(cx, cy, r, slice.StartAngle + slice.SweepAngle * i / steps);
                    if (i == 0)
                    {
                        path.MoveTo(p);
                    }
                    else
                    {
                        path.LineTo(p);
                    }
                }

                for (var i = steps; i >= 0; i--)
                {
                    path.LineTo(At(cx, cy, r * 0.52f, slice.StartAngle + slice.SweepAngle * i / steps));
                }

                path.Close();

                if (slice.Entry.IsMuted)
                {
                    // Выключенная запись — только контур 2 px без заливки.
                    canvas.StrokeColor = color.WithAlpha(dim ? 0.35f : 1f);
                    canvas.StrokeSize = 2;
                    canvas.DrawPath(path);
                }
                else
                {
                    canvas.FillColor = color.WithAlpha(dim || slice.Entry.IsDone ? 0.35f : 0.9f);
                    canvas.FillPath(path);
                }

                if (selected)
                {
                    canvas.StrokeColor = Ink;
                    canvas.StrokeSize = 3;
                    canvas.StrokeLineJoin = LineJoin.Round;
                    canvas.DrawPath(path);
                }
            }
        }

        canvas.StrokeColor = Rim;
        canvas.StrokeSize = 2;
        canvas.DrawCircle(cx, cy, r);

        canvas.FillColor = Hole;
        canvas.FillCircle(cx, cy, r * 0.52f);
        canvas.StrokeColor = HoleEdge;
        canvas.StrokeSize = 1;
        canvas.DrawCircle(cx, cy, r * 0.52f);

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 13;
        canvas.FontColor = Ink.WithAlpha(0.7f);
        for (var hour = 1; hour <= 12; hour++)
        {
            var p = At(cx, cy, r * 0.76f, hour * 30);
            canvas.DrawString(hour.ToString(), p.X - 12, p.Y - 10, 24, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        if (this.ArrowAngle is { } arrow)
        {
            var tip = At(cx, cy, r * 0.97f, arrow);
            canvas.StrokeColor = Arrow;
            canvas.StrokeSize = 3;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(At(cx, cy, r * 0.54f, arrow), tip);
            canvas.FillColor = Arrow;
            canvas.FillCircle(tip, 4);
        }
    }
}
