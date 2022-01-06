using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SvgToTvgServer.Server.Worker
{
    public class TvgBackgroundTaskQueue
    {
        private readonly Channel<TvgWorkerEntry> queue;

        public TvgBackgroundTaskQueue(int capacity)
        {
            queue = Channel.CreateBounded<TvgWorkerEntry>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public async ValueTask QueueBackgroundWorkItemAsync(TvgWorkerEntry workItem)
        {
            if (workItem is null)
                throw new ArgumentNullException(nameof(workItem));

            await queue.Writer.WriteAsync(workItem);
        }

        public async ValueTask<TvgWorkerEntry> DequeueAsync(CancellationToken cancellationToken)
        {
            var workItem = await queue.Reader.ReadAsync(cancellationToken);

            return workItem;
        }
    }
}