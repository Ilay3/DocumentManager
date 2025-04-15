// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ��������� ���� ������ PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// �������� ������� ���� � ��������� �� null
var webRootPath = builder.Environment.WebRootPath ??
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var contentRootPath = builder.Environment.ContentRootPath ??
    Directory.GetCurrentDirectory();

// ��������� ����� � �������� � �������� ������ � ���������������� ����������
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
logger.LogInformation($"TemplatesBasePath ���������: {templatesBasePath}");
logger.LogInformation($"JsonBasePath ���������: {jsonBasePath}");
logger.LogInformation($"������ ���� � �������� Word: {fullTemplatesBasePath}");
logger.LogInformation($"������ ���� � JSON-������: {fullJsonBasePath}");


// ����������� �������� � ������� ������
builder.Services.AddSingleton<IJsonSchemaService>(provider => new JsonSchemaService(fullJsonBasePath));

builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentGenerationService>(provider =>
    new DocumentGenerationService(
        fullTemplatesBasePath,
        outputBasePath,
        provider.GetRequiredService<IDocumentService>()));

// ����������� ������� ������������� ������ � ������� ������
builder.Services.AddTransient<DataInitializer>(provider =>
    new DataInitializer(
        provider.GetRequiredService<ApplicationDbContext>(),
        provider.GetRequiredService<ITemplateService>(),
        provider.GetRequiredService<IJsonSchemaService>(),
        provider.GetRequiredService<ILogger<DataInitializer>>(),
        fullJsonBasePath,
        fullTemplatesBasePath));


var app = builder.Build();

// ����������� ����� ��� �����������
app.Logger.LogInformation($"WebRootPath: {webRootPath}");
app.Logger.LogInformation($"ContentRootPath: {contentRootPath}");
app.Logger.LogInformation($"TemplatesBasePath: {templatesBasePath}");
app.Logger.LogInformation($"JsonBasePath: {jsonBasePath}");
app.Logger.LogInformation($"OutputBasePath: {outputBasePath}");

// �������� ����������� ���������� � ��������� �� ������ ����
if (!string.IsNullOrEmpty(templatesBasePath))
{
    Directory.CreateDirectory(templatesBasePath);
    app.Logger.LogInformation($"������� ����������: {templatesBasePath}");
}
else
{
    app.Logger.LogWarning("���� � �������� Word ������ ��� null");
}

if (!string.IsNullOrEmpty(jsonBasePath))
{
    Directory.CreateDirectory(jsonBasePath);
    app.Logger.LogInformation($"������� ����������: {jsonBasePath}");
}
else
{
    app.Logger.LogWarning("���� � JSON-������ ������ ��� null");
}

if (!string.IsNullOrEmpty(outputBasePath))
{
    Directory.CreateDirectory(outputBasePath);
    app.Logger.LogInformation($"������� ����������: {outputBasePath}");
}
else
{
    app.Logger.LogWarning("���� � ��������������� ������ ������ ��� null");
}

// ����������� �������� � JSON-������ � ������� ����������, ���� ��� �����������
if (!string.IsNullOrEmpty(templatesBasePath) && !Directory.EnumerateFiles(templatesBasePath).Any())
{
    // ����������� �������� Word �� ����� ����������
    var sourceWordTemplates = Path.Combine(contentRootPath, "Templates", "Word");
    if (Directory.Exists(sourceWordTemplates))
    {
        foreach (var file in Directory.GetFiles(sourceWordTemplates, "*.doc*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceWordTemplates, file);
            var targetPath = Path.Combine(templatesBasePath, relativePath);

            // ������� ���������� ��� �������������
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            File.Copy(file, targetPath, true);
            app.Logger.LogInformation($"����������� ������� Word: {targetPath}");
        }
    }
}

if (!string.IsNullOrEmpty(jsonBasePath) && !Directory.EnumerateFiles(jsonBasePath).Any())
{
    // ����������� JSON-������ �� ����� ����������
    var sourceJsonFiles = Path.Combine(contentRootPath, "Templates", "Json");
    if (Directory.Exists(sourceJsonFiles))
    {
        foreach (var file in Directory.GetFiles(sourceJsonFiles, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceJsonFiles, file);
            var targetPath = Path.Combine(jsonBasePath, relativePath);

            // ������� ���������� ��� �������������
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            File.Copy(file, targetPath, true);
            app.Logger.LogInformation($"����������� JSON-�����: {targetPath}");
        }
    }
}

// ������������� ���� ������
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        // ���������� ��������
        app.Logger.LogInformation("���������� �������� ���� ������...");
        context.Database.Migrate();

        // ������������� ������
        app.Logger.LogInformation("������������� ������...");
        var dataInitializer = scope.ServiceProvider.GetRequiredService<DataInitializer>();
        dataInitializer.InitializeAsync().Wait();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "������ ��� ������������� ���� ������");
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