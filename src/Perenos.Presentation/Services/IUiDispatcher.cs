namespace Perenos.Services;

public interface IUiDispatcher
{
    bool TryEnqueue(Action action);
}
