namespace Chrono.Controls;

/// <summary>
/// Область нажатия 44 высотой вокруг маленькой кнопки (DESIGN.md §2.1 size.hit, §5 правило 9): кнопки размеров S (28) и M (34)
/// рисуются по дизайну, а касание чуть выше или ниже капсулы всё равно нажимает кнопку. Внутрь кладётся одна <see cref="Button"/>
/// с <c>VerticalOptions="Center"</c>; высоту 44 и минимальную ширину 44 даёт неявный стиль в Styles.xaml.
/// Сама кнопка остаётся настоящей: ripple, фокус и роль «кнопка» для TalkBack / VoiceOver — её. Обёртка скрыта от экранного диктора,
/// чтобы он не читал кнопку дважды; нажатие вне капсулы она передаёт кнопке сама (нажатие на капсулу обрабатывает сама кнопка).
/// </summary>
public class HitTarget : Grid
{
    public HitTarget()
    {
        // Прозрачная заливка: пустое поле вокруг капсулы тоже принимает касание (без неё Grid нажатие не ловит).
        this.BackgroundColor = Colors.Transparent;
        AutomationProperties.SetIsInAccessibleTree(this, false);

        var tap = new TapGestureRecognizer();
        tap.Tapped += this.OnTapped;
        this.GestureRecognizers.Add(tap);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        // SendClicked выполняет Command кнопки и поднимает её Clicked — так же, как нажатие на саму капсулу.
        if (this.Children.OfType<Button>().FirstOrDefault() is { IsEnabled: true } button)
        {
            ((IButtonController)button).SendClicked();
        }
    }
}
