// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Middleware;
using DocumentManager.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку сессий
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Добавляем доступ к HTTP контексту
builder.Services.AddHttpContextAccessor();

// Добавляем сервисы для внедрения зависимостей
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Путь к директориям из конфигурации
var jsonBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["JsonBasePath"] ?? "Templates/Json");
var templatesBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["TemplatesBasePath"] ?? "Templates/Word");
var outputBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["OutputBasePath"] ?? "Generated");

// Регистрируем службы
builder.Services.AddScoped<IJsonSchemaService, JsonSchemaService>(provider =>
    new JsonSchemaService(jsonBasePath));

builder.Services.AddScoped<ITemplateService, TemplateService>();

builder.Services.AddScoped<IDocumentGenerationService, DocumentGenerationService>(provider =>
    new DocumentGenerationService(
        templatesBasePath,
        outputBasePath,
        provider.GetRequiredService<IDocumentService>(),
        provider.GetRequiredService<ILogger<DocumentGenerationService>>()
    ));

builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddScoped<TemplateManagerService>(provider =>
    new TemplateManagerService(
        provider.GetRequiredService<ApplicationDbContext>(),
        provider.GetRequiredService<ITemplateService>(),
        provider.GetRequiredService<IJsonSchemaService>(),
        provider.GetRequiredService<ILogger<TemplateManagerService>>(),
        jsonBasePath,
        templatesBasePath
    ));

// Регистрируем службы для авторизации и отслеживания прогресса
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddScoped<SimpleAuthService>();

// Добавляем фоновую службу для очистки устаревших операций
builder.Services.AddHostedService<ProgressCleanupService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Запускаем инициализацию базы данных (создание миграций)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при выполнении миграций базы данных");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Используем наше промежуточное ПО для авторизации
app.UseSimpleAuth();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Фоновая служба для очистки устаревших операций
public class ProgressCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProgressCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _maxAge = TimeSpan.FromHours(24);

    public ProgressCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ProgressCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Служба очистки прогресса запущена");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ждем заданный интервал
                await Task.Delay(_cleanupInterval, stoppingToken);

                // Выполняем очистку
                using (var scope = _serviceProvider.CreateScope())
                {
                    var progressService = scope.ServiceProvider.GetRequiredService<ProgressService>();
                    progressService.CleanupOldOperations(_maxAge);
                    _logger.LogInformation("Выполнена очистка устаревших операций");
                }
            }
            catch (OperationCanceledException)
            {
                // Операция отменена, завершаем работу
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке устаревших операций");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Служба очистки прогресса остановлена");
    }
}