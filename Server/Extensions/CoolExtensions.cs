using Microsoft.Extensions.Logging;

namespace SvgToTvgServer.Server.Extensions
{
    public static class CoolExtensions
    {
        public static void LogDebugWhenNotEmpty<T>(this ILogger<T> logger, string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
                logger.LogDebug(msg);
        }
        public static void LogInfoWhenNotEmpty<T>(this ILogger<T> logger, string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
                logger.LogInformation(msg);
        }
        
        public static void LogErrorWhenNotEmpty<T>(this ILogger<T> logger, string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
                logger.LogError(msg);
        }
    }
}