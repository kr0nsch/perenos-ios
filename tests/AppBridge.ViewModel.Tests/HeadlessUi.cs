using AppBridge.Core;
using AppBridge.Core.Transfer;
using Microsoft.UI.Xaml.Media;

// Replace only UI/platform services. The view models, generated MVVM code and transfer
// pipeline are the production sources, so their state transitions remain under test.
namespace Microsoft.UI.Dispatching
{
    public sealed class DispatcherQueue
    {
        public Func<Action, bool>? Handler { get; set; }

        public bool TryEnqueue(Action callback) => Handler?.Invoke(callback) ?? Run(callback);

        private static bool Run(Action callback)
        {
            callback();
            return true;
        }
    }
}

namespace Microsoft.UI.Xaml.Media
{
    public class ImageSource;
}

namespace AppBridge.ViewModels
{
    public enum Flow { Now, Save, InstallSaved }
}

namespace AppBridge.Services
{
    public sealed class IconCache
    {
        public Func<string, Task<ImageSource?>> Load { get; set; } = _ => Task.FromResult<ImageSource?>(null);

        public Task<ImageSource?> GetAsync(string bundleId) => Load(bundleId);
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
