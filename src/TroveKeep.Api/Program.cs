using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Repositories;
using TroveKeep.Services;

// Must be registered before MongoClient is created
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// MongoDB
var mongoSettings = builder.Configuration.GetSection("MongoDb").Get<MongoDbSettings>()!;
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));

// Repositories
builder.Services.AddScoped<ILegoSetRepository, LegoSetRepository>();
builder.Services.AddScoped<IBulkPieceRepository, BulkPieceRepository>();
builder.Services.AddScoped<IBoxRepository, BoxRepository>();
builder.Services.AddScoped<IDrawerContainerRepository, DrawerContainerRepository>();
builder.Services.AddScoped<IDrawerRepository, DrawerRepository>();
builder.Services.AddScoped<IColorRepository, ColorRepository>();
builder.Services.AddScoped<ISetArchiveRepository, SetArchiveRepository>();
builder.Services.AddScoped<IPartArchiveRepository, PartArchiveRepository>();
builder.Services.AddScoped<IPartInventoryArchiveRepository, PartInventoryArchiveRepository>();
builder.Services.AddScoped<IPartCategoryRepository, PartCategoryRepository>();
builder.Services.AddScoped<IImageRepository, ImageRepository>();
builder.Services.AddScoped<ITableTemplateRepository, TableTemplateRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBaseplateRepository, BaseplateRepository>();

// Services
builder.Services.AddScoped<ILegoSetService, LegoSetService>();
builder.Services.AddScoped<IBulkPieceService, BulkPieceService>();
builder.Services.AddScoped<IBoxService, BoxService>();
builder.Services.AddScoped<IDrawerContainerService, DrawerContainerService>();
builder.Services.AddScoped<IDrawerService, DrawerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IRoomExportService, RoomExportService>();
builder.Services.AddScoped<ITableTemplateService, TableTemplateService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBaseplateService, BaseplateService>();
builder.Services.AddHttpClient("SetImages");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
