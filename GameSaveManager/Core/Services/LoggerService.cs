// LoggerService.cs

using System;
using System.IO;

namespace GameSaveManager.Core.Services
{
    public class LoggerService
    {
        private readonly string logFilePath;

        public LoggerService(string logFilePath)
        {
            this.logFilePath = logFilePath;
        }

        public void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        public void LogError(string message)
        {
            Log("ERROR", message);
        }

        private void Log(string logType, string message)
        {
            var logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{logType}] {message}\n";
            File.AppendAllText(logFilePath, logEntry);
        }
    }
}