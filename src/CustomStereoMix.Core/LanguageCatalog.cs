using System.Globalization;
using System.Text.Json;

namespace CustomStereoMix.Core;

public sealed record LanguageDefinition(string Code, string Name, int Order, Dictionary<string, string> Messages);

/// <summary>Shared resources used by both the native host and React.</summary>
public static class LanguageCatalog
{
    public const string Fallback = "en-US";
    public static IReadOnlyList<LanguageDefinition> Languages { get; } = Load();

    private static IReadOnlyList<LanguageDefinition> Load()
    {
        var assembly = typeof(LanguageCatalog).Assembly;
        return assembly.GetManifestResourceNames().Where(n => n.StartsWith("CustomStereoMix.Locales.", StringComparison.Ordinal))
            .Select(n => JsonSerializer.Deserialize<LanguageDefinition>(assembly.GetManifestResourceStream(n)!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .OrderBy(l => l.Order).ToArray();
    }

    public static bool IsSupported(string? code) => Languages.Any(l => l.Code == code);
    public static string Resolve(params string?[] preferences)
    {
        foreach (var preference in preferences)
        {
            if (string.IsNullOrWhiteSpace(preference)) continue;
            var exact = Languages.FirstOrDefault(l => string.Equals(l.Code, preference, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact.Code;
            var primary = preference.Split('-')[0];
            var match = Languages.FirstOrDefault(l => l.Code.Split('-')[0].Equals(primary, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Code;
        }
        return Fallback;
    }

    // Installer seed is separate from user settings, so reinstalling never overwrites
    // audio preferences or an explicitly selected language.
    public static string InitialLanguage(string directory, string? culture = null)
    {
        string? installed = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "initial-language.json")));
            installed = document.RootElement.GetProperty("language").GetString();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        return Resolve(installed, culture ?? CultureInfo.CurrentUICulture.Name);
    }
}

public sealed class Localizer(string language)
{
    private readonly LanguageDefinition _language = LanguageCatalog.Languages.Single(l => l.Code == LanguageCatalog.Resolve(language));
    public string Text(MessageKey key, params object?[] arguments) => Render(new(key, arguments));
    public string Render(LocalizedMessage message)
    {
        var key = message.Key.ToString();
        var template = _language.Messages.GetValueOrDefault(key,
            LanguageCatalog.Languages.Single(l => l.Code == LanguageCatalog.Fallback).Messages.GetValueOrDefault(key, key));
        var arguments = message.Arguments.Select(a => a switch
        {
            LocalizedMessage nested => Render(nested),
            Exception exception => Describe(exception),
            _ => a
        }).ToArray();
        return string.Format(CultureInfo.GetCultureInfo(_language.Code), template, arguments);
    }
    public string Describe(Exception exception) => exception.Data[typeof(LocalizedMessage)] is LocalizedMessage message
        ? Render(message) : exception.Message;
}

/// <summary>Stores identifiers and parameters; rendering always uses the current locale.</summary>
public sealed record LocalizedMessage(MessageKey Key, params object?[] Arguments)
{
    public static LocalizedMessage Empty { get; } = new(MessageKey.Empty);
}

public static class LocalizedErrors
{
    public static T WithMessage<T>(this T exception, MessageKey key, params object?[] arguments) where T : Exception
    {
        exception.Data[typeof(LocalizedMessage)] = new LocalizedMessage(key, arguments);
        return exception;
    }
}
