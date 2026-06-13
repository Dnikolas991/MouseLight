using System;
using Colossal.Logging;

namespace MouseLight
{
    internal static class Logger
    {
        // 日志系统异常不能影响游戏模拟更新。
        private static readonly ILog Log = LogManager.GetLogger(nameof(MouseLight)).SetShowsErrorsInUI(false);

        internal static void Info(string message) => SafeWrite(log => log.Info(message));
        internal static void Warn(string message) => SafeWrite(log => log.Warn(message));
        internal static void Error(string message) => SafeWrite(log => log.Error(message));

        private static void SafeWrite(Action<ILog> write)
        {
            try
            {
                write?.Invoke(Log);
            }
            catch
            {
                // 忽略日志内部异常，避免中断游戏系统更新。
            }
        }
    }
}
