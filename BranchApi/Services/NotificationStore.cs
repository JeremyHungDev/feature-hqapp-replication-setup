using System.Collections.Concurrent;

namespace BranchApi.Services;

public class NotificationStore
{
    private readonly ConcurrentQueue<NotificationMessage> _messages = new();
    private const int MaxMessages = 50;

    public void Add(NotificationMessage message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > MaxMessages)
            _messages.TryDequeue(out _);
    }

    public IReadOnlyList<NotificationMessage> GetRecent() => _messages.ToArray();
}
