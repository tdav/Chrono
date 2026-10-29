using Chrono.Design;
using Chrono.Services;

namespace Chrono.Views;

/// <summary>
/// Рисует 12-часовой циферблат дня по готовым срезам <see cref="DialLayout"/> (DESIGN.md §4.10).
/// Углы — как в DialLayout: 0° — «12 часов», по часовой стрелке. Радиус обода один и тот же для рисования и для касания.
/// Все цвета берутся из ресурсов приложения при каждой отрисовке (их раскладывает ThemeService): после смены темы страница вызывает Invalidate.
/// </summary>
public sealed class DialDrawable : IDrawable
{
    // Геометрия в долях радиуса круга (макет: круг 140, сектора 98–124, цифры на 84, шкала 128–137).
    private const float TickOuter = 0.979f;
    private const float MinorTickInner = 0.943f;
    private const float MajorTickInner = 0.914f;
    private const float RingInner = (float)DialLayout.InnerRadius;
    private const float RingOuter = 0.886f;
    private const float NumeralsAt = 0.60f;
    private const float ArrowFrom = 0.43f;
    private const float ArrowTo = 0.93f;

    private static Color Face => NativeControls.Resource("SecondarySystemGroupedBackground");
    private static Color Scale => NativeControls.Resource("OpaqueSeparator");
    private static Color Secondary => NativeControls.Resource("SecondaryLabel");
    private static Color Ink => NativeControls.Resource("Label");
    private static Color Arrow => NativeControls.Resource("Red");

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
        var face = Face;

        canvas.FillColor = face;
        canvas.FillCircle(cx, cy, r);

        // Шкала: 60 засечек, каждая пятая (час) длиннее и темнее.
        canvas.StrokeLineCap = LineCap.Butt;
        for (var i = 0; i < 60; i++)
        {
            var major = i % 5 == 0;
            canvas.StrokeColor = major ? Secondary : Scale;
            canvas.StrokeSize = major ? 1.5f : 1;
            canvas.DrawLine(At(cx, cy, r * (major ? MajorTickInner : MinorTickInner), i * 6), At(cx, cy, r * TickOuter, i * 6));
        }

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
                var color = NativeControls.Resource(DialLayout.CategoryKey(slice.ColorIndex));

                if (slice.IsTick)
                {
                    // Дело без длительности — засечка на ободе. Как у секторов: выполненная — 35 %, выключенная — бледная тонкая линия,
                    // остальные бледнеют при выборе другой записи.
                    var inner = At(cx, cy, r * 0.85f, slice.StartAngle);
                    var outer = At(cx, cy, r * TickOuter, slice.StartAngle);
                    canvas.StrokeLineCap = LineCap.Butt;
                    if (selected)
                    {
                        canvas.StrokeColor = Ink;
                        canvas.StrokeSize = 8;
                        canvas.DrawLine(At(cx, cy, r * 0.83f, slice.StartAngle), At(cx, cy, r, slice.StartAngle));
                    }

                    var alpha = slice.Entry.IsMuted ? 0.45f : slice.Entry.IsDone ? 0.35f : 1f;
                    canvas.StrokeColor = color.WithAlpha(dim ? Math.Min(alpha, 0.35f) : alpha);
                    canvas.StrokeSize = slice.Entry.IsMuted ? 2 : 4;
                    canvas.DrawLine(inner, outer);
                    continue;
                }

                // Кольцо сектора между RingInner и RingOuter: ломаная с шагом ~3°.
                var path = new PathF();
                var steps = Math.Max(2, (int)Math.Ceiling(slice.SweepAngle / 3));
                for (var i = 0; i <= steps; i++)
                {
                    var p = At(cx, cy, r * RingOuter, slice.StartAngle + slice.SweepAngle * i / steps);
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
                    path.LineTo(At(cx, cy, r * RingInner, slice.StartAngle + slice.SweepAngle * i / steps));
                }

                path.Close();

                if (slice.Entry.IsMuted)
                {
                    // Выключенная запись — только пунктирный контур без заливки.
                    canvas.SaveState();
                    canvas.StrokeColor = color.WithAlpha(dim ? 0.35f : 1f);
                    canvas.StrokeSize = 1.5f;
                    canvas.StrokeDashPattern = [3, 3];
                    canvas.DrawPath(path);
                    canvas.RestoreState();
                }
                else
                {
                    canvas.FillColor = color.WithAlpha(dim || slice.Entry.IsDone ? 0.35f : 1f);
                    canvas.FillPath(path);

                    // Просвет между секторами — контур цвета круга.
                    canvas.StrokeColor = face;
                    canvas.StrokeSize = 2;
                    canvas.StrokeLineJoin = LineJoin.Round;
                    canvas.DrawPath(path);
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

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 13;
        canvas.FontColor = Secondary;
        for (var hour = 1; hour <= 12; hour++)
        {
            var p = At(cx, cy, r * NumeralsAt, hour * 30);
            canvas.DrawString(hour.ToString(), p.X - 24, p.Y - 20, 48, 40, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        if (this.ArrowAngle is { } arrow)
        {
            var tip = At(cx, cy, r * ArrowTo, arrow);
            canvas.StrokeColor = Arrow;
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.DrawLine(At(cx, cy, r * ArrowFrom, arrow), tip);
            canvas.FillColor = Arrow;
            canvas.FillCircle(tip, 4);
        }
    }
}
