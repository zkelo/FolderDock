namespace FolderDock.Core;

/// Owns the startup preference policy independently of the operating system.
public sealed class LoginStartupService
{
    private readonly ILoginStartupStore _store;

    public LoginStartupService(ILoginStartupStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public LoginStartupResult GetStatus() =>
        Execute(() => _store.ReadRegistration().IsRegistered);

    public LoginStartupResult EnsureConfigured() => Execute(ConfigureStartup);

    public LoginStartupResult SetEnabled(bool enabled) => Execute(() =>
    {
        if (enabled)
            _store.Enable();
        else
            _store.Disable();
        return enabled;
    });

    private bool ConfigureStartup()
    {
        var registration = _store.ReadRegistration();
        if (!registration.HasUserPreference ||
            (registration.IsRegistered && !registration.UsesCurrentExecutable))
        {
            _store.Enable();
            return true;
        }

        // An absent registration after a saved preference represents an
        // explicit disable or an external removal. Neither is undone here.
        return registration.IsRegistered;
    }

    private static LoginStartupResult Execute(Func<bool> operation)
    {
        try
        {
            return new LoginStartupResult.Success(operation());
        }
        catch (LoginStartupException exception)
        {
            return new LoginStartupResult.Failure(exception.Error, exception.Message);
        }
    }
}
