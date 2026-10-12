using System.Text.Json;
using System.Text.RegularExpressions;
using CustomStereoMix.Audio;
using CustomStereoMix.Core;
using CustomStereoMix.State;

static class LocalizationTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Resources()
    {
        var expected = new[] { "ko-KR", "en-US", "ja-JP", "zh-CN" };
        Check(expected.All(LanguageCatalog.IsSupported), "A required language is missing.");
        var keys = LanguageCatalog.Languages[0].Messages.Keys.Order().ToArray();
        Check(keys.SequenceEqual(Enum.GetNames<MessageKey>().Order()), "C# identifiers and shared resource keys differ.");
        foreach (var language in LanguageCatalog.Languages)
        {
            Check(language.Messages.Keys.Order().SequenceEqual(keys), $"Missing keys in {language.Code}.");
            foreach (var (source, translated) in language.Messages)
            {
                Check(source == "Empty" || !string.IsNullOrWhiteSpace(translated), $"Empty translation: {source}");
                string[] Arguments(string text) => Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
                var reference = LanguageCatalog.Languages.Single(l => l.Code == "en-US").Messages[source];
                Check(Arguments(reference).SequenceEqual(Arguments(translated)), $"Placeholder mismatch: {language.Code}: {source}");
            }
            var localizer = new Localizer(language.Code);
            Check(localizer.Render(new(MessageKey.UpdateAvailable, "v1.10.0")).Contains("v1.10.0"), "Version argument was lost.");
            Check(localizer.Text(MessageKey.InvalidRange, 10, 250).Contains("10") && localizer.Text(MessageKey.InvalidRange, 10, 250).Contains("250"), "Range arguments failed.");
            var nested = new IOException().WithMessage(MessageKey.DeviceDiscoveryFailed);
            Check(localizer.Render(new(MessageKey.SettingsSaveFailed, nested)).Contains(localizer.Text(MessageKey.DeviceDiscoveryFailed)), "Nested keyed error failed.");
            Check(localizer.Describe(new IOException("USB device error 123")) == "USB device error 123", "OS error details were lost.");
        }
    }

    public static void InstallerSeed()
    {
        var path = Path.Combine(Path.GetTempPath(), "StereoMixLocales-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Check(LanguageCatalog.InitialLanguage(path, "de-DE") == "en-US", "Unsupported OS language did not fall back.");
            foreach (var language in LanguageCatalog.Languages)
            {
                File.WriteAllText(Path.Combine(path, "initial-language.json"), JsonSerializer.Serialize(new { language = language.Code }));
                Check(LanguageCatalog.InitialLanguage(path, "ko-KR") == language.Code, "Installer language did not take priority.");
            }
            File.WriteAllText(Path.Combine(path, "initial-language.json"), "{broken}");
            Check(LanguageCatalog.InitialLanguage(path, "ja-JP") == "ja-JP", "Malformed seed blocked startup.");
            Check(LanguageCatalog.Resolve("unknown", "zh-Hans") == "zh-CN", "Language family resolution failed.");
        }
        finally { Directory.Delete(path, true); }
    }

    public static void PersistenceAndRouting() => Run().GetAwaiter().GetResult();
    private static async Task Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "StereoMixLocaleSettings-" + Guid.NewGuid().ToString("N"), "settings.json");
        var store = new JsonSettingsStore(path);
        var router = new OrderedRouter();
        var model = new MainWindowViewModel(new FakeDevices(), router, store, initialLanguage: "ja-JP");
        await model.InitializeAsync();
        Check(model.Language == "ja-JP", "First launch ignored installer language.");
        var bridge = new UiBridge(model);
        async Task<JsonElement> Patch(object payload) => JsonSerializer.SerializeToElement(await bridge.HandleAsync(
            JsonSerializer.SerializeToElement(new { id = Guid.NewGuid().ToString("N"), command = "patch", payload })));
        await model.StartCommand.ExecuteAsync(null);
        foreach (var code in new[] { "en-US", "zh-CN", "ko-KR", "ja-JP" })
        {
            var response = await Patch(new { language = code });
            Check(response.GetProperty("ok").GetBoolean(), "Language switch was blocked while routing.");
            Check(response.GetProperty("state").GetProperty("statusText").GetString() == new Localizer(code).Text(MessageKey.Routing), "Route state was not retranslated.");
            Check(model.IsRouting && router.Calls.SequenceEqual(new[] { "start" }), "Language switch disrupted audio.");
            var device = bridge.Snapshot().GetProperty("sources")[0];
            Check(device.GetProperty("name").GetString() == "headphone", "Device name changed.");
        }
        Check(!(await Patch(new { gainDb = 7, language = "fr-FR" })).GetProperty("ok").GetBoolean(), "Unknown language accepted.");
        Check(model.GainDb == 0 && model.Language == "ja-JP", "Invalid patch was partially applied.");
        model.Language = "zh-CN";
        await model.ShutdownAsync();
        var saved = await store.LoadAsync();
        Check(saved.Language == "zh-CN", "Language was not saved.");
        var restarted = new MainWindowViewModel(new FakeDevices(), new OrderedRouter(), store, initialLanguage: "en-US");
        await restarted.InitializeAsync();
        Check(restarted.Language == "zh-CN", "Reinstall seed overwrote saved preference.");
        await restarted.ResetSettingsCommand.ExecuteAsync(null);
        Check(restarted.Language == "en-US", "Reset did not restore installation default language.");
        await restarted.ShutdownAsync();
        await store.SaveAsync(new AppSettings { Language = "future-language", Processing = new ProcessingSettings(GainDb: 5) });
        var future = new MainWindowViewModel(new FakeDevices(), new FakeRouter(), store, initialLanguage: "ja-JP");
        await future.InitializeAsync();
        Check(future.Language == "ja-JP" && future.GainDb == 5, "Unknown saved language discarded valid audio settings.");
        await future.ShutdownAsync();
        // Only the uniquely created test directory is removed.
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }
}
