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
                Directory.Delete(services.Paths.DataDirectory, recursive: true);
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
