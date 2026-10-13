using Chrono.ViewModels;

namespace Chrono.Views.Sheets;

/// <summary>Содержимое шторки повтора; хостит его BottomSheet.</summary>
public partial class RepeatSheet : ContentView
{
    public RepeatSheet(RepeatSheetModel model)
    {
        this.InitializeComponent();
        this.BindingContext = model;
    }
}
