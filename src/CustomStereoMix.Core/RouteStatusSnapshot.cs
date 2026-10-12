namespace CustomStereoMix.Core;

public sealed record RouteStatusSnapshot(RouteState State, LocalizedMessage Message)
{
    public static RouteStatusSnapshot Ready { get; } = new(RouteState.Stopped, new(MessageKey.Ready));
}
