using System.Collections.ObjectModel;
using Chrono.Resources.Strings;
using Chrono.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chrono.ViewModels;

/// <summary>Пункт «Надёжности»: название, статус словами (<paramref name="Subtitle"/>) и признак последней строки (без разделителя).</summary>
public sealed record ReliabilityRow(ReliabilityItem Item, string Title, string Subtitle, bool Ok, bool IsLast)
{
    public bool NeedsFix => !this.Ok;

    public bool HasSeparator => !this.IsLast;
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
        foreach (var (status, index) in statuses.Select((status, index) => (status, index)))
        {
            var title = AppResources.ResourceManager.GetString($"RelTitle_{status.Item}", AppResources.Culture) ?? status.Item.ToString();
            var subtitle = AppResources.ResourceManager.GetString($"{(status.Ok ? "RelOk" : "RelProblem")}_{status.Item}", AppResources.Culture) ?? "";
            this.Rows.Add(new ReliabilityRow(status.Item, title, subtitle, status.Ok, index == statuses.Count - 1));
        }
    }

    [RelayCommand]
    private void Fix(ReliabilityRow row) => this.reliability.Fix(row.Item);

    [RelayCommand]
    private Task CloseAsync() => Shell.Current.GoToAsync("..");
}
