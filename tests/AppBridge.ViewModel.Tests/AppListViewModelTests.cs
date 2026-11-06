using AppBridge.Core.Devices;
using AppBridge.ViewModels;

namespace AppBridge.ViewModel.Tests;

public sealed class AppListViewModelTests
{
    [Fact]
    public void SortsByNameAndThenBySize()
    {
        var vm = new TestAppList();
        vm.Replace(Item("small", "Alpha", 100), Item("large", "Zulu", 5000), Item("medium", "Beta", 500));

        Assert.Equal(["Alpha", "Beta", "Zulu"], vm.Visible.Select(a => a.Name));

        vm.SortMode = AppSortMode.SizeDescending;

        Assert.Equal(["Zulu", "Beta", "Alpha"], vm.Visible.Select(a => a.Name));
        Assert.Equal([5000L, 500L, 100L], vm.Visible.Select(a => a.App.SizeBytes ?? -1));
    }

    [Fact]
    public void SortingKeepsIncompatibleAppsAtTheBottom()
    {
        var vm = new TestAppList();
        vm.Replace(
            Item("small", "Compatible", 100),
            Item("blocked", "Unavailable", 9000, selectable: false));

        vm.SortMode = AppSortMode.SizeDescending;

        Assert.Equal(["Compatible", "Unavailable"], vm.Visible.Select(a => a.Name));
        Assert.True(vm.Visible[1].ShowSectionHeader);
    }

    [Fact]
    public void SearchMatchesSavedAppsAppleAccount()
    {
        var vm = new TestAppList();
        vm.Replace(
            Item("one", "Weather", 100, account: "owner@example.com"),
            Item("two", "Maps", 100, account: "other@example.com"));

        vm.SearchText = "OWNER@example";

        Assert.Equal("Weather", Assert.Single(vm.Visible).Name);
    }

    [Fact]
    public void SortingDoesNotChangeFilteredSelectAllScope()
    {
        var vm = new TestAppList();
        vm.Replace(Item("a", "Alpha", 100), Item("b", "Beta", 500), Item("c", "Alphabet", 900));
        vm.SearchText = "Alpha";
        vm.SortMode = AppSortMode.SizeDescending;

        vm.ToggleSelectAllCommand.Execute(null);

        Assert.True(vm.Visible.All(a => a.IsSelected));
        Assert.Equal(2, vm.SelectedCount);
        Assert.False(vm.AllItems.Single(a => a.App.BundleId == "b").IsSelected);
    }

    private static AppItemViewModel Item(string id, string name, long size, string? account = null, bool selectable = true) =>
        new(new InstalledApp(id, name, "1.0", "Developer", 42, null, account, null, size))
        {
            Account = account,
            IsSelectable = selectable,
            SectionHeader = selectable ? "" : "Unavailable",
        };

    private sealed class TestAppList : AppListViewModel
    {
        public override string LoadingText => "Loading";

        public override string EmptyText => "Empty";

        public IReadOnlyList<AppItemViewModel> AllItems => All;

        public void Replace(params AppItemViewModel[] items) => SetItems(items.ToList());
    }
}
