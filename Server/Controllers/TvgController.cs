using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvgToTvgServer.Server.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using ImageMagick;
using SvgToTvgServer.Server.Extensions;
using SvgToTvgServer.Server.Worker;
using SvgToTvgServer.Shared;

namespace SvgToTvgServer.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TvgController : ControllerBase
    {
        private readonly ILogger<TvgController> logger;
        private readonly TvgConfig config;
        private readonly TvgBackgroundTaskQueue taskQueue;

        public TvgController(ILogger<TvgController> logger, IOptions<TvgConfig> config, TvgBackgroundTaskQueue taskQueue)
        {
            this.logger = logger;
            this.config = config.Value;
            this.taskQueue = taskQueue;
        }

        [HttpPost]
        public async Task<IActionResult> Execute([FromForm] string svg)
        {
            if (string.IsNullOrEmpty(svg))
                return BadRequest("Input svg is empty.");

            var workerEntry = new TvgWorkerEntry(BuildBackgroundWorkerItem(svg));
            await taskQueue.QueueBackgroundWorkItemAsync(workerEntry);

            return await workerEntry.Result.Task;
        }

        private async ValueTask<IActionResult> BuildBackgroundWorkerItem(string svg)
        {
            var optimizedSvg = await OptimizeSvg(svg);
            if (optimizedSvg.ExitCode != 0)
                return BadRequest($"Error optimizing svg file.\n{optimizedSvg.StdErr}");

            var tvgt = await ConvertSvgToTvgt(optimizedSvg.StdOut);
            if (tvgt.ExitCode != 0)
                return BadRequest($"Couldn't convert svg file to tvgt file.\n{tvgt.StdErr}");

            var tvg = await ConvertTvgtToTvg(tvgt.StdOut);
            if (tvg.ExitCode != 0)
                return BadRequest($"Couldn't convert tvgt file to tvg file.\n{tvg.StdErr}");

            var tga = await ConvertTvgToTga(tvg.Bytes);
            if (tga.ExitCode != 0)
                return BadRequest($"Couldn't convert tvg file to tga file.\n{tga.StdErr}");

            var png = await ConvertTgaToPng(tga.Bytes);
            
            return Ok(new TvgResult
            {
                Png = Convert.ToBase64String(png),
                Steps = new List<TvgStep>
                {
                    new()
                    {
                        StepName = "Tvgt File",
                        Extension = "tvgt",
                        FileContent = tvgt.StdOut,
                        IsBase64 = false
                    },
                    new()
                    {
                        StepName = "Tvg File",
                        Extension = "tvg",
                        FileContent = Convert.ToBase64String(tvg.Bytes),
                        IsBase64 = true
                    }
                }
            });
        }

        private async Task<ProcessResult> OptimizeSvg(string svg)
        {
            var info = new ProcessStartInfo(config.Svgo)
            {
                ArgumentList = { "--config", config.SvgoConfig, "-"},
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            logger.LogInformation($"Starting process {info.FileName} {string.Join(" ", info.ArgumentList)}");
            var process = Process.Start(info);

            await process!.StandardInput.WriteAsync(svg);
            process.StandardInput.Close();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr  = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            logger.LogDebugWhenNotEmpty(stdout);
            logger.LogErrorWhenNotEmpty(stderr);
            
            logger.LogInformation($"Exit code {process.ExitCode}");

            return new ProcessResult
            {
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = process.ExitCode
            };
        }

        private async Task<ProcessResult> ConvertSvgToTvgt(string svg)
        {
            var info = new ProcessStartInfo(config.Svg2Tvgt)
            {
                ArgumentList = { "-", "-c" },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            logger.LogInformation($"Starting process {info.FileName} {string.Join(" ", info.ArgumentList)}");
            var process = Process.Start(info);

            await process!.StandardInput.WriteAsync(svg);
            process.StandardInput.Close();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr  = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            logger.LogDebugWhenNotEmpty(stdout);
            logger.LogErrorWhenNotEmpty(stderr);
            
            logger.LogInformation($"Exit code {process.ExitCode}");
            
            return new ProcessResult
            {
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = process.ExitCode
            };
        }
        
        private async Task<ProcessResult> ConvertTvgtToTvg(string tvgt)
        {
            var info = new ProcessStartInfo(config.TvgText)
            {
                ArgumentList = { "-I", "tvgt", "-", "-O", "tvg", "-o", "-"},
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            logger.LogInformation($"Starting process {info.FileName} {string.Join(" ", info.ArgumentList)}");
            var process = Process.Start(info);

            await process!.StandardInput.WriteAsync(tvgt);
            process.StandardInput.Close();

            await using var ms = new MemoryStream();
            var buffer = new byte[1024 * 1024];
            int lastRead;
            do
            {
                lastRead = await process.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                ms.Write(buffer, 0, lastRead);
            } while (lastRead > 0);

            var bytes = ms.ToArray();
            var stderr  = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            logger.LogErrorWhenNotEmpty(stderr);
            logger.LogInformation($"Exit code {process.ExitCode}");
            
            return new ProcessResult
            {
                Bytes = bytes,
                StdErr = stderr,
                ExitCode = process.ExitCode
            };
        }
        
        private async Task<ProcessResult> ConvertTvgToTga(byte[] tvg)
        {
            var file = new FileInfo(Path.GetTempFileName());
            
            var info = new ProcessStartInfo(config.TvgRenderer)
            {
                ArgumentList = { "-", "-o", file.FullName },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            
            logger.LogInformation($"Starting process {info.FileName} {string.Join(" ", info.ArgumentList)}");
            var process = Process.Start(info);

            await process!.StandardInput.BaseStream.WriteAsync(tvg);
            process.StandardInput.Close();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr  = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            logger.LogInformation(file.FullName);
            logger.LogDebugWhenNotEmpty(stdout);
            logger.LogErrorWhenNotEmpty(stderr);
            logger.LogInformation($"Exit code {process.ExitCode}");
            
            var result = new ProcessResult
            {
                StdOut = stdout,
                StdErr = stderr,
                ExitCode = process.ExitCode
            };
            
            if (process.ExitCode == 0)
            {
                result.Bytes = await System.IO.File.ReadAllBytesAsync(file.FullName);
                file.Delete();
                return result;
            }
            
            if (file.Exists)
                file.Delete();

            return result;
        }
        
        private async Task<byte[]> ConvertTgaToPng(byte[] tga)
        {
            using var image = new MagickImage(tga, MagickFormat.Tga);
            image.Format = MagickFormat.Png;
            await using var pngStream = new MemoryStream();
            await image.WriteAsync(pngStream);
            return pngStream.ToArray();
        }
    }

    internal class ProcessResult
    {
        public string StdOut { get; set; }
        public byte[] Bytes { get; set; }
        public string StdErr { get; set; }
        public FileInfo File { get; set; }
        public int ExitCode { get; set; }
    }
}