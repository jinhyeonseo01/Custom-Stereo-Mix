namespace CustomStereoMix.State;

public interface IUiDispatcher { void Post(Action action); }

public sealed class ContextUiDispatcher : IUiDispatcher
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    public void Post(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context) action();
        else _context.Post(_ => action(), null);
    }
}
