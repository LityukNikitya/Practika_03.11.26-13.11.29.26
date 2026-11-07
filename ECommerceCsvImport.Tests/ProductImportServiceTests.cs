using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Core.Services;
using Microsoft.Extensions.Logging;

namespace ECommerceCsvImport.Tests
{
    public class ProductImportServiceTests
    {
        private const string Header = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

        private const string AsusRow = "1001,Ноутбук ASUS VivoBook,Другой ноутбук,52990,RUB,Компьютеры,7,/images/asus.jpg,true,ASUS,24,VivoBook";

        private const string SkuError = "Поле sku не может быть пустым";
        private const string NameError = "Поле name не может быть пустым";
        private const string PriceError = "Поле price должно быть числом больше 0";
        private const string StockError = "Остаток на складе должен быть целым числом не меньше 0";
        private const string IsActiveError = "Поле is_active должно иметь значение true, false, 1 или 0";
        private const string BrandError = "Поле brand обязательно для варианта «Электроника»";
        private const string WarrantyError = "Гарантия должна быть целым числом от 0 до 60 месяцев";
        private const string DuplicateError = "Артикул товара уже существует";
        private const string NoDataError = "Данные для импорта отсутствуют";
        private const string FileNotFoundError = "Файл не найден";
        private const string HeaderError = "Неверные заголовки CSV";
        private const string FieldCountError = "Неверное количество полей в строке";

        private sealed class RecordingLogger : ILogger<ProductImportService>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }

        private static string FixturePath(string fileName)
        {
            return Path.Combine(AppContext.BaseDirectory, "testdata", fileName);
        }

        [Fact]
        public void Constructor_NullRepository_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ProductImportService(null!));
        }

        [Fact]
        public async Task ImportFromCsv_FileNotFound_ReturnsFileNotFoundError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");

            var result = await service.ImportFromCsvAsync(path);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Contains(FileNotFoundError, result.Errors[0].Message);
            Assert.Equal(0, result.Errors[0].RowNumber);
        }

        [Fact]
        public async Task ImportFromCsv_ValidElectronicsFixture_ImportsTwoRows()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);

            var result = await service.ImportFromCsvAsync(FixturePath("valid_electronics.csv"));

            Assert.Equal(2, result.TotalRows);
            Assert.Equal(2, result.ImportedCount);
            Assert.Equal(0, result.ErrorCount);
            Assert.Equal(2, repository.Count);
        }

        [Fact]
        public async Task ImportFromCsv_InvalidPriceFixture_ReturnsPriceErrorOnRowTwo()
        {
            var service = new ProductImportService(new InMemoryProductRepository());

            var result = await service.ImportFromCsvAsync(FixturePath("invalid_price.csv"));

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(2, result.Errors[0].RowNumber);
            Assert.Contains(PriceError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromCsv_DuplicateSkuFixture_KeepsFirstProduct()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);

            var result = await service.ImportFromCsvAsync(FixturePath("duplicate_sku.csv"));

            Assert.Equal(2, result.TotalRows);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(3, result.Errors[0].RowNumber);
            Assert.Contains(DuplicateError, result.Errors[0].Message);
            Assert.Equal(1, repository.Count);
            Assert.Equal("Ноутбук Lenovo IdeaPad 3", repository.GetBySku("1001")!.Name);
        }

        [Fact]
        public async Task ImportFromCsv_EmptyFileFixture_ReturnsNoDataError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());

            var result = await service.ImportFromCsvAsync(FixturePath("empty_file.csv"));

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Contains(NoDataError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromCsv_MissingBrandFixture_ReturnsBrandErrorOnRowTwo()
        {
            var service = new ProductImportService(new InMemoryProductRepository());

            var result = await service.ImportFromCsvAsync(FixturePath("missing_brand.csv"));

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(2, result.Errors[0].RowNumber);
            Assert.Contains(BrandError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromCsv_WarrantyOutOfRangeFixture_ReturnsWarrantyErrorOnRowTwo()
        {
            var service = new ProductImportService(new InMemoryProductRepository());

            var result = await service.ImportFromCsvAsync(FixturePath("warranty_out_of_range.csv"));

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(2, result.Errors[0].RowNumber);
            Assert.Contains(WarrantyError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromCsv_BadActiveFixture_ReturnsIsActiveErrorOnRowTwo()
        {
            var service = new ProductImportService(new InMemoryProductRepository());

            var result = await service.ImportFromCsvAsync(FixturePath("bad_active.csv"));

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(2, result.Errors[0].RowNumber);
            Assert.Contains(IsActiveError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_InvalidHeader_ReturnsHeaderError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { "sku,name,description,price,currency,category_name,stock,image_url,is_active" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Equal(1, result.Errors[0].RowNumber);
            Assert.Equal(HeaderError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_WrongFieldCount_ReturnsFieldCountError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Equal(2, result.Errors[0].RowNumber);
            Assert.Equal(FieldCountError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_OnlyWhitespaceLines_ReturnsNoDataError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { string.Empty, "   ", "\t" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Equal(NoDataError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_SeveralErrorsInRow_ReportsAllSevenErrorsInFieldOrder()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, ",,Некорректная строка,-10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61," };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(7, result.ErrorCount);
            Assert.Equal(
                new[] { SkuError, NameError, PriceError, StockError, IsActiveError, BrandError, WarrantyError },
                result.Errors.Select(e => e.Message).ToArray());
            Assert.All(result.Errors, e => Assert.Equal(2, e.RowNumber));
            Assert.Equal(0, repository.Count);
        }

        [Fact]
        public async Task ImportFromLines_EmptySkuNameAndNegativeStock_ReportsEachError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { Header, ",,Описание,100,RUB,Компьютеры,-5,/images/a.jpg,true,Lenovo,24,M" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(new[] { SkuError, NameError, StockError }, result.Errors.Select(e => e.Message).ToArray());
        }

        [Fact]
        public async Task ImportFromLines_SkuAlreadyInRepository_SkipsRow()
        {
            var repository = new InMemoryProductRepository();
            await repository.AddAsync(new Product { Sku = "1001", Name = "Существующий товар" });
            var service = new ProductImportService(repository);

            var result = await service.ImportFromLinesAsync(new[] { Header, AsusRow });

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Equal(1, repository.Count);
            Assert.Single(result.Errors);
            Assert.Equal(DuplicateError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_OnlyHeader_ReturnsNoDataError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { Header };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Contains(NoDataError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_NegativeWarranty_ReturnsWarrantyError()
        {
            var service = new ProductImportService(new InMemoryProductRepository());
            var lines = new[] { Header, "1006,Накопитель SSD Samsung 870 EVO 1TB,Внутренний твердотельный накопитель,5990,RUB,Компьютеры,12,/images/ssd.jpg,true,Samsung,-1,870EVO-1T" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Contains(WarrantyError, result.Errors[0].Message);
        }

        [Fact]
        public async Task ImportFromLines_EmptyOptionalFields_ImportsProduct()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.ImportedCount);
            Assert.Empty(result.Errors);

            var product = repository.GetBySku("1011");

            Assert.NotNull(product);
            Assert.Equal(string.Empty, product.Description);
            Assert.Equal(string.Empty, product.ImageUrl);
        }

        [Fact]
        public async Task ImportFromLines_CyrillicRow_PreservesText()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.ImportedCount);

            var product = repository.GetBySku("1001");

            Assert.NotNull(product);
            Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
            Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
            Assert.Equal("Компьютеры", product.CategoryName);
        }

        [Fact]
        public async Task ImportFromLines_EmptyCurrencyAndCategory_UsesDefaults()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, "1011,Блок питания Corsair CV550,,4990,,,9,,true,Corsair,60,CV550" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.ImportedCount);

            var product = repository.GetBySku("1011");

            Assert.NotNull(product);
            Assert.Equal("RUB", product.Currency);
            Assert.Equal("Без категории", product.CategoryName);
        }

        [Fact]
        public async Task ImportFromLines_NonNumericPrice_ReturnsPriceError()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, "1003,Клавиатура A4Tech Bloody V3M,Механическая игровая клавиатура,abc,RUB,Периферия,7,/images/keyboard.jpg,true,A4Tech,24,V3M" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(0, result.ImportedCount);
            Assert.Single(result.Errors);
            Assert.Contains(PriceError, result.Errors[0].Message);
            Assert.Equal(0, repository.Count);
        }

        [Fact]
        public async Task ImportFromLines_ValidRow_ImportsProductWithAllElectronicsFields()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15" };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.TotalRows);
            Assert.Equal(1, result.ImportedCount);
            Assert.Empty(result.Errors);

            var product = repository.GetBySku("1001");

            Assert.NotNull(product);
            Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
            Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
            Assert.Equal(45990m, product.Price);
            Assert.Equal("RUB", product.Currency);
            Assert.Equal("Компьютеры", product.CategoryName);
            Assert.Equal(5, product.StockQuantity);
            Assert.Equal("/images/laptop.jpg", product.ImageUrl);
            Assert.True(product.IsActive);
            Assert.Equal("Lenovo", product.Brand);
            Assert.Equal(24, product.WarrantyMonths);
            Assert.Equal("IdeaPad3-15", product.ModelNumber);
        }

        [Fact]
        public async Task ImportFromLines_SemicolonDelimited_ImportsProduct()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[]
            {
                Header.Replace(',', ';'),
                "1001;Ноутбук Lenovo IdeaPad 3;Бюджетный ноутбук;45990;RUB;Компьютеры;5;/images/laptop.jpg;true;Lenovo;24;IdeaPad3-15"
            };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.ImportedCount);
            Assert.Empty(result.Errors);
            var product = repository.GetBySku("1001");

            Assert.NotNull(product);
            Assert.Equal("Lenovo", product.Brand);
        }

        [Fact]
        public async Task ImportFromLines_TabDelimited_ImportsProduct()
        {
            var repository = new InMemoryProductRepository();
            var service = new ProductImportService(repository);
            var lines = new[]
            {
                Header.Replace(',', '\t'),
                "1001\tНоутбук Lenovo IdeaPad 3\tБюджетный ноутбук\t45990\tRUB\tКомпьютеры\t5\t/images/laptop.jpg\ttrue\tLenovo\t24\tIdeaPad3-15"
            };

            var result = await service.ImportFromLinesAsync(lines);

            Assert.Equal(1, result.ImportedCount);
            Assert.Empty(result.Errors);
            var product = repository.GetBySku("1001");

            Assert.NotNull(product);
            Assert.Equal(24, product.WarrantyMonths);
        }

        [Fact]
        public async Task ImportFromLines_InvalidRowWithLogger_LogsWarningAndSummary()
        {
            var logger = new RecordingLogger();
            var service = new ProductImportService(new InMemoryProductRepository(), logger);
            var lines = new[] { Header, "1003,Клавиатура A4Tech Bloody V3M,Механическая игровая клавиатура,abc,RUB,Периферия,7,/images/keyboard.jpg,true,A4Tech,24,V3M" };

            await service.ImportFromLinesAsync(lines);

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(PriceError));
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
        }
    }
}
