# Localization

The app currently ships `ko-KR`, `en-US`, `ja-JP` and `zh-CN` (Simplified Chinese).

## Shared message keys

`locales/*.json` contains language metadata (`code`, native `name`, display `order`) and a `messages` dictionary. Keys such as `SettingsLanguage`, `UpdateAvailable` and `InvalidRange` are stable identifiers, independent of the displayed wording. Change a translation value without changing its key.

- React: `t("UpdateAvailable", version)`. `MessageKey` is inferred from the English JSON resource, so invalid keys fail TypeScript compilation.
- C#: `localizer.Text(MessageKey.UpdateAvailable, version)`. `MessageKey` is an enum checked against the resource keys by tests.
- Parameters use `{0}`, `{1}`, etc. Preserve the parameter indexes in every translation; sentence order may vary.
- Status, recovery notices and update results retain `LocalizedMessage(Key, Arguments)`. They render against the current locale, including results which arrived before a language switch.
- Application exceptions retain their original exception type and carry key/arguments in metadata through `WithMessage`. OS and driver details remain as supplied by Windows.
- No source-text matching or replacement is used to identify messages.

The native assembly embeds all locale files. Vite includes them using `import.meta.glob`; production apps do not fetch translation files from the network.

## Language selection

The installer always offers its four languages and writes `initial-language.json` alongside the app before the post-install launch. It never edits the user's settings file.

Startup priority:

1. A supported language saved in `%APPDATA%\CustomStereoMix\settings.json`.
2. A supported language in the installation seed.
3. A supported Windows UI language, including matching language families.
4. English (`en-US`).

Settings → App behavior → Display language applies immediately, updates native tray labels and persists through the existing atomic settings store. It does not stop or reopen WASAPI sessions. An unknown saved language falls back without discarding valid audio settings. Reset restores the installation default along with the other settings. Reinstalling preserves an explicit user preference.

Folder distributions without an installer seed use the Windows language, then English.

## Add a language

1. Copy `locales/en-US.json` to the new BCP 47 language code. Set its code, native name and order; translate every message while preserving placeholders.
2. The language is discovered automatically by both the native catalog and React's selector. No four-language switch statement is required.
3. Add its `[Languages]` entry and corresponding `<name>.AppLanguage` and error messages in `installer/CustomStereoMix.iss`. Supply a compatible Inno Setup translation if it is not bundled with the compiler.
4. Add a translated README and update the language links.
5. Run the tests and build the installer. Test the guide, settings, update/reset dialogs and tray at the minimum window size.

For a new message, add its stable identifier to `MessageKey.cs` and all locale dictionaries. Tests check required languages, enum/resource key agreement and placeholder consistency. Missing translated keys fall back to English at runtime.

```powershell
dotnet run --project tests\CustomStereoMix.Tests\CustomStereoMix.Tests.csproj -c Release
npm --prefix ui run build
.\build_installer.ps1
```

Keep operating-system device names, product names (`CABLE Input`, `CABLE Output`) and units intact. Translate complete sentences rather than assembling word fragments with language-dependent grammar.
