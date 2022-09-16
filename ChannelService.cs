using System.Text;
public static class ChannelService
{
    private static Dictionary<string, List<TaskCompletionSource<(string, byte[])>>> _awaitingQueues = new();
    private static Dictionary<string, Queue<(string, byte[])>> _bufferQueues = new();

    private static List<TaskCompletionSource<(string, byte[])>> GetAwaitingQueue(string channelId)
    {
        List<TaskCompletionSource<(string, byte[])>>? queue;
        lock (_awaitingQueues)
        {
            if (!_awaitingQueues.TryGetValue(channelId, out queue))
            {
                queue = new List<TaskCompletionSource<(string, byte[])>>();
                _awaitingQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    private static Queue<(string, byte[])> GetBufferQueue(string channelId)
    {
        Queue<(string, byte[])>? queue;
        lock (_bufferQueues)
        {
            if (!_bufferQueues.TryGetValue(channelId, out queue))
            {
                queue = new ();
                _bufferQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    public static async Task<(string, byte[])> ReadData(string channelId, CancellationToken cancellationToken)
    {
        var bufferQueue = GetBufferQueue(channelId);
        lock (bufferQueue)
        {
            if (bufferQueue.Count > 0)
            {
                return bufferQueue.Dequeue();
            }
        }

        var source = new TaskCompletionSource<(string, byte[])>();
        var awaitingQueue = GetAwaitingQueue(channelId);
        lock (awaitingQueue)
        {
            awaitingQueue.Add(source);
        }
        cancellationToken.Register(() => {
            source.SetCanceled();
            awaitingQueue.Remove(source);
        });

        try
        {
            return await source.Task;;
        }
        catch(Exception)
        {
            return ("text/plain", Encoding.UTF8.GetBytes("canceled"));
        }
    }

    public static async Task WriteData(string channelId, string contentType, byte[] data)
    {
        TaskCompletionSource<(string, byte[])> source = null;

        var awaitingQueue = GetAwaitingQueue(channelId);
        lock (awaitingQueue)
        {
            if (awaitingQueue.Count > 0)
            {
                source = awaitingQueue[0];
                awaitingQueue.RemoveAt(0);
            }
        }

        if (source != null)
        {
            source.SetResult((contentType, data));
            return;
        }

        var bufferQueue = GetBufferQueue(channelId);
        lock (bufferQueue)
        {
            bufferQueue.Enqueue((contentType, data));
        }
    }
}