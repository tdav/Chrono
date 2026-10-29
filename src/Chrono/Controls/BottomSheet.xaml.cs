using System.Windows.Input;
using Chrono.Design;
using Chrono.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
#if ANDROID
using Google.Android.Material.Dialog;
using Microsoft.Maui.Platform;
#endif

namespace Chrono.Controls;

/// <summary>Детент шторки (DESIGN.md §3.1): средний — стекло по содержимому, большой — почти на весь экран.</summary>
public enum SheetDetent
{
    Medium,
    Large,
}

/// <summary>Какие детенты разрешены: без большого граббер только закрывает шторку свайпом вниз.</summary>
[Flags]
public enum SheetDetents
{
    Medium = 1,
    Large = 2,
}

/// <summary>
/// Нижняя шторка поверх страницы (DESIGN.md §3.1): затемнение, граббер, панель ✕ / заголовок / ✓ и содержимое.
/// Правки внутри — черновик: ✓ выполняет <see cref="ConfirmCommand"/>, ✕, затемнение, свайп вниз и системная «Назад» —
/// <see cref="CancelCommand"/>, а при <see cref="IsDirty"/> сначала спрашивают «Не сохранять / Сохранить / Отменить».
/// Уточнения открываются шагом внутри шторки (<see cref="Push"/>), а не новой шторкой.
/// </summary>
public partial class BottomSheet : ContentView
{
    // Пружина без перелёта cubic-bezier(0.32, 0.72, 0, 1) (DESIGN.md §2.9); ближайшая встроенная форма — быстрый старт и долгое затухание.
    private static readonly Easing Spring = new(t => 1 - Math.Pow(1 - t, 4));

    // Свайп вниз дальше этого закрывает шторку, вверх дальше этого — большой детент.
    private const double SwipeDistance = 80;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen), typeof(bool), typeof(BottomSheet), false,
        propertyChanged: (b, _, n) => _ = ((BottomSheet)b).ShowAsync((bool)n));

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(BottomSheet), string.Empty,
        propertyChanged: (b, _, _) => ((BottomSheet)b).UpdateChrome());

    public static readonly BindableProperty SheetTemplateProperty = BindableProperty.Create(
        nameof(SheetTemplate), typeof(DataTemplate), typeof(BottomSheet), null);

    public static readonly BindableProperty CancelCommandProperty = BindableProperty.Create(
        nameof(CancelCommand), typeof(ICommand), typeof(BottomSheet), null);

    public static readonly BindableProperty ConfirmCommandProperty = BindableProperty.Create(
        nameof(ConfirmCommand), typeof(ICommand), typeof(BottomSheet), null,
        propertyChanged: (b, o, n) =>
        {
            // ✓ неактивна, пока команда не может выполниться (например, «Своё, мин» с ошибкой).
            var sheet = (BottomSheet)b;
            if (o is ICommand old)
            {
                old.CanExecuteChanged -= sheet.OnConfirmCanExecuteChanged;
            }

            if (n is ICommand command)
            {
                command.CanExecuteChanged += sheet.OnConfirmCanExecuteChanged;
            }

            sheet.UpdateChrome();
        });

    public static readonly BindableProperty CanConfirmProperty = BindableProperty.Create(
        nameof(CanConfirm), typeof(bool), typeof(BottomSheet), true,
        propertyChanged: (b, _, _) => ((BottomSheet)b).UpdateChrome());

    public static readonly BindableProperty IsDirtyProperty = BindableProperty.Create(
        nameof(IsDirty), typeof(bool), typeof(BottomSheet), false);

    public static readonly BindableProperty DetentProperty = BindableProperty.Create(
        nameof(Detent), typeof(SheetDetent), typeof(BottomSheet), SheetDetent.Medium,
        propertyChanged: (b, _, _) => ((BottomSheet)b).ApplyDetent(animate: true));

    public static readonly BindableProperty AllowedDetentsProperty = BindableProperty.Create(
        nameof(AllowedDetents), typeof(SheetDetents), typeof(BottomSheet), SheetDetents.Medium | SheetDetents.Large);

    public static readonly BindableProperty ShowGrabberProperty = BindableProperty.Create(
        nameof(ShowGrabber), typeof(bool), typeof(BottomSheet), true,
        propertyChanged: (b, _, n) =>
        {
            var sheet = (BottomSheet)b;
            sheet.Grabber.Opacity = (bool)n ? 1 : 0;
            sheet.Grabber.InputTransparent = !(bool)n;
        });

    // Вложенные шаги: что было в шторке до Push — содержимое и заголовок.
    private readonly Stack<(View? Content, string Title)> steps = new();
    private double panY;
    // На экране диалог «Не сохранять / Сохранить / Отменить».
    private bool asking;
    // Ожидание перемера после смены детента: панель доплывает со старого места на новое.
    private EventHandler? resized;

    public BottomSheet()
    {
        this.InitializeComponent();
    }

    public bool IsOpen
    {
        get => (bool)this.GetValue(IsOpenProperty);
        set => this.SetValue(IsOpenProperty, value);
    }

    /// <summary>Inline-заголовок панели, короче 15 символов.</summary>
    public string Title
    {
        get => (string)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>Шаблон содержимого; разворачивается при первом открытии шторки.</summary>
    public DataTemplate? SheetTemplate
    {
        get => (DataTemplate?)this.GetValue(SheetTemplateProperty);
        set => this.SetValue(SheetTemplateProperty, value);
    }

    /// <summary>Закрытие без переноса правок.</summary>
    public ICommand? CancelCommand
    {
        get => (ICommand?)this.GetValue(CancelCommandProperty);
        set => this.SetValue(CancelCommandProperty, value);
    }

    /// <summary>✓ «Готово»: перенос правок и закрытие.</summary>
    public ICommand? ConfirmCommand
    {
        get => (ICommand?)this.GetValue(ConfirmCommandProperty);
        set => this.SetValue(ConfirmCommandProperty, value);
    }

    /// <summary>false — форма с ошибкой: ✓ неактивна.</summary>
    public bool CanConfirm
    {
        get => (bool)this.GetValue(CanConfirmProperty);
        set => this.SetValue(CanConfirmProperty, value);
    }

    /// <summary>В шторке есть непринятые правки: закрытие без ✓ спрашивает подтверждение.</summary>
    public bool IsDirty
    {
        get => (bool)this.GetValue(IsDirtyProperty);
        set => this.SetValue(IsDirtyProperty, value);
    }

    public SheetDetent Detent
    {
        get => (SheetDetent)this.GetValue(DetentProperty);
        set => this.SetValue(DetentProperty, value);
    }

    public SheetDetents AllowedDetents
    {
        get => (SheetDetents)this.GetValue(AllowedDetentsProperty);
        set => this.SetValue(AllowedDetentsProperty, value);
    }

    /// <summary>Граббер 36 × 5 — когда шторка может менять высоту.</summary>
    public bool ShowGrabber
    {
        get => (bool)this.GetValue(ShowGrabberProperty);
        set => this.SetValue(ShowGrabberProperty, value);
    }

    // «Уменьшение движения»: шторка растворяется вместо выезда, пружин нет (DESIGN.md §2.9).
    private static bool ReduceMotion =>
        IPlatformApplication.Current?.Services.GetService<ThemeService>()?.ReduceMotion ?? false;

    /// <summary>Шаг внутри шторки: слева появляется «назад», ✓ и ✕ прячутся до возврата.</summary>
    public void Push(View step, string title)
    {
        this.steps.Push((this.Body.Content, this.TitleLabel.Text));
        this.Body.Content = step;
        this.TitleLabel.Text = title;
        this.UpdateChrome();
    }

    /// <summary>Возврат на шаг назад; на первом шаге ничего не делает.</summary>
    public void Pop()
    {
        if (this.steps.Count == 0)
        {
            return;
        }

        (this.Body.Content, this.TitleLabel.Text) = this.steps.Pop();
        this.UpdateChrome();
    }

    /// <summary>Системная «Назад»: шаг назад или закрытие шторки. false — шторка закрыта, «Назад» обрабатывает страница.</summary>
    public bool HandleBack()
    {
        if (!this.IsOpen)
        {
            return false;
        }

        if (this.steps.Count > 0)
        {
            this.Pop();
        }
        else
        {
            _ = this.CancelAsync();
        }

        return true;
    }

    /// <summary>
    /// Закрытие без ✓ (✕, затемнение, свайп вниз, «Назад»). С правками — диалог из ✕:
    /// «Не сохранять» отбрасывает, «Сохранить» переносит, «Отменить» оставляет шторку открытой.
    /// </summary>
    public async Task CancelAsync()
    {
        // Шторка уже уезжает: повторное ✕ или свайп не должны второй раз отменять и показывать диалог.
        if (!this.IsOpen)
        {
            return;
        }

        if (this.IsDirty)
        {
            // Диалог уже на экране: второе ✕ или «Назад» не открывает второй поверх.
            if (this.asking)
            {
                return;
            }

            Element? element = this.Parent;
            while (element is not null and not Page)
            {
                element = element.Parent;
            }

            string? choice;
            this.asking = true;
            try
            {
                if (element is not Page page)
                {
                    choice = AppResources.DontSave;
                }
#if ANDROID
                // Android: диалог Material из трёх кнопок. Positive — «Сохранить», Negative — «Отменить» (справа, рядом с ней), Neutral —
                // «Не сохранять» (слева): primary крайняя справа, как в остальных модальных окнах. Список AppCompat с «Сохранить» строкой
                // (DisplayActionSheetAsync) выглядел как старый серый диалог. Любой другой выход — «Назад», касание вне диалога — это «Отменить».
                else if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { } activity)
                {
                    // Продолжение (Confirm, навигация) — после обработчика клика диалога, а не внутри него.
                    var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var dialog = new MaterialAlertDialogBuilder(activity)
                        .SetTitle(AppResources.SheetSaveChanges)!
                        .SetPositiveButton(AppResources.Save, (_, _) => answer.TrySetResult(AppResources.Save))!
                        .SetNegativeButton(AppResources.SheetCancel, (_, _) => answer.TrySetResult(AppResources.SheetCancel))!
                        .SetNeutralButton(AppResources.DontSave, (_, _) => answer.TrySetResult(AppResources.DontSave))!
                        .Create();
                    // Закрытие диалога приходит после нажатия кнопки, поэтому TrySetResult выше уже сработал; здесь — только выходы без кнопки.
                    dialog.DismissEvent += (_, _) => answer.TrySetResult(AppResources.SheetCancel);
                    dialog.Show();

                    // Тема активити MAUI — не Material, и диалог рисовался серой прямоугольной коробкой с белыми кнопками:
                    // поверхность — непрозрачное крупное стекло со скруглением 28, кнопки — цвет акцента.
                    var density = activity.Resources!.DisplayMetrics!.Density;
                    var surface = new Android.Graphics.Drawables.GradientDrawable();
                    surface.SetCornerRadius(28 * density);
                    surface.SetColor(NativeControls.Resource("GlassOpaque").ToPlatform());
                    dialog.Window?.SetBackgroundDrawable(new Android.Graphics.Drawables.InsetDrawable(surface, (int)(16 * density)));
                    var tint = NativeControls.Resource("Tint").ToPlatform();
                    dialog.GetButton((int)Android.Content.DialogButtonType.Positive)?.SetTextColor(tint);
                    dialog.GetButton((int)Android.Content.DialogButtonType.Negative)?.SetTextColor(tint);
                    dialog.GetButton((int)Android.Content.DialogButtonType.Neutral)?.SetTextColor(tint);

                    choice = await answer.Task;
                }
#endif
                else
                {
                    choice = await page.DisplayActionSheetAsync(null, AppResources.SheetCancel, AppResources.DontSave, AppResources.Save);
                }
            }
            finally
            {
                this.asking = false;
            }

            // Пока висел диалог, шторку закрыли другим путём: второй раз не отменяем.
            if (!this.IsOpen)
            {
                return;
            }

            if (choice == AppResources.Save)
            {
                this.Confirm();
                return;
            }

            // «Отменить»: шторка остаётся открытой.
            if (choice != AppResources.DontSave)
            {
                return;
            }
        }

        this.CancelCommand?.Execute(null);
    }

    private void Confirm()
    {
        // !IsOpen: шторка уже уезжает после ✕ — ✓ не должна принять правки задним числом.
        if (!this.IsOpen || !this.CanConfirm || this.ConfirmCommand?.CanExecute(null) == false)
        {
            return;
        }

        this.ConfirmCommand?.Execute(null);
    }

    // Появление и закрытие: пружина 0.5 с снизу и затемнение; при «Уменьшении движения» — растворение 0.2 с.
    private async Task ShowAsync(bool open)
    {
        this.Panel.CancelAnimations();
        this.ScrimView.CancelAnimations();
        this.CancelAnimations();
        var reduceMotion = ReduceMotion;

        if (open)
        {
            // Содержимое создаётся при первом открытии: восемь шторок редактора со списками,
            // собранные вместе со страницей, задерживали её открытие почти на секунду.
            if (this.Body.Content is null && this.steps.Count == 0 && this.SheetTemplate is { } template)
            {
                this.Body.Content = (View)template.CreateContent();
            }

            this.ApplyDetent(animate: false);
            this.IsVisible = true;
            if (reduceMotion)
            {
                this.Opacity = 0;
                await this.FadeToAsync(1, 200);
            }
            else
            {
                this.Panel.TranslationY = this.Height > 0 ? this.Height : 1000;
                this.ScrimView.Opacity = 0;
                await Task.WhenAll(this.Panel.TranslateToAsync(0, 0, 500, Spring), this.ScrimView.FadeToAsync(1, 250));
            }

            return;
        }

        if (!this.IsVisible)
        {
            return;
        }

        // Страница позади возвращается вместе с выездом панели, а не скачком после него.
        if (this.Parent is Layout behind)
        {
            var back = reduceMotion ? 0u : 250u;
            foreach (var view in behind.Children.OfType<VisualElement>().Where(v => v is not BottomSheet))
            {
                _ = view.ScaleToAsync(1, back, Easing.CubicIn);
                _ = view.TranslateToAsync(0, 0, back, Easing.CubicIn);
            }
        }

        if (reduceMotion)
        {
            await this.FadeToAsync(0, 200);
        }
        else
        {
            await Task.WhenAll(
                this.Panel.TranslateToAsync(0, this.Panel.Height + this.Panel.Margin.Bottom, 250, Easing.CubicIn),
                this.ScrimView.FadeToAsync(0, 250));
        }

        // Шторку открыли снова, пока она уезжала: оставляем открытой.
        if (this.IsOpen)
        {
            return;
        }

        this.IsVisible = false;
        this.Opacity = 1;
        // Растворение при смене детента могло оборваться закрытием: следующее открытие — с непрозрачной панелью.
        this.Panel.Opacity = 1;
        this.Panel.TranslationY = 0;
        this.ScrimView.Opacity = 1;
        while (this.steps.Count > 0)
        {
            this.Pop();
        }

        // Следующее открытие начнётся со среднего детента.
        this.Detent = SheetDetent.Medium;
    }

    // Вид детента: средний — стекло по содержимому с отступом 8 и углами 47; большой — непрозрачный фон почти на весь экран,
    // верхние углы 38, а страница позади (соседи шторки в общем Grid) уменьшается до 0.92 и сдвигается вниз на 46.
    private void ApplyDetent(bool animate)
    {
        var large = this.Detent == SheetDetent.Large && this.IsOpen;
        var reduceMotion = ReduceMotion;
        var motion = animate && this.IsOpen && this.IsVisible;
        // Видимое место панели — с учётом идущего сдвига (открытие или прошлая смена детента).
        var oldY = this.Panel.Y + this.Panel.TranslationY;

        // Прежнее ожидание снимается всегда: двойное переключение за кадр не меняет размер,
        // и подписка сработала бы позже, при постороннем перемере, со старым местом.
        if (this.resized is not null)
        {
            this.Panel.SizeChanged -= this.resized;
            this.resized = null;
        }

        if (large)
        {
            this.Panel.Margin = new Thickness(0, 12, 0, 0);
            this.Panel.VerticalOptions = LayoutOptions.Fill;
            this.Panel.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(38, 38, 0, 0) };
            this.Panel.SetDynamicResource(BackgroundColorProperty, "ElevatedGroupedBackground");
        }
        else
        {
            this.Panel.Margin = new Thickness(8);
            this.Panel.VerticalOptions = LayoutOptions.End;
            this.Panel.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(47) };
            this.Panel.SetDynamicResource(BackgroundColorProperty, "GlassFillLarge");
        }

        // Тень shadowGlass — только у среднего детента: 10 % в светлой теме, 45 % в тёмной.
        this.Panel.Shadow.Opacity = large ? 0 : Application.Current?.RequestedTheme == AppTheme.Dark ? 0.45f : 0.10f;

        // Смена детента: панель плывёт со старого места на новое пружиной 0.5 с (новое место известно после перемера);
        // при «Уменьшении движения» — растворяется.
        if (motion && reduceMotion)
        {
            this.Panel.Opacity = 0;
            _ = this.Panel.FadeToAsync(1, 200);
        }
        else if (motion)
        {
            this.resized = (_, _) =>
            {
                this.Panel.SizeChanged -= this.resized;
                this.resized = null;
                this.Panel.TranslationY = oldY - this.Panel.Y;
                _ = this.Panel.TranslateToAsync(0, 0, 500, Spring);
            };
            this.Panel.SizeChanged += this.resized;
        }

        if (this.Parent is not Layout layout)
        {
            return;
        }

        var duration = motion && !reduceMotion ? 500u : 0u;
        foreach (var view in layout.Children.OfType<VisualElement>().Where(v => v is not BottomSheet))
        {
            _ = view.ScaleToAsync(large ? 0.92 : 1, duration, Spring);
            _ = view.TranslateToAsync(0, large ? 46 : 0, duration, Spring);
        }
    }

    // Кнопки и затемнение в панели меняются вместе с шагом и доступностью ✓.
    private void UpdateChrome()
    {
        var nested = this.steps.Count > 0;
        if (!nested)
        {
            this.TitleLabel.Text = this.Title;
        }

        this.CancelButton.IsVisible = !nested;
        this.BackButton.IsVisible = nested;
        var canConfirm = this.CanConfirm && this.ConfirmCommand?.CanExecute(null) != false;
        this.ConfirmButton.IsVisible = !nested && this.ConfirmCommand is not null;
        this.ConfirmButton.IsEnabled = canConfirm;
        this.ConfirmButton.Opacity = canConfirm ? 1 : 0.4;
    }

    private void OnConfirmCanExecuteChanged(object? sender, EventArgs e) => this.UpdateChrome();

    private void OnCancelTapped(object? sender, TappedEventArgs e) => _ = this.CancelAsync();

    private void OnConfirmTapped(object? sender, TappedEventArgs e) => this.Confirm();

    private void OnBackTapped(object? sender, TappedEventArgs e) => this.Pop();

    private void OnGrabberTapped(object? sender, TappedEventArgs e)
    {
        if (this.ShowGrabber && this.AllowedDetents.HasFlag(SheetDetents.Large))
        {
            this.Detent = this.Detent == SheetDetent.Large ? SheetDetent.Medium : SheetDetent.Large;
        }
    }

    // Свайп по грабберу и панели: решение — по завершении жеста (вниз — закрыть, вверх — большой детент). Панель за пальцем
    // не едет: на Android TotalY считается от касаемого элемента, и сдвиг его родителя сбивал бы расстояние.
    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                this.panY = e.TotalY;
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (this.panY > SwipeDistance)
                {
                    _ = this.CancelAsync();
                }
                else if (this.panY < -SwipeDistance && this.ShowGrabber && this.AllowedDetents.HasFlag(SheetDetents.Large))
                {
                    this.Detent = SheetDetent.Large;
                }

                this.panY = 0;
                break;
        }
    }
}
