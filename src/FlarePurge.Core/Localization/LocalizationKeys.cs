namespace FlarePurge.Core.Localization;

public static class LocalizationKeys
{
    // Swift xcstrings keys use dotted namespaces (error.unauthorized.invalid).
    // Windows .resw does not accept dots in <data name=".."/> — the migrator
    // rewrites them to underscores. Core callers keep passing the dotted form;
    // this normalizer lets the resolver do the dot→underscore swap once.
    public static string Normalize(string key) => key.Replace('.', '_');
}
