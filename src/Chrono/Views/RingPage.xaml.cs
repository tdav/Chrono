using Chrono.Design;
using Chrono.ViewModels;

namespace Chrono.Views;

public partial class RingPage : ContentPage
{
    private readonly ThemeService themes;

    public RingPage(RingViewModel viewModel, ThemeService themes)
    {
        this.InitializeComponent();
        this.BindingContext = viewModel;
        this.themes = themes;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // «Уменьшение движения» (DESIGN.md §2.9): волны неподвижны — кольца стоят там, где их поставила разметка.
        if (this.themes.ReduceMotion)
        {
            return;
        }

        // Две волны расходятся от круга колокола (112) до края поля (176) за 2.4 с, вторая — на полпериода позже.
        var waves = new[] { this.Wave1, this.Wave2 };
        for (var i = 0; i < waves.Length; i++)
        {
            var wave = waves[i];
            var shift = i / 2.0;
            new Animation(v =>
            {
                var phase = (v + shift) % 1;
                var scale = (112 + 64 * phase) / wave.WidthRequest;
                wave.Scale = scale;
                // Масштаб растягивает и линию: делим на него, чтобы кольцо оставалось 1.5 pt.
                wave.StrokeThickness = 1.5 / scale;
                wave.Opacity = 0.6 * (1 - phase);
            }).Commit(wave, "wave", length: 2400, repeat: () => true);
        }
    }

    protected override void OnDisappearing()
    {
        this.Wave1.AbortAnimation("wave");
        this.Wave2.AbortAnimation("wave");
        base.OnDisappearing();
    }
}
