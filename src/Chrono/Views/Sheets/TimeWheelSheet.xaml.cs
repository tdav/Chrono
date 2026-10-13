using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

public partial class TimeWheelSheet : ContentView
{
    // Высота строки барабана; заголовок 84 = (224 − 56) / 2, поэтому строка i в центре при смещении i × 56.
    private const double RowHeight = 56;

    private TimeSheetModel? model;

    public TimeWheelSheet()
    {
        this.InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (this.model is not null)
        {
            this.model.ScrollRequested -= this.OnScrollRequested;
        }

        this.model = this.BindingContext as TimeSheetModel;
        if (this.model is not null)
        {
            this.model.ScrollRequested += this.OnScrollRequested;
        }
    }

    private void OnScrollRequested(object? sender, EventArgs e)
    {
        // Шторка только что стала видимой: список ещё не разложен, прокрутка до раскладки игнорируется.
        this.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
        {
            this.HourWheel.ScrollTo(this.model!.Hour, position: ScrollToPosition.Center, animate: false);
            this.MinuteWheel.ScrollTo(this.model.Minute, position: ScrollToPosition.Center, animate: false);
        });
    }

    private void OnHourScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var index = Math.Clamp((int)Math.Round(e.VerticalOffset / RowHeight), 0, 23);
        if (this.model is not null && this.model.Hour != index)
        {
            this.model.Hour = index;
        }
    }

    private void OnMinuteScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var index = Math.Clamp((int)Math.Round(e.VerticalOffset / RowHeight), 0, 59);
        if (this.model is not null && this.model.Minute != index)
        {
            this.model.Minute = index;
        }
    }
}
