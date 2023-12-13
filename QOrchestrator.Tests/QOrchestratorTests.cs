using Common.Shared;

namespace QOrchestrator.Tests;

public class QOrchestratorTests
{
    [Fact]
    public void OutCount_Exception()
    {
        var now = DateTime.UtcNow;
        var genVal = (int v) => now.AddDays(v);
        var inQs = GenerateQs<DateTime>(5, 5, genVal);
        var outQ = new QueueWrapper<Event<DateTime>>(new());
        Assert.Throws<InvalidOperationException>(
            () => new Orchestrator<Event<DateTime>>(inQs, outQ, 5, 0)
        );
    }

    [Fact]
    public void BufSize_Exception()
    {
        var now = DateTime.UtcNow;
        var genVal = (int v) => now.AddDays(v);
        var inQs = GenerateQs<DateTime>(5, 5, genVal);
        var outQ = new QueueWrapper<Event<DateTime>>(new());
        Assert.Throws<InvalidOperationException>(
            () => new Orchestrator<Event<DateTime>>(inQs, outQ, 1, 2)
        );
    }

    [Theory]
    [InlineData(5, 5, 5, 3)]
    [InlineData(5, 5, 3, 3)]
    [InlineData(5, 5, 1, 1)]
    [InlineData(5, 2, 5, 5)]
    public void DateTime_Success(int n, int qCount, int bufSize, int outCount)
    {
        var now = DateTime.UtcNow;
        var genVal = (int v) => now.AddDays(v);
        var inQs = GenerateQs<DateTime>(n, qCount, genVal);
        var outQ = new QueueWrapper<Event<DateTime>>(new());
        var sut = new Orchestrator<Event<DateTime>>(inQs, outQ, bufSize, outCount);
        sut.Run();
        AssertOutQ(outQ, n, qCount, outCount, genVal);
    }

    [Theory]
    [InlineData(5, 5, 5, 3)]
    [InlineData(5, 5, 3, 3)]
    [InlineData(5, 5, 1, 1)]
    [InlineData(5, 2, 5, 5)]
    public void Int_Success(int n, int qCount, int bufSize, int outCount)
    {
        var genVal = (int v) => v;
        var inQs = GenerateQs<int>(n, qCount, genVal);
        var outQ = new QueueWrapper<Event<int>>(new());
        var sut = new Orchestrator<Event<int>>(inQs, outQ, bufSize, outCount);
        sut.Run();
        AssertOutQ(outQ, n, qCount, outCount, genVal);
    }

    [Fact]
    public void DifferentSizedInQs_Success()
    {
        var inQs = GenerateQs<int>(5, 5, v => v);
        // remove an event from each queue, provided its not one of the 3 largest values
        // used for validation
        for (var i = 0; i < inQs.Length; i++)
        {
            var e = inQs[i].Dequeue();
            if (e?.Value is 25 or 24 or 23)
            {
                inQs[i].Enqueue(e);
            }

            break;
        }
        var outQ = new QueueWrapper<Event<int>>(new());
        var sut = new Orchestrator<Event<int>>(inQs, outQ, 3, 3);
        sut.Run();
        AssertOutQ(outQ, 5, 5, 3, v => v);
    }

    private QueueWrapper<Event<T>>[] GenerateQs<T>(int n, int qCount, Func<int, T> genVal)
        where T : IComparable
    {
        var rnd = new Random();
        var vals = Enumerable.Range(1, n * qCount).ToList();
        var qs = new QueueWrapper<Event<T>>[n];
        for (var i = 0; i < n; i++)
        {
            var q = new Queue<Event<T>>();
            for (var j = 0; j < qCount; j++)
            {
                var idx = rnd.Next(vals.Count);
                var val = vals[idx];
                vals.RemoveAt(idx);
                q.Enqueue(new Event<T>(genVal(val)));
            }
            qs[i] = new QueueWrapper<Event<T>>(q);
        }

        return qs;
    }

    private void AssertOutQ<T>(
        IQueue<Event<T>> outQ,
        int n,
        int qCount,
        int outCount,
        Func<int, T> genVal
    )
        where T : IComparable
    {
        Enumerable
            .Range(n * qCount - outCount + 1, outCount)
            .Reverse()
            .ForEach(v => Assert.Equal(genVal(v), outQ.Dequeue().NotNull().Value));
    }
}
