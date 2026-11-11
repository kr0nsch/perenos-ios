using Perenos.Core;
using Perenos.Core.Transfer;

// Only platform adapters are replaced. Production view models and transfers remain under test.
namespace Perenos.ViewModels
{
    public enum Flow { Now, Save, InstallSaved, Restore }
}

namespace Perenos.Services
{
    public sealed class TestDispatcher : IUiDispatcher
    {
        public Func<Action, bool>? Handler { get; set; }

        public bool TryEnqueue(Action action) => Handler?.Invoke(action) ?? Run(action);

        private static bool Run(Action action)
        {
            action();
            return true;
        }
    }

    public sealed class IconCache
    {
        public Func<string, Task<byte[]?>> Load { get; set; } = _ => Task.FromResult<byte[]?>(null);

        public void Store(string bundleId, byte[] png) { }

        public Task<byte[]?> GetAsync(string bundleId) => Load(bundleId);
    }

    public sealed class AppServices(string downloadDirectory, TransferRunner transfers)
    {
        public string DownloadDirectory { get; } = downloadDirectory;

        public SettingsStore Settings { get; } = new(Path.Combine(downloadDirectory, "settings.json"));

        public TransferRunner Transfers { get; } = transfers;
    }

    public static class KeepAwake
    {
        public static void Enable() { }

        public static void Disable() { }
    }

    public static class Loc
    {
        public static string Get(string key) => key;

        public static string Format(string key, params object?[] values) => $"{key}: {string.Join(", ", values)}";

        public static string Plural(string key, int count, params object?[] values) => Format(key, [count, .. values]);
    }
}
