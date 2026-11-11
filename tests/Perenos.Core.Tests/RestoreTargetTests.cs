using Perenos.Services;
using Perenos.ViewModels;

namespace Perenos.Core.Tests;

[Collection("Environment")]
public sealed class RestoreTargetTests
{
    [Fact]
    public async Task SavedFlowDoesNotReuseTheRestoreTarget()
    {
        var demo = Environment.GetEnvironmentVariable("PERENOS_DEMO");
        var saved = Environment.GetEnvironmentVariable("PERENOS_DEMO_SAVED");
        Environment.SetEnvironmentVariable("PERENOS_DEMO", "1");
        Environment.SetEnvironmentVariable("PERENOS_DEMO_SAVED", "1");
        var services = AppServices.Create();
        try
        {
            var vm = new MainViewModel(services, new ImmediateDispatcher());
            vm.RestoreAppsCommand.Execute(null);
            var phone = new DeviceItemViewModel("source", _ => Task.CompletedTask) { Status = DeviceStatus.Ready };
            vm.Source = phone;
            vm.Target = phone;
            await vm.Library.RefreshAsync();
            Assert.True(vm.CanInstallSaved);

            vm.InstallSavedAppsCommand.Execute(null);

            Assert.Null(vm.Target);
            Assert.Equal(WizardStep.Target, vm.Step);
        }
        finally
        {
            services.Dispose();
            Environment.SetEnvironmentVariable("PERENOS_DEMO", demo);
            Environment.SetEnvironmentVariable("PERENOS_DEMO_SAVED", saved);
            if (services.Paths.DataDirectory.StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
                await DeleteDemoDirectoryAsync(services.Paths.DataDirectory);
        }
    }

    private static async Task DeleteDemoDirectoryAsync(string directory)
    {
        // Parallel tests share AppLog, so Windows can briefly keep the daily log open.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(50);
            }
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool TryEnqueue(Action action)
        {
            action();
            return true;
        }
    }
}
