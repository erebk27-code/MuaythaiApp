namespace MuaythaiApp.Security;

public static class AppSession
{
    public static AppRole? CurrentRole { get; private set; }

    public static bool IsAdmin => CurrentRole == AppRole.Admin;

    public static bool IsLoggedIn => CurrentRole.HasValue;

    public static void Start(AppRole role)
    {
        CurrentRole = role;
    }

    public static void End()
    {
        CurrentRole = null;
    }
}
