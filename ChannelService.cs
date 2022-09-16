public static class ChannelService
{
    private static Dictionary<string, Queue<TaskCompletionSource<(string, Stream)>>> _awaitingQueues = new();
    private static Dictionary<string, Queue<TaskCompletionSource<(string, Stream)>>> _bufferQueues = new();

    private static Queue<TaskCompletionSource<(string, Stream)>> GetAwaitingQueue(string channelId)
    {
        Queue<TaskCompletionSource<(string, Stream)>>? queue;
        lock (_awaitingQueues)
        {
            if (!_awaitingQueues.TryGetValue(channelId, out queue))
            {
                queue = new Queue<TaskCompletionSource<(string, Stream)>>();
                _awaitingQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    private static Queue<TaskCompletionSource<(string, Stream)>> GetBufferQueue(string channelId)
    {
        Queue<TaskCompletionSource<(string, Stream)>>? queue;
        lock (_bufferQueues)
        {
            if (!_bufferQueues.TryGetValue(channelId, out queue))
            {
                queue = new Queue<TaskCompletionSource<(string, Stream)>>();
                _bufferQueues.Add(channelId, queue);
            }
        }
        return queue;
    }

    public static async Task<(string, Stream)> ReadStream(string channelId)
    {
        TaskCompletionSource<(string, Stream)> source = null;

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

        source = new TaskCompletionSource<(string, Stream)>();
        var awaitingQueue = GetAwaitingQueue(channelId);
        lock (awaitingQueue)
        {
            awaitingQueue.Enqueue(source);
        }
        return await source.Task;
    }

    public static async Task WriteStream(string channelId, string contentType, Stream stream)
    {
        TaskCompletionSource<(string, Stream)> source = null;

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
            source.SetResult((contentType, stream));
            return;
        }

        var bufferQueue = GetBufferQueue(channelId);
        lock (bufferQueue)
        {
            source = new TaskCompletionSource<(string, Stream)>();
            bufferQueue.Enqueue(source);
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        buffer.Seek(0, SeekOrigin.Begin);
        source.SetResult((contentType, buffer));
    }
}