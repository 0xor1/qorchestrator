namespace QOrchestrator;

public class QueueWrapper<T> : IQueue<T>
{
    private readonly Queue<T> _internal;

    public QueueWrapper(Queue<T> @internal)
    {
        _internal = @internal;
    }

    public int Count => _internal.Count;

    public void Enqueue(T item)
    {
        _internal.Enqueue(item);
    }

    public T? Dequeue()
    {
        _internal.TryDequeue(out var i);
        return i;
    }
}
