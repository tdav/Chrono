using Microsoft.Maui.Controls.Handlers;

namespace Chrono;

/// <summary>
/// Вкладки нижнего меню (TabBar в AppShell) без анимации перехода. Shell .NET 11 на Android переключает вкладки
/// через ViewPager2 и всегда с плавной прокруткой (ShellItemHandler.MapCurrentItem передаёт animate: true,
/// флаг GoToAsync до него не доходит): страницы проезжали вбок, и экран мерцал. Здесь вкладка меняется сразу.
/// </summary>
public class TabShellItemHandler : ShellItemHandler
{
    protected override void OnSectionChanged(ShellSection shellSection, bool animate)
        => base.OnSectionChanged(shellSection, animate: false);
}
