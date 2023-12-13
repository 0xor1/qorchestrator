using Common.Shared;

namespace QOrchestrator;

public class Orchestrator<T>
    where T : class, IComparable
{
    private readonly IQueue<T>[] _inQs;
    private readonly IQueue<T> _outQ;
    private readonly QAndItem<T>?[] _buf;
    private readonly int _outCount;

    public Orchestrator(IQueue<T>[] inQs, IQueue<T> outQ, int bufSize, int outCount)
    {
        if (outCount < 1)
        {
            throw new InvalidOperationException("outCount must be a positive integer");
        }
        if (bufSize < outCount)
        {
            throw new InvalidOperationException("bufSize must be greater or equal to outCount");
        }

        _inQs = inQs;
        _outQ = outQ;
        _buf = new QAndItem<T>?[bufSize];
        _outCount = outCount;
    }

    public void Run()
    {
        SortInQs();
        WriteToOutQ();
    }

    private void SortInQs()
    {
        foreach (var q in _inQs)
        {
            if (q.Count <= _buf.Length)
            {
                BufferedSort(q);
            }
            else
            {
                InPlaceSort(q);
            }
        }
    }

    private void BufferedSort(IQueue<T> q)
    {
        // ensure buffer is clear
        Array.Clear(_buf);

        // dequeue everything
        var total = q.Count;
        for (var i = 0; i < total; i++)
        {
            _buf[i] = new(q, q.Dequeue().NotNull());
        }

        // sort buffer ascending
        Array.Sort(_buf);

        // enqueue everything largest first
        for (var i = 1; i <= total; i++)
        {
            q.Enqueue(_buf[^i].NotNull().Item);
        }
    }

    private static void InPlaceSort(IQueue<T> q)
    {
        for (var i = 1; i <= q.Count; i++)
        {
            var idx = GetIndexOfMax(q, q.Count - i);
            KickToBack(q, idx);
        }
    }

    private static int GetIndexOfMax(IQueue<T> q, int limitIdx)
    {
        var total = q.Count;
        var max = q.Dequeue().NotNull();
        q.Enqueue(max);
        var idx = 0;
        for (var i = 1; i < total; i++)
        {
            var item = q.Dequeue().NotNull();
            if (i <= limitIdx && item.CompareTo(max) > 0)
            {
                max = item;
                idx = i;
            }
            q.Enqueue(item);
        }
        return idx;
    }

    private static void KickToBack(IQueue<T> q, int idx)
    {
        var total = q.Count;
        T? max = null;
        for (var i = 0; i < total; i++)
        {
            var item = q.Dequeue();
            if (i != idx)
            {
                q.Enqueue(item.NotNull());
            }
            else
            {
                max = item;
            }
        }

        q.Enqueue(max.NotNull());
    }

    private void WriteToOutQ()
    {
        // ensure buffer is clear
        Array.Clear(_buf);
        var bufCount = 0;
        foreach (var q in _inQs)
        {
            var item = q.Dequeue();
            while (item != null)
            {
                if (bufCount < _outCount)
                {
                    // let the buffer fill up to outCount
                    // always 0 because nulls get sorted first
                    _buf[0] = new(q, item);
                    bufCount++;
                    Array.Sort(_buf);
                }
                else if (_buf[^bufCount].NotNull().Item.CompareTo(item) < 1)
                {
                    // if the current item is greater than the existing smallest
                    // item, replace it and sort the buffer, and place the old item back
                    // on its original queue so as to not lose it.
                    var old = _buf[^bufCount].NotNull();
                    old.Q.Enqueue(old.Item);
                    _buf[^bufCount] = new(q, item);
                    Array.Sort(_buf);
                }
                else
                {
                    // the current item is smaller than what is already in the buffer so
                    // break out from processing this pre sorted q
                    break;
                }

                item = q.Dequeue();
            }
        }

        // write buf to outQ
        for (var i = 1; i <= bufCount; i++)
            _outQ.Enqueue(_buf[^i].NotNull().Item);
    }

    // QAndItem maintains a record of an item and the
    // queue it originally came from, so the item can be put back
    // on its original queue if it is no longer in the selected items
    private record QAndItem<T>(IQueue<T> Q, T Item) : IComparable
        where T : IComparable
    {
        public int CompareTo(object? obj) => Item.CompareTo((obj as QAndItem<T>).Item);
    }
}
