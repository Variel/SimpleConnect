public static class ChannelService
{
    private static Dictionary<string, Queue<TaskCompletionSource<(string, byte[])>>> _awaitingQueues = new();
    private static Dictionary<string, Queue<TaskCompletionSource<(string, byte[])>>> _bufferQueues = new();

    private static Queue<TaskCompletionSource<(string, byte[])>> GetAwaitingQueue(string channelId)
    {
        Queue<TaskCompletionSource<(string, byte[])>>? queue;
        lock (_awaitingQueues)
        {
            if (!_awaitingQueues.TryGetValue(channelId, out queue))
            {
                queue = new Queue<TaskCompletionSource<(string, byte[])>>();
                _awaitingQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    private static Queue<TaskCompletionSource<(string, byte[])>> GetBufferQueue(string channelId)
    {
        Queue<TaskCompletionSource<(string, byte[])>>? queue;
        lock (_bufferQueues)
        {
            if (!_bufferQueues.TryGetValue(channelId, out queue))
            {
                queue = new Queue<TaskCompletionSource<(string, byte[])>>();
                _bufferQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    public static async Task<(string, byte[])> ReadData(string channelId)
    {
        TaskCompletionSource<(string, byte[])> source = null;

        var bufferQueue = GetBufferQueue(channelId);
        lock (bufferQueue)
        {
            if (bufferQueue.Count > 0)
            {
                source = bufferQueue.Dequeue();
            }
        }

        if (source != null)
        {
            return await source.Task;
        }

        source = new TaskCompletionSource<(string, byte[])>();
        var awaitingQueue = GetAwaitingQueue(channelId);
        lock (awaitingQueue)
        {
            awaitingQueue.Enqueue(source);
        }
        return await source.Task;
    }

    public static async Task WriteData(string channelId, string contentType, byte[] data)
    {
        TaskCompletionSource<(string, byte[])> source = null;

        var awaitingQueue = GetAwaitingQueue(channelId);
        lock (awaitingQueue)
        {
            if (awaitingQueue.Count > 0)
            {
                source = awaitingQueue.Dequeue();
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
            source = new ();
            bufferQueue.Enqueue(source);
        }

        source.SetResult((contentType, data));
    }
}