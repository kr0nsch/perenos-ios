using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perenos.Services;

namespace Perenos.ViewModels;

public enum AppSortMode
{
    Name,
    SizeDescending,
}

/// <summary>
/// A searchable, checkable list of apps. The apps step shows either the apps on the source
/// iPhone (<see cref="AppsViewModel"/>) or the apps saved on this PC (<see cref="SavedAppsViewModel"/>).
/// </summary>
public abstract partial class AppListViewModel : ObservableObject
{
    private List<AppItemViewModel> _all = [];

    public ObservableCollection<AppItemViewModel> Visible { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortButtonText), nameof(IsNameSort), nameof(IsSizeSort))]
    public partial AppSortMode SortMode { get; set; }

    public string SortButtonText => Loc.Get(SortMode == AppSortMode.Name ? "Apps_SortByName" : "Apps_SortBySize");

    public string SortByNameText => Loc.Get("Apps_SortByName");

    public string SortBySizeText => Loc.Get("Apps_SortBySize");

    public bool IsNameSort => SortMode == AppSortMode.Name;

    public bool IsSizeSort => SortMode == AppSortMode.SizeDescending;

    [RelayCommand]
    private void SetSortMode(string mode) => SortMode = Enum.Parse<AppSortMode>(mode);

    partial void OnSortModeChanged(AppSortMode value) => ApplyFilter();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowList), nameof(ShowEmpty))]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary), nameof(HasSelection), nameof(SelectAllText))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList), nameof(ShowEmpty))]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoMatches))]
    public partial int VisibleCount { get; set; }

    /// <summary>Small print under the list, such as apps that can't be shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFootnote))]
    public partial string Footnote { get; set; } = "";

    [ObservableProperty]
    public partial string? FootnoteDetail { get; set; }

    public bool HasFootnote => Footnote.Length > 0;

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool ShowList => !IsLoading && !HasError && TotalCount > 0;

    public bool ShowEmpty => !IsLoading && !HasError && TotalCount == 0;

    public bool ShowNoMatches => ShowList && VisibleCount == 0;

    public bool HasSelection => SelectedCount > 0;

    public abstract string LoadingText { get; }

    public abstract string EmptyText { get; }

    public string SelectAllText => SelectionScope.Count > 0 && SelectionScope.All(a => a.IsSelected) ? Loc.Get("Apps_DeselectAll") : Loc.Get("Apps_SelectAll");

    public string SelectAllHint => Loc.Get("Apps_SelectVisibleHint");

    public virtual string SelectionSummary
    {
        get
        {
            if (SelectedCount == 0)
                return Loc.Get("Apps_NoneSelected");
            var bytes = SelectedBytes;
            var count = Loc.Plural("Apps_SelectedCount", SelectedCount);
            return bytes > 0 ? $"{count}  ·  {Format.Bytes(bytes)}" : count;
        }
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged((string?)null);
        foreach (var item in _all)
            item.RefreshLocalization();
    }

    protected IReadOnlyList<AppItemViewModel> All => _all;

    protected long SelectedBytes => _all.Where(a => a.IsSelected).Sum(a => a.App.SizeBytes ?? 0);

    /// <summary>Select All applies only to apps matching the active search and filter that can be installed.</summary>
    private List<AppItemViewModel> SelectionScope => (string.IsNullOrWhiteSpace(SearchText) ? _all.Where(a => IncludeInFilter(a)) : Visible.AsEnumerable()).Where(a => a.IsSelectable).ToList();

    public void ClearSelection()
    {
        foreach (var item in _all)
            item.IsSelected = false;
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var scope = SelectionScope;
        var select = scope.Any(a => !a.IsSelected);
        foreach (var item in scope)
            item.IsSelected = select;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(SelectAllText));
    }

    protected void SetItems(List<AppItemViewModel> items)
    {
        _all = items;
        foreach (var item in items)
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
                    SelectedCount = _all.Count(a => a.IsSelected);
            };
        TotalCount = items.Count;
        SelectedCount = items.Count(a => a.IsSelected);
        ApplyFilter();
    }

    protected void Clear()
    {
        _all = [];
        Visible.Clear();
        TotalCount = 0;
        VisibleCount = 0;
        SelectedCount = 0;
        Footnote = "";
        FootnoteDetail = null;
    }

    protected virtual bool IncludeInFilter(AppItemViewModel item) => true;

    protected void ApplyFilter()
    {
        var matches = _all.Where(a => IncludeInFilter(a) && a.Matches(SearchText.Trim()));
        // Keep incompatible saved apps in their own group at the bottom, whatever the sort mode.
        var ordered = matches.OrderByDescending(a => a.IsSelectable);
        var sorted = SortMode == AppSortMode.SizeDescending
            ? ordered.ThenByDescending(a => a.App.SizeBytes ?? -1).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            : ordered.ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase);
        var visible = sorted.ThenBy(a => a.App.BundleId, StringComparer.OrdinalIgnoreCase).ToList();
        Visible.Clear();
        var headerShown = false;
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].ShowSeparator = i > 0;
            visible[i].ShowSectionHeader = !visible[i].IsSelectable && !headerShown && visible[i].SectionHeader.Length > 0;
            headerShown |= visible[i].ShowSectionHeader;
            Visible.Add(visible[i]);
        }

        VisibleCount = visible.Count;
    }
}
