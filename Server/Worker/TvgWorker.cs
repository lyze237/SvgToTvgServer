using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SvgToTvgServer.Server.Worker
{
    public class TvgWorker : BackgroundService
    {
        private readonly TvgBackgroundTaskQueue taskQueue;
        private readonly ILogger<TvgWorker> logger;

        public TvgWorker(ILogger<TvgWorker> logger, TvgBackgroundTaskQueue taskQueue)
        {
            this.logger = logger;
            this.taskQueue = taskQueue;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation($"Background worker is running.");

            return ProcessTaskQueueAsync(stoppingToken);
        }

        private async Task ProcessTaskQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var workItem = await taskQueue.DequeueAsync(cancellationToken);
                
                try
                {
                    workItem.Result.SetResult(await workItem.Task);
                }
                catch (OperationCanceledException)
                {
                    workItem.Result.SetCanceled(cancellationToken);
                }
                catch (Exception e)
                {
                    workItem.Result.SetException(e);
                    logger.LogError(e, "Error occurred executing task work item.");
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Background worker is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}