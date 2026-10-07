using System.Collections.ObjectModel;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

public sealed record ReliabilityRow(ReliabilityItem Item, string Title, bool Ok)
{
    public string Mark => this.Ok ? "✅" : "⚠️";

    public bool NeedsFix => !this.Ok;
}

public sealed partial class ReliabilityViewModel : ObservableObject
{
    private readonly IReliabilityChecks reliability;

    public ReliabilityViewModel(IReliabilityChecks reliability)
    {
        this.reliability = reliability;
    }

    public ObservableCollection<ReliabilityRow> Rows { get; } = [];

    public bool IsAndroid => DeviceInfo.Current.Platform == DevicePlatform.Android;

    public bool IsIos => DeviceInfo.Current.Platform == DevicePlatform.iOS;

    public async Task RefreshAsync()
    {
        var statuses = await this.reliability.GetStatusAsync();
        this.Rows.Clear();
        foreach (var status in statuses)
        {
            var title = AppResources.ResourceManager.GetString($"Rel_{status.Item}", AppResources.Culture) ?? status.Item.ToString();
            this.Rows.Add(new ReliabilityRow(status.Item, title, status.Ok));
        }
    }

    [RelayCommand]
    private void Fix(ReliabilityRow row) => this.reliability.Fix(row.Item);

    [RelayCommand]
    private Task CloseAsync() => Shell.Current.GoToAsync("..");
}
