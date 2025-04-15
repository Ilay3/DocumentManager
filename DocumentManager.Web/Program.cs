// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Настройка базы данных PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Получаем базовые пути с проверкой на null
var webRootPath = builder.Environment.WebRootPath ??
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var contentRootPath = builder.Environment.ContentRootPath ??
    Directory.GetCurrentDirectory();

// Настройка путей к шаблонам и выходным файлам с гарантированными значениями
var templatesBasePath = builder.Configuration.GetValue<string>("TemplatesBasePath") ??
    "Templates/Word";

var jsonBasePath = builder.Configuration.GetValue<string>("JsonBasePath") ??
    "Templates/Json";

var outputBasePath = builder.Configuration.GetValue<string>("OutputBasePath") ??
    Path.Combine(contentRootPath, "Generated");


var fullTemplatesBasePath = Path.IsPathRooted(templatesBasePath) ?
    templatesBasePath : Path.Combine(webRootPath, templatesBasePath);

var fullJsonBasePath = Path.IsPathRooted(jsonBasePath) ?
    jsonBasePath : Path.Combine(webRootPath, jsonBasePath);

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation($"WebRootPath: {webRootPath}");
logger.LogInformation($"ContentRootPath: {contentRootPath}");
logger.LogInformation($"TemplatesBasePath настройка: {templatesBasePath}");
logger.LogInformation($"JsonBasePath настройка: {jsonBasePath}");
logger.LogInformation($"Полный путь к шаблонам Word: {fullTemplatesBasePath}");
logger.LogInformation($"Полный путь к JSON-файлам: {fullJsonBasePath}");


// Регистрация сервисов с полными путями
builder.Services.AddSingleton<IJsonSchemaService>(provider => new JsonSchemaService(fullJsonBasePath));

builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentGenerationService>(provider =>
    new DocumentGenerationService(
        fullTemplatesBasePath,
        outputBasePath,
        provider.GetRequiredService<IDocumentService>()));

// Регистрация сервиса инициализации данных с полными путями
builder.Services.AddTransient<DataInitializer>(provider =>
    new DataInitializer(
        provider.GetRequiredService<ApplicationDbContext>(),
        provider.GetRequiredService<ITemplateService>(),
        provider.GetRequiredService<IJsonSchemaService>(),
        provider.GetRequiredService<ILogger<DataInitializer>>(),
        fullJsonBasePath,
        fullTemplatesBasePath));


var app = builder.Build();

// Логирование путей для диагностики
app.Logger.LogInformation($"WebRootPath: {webRootPath}");
app.Logger.LogInformation($"ContentRootPath: {contentRootPath}");
app.Logger.LogInformation($"TemplatesBasePath: {templatesBasePath}");
app.Logger.LogInformation($"JsonBasePath: {jsonBasePath}");
app.Logger.LogInformation($"OutputBasePath: {outputBasePath}");

// Создание необходимых директорий с проверкой на пустые пути
if (!string.IsNullOrEmpty(templatesBasePath))
{
    Directory.CreateDirectory(templatesBasePath);
    app.Logger.LogInformation($"Создана директория: {templatesBasePath}");
}
else
{
    app.Logger.LogWarning("Путь к шаблонам Word пустой или null");
}

if (!string.IsNullOrEmpty(jsonBasePath))
{
    Directory.CreateDirectory(jsonBasePath);
    app.Logger.LogInformation($"Создана директория: {jsonBasePath}");
}
else
{
    app.Logger.LogWarning("Путь к JSON-файлам пустой или null");
}

if (!string.IsNullOrEmpty(outputBasePath))
{
    Directory.CreateDirectory(outputBasePath);
    app.Logger.LogInformation($"Создана директория: {outputBasePath}");
}
else
{
    app.Logger.LogWarning("Путь к сгенерированным файлам пустой или null");
}

// Копирование шаблонов и JSON-файлов в рабочие директории, если они отсутствуют
if (!string.IsNullOrEmpty(templatesBasePath) && !Directory.EnumerateFiles(templatesBasePath).Any())
{
    // Копирование шаблонов Word из папки приложения
    var sourceWordTemplates = Path.Combine(contentRootPath, "Templates", "Word");
    if (Directory.Exists(sourceWordTemplates))
    {
        foreach (var file in Directory.GetFiles(sourceWordTemplates, "*.doc*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceWordTemplates, file);
            var targetPath = Path.Combine(templatesBasePath, relativePath);

            // Создаем директории при необходимости
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            File.Copy(file, targetPath, true);
            app.Logger.LogInformation($"Копирование шаблона Word: {targetPath}");
        }
    }
}

if (!string.IsNullOrEmpty(jsonBasePath) && !Directory.EnumerateFiles(jsonBasePath).Any())
{
    // Копирование JSON-файлов из папки приложения
    var sourceJsonFiles = Path.Combine(contentRootPath, "Templates", "Json");
    if (Directory.Exists(sourceJsonFiles))
    {
        foreach (var file in Directory.GetFiles(sourceJsonFiles, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceJsonFiles, file);
            var targetPath = Path.Combine(jsonBasePath, relativePath);

            // Создаем директории при необходимости
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            File.Copy(file, targetPath, true);
            app.Logger.LogInformation($"Копирование JSON-файла: {targetPath}");
        }
    }
}

// Инициализация базы данных
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Применение миграций
        app.Logger.LogInformation("Применение миграций базы данных...");
        context.Database.Migrate();

        // Инициализация данных
        app.Logger.LogInformation("Инициализация данных...");
        var dataInitializer = scope.ServiceProvider.GetRequiredService<DataInitializer>();
        dataInitializer.InitializeAsync().Wait();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Ошибка при инициализации базы данных");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();