using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TroveKeep.Api.Auth;
using TroveKeep.Api.Filters;
using TroveKeep.Core.Interfaces.Repositories;
using TroveKeep.Core.Interfaces.Services;
using TroveKeep.Migrations;
using TroveKeep.Repositories;
using TroveKeep.Services;

// Must be registered before MongoClient is created
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>());
builder.Services.AddOpenApi();

// Multi-user auth: cookie sessions (not bearer tokens) because the SPA is served same-origin.
// The fallback policy locks everything down by default; public endpoints opt out with
// [AllowAnonymous] explicitly.
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "trovekeep.sid";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        // The SPA wants a 401 it can react to, not an HTML redirect to a non-existent login page.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Mongo cookie serialization of the user principal is not needed: the collection ids travel as
// plain string claims (see CurrentUser), so no custom claim type mapping is required.

// MongoDB
var mongoSettings = builder.Configuration.GetSection("MongoDb").Get<MongoDbSettings>()!;
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<ICollectionMemberRepository, CollectionMemberRepository>();
builder.Services.AddScoped<IAllocationRepository, AllocationRepository>();
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
builder.Services.AddScoped<ILabelTargetRepository, LabelTargetRepository>();
builder.Services.AddScoped<ILabelPrintConfigRepository, LabelPrintConfigRepository>();
builder.Services.AddScoped<ISetPhotoRepository, SetPhotoRepository>();
builder.Services.AddScoped<ITableTemplateRepository, TableTemplateRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBaseplateRepository, BaseplateRepository>();

// Services
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICollectionScope, CollectionScope>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<AuthCookieFactory>();
builder.Services.AddScoped<BootstrapService>();
builder.Services.AddScoped<ILegoSetService, LegoSetService>();
builder.Services.AddScoped<IBulkPieceService, BulkPieceService>();
builder.Services.AddScoped<IBoxService, BoxService>();
builder.Services.AddScoped<IDrawerContainerService, DrawerContainerService>();
builder.Services.AddScoped<IDrawerService, DrawerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<ILabelTargetService, LabelTargetService>();
builder.Services.AddScoped<ISetPhotoService, SetPhotoService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IRoomExportService, RoomExportService>();
builder.Services.AddScoped<ITableTemplateService, TableTemplateService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBaseplateService, BaseplateService>();
builder.Services.AddScoped<IPlanningService, PlanningService>();
builder.Services.AddScoped<IMocReconciliationService, MocReconciliationService>();
builder.Services.AddHttpClient("SetImages");
builder.Services.AddHttpClient("LabelServer");

// Label printing (client download vs remote label-tool server)
var labelPrintSettings = builder.Configuration.GetSection("LabelTool").Get<LabelPrintSettings>() ?? new LabelPrintSettings();
builder.Services.AddSingleton(labelPrintSettings);
builder.Services.AddScoped<ILabelPrintService, LabelPrintService>();
builder.Services.AddScoped<ILabelServerClient, LabelServerClient>();
builder.Services.AddKeyedScoped<ILabelImageResolver>(LabelImageResolver.ServerKey,
    (sp, _) => new LabelImageResolver(sp.GetRequiredService<IImageService>(), labelPrintSettings, inline: true));
builder.Services.AddKeyedScoped<ILabelImageResolver>(LabelImageResolver.ClientKey,
    (sp, _) => new LabelImageResolver(sp.GetRequiredService<IImageService>(), labelPrintSettings, inline: false));
builder.Services.AddScoped<ILabelDispatchService, LabelDispatchService>();
builder.Services.AddScoped<IScannerService, ScannerService>();

var app = builder.Build();

Console.WriteLine("Running database migrations...");
await new MigrationRunner(
    app.Services.GetRequiredService<IMongoDatabase>(),
    builder.Configuration["Migration:BackupDir"]
).RunAsync();
Console.WriteLine("Database migrations completed.");

// First-run provisioning: create the admin and its default collection membership (see
// BootstrapService). No-op once at least one user exists. Resolved from an explicit scope:
// it depends on scoped repositories, which cannot be resolved from the root provider.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<BootstrapService>().RunAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Serves index.html for every non-API path (including /login), so the SPA router takes over.
app.MapFallbackToFile("index.html");

app.Run();
