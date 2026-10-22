using Chrono.Services;
using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

public partial class CalendarSheet : ContentView
{
    private readonly ISoundPlayer? sound = IPlatformApplication.Current?.Services.GetService<ISoundPlayer>();

    public CalendarSheet()
    {
        this.InitializeComponent();
    }

    // Листание месяца или года и «Сегодня» всегда меняют показанное — щелчок на каждое нажатие.
    private void OnTick(object? sender, EventArgs e) => this.sound?.Tick();

    // Прошедший или пустой день не выбирается — и не щёлкает (то же условие, что в PickCommand).
    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is CalendarDayItem { IsDisabled: false, Cell.Date: not null })
        {
            this.sound?.Tick();
        }
    }
}
