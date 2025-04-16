// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// Регистрируем поддержку кодировок для корректной работы с русскими символами
Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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
    "Generated";

var fullTemplatesBasePath = Path.IsPathRooted(templatesBasePath) ?
    templatesBasePath : Path.Combine(webRootPath, templatesBasePath);

var fullJsonBasePath = Path.IsPathRooted(jsonBasePath) ?
    jsonBasePath : Path.Combine(webRootPath, jsonBasePath);

var fullOutputBasePath = Path.IsPathRooted(outputBasePath) ?
    outputBasePath : Path.Combine(webRootPath, outputBasePath);

// Настройка логирования
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Регистрация сервисов
builder.Services.AddSingleton<IJsonSchemaService>(provider =>
    new JsonSchemaService(fullJsonBasePath));

builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentService>(provider =>
    new DocumentService(
        provider.GetRequiredService<ApplicationDbContext>(),
        provider.GetRequiredService<ILogger<DocumentService>>()));

builder.Services.AddScoped<IDocumentGenerationService>(provider =>
    new DocumentGenerationService(
        fullTemplatesBasePath,
        fullOutputBasePath,
        provider.GetRequiredService<IDocumentService>(),
        provider.GetRequiredService<ILogger<DocumentGenerationService>>()));


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });


// Регистрация сервиса инициализации данных
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
app.Logger.LogInformation($"TemplatesBasePath: {templatesBasePath} -> {fullTemplatesBasePath}");
app.Logger.LogInformation($"JsonBasePath: {jsonBasePath} -> {fullJsonBasePath}");
app.Logger.LogInformation($"OutputBasePath: {outputBasePath} -> {fullOutputBasePath}");

// Создание необходимых директорий
Directory.CreateDirectory(fullTemplatesBasePath);
app.Logger.LogInformation($"Создана/проверена директория шаблонов: {fullTemplatesBasePath}");

Directory.CreateDirectory(fullJsonBasePath);
app.Logger.LogInformation($"Создана/проверена директория JSON: {fullJsonBasePath}");

Directory.CreateDirectory(fullOutputBasePath);
app.Logger.LogInformation($"Создана/проверена директория вывода: {fullOutputBasePath}");

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