using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SvgToTvgServer.Server.Worker
{
    public class TvgWorkerEntry
    {
        public ValueTask<IActionResult> Task { get; set; }
        public TaskCompletionSource<IActionResult> Result { get; }

        public TvgWorkerEntry(ValueTask<IActionResult> task)
        {
            Task = task;
            Result = new TaskCompletionSource<IActionResult>();
        }
    }
}