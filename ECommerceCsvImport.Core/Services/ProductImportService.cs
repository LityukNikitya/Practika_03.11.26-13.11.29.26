using System.Globalization;
using ECommerceCsvImport.Core.Models;
using Microsoft.Extensions.Logging;

namespace ECommerceCsvImport.Core.Services
{
    public class ProductImportService : IProductImportService
    {
        private const string DefaultCurrency = "RUB";
        private const string DefaultCategory = "Без категории";
        private const int MinWarrantyMonths = 0;
        private const int MaxWarrantyMonths = 60;

        private static readonly string[] RequiredColumns =
        {
            "sku", "name", "description", "price", "currency", "category_name", "stock",
            "image_url", "is_active", "brand", "warranty_months", "model_number"
        };

        private static readonly char[] SupportedDelimiters = { ',', ';', '\t' };

        private readonly IProductRepository _repository;
        private readonly ILogger<ProductImportService>? _logger;

        public ProductImportService(IProductRepository repository, ILogger<ProductImportService>? logger = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger;
        }

        // Импорт из файла: проверка пути, асинхронное чтение строк, передача в ImportFromLinesAsync.
        public async Task<ImportResult> ImportFromCsvAsync(string filePath)
        {
            var result = new ImportResult();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger?.LogWarning("Файл не найден: {FilePath}", filePath);
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
                return result;
            }

            string[] lines;

            try
            {
                lines = await File.ReadAllLinesAsync(filePath);
            }
            catch (IOException ex)
            {
                _logger?.LogWarning(ex, "Не удалось прочитать файл: {FilePath}", filePath);
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Не удалось прочитать файл" });
                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Не удалось прочитать файл: {FilePath}", filePath);
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Не удалось прочитать файл" });
                return result;
            }

            return await ImportFromLinesAsync(lines);
        }

        // Импорт из набора строк CSV: заголовок, разделитель, валидация строк, проверка дубликатов, добавление в репозиторий.
        public async Task<ImportResult> ImportFromLinesAsync(IEnumerable<string> lines)
        {
            var result = new ImportResult();
            var allLines = lines.ToList();

            int headerIndex = allLines.FindIndex(line => !string.IsNullOrWhiteSpace(line));

            if (headerIndex < 0)
            {
                _logger?.LogWarning("Данные для импорта отсутствуют");
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
                return result;
            }

            int headerRow = headerIndex + 1;
            char delimiter = DetectDelimiter(allLines[headerIndex]);
            var headerFields = CsvLineParser.Parse(allLines[headerIndex], delimiter);

            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headerFields.Count; i++)
            {
                columnMap.TryAdd(headerFields[i].Trim(), i);
            }

            if (RequiredColumns.Any(column => !columnMap.ContainsKey(column)))
            {
                _logger?.LogWarning("Строка {RowNumber}: {Message}", headerRow, "Неверные заголовки CSV");
                result.Errors.Add(new ImportError { RowNumber = headerRow, Message = "Неверные заголовки CSV" });
                return result;
            }

            int expectedFieldCount = headerFields.Count;
            var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = headerIndex + 1; index < allLines.Count; index++)
            {
                var line = allLines[index];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result.TotalRows++;
                int rowNumber = index + 1;

                var values = CsvLineParser.Parse(line, delimiter);

                if (values.Count != expectedFieldCount)
                {
                    _logger?.LogWarning("Строка {RowNumber}: {Message}", rowNumber, "Неверное количество полей в строке");
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        Message = "Неверное количество полей в строке"
                    });
                    continue;
                }

                var rowErrors = new List<ImportError>();
                var row = ParseRow(values, columnMap, rowNumber, rowErrors);

                if (row == null)
                {
                    foreach (var rowError in rowErrors)
                    {
                        _logger?.LogWarning("Строка {RowNumber}: {Message}", rowError.RowNumber, rowError.Message);
                    }

                    result.Errors.AddRange(rowErrors);
                    continue;
                }

                bool isDuplicate = _repository.GetBySku(row.Sku) != null || importedSkus.Contains(row.Sku);

                if (isDuplicate)
                {
                    _logger?.LogWarning("Строка {RowNumber}: {Message} ({Sku})", rowNumber, "Артикул товара уже существует", row.Sku);
                    result.Errors.Add(new ImportError
                    {
                        RowNumber = rowNumber,
                        Message = "Артикул товара уже существует"
                    });
                    continue;
                }

                await _repository.AddAsync(MapToProduct(row));
                importedSkus.Add(row.Sku);
                result.ImportedCount++;
            }

            if (result.TotalRows == 0 && result.Errors.Count == 0)
            {
                _logger?.LogWarning("Данные для импорта отсутствуют");
                result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
            }

            _logger?.LogInformation(
                "Импорт завершен: всего строк {TotalRows}, импортировано {ImportedCount}, ошибок {ErrorCount}",
                result.TotalRows,
                result.ImportedCount,
                result.ErrorCount);

            return result;
        }

        // Определяет разделитель по строке заголовка: первый из , ; табуляция, при котором найдены все обязательные колонки.
        private static char DetectDelimiter(string headerLine)
        {
            foreach (var delimiter in SupportedDelimiters)
            {
                var fields = CsvLineParser.Parse(headerLine, delimiter);

                bool hasAllColumns = RequiredColumns.All(column =>
                    fields.Any(field => string.Equals(field.Trim(), column, StringComparison.OrdinalIgnoreCase)));

                if (hasAllColumns)
                {
                    return delimiter;
                }
            }

            return ',';
        }

        // Разбор и валидация одной строки данных; при ошибках возвращает null и заполняет rowErrors.
        private static ProductImportRow? ParseRow(
            IReadOnlyList<string> values,
            IReadOnlyDictionary<string, int> columnMap,
            int rowNumber,
            List<ImportError> rowErrors)
        {
            string GetValue(string columnName)
            {
                if (columnMap.TryGetValue(columnName, out int columnIndex) && columnIndex < values.Count)
                {
                    return values[columnIndex];
                }

                return string.Empty;
            }

            var row = new ProductImportRow
            {
                Sku = GetValue("sku").Trim(),
                Name = GetValue("name").Trim(),
                Description = GetValue("description").Trim(),
                Currency = GetValue("currency").Trim(),
                CategoryName = GetValue("category_name").Trim(),
                ImageUrl = GetValue("image_url").Trim(),
                Brand = GetValue("brand").Trim(),
                ModelNumber = GetValue("model_number").Trim()
            };

            if (string.IsNullOrWhiteSpace(row.Sku))
            {
                AddError(rowErrors, rowNumber, "Поле sku не может быть пустым");
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                AddError(rowErrors, rowNumber, "Поле name не может быть пустым");
            }

            bool priceValid = TryParseDecimal(GetValue("price"), out decimal price) && price > 0;

            if (priceValid)
            {
                row.Price = price;
            }
            else
            {
                AddError(rowErrors, rowNumber, "Поле price должно быть числом больше 0");
            }

            bool stockValid = TryParseInt(GetValue("stock"), out int stock) && stock >= 0;

            if (stockValid)
            {
                row.StockQuantity = stock;
            }
            else
            {
                AddError(rowErrors, rowNumber, "Остаток на складе должен быть целым числом не меньше 0");
            }

            bool activeValid = TryParseBool(GetValue("is_active"), out bool isActive);

            if (activeValid)
            {
                row.IsActive = isActive;
            }
            else
            {
                AddError(rowErrors, rowNumber, "Поле is_active должно иметь значение true, false, 1 или 0");
            }

            if (string.IsNullOrWhiteSpace(row.Brand))
            {
                AddError(rowErrors, rowNumber, "Поле brand обязательно для варианта «Электроника»");
            }

            bool warrantyValid = TryParseInt(GetValue("warranty_months"), out int warrantyMonths)
                                 && warrantyMonths >= MinWarrantyMonths
                                 && warrantyMonths <= MaxWarrantyMonths;

            if (warrantyValid)
            {
                row.WarrantyMonths = warrantyMonths;
            }
            else
            {
                AddError(rowErrors, rowNumber, "Гарантия должна быть целым числом от 0 до 60 месяцев");
            }

            return rowErrors.Count > 0 ? null : row;
        }

        // Добавляет ошибку строки в список.
        private static void AddError(List<ImportError> errors, int rowNumber, string message)
        {
            errors.Add(new ImportError { RowNumber = rowNumber, Message = message });
        }

        // Преобразует разобранную строку в товар с нормализацией валюты и категории.
        private static Product MapToProduct(ProductImportRow row)
        {
            return new Product
            {
                Sku = row.Sku,
                Name = row.Name,
                Description = row.Description,
                Price = row.Price,
                Currency = NormalizeCurrency(row.Currency),
                CategoryName = NormalizeCategory(row.CategoryName),
                StockQuantity = row.StockQuantity,
                ImageUrl = row.ImageUrl,
                IsActive = row.IsActive,
                Brand = row.Brand,
                WarrantyMonths = row.WarrantyMonths,
                ModelNumber = row.ModelNumber
            };
        }

        // Пустая валюта заменяется на RUB, иначе верхний регистр.
        private static string NormalizeCurrency(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultCurrency;
            }

            return value.Trim().ToUpperInvariant();
        }

        // Пустая категория заменяется на «Без категории».
        private static string NormalizeCategory(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultCategory;
            }

            return value.Trim();
        }

        // Разбор десятичного числа в инвариантной культуре.
        private static bool TryParseDecimal(string value, out decimal result)
        {
            return decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out result);
        }

        // Разбор целого числа в инвариантной культуре.
        private static bool TryParseInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
        }

        // Разбор логического значения: true, false, 1, 0 без учета регистра.
        private static bool TryParseBool(string value, out bool result)
        {
            string normalized = value.Trim();

            if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) || normalized == "1")
            {
                result = true;
                return true;
            }

            if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) || normalized == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }
    }
}
