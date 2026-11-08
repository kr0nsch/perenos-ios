namespace AppBridge.Core.Ipatool;

public sealed record IpatoolAccount(string Email, string Name);

public sealed record OwnedApp(long AppId, string BundleId, string Name, string Version, DateTimeOffset? PurchaseDate, string? LatestExternalVersionId = null);

internal sealed record OwnedAppsPage(int Page, int TotalCount, IReadOnlyList<OwnedApp> Apps);

public enum LoginStatus
{
    SignedIn,
    CodeRequired,
    Failed,
}

public sealed record LoginResult(LoginStatus Status, IpatoolAccount? Account = null, string? Error = null);

public enum IpatoolErrorKind
{
    Unknown,
    ToolMissing,
    NotSignedIn,
    SessionExpired,
    HistoryUnavailable,
    LicenseRequired,
    PaidAppNotSupported,
    SubscriptionRequired,
    TemporarilyUnavailable,
    AppNotFound,
    Network,
}

public sealed class IpatoolException(IpatoolErrorKind kind, string message) : Exception(message)
{
    public IpatoolErrorKind Kind { get; } = kind;
}

public sealed record DownloadRequest(
    long AppId,
    string BundleId,
    string OutputPath,
    string? ExternalVersionId = null,
    bool AcquireLicense = false);

public enum DownloadPhase
{
    Downloading,
    Finalizing,
}

/// <param name="Fraction">0..1 when the total size is known, otherwise null.</param>
public readonly record struct DownloadProgress(DownloadPhase Phase, double? Fraction, long BytesReceived);
