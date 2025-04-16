// DocumentManager.Web/Program.cs
using DocumentManager.Core.Interfaces;
using DocumentManager.Infrastructure.Data;
using DocumentManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// ������������ ��������� ��������� ��� ���������� ������ � �������� ���������
Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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
    "Generated";

var fullTemplatesBasePath = Path.IsPathRooted(templatesBasePath) ?
    templatesBasePath : Path.Combine(webRootPath, templatesBasePath);

var fullJsonBasePath = Path.IsPathRooted(jsonBasePath) ?
    jsonBasePath : Path.Combine(webRootPath, jsonBasePath);

var fullOutputBasePath = Path.IsPathRooted(outputBasePath) ?
    outputBasePath : Path.Combine(webRootPath, outputBasePath);

// ��������� �����������
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// ����������� ��������
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


// ����������� ������� ������������� ������
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
app.Logger.LogInformation($"TemplatesBasePath: {templatesBasePath} -> {fullTemplatesBasePath}");
app.Logger.LogInformation($"JsonBasePath: {jsonBasePath} -> {fullJsonBasePath}");
app.Logger.LogInformation($"OutputBasePath: {outputBasePath} -> {fullOutputBasePath}");

// �������� ����������� ����������
Directory.CreateDirectory(fullTemplatesBasePath);
app.Logger.LogInformation($"�������/��������� ���������� ��������: {fullTemplatesBasePath}");

Directory.CreateDirectory(fullJsonBasePath);
app.Logger.LogInformation($"�������/��������� ���������� JSON: {fullJsonBasePath}");

Directory.CreateDirectory(fullOutputBasePath);
app.Logger.LogInformation($"�������/��������� ���������� ������: {fullOutputBasePath}");

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