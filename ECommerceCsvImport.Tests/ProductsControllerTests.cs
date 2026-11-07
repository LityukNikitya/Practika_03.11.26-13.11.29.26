using System.Text;
using ECommerceCsvImport.Controllers;
using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Core.Services;
using ECommerceCsvImport.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace ECommerceCsvImport.Tests
{
    public class ProductsControllerTests
    {
        private sealed class StubEnvironment : IWebHostEnvironment
        {
            public string WebRootPath { get; set; } = string.Empty;
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string ApplicationName { get; set; } = "Test";
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string ContentRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Development";
        }

        private sealed class RecordingImportService : IProductImportService
        {
            public string? LastPath { get; private set; }
            public bool FileExistedDuringImport { get; private set; }
            public string? ContentDuringImport { get; private set; }

            public Task<ImportResult> ImportFromCsvAsync(string filePath)
            {
                LastPath = filePath;
                FileExistedDuringImport = File.Exists(filePath);

                if (FileExistedDuringImport)
                {
                    ContentDuringImport = File.ReadAllText(filePath);
                }

                return Task.FromResult(new ImportResult { TotalRows = 1, ImportedCount = 1 });
            }

            public Task<ImportResult> ImportFromLinesAsync(IEnumerable<string> lines)
            {
                return Task.FromResult(new ImportResult());
            }
        }

        private static Product CreateProduct(string sku, string name, decimal price, int stock, string category)
        {
            return new Product
            {
                Sku = sku,
                Name = name,
                Price = price,
                StockQuantity = stock,
                CategoryName = category
            };
        }

        private static async Task<InMemoryProductRepository> CreateRepositoryAsync()
        {
            var repository = new InMemoryProductRepository();
            await repository.AddAsync(CreateProduct("1", "Ноутбук Lenovo", 45000m, 5, "Компьютеры"));
            await repository.AddAsync(CreateProduct("2", "Мышь Logitech", 1000m, 20, "Периферия"));
            await repository.AddAsync(CreateProduct("3", "Монитор Dell", 30000m, 2, "Компьютеры"));
            return repository;
        }

        private static async Task<ProductsController> CreateControllerAsync(
            IProductRepository? repository = null,
            IProductImportService? importService = null,
            string webRoot = "")
        {
            repository ??= await CreateRepositoryAsync();
            importService ??= new ProductImportService(repository);

            return new ProductsController(repository, importService, new StubEnvironment { WebRootPath = webRoot });
        }

        private static ProductsIndexViewModel GetIndexModel(ActionResult result)
        {
            var view = Assert.IsType<ViewResult>(result);
            return Assert.IsType<ProductsIndexViewModel>(view.Model);
        }

        private static ImportViewModel GetImportModel(ActionResult result)
        {
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Import", view.ViewName);
            return Assert.IsType<ImportViewModel>(view.Model);
        }

        private static IFormFile CreateFormFile(string content, string fileName)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName);
        }

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProductsController(null!, new RecordingImportService(), new StubEnvironment()));
            Assert.Throws<ArgumentNullException>(() =>
                new ProductsController(new InMemoryProductRepository(), null!, new StubEnvironment()));
            Assert.Throws<ArgumentNullException>(() =>
                new ProductsController(new InMemoryProductRepository(), new RecordingImportService(), null!));
        }

        [Fact]
        public async Task Index_TotalsSearchAndCategoryFilter_ReturnsFilteredProducts()
        {
            var all = GetIndexModel((await CreateControllerAsync()).Index(null, null, null));

            Assert.Equal(3, all.Products.Count);
            Assert.Equal(3, all.AllProductsCount);
            Assert.Equal(76000m, all.AllProductsTotalValue);
            Assert.Equal(new[] { "Компьютеры", "Периферия" }, all.Categories);

            var search = GetIndexModel((await CreateControllerAsync()).Index("  мышь ", null, null));

            Assert.Single(search.Products);
            Assert.Equal("2", search.Products[0].Sku);
            Assert.Equal(3, search.AllProductsCount);

            var category = GetIndexModel((await CreateControllerAsync()).Index(null, "компьютеры", null));

            Assert.Equal(2, category.Products.Count);
            Assert.Equal(75000m, category.TotalValue);

            var combined = GetIndexModel((await CreateControllerAsync()).Index("Монитор", "Компьютеры", null));

            Assert.Single(combined.Products);
            Assert.Equal("3", combined.Products[0].Sku);

            var empty = GetIndexModel((await CreateControllerAsync(new InMemoryProductRepository())).Index(null, null, null));

            Assert.Empty(empty.Products);
            Assert.Equal(0, empty.AllProductsCount);
        }

        [Fact]
        public async Task Index_SortOrder_SortsProducts()
        {
            var controller = await CreateControllerAsync();

            Assert.Equal(new[] { "2", "3", "1" }, GetIndexModel(controller.Index(null, null, "price_asc")).Products.Select(p => p.Sku).ToArray());
            Assert.Equal(new[] { "1", "3", "2" }, GetIndexModel(controller.Index(null, null, "price_desc")).Products.Select(p => p.Sku).ToArray());
            Assert.Equal(new[] { "3", "1", "2" }, GetIndexModel(controller.Index(null, null, "stock_asc")).Products.Select(p => p.Sku).ToArray());
            Assert.Equal(new[] { "2", "1", "3" }, GetIndexModel(controller.Index(null, null, "stock_desc")).Products.Select(p => p.Sku).ToArray());
            Assert.Equal(new[] { "1", "2", "3" }, GetIndexModel(controller.Index(null, null, "unknown")).Products.Select(p => p.Sku).ToArray());
        }

        [Fact]
        public async Task Export_ReturnsCsvFileWithProducts()
        {
            var result = (await CreateControllerAsync()).Export();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", file.ContentType);
            Assert.Equal("products.csv", file.FileDownloadName);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.FileContents.Take(3).ToArray());
            Assert.Contains("Ноутбук Lenovo", Encoding.UTF8.GetString(file.FileContents));
        }

        [Fact]
        public async Task ImportGet_ReturnsEmptyImportViewModel()
        {
            var result = (await CreateControllerAsync()).Import();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ImportViewModel>(view.Model);
            Assert.Equal(string.Empty, model.FilePath);
            Assert.Null(model.Result);
        }

        [Fact]
        public async Task ImportPost_EmptyPath_ReturnsPathError()
        {
            var importService = new RecordingImportService();
            var controller = await CreateControllerAsync(importService: importService);

            var model = GetImportModel(await controller.Import("   ", null));

            Assert.Null(importService.LastPath);
            Assert.NotNull(model.Result);
            Assert.Single(model.Result!.Errors);
            Assert.Equal("Укажите путь к CSV-файлу", model.Result.Errors[0].Message);
            Assert.Equal(0, model.Result.Errors[0].RowNumber);
        }

        [Fact]
        public async Task ImportPost_PathInput_IsResolvedAgainstWebRootOrUsedAsIs()
        {
            var webRoot = Path.Combine(Path.GetTempPath(), "webroot");
            var rooted = Path.Combine(Path.GetTempPath(), "any.csv");

            var slashService = new RecordingImportService();
            await (await CreateControllerAsync(importService: slashService, webRoot: webRoot)).Import(" /csv/a.csv ", null);
            Assert.Equal(Path.Combine(webRoot, "csv/a.csv"), slashService.LastPath);

            var backslashService = new RecordingImportService();
            await (await CreateControllerAsync(importService: backslashService, webRoot: webRoot)).Import("\\csv\\a.csv", null);
            Assert.Equal(Path.Combine(webRoot, "csv\\a.csv"), backslashService.LastPath);

            var relativeService = new RecordingImportService();
            await (await CreateControllerAsync(importService: relativeService, webRoot: webRoot)).Import("csv/a.csv", null);
            Assert.Equal(Path.Combine(webRoot, "csv/a.csv"), relativeService.LastPath);

            var rootedService = new RecordingImportService();
            await (await CreateControllerAsync(importService: rootedService, webRoot: "unused")).Import(rooted, null);
            Assert.Equal(rooted, rootedService.LastPath);
        }

        [Fact]
        public async Task ImportPost_UploadedFile_ImportsFromTempFileAndDeletesIt()
        {
            var importService = new RecordingImportService();
            var controller = await CreateControllerAsync(importService: importService);
            var file = CreateFormFile("sku,name", "products.csv");

            var model = GetImportModel(await controller.Import("ignored.csv", file));

            Assert.NotNull(importService.LastPath);
            Assert.True(importService.FileExistedDuringImport);
            Assert.Equal("sku,name", importService.ContentDuringImport);
            Assert.False(File.Exists(importService.LastPath));
            Assert.Equal("ignored.csv", model.FilePath);
            Assert.Equal(1, model.Result!.ImportedCount);
        }

        [Fact]
        public async Task ImportPost_RealServiceWithValidFile_ImportsIntoRepository()
        {
            var repository = new InMemoryProductRepository();
            var controller = await CreateControllerAsync(repository);
            var content = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number\n"
                + "1001,Ноутбук,Описание,100,RUB,Компьютеры,5,/images/a.jpg,true,Lenovo,24,M1\n";
            var file = CreateFormFile(content, "products.csv");

            var model = GetImportModel(await controller.Import(string.Empty, file));

            Assert.Equal(1, model.Result!.ImportedCount);
            Assert.Equal(1, repository.Count);
        }

        [Fact]
        public async Task Details_ExistingSku_ReturnsViewWithProduct()
        {
            var result = (await CreateControllerAsync()).Details("2");

            var view = Assert.IsType<ViewResult>(result);
            var product = Assert.IsType<Product>(view.Model);
            Assert.Equal("2", product.Sku);
            Assert.Equal("Мышь Logitech", product.Name);
        }

        [Fact]
        public async Task Details_UnknownOrEmptySku_ReturnsNotFound()
        {
            var controller = await CreateControllerAsync();

            Assert.IsType<NotFoundResult>(controller.Details("999"));
            Assert.IsType<NotFoundResult>(controller.Details("   "));
            Assert.IsType<NotFoundResult>(controller.Details(string.Empty));
        }
    }
}
