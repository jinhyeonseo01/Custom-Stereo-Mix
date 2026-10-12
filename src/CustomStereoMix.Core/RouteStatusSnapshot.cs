namespace CustomStereoMix.Core;

public sealed record RouteStatusSnapshot(RouteState State, string Message)
{
    public static RouteStatusSnapshot Ready { get; } = new(RouteState.Stopped, "Ready");
}
