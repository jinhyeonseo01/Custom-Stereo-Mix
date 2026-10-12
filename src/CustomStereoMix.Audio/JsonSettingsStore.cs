using System.Text.Json;
using CustomStereoMix.Core;

namespace CustomStereoMix.Audio;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public LocalizedMessage? LoadWarning { get; private set; }

    public JsonSettingsStore(string? path = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _path = path ?? Path.Combine(appData, "CustomStereoMix", "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LoadWarning = null;
            if (!File.Exists(_path)) return new AppSettings();
            try { return await ReadAsync(_path, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                LoadWarning = new(MessageKey.SettingsLoadRecovery);
                if (File.Exists(_path + ".bak"))
                {
                    try
                    {
                        var backup = await ReadAsync(_path + ".bak", cancellationToken).ConfigureAwait(false);
                        LoadWarning = new(MessageKey.SettingsBackupRecovery);
                        return backup;
                    }
                    catch (Exception backupError) when (backupError is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { }
                }
                return new AppSettings();
            }
        }
        finally { _gate.Release(); }
    }

    private static async Task<AppSettings> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Empty settings.");
        Validate(settings);
        return settings;
    }

    private static void Validate(AppSettings settings)
    {
        AudioSettingsValidator.Validate(settings.Engine, settings.Processing, settings.Microphone);
        foreach (var size in new[] { settings.SimpleWindow, settings.AdvancedWindow })
            if (size is null || !double.IsFinite(size.Width) || !double.IsFinite(size.Height) ||
                size.Width is < 320 or > 10000 || size.Height is < 320 or > 10000)
                throw new InvalidDataException("Invalid window size.");
        if (settings.Graph is not null && GraphValidator.GetError(settings.Graph) is { } issue)
            throw new InvalidDataException().WithMessage(issue.Key);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_path))
            {
                // Keep a separate 1.x original: rolling .bak will become a v2 file.
                if (settings.Graph is not null && !File.Exists(_path + ".v1.bak"))
                {
                    try
                    {
                        using var original = JsonDocument.Parse(await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false));
                        if (!original.RootElement.TryGetProperty("graph", out var graph) || graph.ValueKind == JsonValueKind.Null)
                            File.Copy(_path, _path + ".v1.bak", overwrite: false);
                    }
                    catch (JsonException) { /* A corrupt original already follows normal recovery. */ }
                }
                File.Replace(temporary, _path, _path + ".bak");
            }
            else File.Move(temporary, _path);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }
}
