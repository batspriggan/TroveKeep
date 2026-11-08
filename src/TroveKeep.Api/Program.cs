using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
    // Enforces the collection access level (Viewer = read-only) on every write endpoint.
    options.Filters.Add<CollectionWriteGuardFilter>();
});
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

// The cookie session is encrypted with the Data Protection key ring. By default that ring lives
// inside the container (~/.aspnet/DataProtection-Keys) and is regenerated on every recreation,
// which silently invalidates every existing session. Pointing it at a mounted directory keeps
// sessions valid across restarts and redeploys; unset means the ASP.NET Core default (fine for
// local development).
if (builder.Configuration["DataProtection:KeyRingPath"] is { Length: > 0 } keyRingPath)
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

// Optional OIDC login (Pangolin), registered only when explicitly enabled and fully configured.
// The cookie scheme above remains the single session mechanism: OIDC authenticates the user once,
// we then resolve (or auto-provision) the local account and issue the very same application
// cookie the local login does. With the section missing/disabled nothing is added and the app
// behaves exactly as before.
var oidcSettings = builder.Configuration.GetSection(OidcSettings.SectionName).Get<OidcSettings>()
    ?? new OidcSettings();
builder.Services.AddSingleton(oidcSettings);
if (oidcSettings.IsEnabled)
{
    builder.Services
        .AddAuthentication()
        .AddOpenIdConnect(OidcSettings.Scheme, options =>
        {
            options.Authority = oidcSettings.Authority;
            options.ClientId = oidcSettings.ClientId;
            options.ClientSecret = oidcSettings.ClientSecret;
            options.RequireHttpsMetadata = oidcSettings.RequireHttpsMetadata;
            options.ResponseType = "code";
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.CallbackPath = OidcSettings.CallbackPath;
            // Never let the OIDC handler mint a session: it hands us the external principal and
            // we sign the application cookie ourselves (same claims, roles and collection ids).
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

            options.Scope.Clear();
            foreach (var scope in oidcSettings.ParsedScopes) options.Scope.Add(scope);

            options.Events = new OpenIdConnectEvents
            {
                // Provider-side failures (state mismatch, discovery error, user denied consent)
                // must land back on the SPA login page, not on a raw 500.
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect(OidcErrorRedirect(context.Failure?.Message));
                    return Task.CompletedTask;
                },

                // We are the application: resolve the external identity to a local account and
                // emit our own cookie. Anything else is reported as a clean login error.
                OnTicketReceived = async context =>
                {
                    // Suppress the handler's default sign-in; we issue the cookie below.
                    context.HandleResponse();

                    var principal = context.Principal;
                    var subject = principal?.FindFirst("sub")?.Value
                        ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var email = principal?.FindFirst("email")?.Value
                        ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    var displayName = principal?.FindFirst("name")?.Value
                        ?? principal?.FindFirst("preferred_username")?.Value
                        ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                        ?? email;

                    var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                    var cookies = context.HttpContext.RequestServices.GetRequiredService<AuthCookieFactory>();

                    var result = await auth.ResolveExternalUserAsync(
                        AuthService.OidcProvider, subject ?? string.Empty, email, displayName ?? string.Empty);

                    if (!result.Success || result.User is null)
                    {
                        context.Response.Redirect(OidcErrorRedirect(result.Error));
                        return;
                    }

                    await cookies.SignInAsync(context.HttpContext, result.User);
                    context.Response.Redirect("/");
                },
            };
        });
}
else if (oidcSettings.Enabled)
{
    Console.WriteLine(
        "Warning: Auth:Oidc:Enabled is true but Authority/ClientId are empty; OIDC login stays disabled.");
}

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
// Access levels come from collection_members, not from the cookie claims: see ICollectionMembershipCache.
builder.Services.AddScoped<ICollectionMembershipCache, CollectionMembershipCache>();
builder.Services.AddScoped<ICollectionScope, CollectionScope>();
builder.Services.AddScoped<ICollectionWriteTargetResolver, CollectionWriteTargetResolver>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<AuthCookieFactory>();
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

// Application mode (first-run setup) is process-wide state kept in meta/_id="app_config"; the
// technical principal of single-user mode is likewise built once and cached.
builder.Services.AddSingleton<IAppStateService, AppStateService>();
builder.Services.AddSingleton<TechnicalUserProvider>();

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
    builder.Configuration["Migration:BackupDir"],
    builder.Configuration["Migration:DefaultCollectionName"]
).RunAsync();
Console.WriteLine("Database migrations completed.");

// First-run provisioning is migration 006's job (default collection + non-loggable technical
// account). Here we only decide the application mode and, while the instance still needs to be
// configured, print the one-shot setup token the operator must pass to POST /api/setup.
var appState = app.Services.GetRequiredService<IAppStateService>();
var initialState = await appState.GetAsync();
if (!initialState.Initialized || !initialState.MultiUserEnabled)
{
    var setupToken = appState.EnsureSetupToken();
    app.Logger.LogWarning(
        "TroveKeep setup token: {Token} (required by POST /api/setup while the instance is not configured)",
        setupToken);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
// Must sit between authentication and authorization: it may reject an uninitialized instance or
// replace the principal with the technical account of single-user mode.
app.UseMiddleware<AppModeMiddleware>();
// Primes the caller's collection access levels for this request (sync reads afterwards); sits
// before authorization because the write guard filter consults them.
app.UseMiddleware<CollectionMembershipMiddleware>();
app.UseAuthorization();
app.MapControllers();
// Serves index.html for every non-API path (including /login and /setup), so the SPA router takes
// over. The shell must be readable without a session, otherwise a logged-out instance answers 401
// to the browser and renders a blank page with no way back to the login form.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

// Sends the SPA back to /login with a machine-readable reason instead of a bare 500 when the
// OIDC round-trip cannot be completed.
static string OidcErrorRedirect(string? reason) =>
    "/login?error=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(reason) ? "oidc_login_failed" : reason);
