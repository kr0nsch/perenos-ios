using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Perenos.Services;

/// <summary>Per-user file lock and loopback activation without Windows App SDK.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly string _lockPath;
    private readonly string _activationPath;
    private readonly FileStream? _lock;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private TcpListener? _listener;
    private Task? _loop;

    public SingleInstance(string directory)
    {
        Directory.CreateDirectory(directory);
        _lockPath = Path.Combine(directory, "ui.lock");
        _activationPath = Path.Combine(directory, "ui.endpoint");
        try
        {
            _lock = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_lockPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            IsFirst = true;
        }
        catch (IOException)
        {
        }
    }

    public bool IsFirst { get; }

    public void Start(Action activate)
    {
        if (_lock is null)
            throw new InvalidOperationException("Another application instance owns the activation lock.");
        if (_listener is not null)
            throw new InvalidOperationException("Activation is already listening.");
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = $"{((IPEndPoint)_listener.LocalEndpoint).Port}{Environment.NewLine}{_token}{Environment.NewLine}";
        File.WriteAllText(_activationPath, endpoint, new UTF8Encoding(false));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_activationPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _loop = Task.Run(() => ListenAsync(activate));
    }

    public bool ActivateOther()
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var reader = new StreamReader(_activationPath, Encoding.UTF8);
                if (!int.TryParse(reader.ReadLine(), out var port) || reader.ReadLine() is not { } token)
                    continue;
                using var client = new TcpClient();
                if (!client.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(1)))
                    return false;
                using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                writer.WriteLine(token);
                return client.GetStream().ReadByte() == 1;
            }
            catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException or ObjectDisposedException or AggregateException)
            {
            }
            Thread.Sleep(100);
        }
        return false;
    }

    private async Task ListenAsync(Action activate)
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var client = await _listener!.AcceptTcpClientAsync(_cts.Token);
                using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                var received = await reader.ReadLineAsync(_cts.Token);
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(received ?? ""), Encoding.UTF8.GetBytes(_token)))
                    continue;
                activate();
                client.GetStream().WriteByte(1);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                if (_cts.IsCancellationRequested)
                    return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Stop();
        if (_lock is not null)
        {
            try
            {
                File.Delete(_activationPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        _lock?.Dispose();
        _cts.Dispose();
    }
}
