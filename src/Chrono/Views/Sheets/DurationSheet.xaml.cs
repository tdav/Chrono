using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

/// <summary>Содержимое шторки длительности; модель приходит через BindingContext.</summary>
public partial class DurationSheet : ContentView
{
    private DurationSheetModel? model;

    public DurationSheet()
    {
        this.InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (this.model is not null)
        {
            this.model.Done -= this.OnDone;
        }

        this.model = this.BindingContext as DurationSheetModel;
        if (this.model is not null)
        {
            this.model.Done += this.OnDone;
        }
    }

    // «Окей», «Отмена», затемнение и «Назад» сходятся в Done: без снятия фокуса шторка закрывается, а клавиатура остаётся.
    private async void OnDone(object? sender, bool accepted)
    {
        this.CustomEntry.Unfocus();
        await this.CustomEntry.HideSoftInputAsync(CancellationToken.None);
    }
}
