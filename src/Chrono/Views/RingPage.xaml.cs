using Chrono.ViewModels;

namespace Chrono.Views;

public partial class RingPage : ContentPage
{
    public RingPage(RingViewModel viewModel)
    {
        this.InitializeComponent();
        this.BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Колокол качается -14° ↔ 14° бесконечно.
        new Animation
        {
            { 0, 0.5, new Animation(v => this.Bell.Rotation = v, -14, 14, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => this.Bell.Rotation = v, 14, -14, Easing.SinInOut) },
        }.Commit(this.Bell, "swing", length: 1000, repeat: () => true);

        // Три волны расходятся со сдвигом в треть периода.
        var waves = new[] { this.Wave1, this.Wave2, this.Wave3 };
        for (var i = 0; i < waves.Length; i++)
        {
            var wave = waves[i];
            var shift = i / 3.0;
            new Animation(v =>
            {
                var phase = (v + shift) % 1;
                wave.Scale = 0.55 + 0.7 * phase;
                wave.Opacity = 0.7 * (1 - phase);
            }).Commit(wave, "wave", length: 2000, repeat: () => true);
        }
    }

    protected override void OnDisappearing()
    {
        this.Bell.AbortAnimation("swing");
        this.Wave1.AbortAnimation("wave");
        this.Wave2.AbortAnimation("wave");
        this.Wave3.AbortAnimation("wave");
        base.OnDisappearing();
    }
}
