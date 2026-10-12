namespace CustomStereoMix.Core;

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

public sealed class MemoryStartupService : IStartupService
{
    public bool IsEnabled { get; private set; }
    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}
