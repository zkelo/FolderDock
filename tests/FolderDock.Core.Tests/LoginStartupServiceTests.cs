using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class LoginStartupServiceTests
{
    [Fact]
    public void FirstForegroundLaunchEnablesStartupOnce()
    {
        var store = new FakeStartupStore(new(false, false, false));
        var startup = new LoginStartupService(store);

        Assert.Equal(new LoginStartupResult.Success(true), startup.EnsureConfigured());
        Assert.Equal(new LoginStartupResult.Success(true), startup.EnsureConfigured());
        Assert.Equal(1, store.EnableCalls);
    }

    [Fact]
    public void ExplicitDisableSurvivesLaterLaunches()
    {
        var store = new FakeStartupStore(new(true, true, true));
        var startup = new LoginStartupService(store);

        Assert.Equal(new LoginStartupResult.Success(false), startup.SetEnabled(false));
        Assert.Equal(new LoginStartupResult.Success(false), startup.EnsureConfigured());
        Assert.Equal(1, store.DisableCalls);
        Assert.Equal(0, store.EnableCalls);
    }

    [Fact]
    public void ExplicitDisableBeforeInitialConfigurationIsPreserved()
    {
        var store = new FakeStartupStore(new(false, false, false));
        var startup = new LoginStartupService(store);

        startup.SetEnabled(false);
        startup.EnsureConfigured();

        Assert.False(store.Registration.IsRegistered);
        Assert.Equal(0, store.EnableCalls);
    }

    [Fact]
    public void ExternallyRemovedRegistrationIsNotRecreated()
    {
        var store = new FakeStartupStore(new(true, false, false));

        var result = new LoginStartupService(store).EnsureConfigured();

        Assert.Equal(new LoginStartupResult.Success(false), result);
        Assert.Equal(0, store.EnableCalls);
        Assert.Equal(0, store.DisableCalls);
    }

    [Fact]
    public void MovingTheApplicationUpdatesAnExistingRegistration()
    {
        var store = new FakeStartupStore(new(true, true, false));

        var result = new LoginStartupService(store).EnsureConfigured();

        Assert.Equal(new LoginStartupResult.Success(true), result);
        Assert.True(store.Registration.UsesCurrentExecutable);
        Assert.Equal(1, store.EnableCalls);
    }

    [Fact]
    public void ExistingCurrentRegistrationDoesNotWriteSettingsAgain()
    {
        var store = new FakeStartupStore(new(true, true, true));

        new LoginStartupService(store).EnsureConfigured();

        Assert.Equal(0, store.EnableCalls);
        Assert.Equal(0, store.DisableCalls);
    }

    [Fact]
    public void UserCanReenablePreviouslyDisabledStartup()
    {
        var store = new FakeStartupStore(new(true, false, false));

        var result = new LoginStartupService(store).SetEnabled(true);

        Assert.Equal(new LoginStartupResult.Success(true), result);
        Assert.True(store.Registration.IsRegistered);
        Assert.Equal(1, store.EnableCalls);
    }

    [Fact]
    public void ReadFailureDoesNotLookLikeDisabledStartupOrTriggerAWrite()
    {
        var store = new FakeStartupStore(new(true, true, true))
        {
            ReadFailure = new(LoginStartupError.AccessDenied, "read denied")
        };
        var startup = new LoginStartupService(store);

        Assert.Equal(new LoginStartupResult.Failure(LoginStartupError.AccessDenied, "read denied"),
            startup.GetStatus());
        Assert.Equal(new LoginStartupResult.Failure(LoginStartupError.AccessDenied, "read denied"),
            startup.EnsureConfigured());
        Assert.Equal(0, store.EnableCalls);
        Assert.Equal(0, store.DisableCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedChangesReportFailureAndStatusCanBeReadAgain(bool enabled)
    {
        var store = new FakeStartupStore(new(true, !enabled, true))
        {
            WriteFailure = new(LoginStartupError.StorageUnavailable, "write failed")
        };
        var startup = new LoginStartupService(store);

        Assert.Equal(new LoginStartupResult.Failure(LoginStartupError.StorageUnavailable, "write failed"),
            startup.SetEnabled(enabled));
        Assert.Equal(new LoginStartupResult.Success(!enabled), startup.GetStatus());

        store.WriteFailure = null;
        Assert.Equal(new LoginStartupResult.Success(enabled), startup.SetEnabled(enabled));
        Assert.Equal(new LoginStartupResult.Success(enabled), startup.GetStatus());
    }

    [Fact]
    public void FailedDefaultRegistrationCanBeRetried()
    {
        var store = new FakeStartupStore(new(false, false, false))
        {
            WriteFailure = new(LoginStartupError.AccessDenied, "write denied")
        };
        var startup = new LoginStartupService(store);

        Assert.IsType<LoginStartupResult.Failure>(startup.EnsureConfigured());
        store.WriteFailure = null;

        Assert.Equal(new LoginStartupResult.Success(true), startup.EnsureConfigured());
        Assert.True(store.Registration.HasUserPreference);
    }

    private sealed class FakeStartupStore(LoginStartupRegistration registration) : ILoginStartupStore
    {
        public LoginStartupRegistration Registration { get; private set; } = registration;
        public LoginStartupException? ReadFailure { get; set; }
        public LoginStartupException? WriteFailure { get; set; }
        public int EnableCalls { get; private set; }
        public int DisableCalls { get; private set; }

        public LoginStartupRegistration ReadRegistration()
        {
            if (ReadFailure is not null) throw ReadFailure;
            return Registration;
        }

        public void Enable()
        {
            EnableCalls++;
            if (WriteFailure is not null) throw WriteFailure;
            Registration = new(true, true, true);
        }

        public void Disable()
        {
            DisableCalls++;
            if (WriteFailure is not null) throw WriteFailure;
            Registration = new(true, false, false);
        }
    }
}
