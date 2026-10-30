using Chrono.Design;

namespace Chrono.Views;

/// <summary>
/// Сцена заставки Chrono (DESIGN.md §4.7): «время · IT · цифровые технологии» на чёрном фоне, 1,8 с до итоговой композиции.
/// Кадр t = 0 — стартовая композиция, она же значок системной заставки: кольцо, 12 часовых рисок, стрелки на 12 и ось —
/// в центре окна, в тех же dp и том же цвете. Из него вырастают слои: сетка точек и столбцы 0/1, вспышка 60 рисок,
/// кольца-шестерни, дорожки печатной платы, стрелки к текущему времени, табло HH:MM:SS и «Chrono». Итоговая композиция
/// затем лежит слоем поверх окна, под ней уже готовый Shell, и растворяется в него (SplashPage).
/// Фон сцена рисует сама: в слое окна под ней нет страницы.
/// Заставка намеренно вне темы: фон #000000 и белые вторичные линии заданы литералами — системная заставка знает только
/// свою тему, и первый кадр обязан с ней совпасть. Акцент — системный тёмной темы (Material You), без него — оранжевый
/// Tint тёмного вида.
/// Всё, что не зависит от времени (место циферблата, сетка, столбцы цифр, дорожки, позиции букв), считается один раз
/// на размер (Layout); цвета с прозрачностью — заранее, 65 ступеней; на кадр — только рисование, по методу на слой.
/// Координаты считаются вручную, без Translate/Scale холста: на Android ScalingCanvas не умножает сдвиг на плотность.
/// </summary>
public sealed class SplashDrawable : IDrawable
{
    /// <summary>Итоговая композиция, с: конец анимации; её «Уменьшение движения» показывает сразу.</summary>
    public const float Composed = 1.8f;

    // ── Стартовая композиция = значок системной заставки, dp от центра окна. Те же числа — в
    //    Platforms/Android/Resources/drawable-v31/splash_dial.xml (холст 288 × 288 dp, видимый круг 192 dp) и
    //    Resources/Splash/splash.svg (iOS и Android 8–11). Менять только вместе.
    private const float RingRadius = 84f;
    private const float RingStroke = 2f;
    private const float HourTickFrom = 70f;
    private const float TickTo = 78f;
    private const float HourTickStroke = 2.5f;
    private const float HourHand = 40f;
    private const float HourHandStroke = 5f;
    private const float MinuteHand = 62f;
    private const float MinuteHandStroke = 3f;
    private const float HubRadius = 5f;
    private const float HubHole = 2f;

    // ── Остальной циферблат — в тех же единицах; на экране всё умножается на масштаб циферблата.
    private const float MinuteTickFrom = 74f;
    private const float MinuteTickStroke = 1.2f;
    private const float SecondHand = 74f;
    private const float SecondTail = 16f;
    private const float SecondHandStroke = 1.2f;
    private const float InnerRingRadius = 94f;
    private const float InnerRingStroke = 1f;
    private const int InnerRingDashes = 120;
    private const float OuterRingRadius = 103f;
    private const float OuterRingStroke = 4f;
    private const int OuterRingDashes = 48;

    // Внешний край кольца-шестерни: по нему считаются место циферблата (≈ 55 % меньшей стороны экрана) и начало дорожек.
    private const float Footprint = 105f;

    // ── Табло и надпись под циферблатом — в долях Footprint на экране; кегли — от него же, а не от системного размера текста.
    private const float BoardGap = 0.30f;
    private const float BoardSize = 0.25f;
    private const float BoardLine = 1.25f;
    private const float TitleGap = 0.22f;
    private const float TitleSize = 0.40f;
    private const float TitleLine = 1.2f;
    private const float Below = BoardGap + (BoardSize * BoardLine) + TitleGap + (TitleSize * TitleLine);

    // Ячейка табло в кеглях: цифра по центру своей ячейки — табличные цифры при любом шрифте.
    private const float DigitCell = 0.64f;
    private const float ColonCell = 0.34f;

    // ── Фон: сетка точек, дорожки платы.
    private const float GridStep = 24f;
    private const float GridDot = 1.5f;
    private const float GridBand = 36f;
    private const float TraceStroke = 1.2f;
    private const float TraceDuration = 0.55f;

    // Ступени прозрачности палитр.
    private const int Steps = 64;

#if ANDROID
    // Inter, как во всём приложении на Android: Graphics ищет шрифт по имени файла в assets — туда его кладёт MauiFont.
    private static readonly IFont TextFont = new Microsoft.Maui.Graphics.Font("Inter-Medium.ttf");
    private static readonly IFont TitleFont = new Microsoft.Maui.Graphics.Font("Inter-Bold.ttf", FontWeights.Bold);
#else
    // iOS — системный SF, как во всём приложении на iOS.
    private static readonly IFont TextFont = Microsoft.Maui.Graphics.Font.Default;
    private static readonly IFont TitleFont = Microsoft.Maui.Graphics.Font.DefaultBold;
#endif

    private static readonly string[] Digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];
    private static readonly string[] TitleLetters = ["C", "h", "r", "o", "n", "o"];
    private static readonly float[] TickSin = [.. Enumerable.Range(0, 60).Select(i => MathF.Sin(i * MathF.Tau / 60))];
    private static readonly float[] TickCos = [.. Enumerable.Range(0, 60).Select(i => MathF.Cos(i * MathF.Tau / 60))];

    // Пунктиры — в толщинах линии (так их задаёт Microsoft.Maui.Graphics). Толщина колец растёт вместе с радиусом, поэтому
    // штрихов на окружности целое число при любом масштабе. Точка сетки — штрих ≈ 0 с круглыми концами через шаг сетки.
    private static readonly float[] GridDash = [0.01f, (GridStep / GridDot) - 0.01f];
    private static readonly float[] InnerDash =
    [
        MathF.Tau * InnerRingRadius / InnerRingDashes * 0.5f / InnerRingStroke,
        MathF.Tau * InnerRingRadius / InnerRingDashes * 0.5f / InnerRingStroke,
    ];

    private static readonly float[] OuterDash =
    [
        MathF.Tau * OuterRingRadius / OuterRingDashes * 0.34f / OuterRingStroke,
        MathF.Tau * OuterRingRadius / OuterRingDashes * 0.66f / OuterRingStroke,
    ];

    // Дорожки платы: угол выхода (0° — вверх, по часовой), ось к боковому краю (иначе — к верхнему или нижнему) и доля
    // пути до края. Порядок — порядок прорастания.
    private static readonly (float Angle, bool Sideways, float Reach)[] Routes =
    [
        (300, true, 0.92f), (322, false, 0.84f), (340, false, 0.97f), (353, false, 0.78f),
        (8, false, 0.93f), (24, false, 0.86f), (40, false, 0.95f), (60, true, 0.90f),
        (80, true, 0.80f), (102, false, 0.88f), (258, false, 0.82f), (280, true, 0.97f),
    ];

    private readonly Color accent;
    private readonly Color[] accentAlpha = new Color[Steps + 1];
    private readonly Color[] whiteAlpha = new Color[Steps + 1];
    private readonly Color[] accentToWhite = new Color[Steps + 1];
    private readonly float[] titleX = new float[TitleLetters.Length];
    private readonly TaskCompletionSource firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Время, на котором застыла итоговая композиция.
    private DateTime? composedAt;

    // Раскладка на текущий размер (Layout).
    private SizeF size = new(-1, -1);
    private PointF center;
    private PointF dial;
    private float scale;
    private float boardLeft;
    private float boardTop;
    private float boardSize;
    private float titleTop;
    private float titleSize;
    private float gridX0;
    private float gridY0;
    private float gridReach;
    private int gridRows;
    private float rainSize;
    private Column[] columns = [];
    private Trace[] traces = [];

    public SplashDrawable()
    {
        // Акцент тёмного вида: системный Material You (тон 60 — тот же @android:color/system_accent1_400, что у значка системной
        // заставки), без него — оранжевый Tint тёмной темы из DESIGN.json (#FF9230, как в splash.svg).
        this.accent = SystemAccessibility.Accent(Appearance.Dark) ?? Color.FromArgb(ChronoColors.All.First(token => token.Key == "Tint").Dark);
        for (var i = 0; i <= Steps; i++)
        {
            var amount = (float)i / Steps;
            this.accentAlpha[i] = this.accent.WithAlpha(amount);
            this.whiteAlpha[i] = Colors.White.WithAlpha(amount);
            this.accentToWhite[i] = new Color(Lerp(this.accent.Red, 1, amount), Lerp(this.accent.Green, 1, amount), Lerp(this.accent.Blue, 1, amount));
        }
    }

    /// <summary>Время сцены, с: 0 — стартовая композиция, <see cref="Composed"/> — итоговая.</summary>
    public float Time { get; set; }

    /// <summary>Первый кадр на экране: до него видна системная заставка с тем же кадром, отсчёт анимации — с него.</summary>
    public Task FirstFrame => this.firstFrame.Task;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width != this.size.Width || dirtyRect.Height != this.size.Height)
        {
            this.Layout(canvas, dirtyRect.Width, dirtyRect.Height);
        }

        var t = this.Time;
        // Итоговая композиция — застывший кадр: слой поверх окна перерисовывается сам (в начале растворения), и стрелки
        // с табло не должны прыгнуть на секунды, прошедшие, пока под ним строился Shell.
        if (t < Composed)
        {
            this.composedAt = null;
        }

        var now = t >= Composed ? this.composedAt ??= DateTime.Now : DateTime.Now;

        canvas.FillColor = Colors.Black;
        canvas.FillRectangle(dirtyRect);

        // 0–0,6 с: циферблат из значка (центр окна, масштаб 1) плавно переезжает и растёт к итоговому месту.
        var settle = EaseInOut(Phase(t, 0, 0.6f));
        var c = new PointF(Lerp(this.center.X, this.dial.X, settle), Lerp(this.center.Y, this.dial.Y, settle));
        var k = Lerp(1, this.scale, settle);

        this.DrawGrid(canvas, t, c);
        this.DrawRain(canvas, t);

        // Под циферблатом фон чистый: точки и цифры не просвечивают между рисками.
        canvas.FillColor = Colors.Black;
        canvas.FillCircle(c.X, c.Y, (Footprint + 3) * k);

        this.DrawTraces(canvas, t, c.X - this.dial.X, c.Y - this.dial.Y);
        this.DrawDial(canvas, t, c, k, now);
        this.DrawBoard(canvas, t, now);
        this.DrawTitle(canvas, t);

        this.firstFrame.TrySetResult();
    }

    // Доля пути от from до to, 0…1.
    private static float Phase(float t, float from, float to) => Math.Clamp((t - from) / (to - from), 0, 1);

    private static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);

    private static float EaseOut(float x) => 1 - ((1 - x) * (1 - x) * (1 - x));

    private static float EaseInOut(float x) => x < 0.5f ? 4 * x * x * x : 1 - (MathF.Pow((-2 * x) + 2, 3) / 2);

    // Пружина без начальной скорости: недодемпфированный отклик (ζ ≈ 0,7, перелёт ≈ 5 %), к концу фазы успокаивается.
    private static float Spring(float x) =>
        x >= 1 ? 1 : 1 - (MathF.Exp(-7 * x) * (MathF.Cos(7.14f * x) + (0.98f * MathF.Sin(7.14f * x))));

    // Цвет палитры с прозрачностью (или долей белого) amount: готовые ступени, без выделений на кадр.
    private static Color Ink(Color[] palette, float amount) => palette[(int)((Math.Clamp(amount, 0, 1) * Steps) + 0.5f)];

    // Раскладка на размер окна — один раз на размер: место циферблата, табло, надписи, сетка, столбцы цифр и дорожки.
    private void Layout(ICanvas canvas, float width, float height)
    {
        this.size = new SizeF(width, height);
        this.center = new PointF(width / 2, height / 2);

        // Циферблат с кольцом-шестерней — ≈ 55 % меньшей стороны. Если под ним хватает места табло и надписи, он остаётся
        // в центре окна, там же, где значок; иначе (альбомная) вся группа центрируется по высоте и при нужде уменьшается.
        var footprint = 0.275f * Math.Min(width, height);
        var dialY = height / 2;
        if (dialY + (footprint * (1 + Below)) > height * 0.94f)
        {
            footprint = Math.Min(footprint, height * 0.86f / (2 + Below));
            dialY = ((height - (footprint * (2 + Below))) / 2) + footprint;
        }

        this.dial = new PointF(width / 2, dialY);
        this.scale = footprint / Footprint;
        this.boardSize = BoardSize * footprint;
        this.boardTop = dialY + (footprint * (1 + BoardGap));
        this.boardLeft = (width / 2) - ((6 * DigitCell + 2 * ColonCell) * this.boardSize / 2);
        this.titleSize = TitleSize * footprint;
        this.titleTop = this.boardTop + (BoardLine * this.boardSize) + (TitleGap * footprint);

        // Буквы «Chrono» — по ширине префиксов (с кернингом), измеренной на кегле 100: на мелком кегле округление заметнее.
        var unit = this.titleSize / 100;
        var left = (width / 2) - (canvas.GetStringSize("Chrono", TitleFont, 100).Width * unit / 2);
        for (var i = 0; i < TitleLetters.Length; i++)
        {
            this.titleX[i] = left + (i == 0 ? 0 : canvas.GetStringSize("Chrono"[..i], TitleFont, 100).Width * unit);
        }

        // Сетка: узел — в центре циферблата; фронт проявления с полосой доходит до дальнего угла.
        this.gridX0 = this.dial.X % GridStep;
        this.gridY0 = this.dial.Y % GridStep;
        this.gridRows = (int)((height - this.gridY0) / GridStep) + 1;
        var farX = Math.Max(this.dial.X, width - this.dial.X);
        var farY = Math.Max(this.dial.Y, height - this.dial.Y);
        this.gridReach = MathF.Sqrt((farX * farX) + (farY * farY)) + GridBand;

        // Раскладка случайная, но одна и та же на каждом запуске.
        var random = new Random(2026);

        // Столбцы 0/1 — редкие, по узлам сетки. Тело и голова — две строки с одинаковым числом строк (у головы сверху пустые),
        // рисуются из одной точки: строки совпадают без расчёта интерлиньяжа. Четыре варианта, соседние отличаются
        // одной-двумя цифрами: столбец мерцает, а не перерисовывается целиком.
        this.rainSize = MathF.Max(10, 0.027f * Math.Min(width, height));
        var count = Math.Clamp((int)MathF.Round(width / 60), 5, 14);
        this.columns = new Column[count];
        for (var i = 0; i < count; i++)
        {
            var x = (i + 0.3f + (0.4f * random.NextSingle())) * width / count;
            var length = random.Next(9, 16);
            var bits = new char[length];
            for (var j = 0; j < length; j++)
            {
                bits[j] = random.Next(2) == 0 ? '0' : '1';
            }

            var body = new string[4];
            var head = new string[4];
            for (var variant = 0; variant < 4; variant++)
            {
                if (variant > 0)
                {
                    for (var flips = random.Next(1, 3); flips > 0; flips--)
                    {
                        var at = random.Next(length);
                        bits[at] = bits[at] == '0' ? '1' : '0';
                    }
                }

                body[variant] = string.Join('\n', bits.Take(length - 2));
                head[variant] = new string('\n', length - 2) + bits[length - 2] + "\n" + bits[length - 1];
            }

            this.columns[i] = new Column(
                this.gridX0 + (MathF.Round((x - this.gridX0) / GridStep) * GridStep),
                height * (-0.3f + (1.05f * random.NextSingle())),
                22 + (26 * random.NextSingle()),
                0.15f * random.NextSingle(),
                4 * random.NextSingle(),
                body,
                head);
        }

        // Дорожки платы: из-под кольца-шестерни к краю — диагональ 45°, прямой участок, ещё диагональ, прямой до узла.
        // Ось — по таблице; если вдоль неё места мало (альбомная), дорожка уходит по другой оси, а без места или под
        // системной панелью — не рисуется. Поля: 20 dp у боковых краёв, 32 dp сверху и снизу (статус-бар, панель навигации).
        // Вниз дорожка идёт только сбоку от табло и надписи. Концы — не ближе 56 dp к верху и низу (под панелями системы).
        var traces = new List<Trace>(Routes.Length);
        var least = 0.2f * Math.Min(width, height);
        var textHalf = Math.Max((width / 2) - left, (6 * DigitCell + 2 * ColonCell) * this.boardSize / 2) + 12;
        for (var i = 0; i < Routes.Length; i++)
        {
            var (angle, sideways, reach) = Routes[i];
            var ux = MathF.Sin(angle * MathF.PI / 180);
            var uy = -MathF.Cos(angle * MathF.PI / 180);
            var p0 = new PointF(this.dial.X + (ux * (Footprint + 1) * this.scale), this.dial.Y + (uy * (Footprint + 1) * this.scale));
            var roomX = ux > 0 ? width - 20 - p0.X : p0.X - 20;
            var roomY = uy < 0 ? p0.Y - 56 : MathF.Abs(p0.X - this.dial.X) >= textHalf ? height - 56 - p0.Y : 0;
            var side = sideways ? roomX >= least || roomY < least : roomY < least;
            var room = side ? roomX : roomY;
            if (room < least || (side && (p0.Y < 32 || p0.Y > height - 32)))
            {
                continue;
            }

            // Основное направление и уход в сторону (наружу от циферблата) — единичные шаги по осям.
            var (px, py) = side ? ((float)MathF.Sign(ux), 0f) : (0f, (float)MathF.Sign(uy));
            var (qx, qy) = side ? (0f, (float)MathF.Sign(uy)) : ((float)MathF.Sign(ux), 0f);
            var drift = side ? (uy < 0 ? p0.Y - 32 : height - 32 - p0.Y) : roomX;
            var run = room * reach;
            var s1 = Math.Max(0, Math.Min(Math.Min(0.18f * run, 0.35f * footprint), 0.6f * drift));
            var s2 = Math.Max(0, Math.Min(Math.Min(0.10f * run, 0.25f * footprint), (0.6f * drift) - s1));
            var straight = 0.30f * run;
            var p1 = new PointF(p0.X + ((px + qx) * s1), p0.Y + ((py + qy) * s1));
            var p2 = new PointF(p1.X + (px * straight), p1.Y + (py * straight));
            var p3 = new PointF(p2.X + ((px + qx) * s2), p2.Y + ((py + qy) * s2));
            var rest = run - s1 - straight - s2;
            var p4 = new PointF(p3.X + (px * rest), p3.Y + (py * rest));
            var lengths = new float[5];
            lengths[1] = MathF.Sqrt(2) * s1;
            lengths[2] = lengths[1] + straight;
            lengths[3] = lengths[2] + (MathF.Sqrt(2) * s2);
            lengths[4] = lengths[3] + rest;
            traces.Add(new Trace([p0, p1, p2, p3, p4], lengths, 0.40f + (i * 0.025f)));
        }

        this.traces = [.. traces];
    }

    // Сетка точек шагом 24 (0–0,6 с): проявляется кругом от циферблата, по фронту бежит полоса акцента. Ряд точек — одна линия
    // с пунктиром «точка через шаг»: ≈ 40 линий на кадр вместо ≈ 700 кругов.
    private void DrawGrid(ICanvas canvas, float t, PointF c)
    {
        var reveal = EaseOut(Phase(t, 0, 0.6f));
        if (reveal <= 0)
        {
            return;
        }

        var front = reveal * this.gridReach;
        var bandFrom = front - GridBand;
        var dots = Ink(this.whiteAlpha, 0.18f);
        var band = Ink(this.accentAlpha, 0.5f);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = GridDot;
        canvas.StrokeDashOffset = 0;
        canvas.StrokeDashPattern = null;
        canvas.StrokeDashPattern = GridDash;
        for (var row = 0; row < this.gridRows; row++)
        {
            var y = this.gridY0 + (row * GridStep);
            var dy = MathF.Abs(y - c.Y);
            if (dy >= front)
            {
                continue;
            }

            var outer = MathF.Sqrt((front * front) - (dy * dy));
            var inner = dy < bandFrom ? MathF.Sqrt((bandFrom * bandFrom) - (dy * dy)) : 0;
            canvas.StrokeColor = dots;
            Row(y, c.X - outer, c.X + outer);
            canvas.StrokeColor = band;
            Row(y, c.X - outer, c.X - inner);
            Row(y, c.X + inner, c.X + outer);
        }

        canvas.StrokeDashPattern = null;

        // Точки ряда y от from до to — только на узлах сетки: штрих пунктира начинается в начале линии.
        void Row(float y, float from, float to)
        {
            var first = MathF.Ceiling((MathF.Max(from, 0) - this.gridX0) / GridStep);
            var last = MathF.Floor((MathF.Min(to, this.size.Width) - this.gridX0) / GridStep);
            if (last >= first)
            {
                canvas.DrawLine(this.gridX0 + (first * GridStep), y, this.gridX0 + (last * GridStep) + 0.5f, y);
            }
        }
    }

    // Столбцы двоичных цифр: проявляются за 0–0,6 с и медленно стекают вниз; тело — белый 10 %, голова внизу — 20 %.
    private void DrawRain(ICanvas canvas, float t)
    {
        canvas.Font = TextFont;
        canvas.FontSize = this.rainSize;
        foreach (var column in this.columns)
        {
            var fade = EaseOut(Phase(t, column.Delay, column.Delay + 0.45f));
            if (fade <= 0)
            {
                continue;
            }

            var y = column.Top + (column.Speed * t);
            var variant = (int)((t * 7) + column.Flicker) & 3;
            canvas.FontColor = Ink(this.whiteAlpha, 0.10f * fade);
            canvas.DrawString(column.Body[variant], column.X - this.rainSize, y, 2 * this.rainSize, this.size.Height,
                HorizontalAlignment.Center, VerticalAlignment.Top, TextFlow.OverflowBounds);
            canvas.FontColor = Ink(this.whiteAlpha, 0.20f * fade);
            canvas.DrawString(column.Head[variant], column.X - this.rainSize, y, 2 * this.rainSize, this.size.Height,
                HorizontalAlignment.Center, VerticalAlignment.Top, TextFlow.OverflowBounds);
        }
    }

    // Дорожки платы (0,40–1,30): прорастают по длине с яркой головой, на конце загорается узел с ореолом.
    // Привязаны к итоговому месту циферблата; пока он доезжает, сдвигаются вместе с ним (dx, dy).
    private void DrawTraces(ICanvas canvas, float t, float dx, float dy)
    {
        canvas.StrokeLineCap = LineCap.Round;
        foreach (var trace in this.traces)
        {
            var grow = EaseInOut(Phase(t, trace.Delay, trace.Delay + TraceDuration));
            if (grow <= 0)
            {
                continue;
            }

            var length = grow * trace.Lengths[^1];
            var tip = trace.Points[0];
            canvas.StrokeSize = TraceStroke;
            canvas.StrokeColor = Ink(this.accentAlpha, 0.5f);
            for (var i = 1; i < trace.Points.Length; i++)
            {
                var from = trace.Points[i - 1];
                tip = trace.Points[i];
                if (trace.Lengths[i] > length)
                {
                    var part = (length - trace.Lengths[i - 1]) / (trace.Lengths[i] - trace.Lengths[i - 1]);
                    tip = new PointF(Lerp(from.X, tip.X, part), Lerp(from.Y, tip.Y, part));
                }

                canvas.DrawLine(from.X + dx, from.Y + dy, tip.X + dx, tip.Y + dy);
                if (trace.Lengths[i] >= length)
                {
                    break;
                }
            }

            if (grow < 1)
            {
                canvas.FillColor = Ink(this.whiteAlpha, 0.8f);
                canvas.FillCircle(tip.X + dx, tip.Y + dy, 1.6f);
                continue;
            }

            var pop = Phase(t, trace.Delay + TraceDuration, trace.Delay + TraceDuration + 0.3f);
            canvas.StrokeSize = 1;
            canvas.StrokeColor = Ink(this.accentAlpha, 0.6f * (1 - pop));
            canvas.DrawCircle(tip.X + dx, tip.Y + dy, Lerp(3, 11, EaseOut(pop)));
            canvas.StrokeSize = TraceStroke;
            canvas.StrokeColor = Ink(this.accentAlpha, 0.9f);
            canvas.DrawCircle(tip.X + dx, tip.Y + dy, 3);
            canvas.FillColor = this.accent;
            canvas.FillCircle(tip.X + dx, tip.Y + dy, 1.2f);
        }
    }

    // Циферблат с центром c и масштабом k к значку: кольца-шестерни, кольцо и риски, стрелки, ось.
    private void DrawDial(ICanvas canvas, float t, PointF c, float k, DateTime now)
    {
        // 0,30–1,20: кольца-шестерни проявляются, раздвигаются от 0,92 радиуса и вращаются навстречу друг другу, замедляясь.
        // Поворот — сдвигом пунктира, тоже в толщинах линии: угол (рад) × радиус / толщина. Оба сдвига положительные.
        var rings = EaseOut(Phase(t, 0.30f, 0.55f));
        if (rings > 0)
        {
            var spread = k * Lerp(0.92f, 1, EaseOut(Phase(t, 0.30f, 0.70f)));
            var turn = EaseOut(Phase(t, 0.30f, 1.20f));
            canvas.StrokeLineCap = LineCap.Butt;
            canvas.StrokeSize = InnerRingStroke * spread;
            canvas.StrokeDashOffset = turn * 0.9f * InnerRingRadius / InnerRingStroke;
            canvas.StrokeDashPattern = null;
            canvas.StrokeDashPattern = InnerDash;
            canvas.StrokeColor = Ink(this.whiteAlpha, 0.28f * rings);
            canvas.DrawCircle(c.X, c.Y, InnerRingRadius * spread);
            canvas.StrokeSize = OuterRingStroke * spread;
            canvas.StrokeDashOffset = (1 - turn) * 0.6f * OuterRingRadius / OuterRingStroke;
            canvas.StrokeDashPattern = null;
            canvas.StrokeDashPattern = OuterDash;
            canvas.StrokeColor = Ink(this.accentAlpha, 0.55f * rings);
            canvas.DrawCircle(c.X, c.Y, OuterRingRadius * spread);
            canvas.StrokeDashPattern = null;
            canvas.StrokeDashOffset = 0;
        }

        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeColor = this.accent;
        canvas.StrokeSize = RingStroke * k;
        canvas.DrawCircle(c.X, c.Y, RingRadius * k);

        // 0,10–0,90: риски загораются по часовой по одной — вспышка акцентом с бликом и угасание до 40 %;
        // 12 часовых длиннее и горят с первого кадра (они есть на значке).
        for (var i = 0; i < 60; i++)
        {
            var hour = i % 5 == 0;
            var lit = Phase(t, 0.10f + (i * 0.0105f), 0.28f + (i * 0.0105f));
            if (!hour && lit <= 0)
            {
                continue;
            }

            var from = (hour ? HourTickFrom : MinuteTickFrom) * k;
            var x1 = c.X + (TickSin[i] * from);
            var y1 = c.Y - (TickCos[i] * from);
            var x2 = c.X + (TickSin[i] * TickTo * k);
            var y2 = c.Y - (TickCos[i] * TickTo * k);
            canvas.StrokeSize = (hour ? HourTickStroke : MinuteTickStroke) * k;
            canvas.StrokeColor = hour ? this.accent : Ink(this.accentAlpha, Lerp(1, 0.4f, EaseOut(lit)));
            canvas.DrawLine(x1, y1, x2, y2);
            if (lit > 0 && lit < 1)
            {
                canvas.StrokeColor = Ink(this.whiteAlpha, 0.6f * (1 - lit) * (1 - lit));
                canvas.DrawLine(x1, y1, x2, y2);
            }
        }

        // 0,60–1,40: стрелки от 12 к текущему времени с пружинным докатом; часовая и минутная из акцента значка
        // становятся белыми; секундная — тонкая, акцентом, дальше идёт плавно.
        var hourAngle = ((now.Hour % 12) + (now.Minute / 60f) + (now.Second / 3600f)) * 30;
        var minuteAngle = (now.Minute + (now.Second / 60f)) * 6;
        var secondAngle = (now.Second + (now.Millisecond / 1000f)) * 6;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeColor = Ink(this.accentToWhite, EaseInOut(Phase(t, 0.60f, 1.00f)));
        Hand(Spring(Phase(t, 0.60f, 1.30f)) * hourAngle, 0, HourHand, HourHandStroke);
        Hand(Spring(Phase(t, 0.66f, 1.40f)) * minuteAngle, 0, MinuteHand, MinuteHandStroke);
        var second = EaseOut(Phase(t, 0.90f, 1.15f));
        if (second > 0)
        {
            canvas.StrokeColor = Ink(this.accentAlpha, second);
            Hand(Spring(Phase(t, 0.90f, 1.40f)) * secondAngle, -SecondTail, SecondHand, SecondHandStroke);
        }

        canvas.FillColor = this.accent;
        canvas.FillCircle(c.X, c.Y, HubRadius * k);
        canvas.FillColor = Colors.Black;
        canvas.FillCircle(c.X, c.Y, HubHole * k);

        // Стрелка под углом angle (0° — 12 часов, по часовой) от радиуса from до to.
        void Hand(float angle, float from, float to, float stroke)
        {
            var sin = MathF.Sin(angle * MathF.PI / 180);
            var cos = MathF.Cos(angle * MathF.PI / 180);
            canvas.StrokeSize = stroke * k;
            canvas.DrawLine(c.X + (sin * from * k), c.Y - (cos * from * k), c.X + (sin * to * k), c.Y - (cos * to * k));
        }
    }

    // 1,00–1,60: табло HH:MM:SS под циферблатом — проявляется снизу, цифры перебираются акцентом и по очереди
    // «защёлкиваются» белым на текущем времени; дальше идут секунды.
    private void DrawBoard(ICanvas canvas, float t, DateTime now)
    {
        var show = EaseOut(Phase(t, 1.00f, 1.18f));
        if (show <= 0)
        {
            return;
        }

        Span<int> value = [now.Hour / 10, now.Hour % 10, now.Minute / 10, now.Minute % 10, now.Second / 10, now.Second % 10];
        var top = this.boardTop + ((1 - EaseOut(Phase(t, 1.00f, 1.30f))) * 0.3f * this.boardSize);
        var x = this.boardLeft;
        canvas.Font = TextFont;
        canvas.FontSize = this.boardSize;
        for (var cell = 0; cell < 8; cell++)
        {
            var colon = cell is 2 or 5;
            var width = (colon ? ColonCell : DigitCell) * this.boardSize;
            var glyph = ":";
            if (colon)
            {
                canvas.FontColor = Ink(this.accentAlpha, 0.9f * show);
            }
            else
            {
                var digit = cell - (cell / 3);
                var lockAt = 1.14f + (digit * 0.075f);
                if (t < lockAt)
                {
                    // Перебор: новая цифра каждые 1/30 с, у каждого разряда своя.
                    var hash = (uint)(digit + 1) * 2654435761u ^ (uint)(t * 30) * 40503u;
                    glyph = Digits[(int)(hash % 10)];
                    canvas.FontColor = Ink(this.accentAlpha, 0.75f * show);
                }
                else
                {
                    glyph = Digits[value[digit]];
                    canvas.FontColor = Ink(this.whiteAlpha, Lerp(1, 0.9f, Phase(t, lockAt, lockAt + 0.2f)) * show);
                }
            }

            canvas.DrawString(glyph, x, top, width, 2 * this.boardSize, HorizontalAlignment.Center, VerticalAlignment.Top, TextFlow.OverflowBounds);
            x += width;
        }
    }

    // 1,20–1,80: «Chrono» крупно белым — буквы по очереди поднимаются снизу и проявляются.
    private void DrawTitle(ICanvas canvas, float t)
    {
        canvas.Font = TitleFont;
        canvas.FontSize = this.titleSize;
        for (var i = 0; i < TitleLetters.Length; i++)
        {
            var rise = Phase(t, 1.20f + (i * 0.05f), 1.56f + (i * 0.05f));
            if (rise <= 0)
            {
                break;
            }

            var eased = EaseOut(rise);
            canvas.FontColor = Ink(this.whiteAlpha, eased);
            canvas.DrawString(TitleLetters[i], this.titleX[i], this.titleTop + ((1 - eased) * 0.45f * this.titleSize),
                2 * this.titleSize, TitleLine * this.titleSize * 2, HorizontalAlignment.Left, VerticalAlignment.Top, TextFlow.OverflowBounds);
        }
    }

    // Столбец двоичных цифр: x, верх в начале, скорость стекания (dp/с), задержка проявления, сдвиг мерцания, варианты строк.
    private sealed record Column(float X, float Top, float Speed, float Delay, float Flicker, string[] Body, string[] Head);

    // Дорожка платы: точки ломаной, длины пути до каждой точки и начало прорастания, с.
    private sealed record Trace(PointF[] Points, float[] Lengths, float Delay);
}
