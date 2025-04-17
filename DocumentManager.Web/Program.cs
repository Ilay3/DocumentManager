// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using DocumentManager.Web.Middleware;
using DocumentManager.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ��������� ��������� ������
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ��������� ������ � HTTP ���������
builder.Services.AddHttpContextAccessor();

// ��������� ������� ��� ��������� ������������
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ���� � ����������� �� ������������
var jsonBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["JsonBasePath"] ?? "Templates/Json");
var templatesBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["TemplatesBasePath"] ?? "Templates/Word");
var outputBasePath = Path.Combine(builder.Environment.WebRootPath, builder.Configuration["OutputBasePath"] ?? "Generated");

// ������������ ������
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

// ������������ ������ ��� ����������� � ������������ ���������
builder.Services.AddSingleton<ProgressService>();
builder.Services.AddScoped<SimpleAuthService>();

// ��������� ������� ������ ��� ������� ���������� ��������
builder.Services.AddHostedService<ProgressCleanupService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ��������� ������������� ���� ������ (�������� ��������)
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
        logger.LogError(ex, "������ ��� ���������� �������� ���� ������");
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

// ���������� ���� ������������� �� ��� �����������
app.UseSimpleAuth();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ������� ������ ��� ������� ���������� ��������
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
        _logger.LogInformation("������ ������� ��������� ��������");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ���� �������� ��������
                await Task.Delay(_cleanupInterval, stoppingToken);

                // ��������� �������
                using (var scope = _serviceProvider.CreateScope())
                {
                    var progressService = scope.ServiceProvider.GetRequiredService<ProgressService>();
                    progressService.CleanupOldOperations(_maxAge);
                    _logger.LogInformation("��������� ������� ���������� ��������");
                }
            }
            catch (OperationCanceledException)
            {
                // �������� ��������, ��������� ������
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��� ������� ���������� ��������");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("������ ������� ��������� �����������");
    }
}