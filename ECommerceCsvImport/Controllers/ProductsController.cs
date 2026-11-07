using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Core.Services;
using ECommerceCsvImport.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceCsvImport.Controllers
{
    public class ProductsController : Controller
    {
        private const string PriceAscending = "price_asc";
        private const string PriceDescending = "price_desc";
        private const string StockAscending = "stock_asc";
        private const string StockDescending = "stock_desc";

        private readonly IProductRepository _repository;
        private readonly IProductImportService _importService;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(
            IProductRepository repository,
            IProductImportService importService,
            IWebHostEnvironment environment)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        // Список товаров с поиском, фильтром по категории и сортировкой
        [HttpGet]
        public ActionResult Index(string? searchString, string? categoryFilter, string? sortOrder)
        {
            var allProducts = _repository.GetAll();
            IEnumerable<Product> products = allProducts;

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var term = searchString.Trim();
                products = products.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                products = products.Where(p => string.Equals(p.CategoryName, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            products = sortOrder switch
            {
                PriceAscending => products.OrderBy(p => p.Price),
                PriceDescending => products.OrderByDescending(p => p.Price),
                StockAscending => products.OrderBy(p => p.StockQuantity),
                StockDescending => products.OrderByDescending(p => p.StockQuantity),
                _ => products
            };

            var viewModel = new ProductsIndexViewModel
            {
                Products = products.ToList(),
                SearchString = searchString,
                CategoryFilter = categoryFilter,
                SortOrder = sortOrder,
                AllProductsCount = allProducts.Count,
                AllProductsTotalValue = allProducts.Sum(p => p.Price),
                Categories = allProducts
                    .Select(p => p.CategoryName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c)
                    .ToList()
            };

            return View(viewModel);
        }

        // Карточка товара по артикулу
        [HttpGet]
        public ActionResult Details(string? sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                return NotFound();
            }

            var product = _repository.GetBySku(sku);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // Экспорт всех товаров в CSV (UTF-8 с BOM для Excel)
        [HttpGet]
        public ActionResult Export()
        {
            return File(ProductCsvExporter.Export(_repository.GetAll()), "text/csv", "products.csv");
        }

        // Форма импорта
        [HttpGet]
        public ActionResult Import()
        {
            return View(new ImportViewModel());
        }

        // Импорт по пути или из загруженного файла
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(string filePath, IFormFile? file)
        {
            string? tempPath = null;

            try
            {
                string? resolvedPath;

                if (file != null && !string.IsNullOrEmpty(file.FileName))
                {
                    try
                    {
                        tempPath = Path.GetTempFileName();

                        using (var stream = new FileStream(tempPath, FileMode.Create))
                        {
                            file.CopyTo(stream);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        return View("Import", new ImportViewModel
                        {
                            FilePath = filePath ?? string.Empty,
                            Result = new ImportResult
                            {
                                Errors =
                                {
                                    new ImportError
                                    {
                                        RowNumber = 0,
                                        Message = "Не удалось сохранить загруженный файл"
                                    }
                                }
                            }
                        });
                    }

                    resolvedPath = tempPath;
                }
                else
                {
                    resolvedPath = ResolveFilePath(filePath);
                }

                if (string.IsNullOrWhiteSpace(resolvedPath))
                {
                    return View("Import", new ImportViewModel
                    {
                        FilePath = filePath ?? string.Empty,
                        Result = new ImportResult
                        {
                            Errors =
                            {
                                new ImportError
                                {
                                    RowNumber = 0,
                                    Message = "Укажите путь к CSV-файлу"
                                }
                            }
                        }
                    });
                }

                var result = await _importService.ImportFromCsvAsync(resolvedPath);

                return View("Import", new ImportViewModel
                {
                    FilePath = filePath ?? string.Empty,
                    Result = result
                });
            }
            finally
            {
                if (tempPath != null)
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        // Преобразует введенный путь: относительный и начинающийся с / считается от wwwroot
        private string? ResolveFilePath(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim();

            if (trimmed.StartsWith('/') || trimmed.StartsWith('\\'))
                return Path.Combine(_environment.WebRootPath, trimmed.TrimStart('/', '\\'));

            if (Path.IsPathRooted(trimmed))
                return trimmed;

            return Path.Combine(_environment.WebRootPath, trimmed);
        }
    }
}
