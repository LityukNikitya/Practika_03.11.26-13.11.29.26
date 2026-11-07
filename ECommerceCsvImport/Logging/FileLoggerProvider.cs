using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ECommerceCsvImport.Logging
{
    // Провайдер ILogger, дописывающий сообщения приложения в текстовый файл.
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private const string CategoryPrefix = "ECommerceCsvImport";

        private readonly string _filePath;
        private readonly object _lock = new();

        public FileLoggerProvider(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            _filePath = filePath;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
        }

        private void Append(string text)
        {
            lock (_lock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(_filePath, text, Encoding.UTF8);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= LogLevel.Information
                    && logLevel != LogLevel.None
                    && category.StartsWith(CategoryPrefix, StringComparison.Ordinal);
            }

            // Форматирует запись и добавляет её в файл; ошибки записи не пробрасываются.
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var timestamp = TimeProvider.System.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var line = $"{timestamp} [{logLevel}] {category}: {formatter(state, exception)}{Environment.NewLine}";

                if (exception is not null)
                {
                    line += exception + Environment.NewLine;
                }

                provider.Append(line);
            }
        }
    }
}
