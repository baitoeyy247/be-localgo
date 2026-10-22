namespace LocalGo.Infrastructure.Notifications;

public interface INotifyStdout
{
    void WriteLine(string line);
}

public sealed class NotifyStdout : INotifyStdout
{
    public void WriteLine(string line) => Console.Out.WriteLine(line);
}
