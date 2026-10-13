using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

/// <summary>Содержимое шторки звука; хостит его BottomSheet.</summary>
public partial class SoundSheet : ContentView
{
    public SoundSheet(SoundSheetModel model)
    {
        this.InitializeComponent();
        this.BindingContext = model;
    }
}
