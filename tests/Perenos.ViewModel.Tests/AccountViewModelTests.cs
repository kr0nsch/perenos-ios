using Perenos.Core;
using Perenos.Core.Ipatool;
using Perenos.Services;
using Perenos.ViewModels;

namespace Perenos.ViewModel.Tests;

public class AccountViewModelTests
{
    [Fact]
    public async Task FailedSignOutKeepsTheAccountSignedInAndShowsAnActionableError()
    {
        var store = new FakeStore
        {
            SignOutBehavior = () => Task.FromException(new IpatoolException(IpatoolErrorKind.Network, "Network unavailable")),
        };
        var account = await SignedInAsync(store);
        var changes = new List<string?>();
        account.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        await account.SignOutCommand.ExecuteAsync(null);

        Assert.True(account.IsSignedIn);
        Assert.Equal("owner@example.com", account.Email);
        Assert.Equal("Owner", account.Name);
        Assert.NotNull(store.Account);
        Assert.Equal(Loc.Get("Account_Error_SignOut"), account.Error);
        Assert.True(account.HasSignOutError);
        Assert.Contains(nameof(AccountViewModel.HasSignOutError), changes);
        Assert.False(account.IsBusy);
        Assert.True(account.SignOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task SuccessfulSignOutClearsTheAccountAndSensitiveForm()
    {
        var store = new FakeStore();
        var account = await SignedInAsync(store);
        account.InputEmail = "stale@example.com";
        account.InputPassword = "old password";
        account.InputCode = "123456";
        account.IsCodeRequired = true;
        account.Error = "Previous error";

        await account.SignOutCommand.ExecuteAsync(null);

        Assert.True(account.IsSignedOut);
        Assert.Null(store.Account);
        Assert.Empty(account.Email);
        Assert.Empty(account.Name);
        Assert.Empty(account.InputEmail);
        Assert.Empty(account.InputPassword);
        Assert.Empty(account.InputCode);
        Assert.False(account.IsCodeRequired);
        Assert.Null(account.Error);
        Assert.False(account.HasSignOutError);
        Assert.False(account.IsBusy);
        Assert.False(account.SignOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedSignOutCanBeRetriedSuccessfully()
    {
        var attempts = 0;
        var store = new FakeStore
        {
            SignOutBehavior = () => ++attempts == 1
                ? Task.FromException(new IOException("First attempt failed"))
                : Task.CompletedTask,
        };
        var account = await SignedInAsync(store);

        await account.SignOutCommand.ExecuteAsync(null);
        Assert.True(account.HasSignOutError);
        Assert.True(account.SignOutCommand.CanExecute(null));

        await account.SignOutCommand.ExecuteAsync(null);

        Assert.Equal(2, store.SignOutCalls);
        Assert.True(account.IsSignedOut);
        Assert.Null(store.Account);
        Assert.Null(account.Error);
        Assert.False(account.HasSignOutError);
    }

    [Fact]
    public async Task SigningOutPreventsAnotherSignOutOrSignInUntilTheRequestCompletes()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FakeStore { SignOutBehavior = () => completion.Task };
        var account = await SignedInAsync(store);
        account.InputEmail = "another@example.com";
        account.InputPassword = "another password";
        var signOutChanges = 0;
        account.SignOutCommand.CanExecuteChanged += (_, _) => signOutChanges++;

        var signOut = account.SignOutCommand.ExecuteAsync(null);
        try
        {
            Assert.True(account.IsBusy);
            Assert.True(account.IsSignedIn);
            Assert.False(account.SignOutCommand.CanExecute(null));
            Assert.False(account.SignInCommand.CanExecute(null));
            Assert.True(signOutChanges > 0);

            // ExecuteAsync can be called directly even when CanExecute is false.
            await account.SignOutCommand.ExecuteAsync(null);
            await account.SignInCommand.ExecuteAsync(null);

            Assert.Equal(1, store.SignOutCalls);
            Assert.Equal(0, store.LoginCalls);
            Assert.Equal("owner@example.com", account.Email);
        }
        finally
        {
            completion.TrySetResult();
            await signOut;
        }

        Assert.True(account.IsSignedOut);
        Assert.False(account.IsBusy);
    }

    [Fact]
    public async Task SigningInPreventsSignOutUntilSignInCompletes()
    {
        var completion = new TaskCompletionSource<LoginResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FakeStore { Account = null, LoginBehavior = () => completion.Task };
        var account = new AccountViewModel(store);
        await account.RefreshAsync();
        account.InputEmail = "owner@example.com";
        account.InputPassword = "password";
        Assert.True(account.SignInCommand.CanExecute(null));

        var signIn = account.SignInCommand.ExecuteAsync(null);
        try
        {
            Assert.True(account.IsBusy);
            Assert.False(account.SignOutCommand.CanExecute(null));
            await account.SignOutCommand.ExecuteAsync(null);
            Assert.Equal(0, store.SignOutCalls);
        }
        finally
        {
            completion.TrySetResult(new LoginResult(LoginStatus.SignedIn, new IpatoolAccount("owner@example.com", "Owner")));
            await signIn;
        }

        Assert.True(account.IsSignedIn);
        Assert.Equal("owner@example.com", account.Email);
        Assert.False(account.IsBusy);
        Assert.True(account.SignOutCommand.CanExecute(null));
    }

    [Fact]
    public async Task SignedOutAccountCannotStartARevokeRequest()
    {
        var store = new FakeStore { Account = null };
        var account = new AccountViewModel(store);
        await account.RefreshAsync();

        Assert.False(account.SignOutCommand.CanExecute(null));
        await account.SignOutCommand.ExecuteAsync(null);

        Assert.Equal(0, store.SignOutCalls);
        Assert.True(account.IsSignedOut);
        Assert.Null(account.Error);
    }

    [Fact]
    public async Task UnexpectedSignInErrorShowsGenericMessageAndAllowsASuccessfulRetry()
    {
        var attempts = 0;
        var store = new FakeStore
        {
            Account = null,
            LoginBehavior = () => ++attempts == 1
                ? Task.FromException<LoginResult>(new IOException("temporary disk failure"))
                : Task.FromResult(new LoginResult(LoginStatus.SignedIn, new IpatoolAccount("owner@example.com", "Owner"))),
        };
        var account = new AccountViewModel(store);
        await account.RefreshAsync();
        account.InputEmail = "owner@example.com";
        account.InputPassword = "password";

        await account.SignInCommand.ExecuteAsync(null);

        Assert.True(account.IsSignedOut);
        Assert.Equal(Loc.Get("Account_Error_Generic"), account.Error);
        Assert.DoesNotContain("temporary disk failure", account.Error);
        Assert.False(account.IsBusy);
        Assert.True(account.SignInCommand.CanExecute(null));

        await account.SignInCommand.ExecuteAsync(null);

        Assert.Equal(2, store.LoginCalls);
        Assert.True(account.IsSignedIn);
        Assert.Equal("owner@example.com", account.Email);
        Assert.Null(account.Error);
        Assert.False(account.IsBusy);
    }

    private static async Task<AccountViewModel> SignedInAsync(FakeStore store)
    {
        var account = new AccountViewModel(store);
        await account.RefreshAsync();
        Assert.True(account.IsSignedIn);
        return account;
    }

    private sealed class FakeStore : IAppStoreClient
    {
        public bool IsAvailable => true;

        public IpatoolAccount? Account { get; set; } = new("owner@example.com", "Owner");

        public Func<Task>? SignOutBehavior { get; init; }

        public Func<Task<LoginResult>>? LoginBehavior { get; init; }

        public int SignOutCalls { get; private set; }

        public int LoginCalls { get; private set; }

        public Task<IpatoolAccount?> GetAccountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Account);

        public Task<IReadOnlyList<OwnedApp>> GetOwnedAppsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<LoginResult> LoginAsync(string email, string password, string? authCode, CancellationToken cancellationToken = default)
        {
            LoginCalls++;
            var result = LoginBehavior is null
                ? new LoginResult(LoginStatus.SignedIn, new IpatoolAccount(email, "Owner"))
                : await LoginBehavior();
            if (result.Status == LoginStatus.SignedIn)
                Account = result.Account;
            return result;
        }

        public async Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            SignOutCalls++;
            if (SignOutBehavior is not null)
                await SignOutBehavior();
            Account = null;
        }

        public Task<string> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
