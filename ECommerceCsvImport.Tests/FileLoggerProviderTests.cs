using ECommerceCsvImport.Logging;
using Microsoft.Extensions.Logging;

namespace ECommerceCsvImport.Tests
{
    public class FileLoggerProviderTests
    {
        [Fact]
        public void Log_WarningFromAppCategory_WritesLineToFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(directory, "logs", "import.log");

            try
            {
                using var provider = new FileLoggerProvider(filePath);
                var appLogger = provider.CreateLogger("ECommerceCsvImport.Core.Services.ProductImportService");
                var frameworkLogger = provider.CreateLogger("Microsoft.AspNetCore.Hosting");

                appLogger.LogWarning("Строка 5 пропущена: неверная цена");
                frameworkLogger.LogWarning("Служебное сообщение фреймворка");

                var content = File.ReadAllText(filePath);

                Assert.Contains("[Warning] ECommerceCsvImport.Core.Services.ProductImportService: Строка 5 пропущена: неверная цена", content);
                Assert.DoesNotContain("Служебное сообщение фреймворка", content);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
