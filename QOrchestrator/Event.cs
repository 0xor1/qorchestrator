namespace QOrchestrator;

public class Event<T> : IComparable
    where T : IComparable
{
    public T Value { get; }

    public Event(T value)
    {
        Value = value;
    }

    public int CompareTo(object? obj) => Value.CompareTo((obj as Event<T>).Value);
}
