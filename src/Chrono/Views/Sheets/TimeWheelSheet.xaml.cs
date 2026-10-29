using Chrono.Services;

namespace Chrono.Views.Sheets;

public partial class TimeWheelSheet : ContentView
{
    private readonly ISoundPlayer? sound = IPlatformApplication.Current?.Services.GetService<ISoundPlayer>();

    public TimeWheelSheet()
    {
        this.InitializeComponent();
    }

    // Нажатие на быстрое время («06:30», «Сейчас»): барабаны переходят прыжком, их щелчок не слышен — щёлкает кнопка.
    private void OnTick(object? sender, EventArgs e) => this.sound?.Tick();
}
