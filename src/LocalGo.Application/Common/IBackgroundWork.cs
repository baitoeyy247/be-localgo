namespace LocalGo.Application.Common;

/// <summary>Runs work after the HTTP response, in a new DI scope.</summary>
public interface IBackgroundWork
{
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> work);
}
