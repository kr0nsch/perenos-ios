using System.Globalization;
using System.Resources;

namespace Perenos.Services;

public static class Loc
{
    private static readonly ResourceManager Resources = new("Perenos.Strings.Resources", typeof(Loc).Assembly);
    private static CultureInfo _culture = CultureInfo.CurrentUICulture;

    public static event EventHandler? Changed;

    public static string Language { get; private set; } = "";

    public static string CultureName => _culture.Name;

    public static void ApplyLanguage(string language)
    {
        Language = language;
        _culture = string.IsNullOrEmpty(language)
            ? CultureInfo.InstalledUICulture
            : CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        try
        {
            var value = Resources.GetString(key, _culture);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    public static string Format(string key, params object?[] args) => string.Format(_culture, Get(key), args);

    public static string Plural(string key, long count)
    {
        var form = PluralForm(count);
        var text = Get($"{key}_{form}");
        if (text == $"{key}_{form}")
            text = Get($"{key}_Many");
        return string.Format(_culture, text, count);
    }

    private static string PluralForm(long count)
    {
        if (_culture.TwoLetterISOLanguageName == "ru")
        {
            var n = Math.Abs(count);
            if (n % 10 == 1 && n % 100 != 11)
                return "One";
            if (n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14)
                return "Few";
            return "Many";
        }

        return count == 1 ? "One" : "Many";
    }
}
