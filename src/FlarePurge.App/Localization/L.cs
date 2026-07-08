using FlarePurge.Core.Localization;

namespace FlarePurge.App.Localization;

/// <summary>
/// Short helper for x:Bind static-function lookups and code-behind. Resolves via the
/// Localizer installed at startup (<see cref="LocalizationBootstrap"/>). x:Bind callers:
/// <c>{x:Bind local:L.S('key'), Mode=OneTime}</c>. The language override is apply-on-restart
/// so OneTime binding is correct.
/// </summary>
public static class L
{
    public static string S(string key) => Localizer.Get(key);

    public static string Format(string key, object? arg0)
        => string.Format(Localizer.Get(key), arg0);

    public static string Format(string key, object? arg0, object? arg1)
        => string.Format(Localizer.Get(key), arg0, arg1);

    public static string Format(string key, object? arg0, object? arg1, object? arg2)
        => string.Format(Localizer.Get(key), arg0, arg1, arg2);
}
