using System;

namespace FlarePurge.Core.Localization;

public static class Localizer
{
    private static Func<string, string> _resolver = static key => key;

    public static void SetResolver(Func<string, string> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public static void ResetResolver()
        => _resolver = static key => key;

    public static string Get(string key) => _resolver(key);
}
