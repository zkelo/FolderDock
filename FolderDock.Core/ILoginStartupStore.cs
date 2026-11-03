namespace FolderDock.Core;

/// Persists the current user's login-startup choice. Implementations translate
/// expected platform failures to LoginStartupException.
public interface ILoginStartupStore
{
    LoginStartupRegistration ReadRegistration();
    void Enable();
    void Disable();
}
