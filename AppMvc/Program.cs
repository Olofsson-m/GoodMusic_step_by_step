using Services;
using Configuration.Extensions;
using DbContext.Extensions;
using DbRepos;
using Encryption.Extensions;
using Microsoft.Extensions.Options;
using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

//adding support for several secret sources and database sources
//to use either user secrets or azure key vault depending on UseAzureKeyVault tag in appsettings.json
builder.Configuration.AddSecrets(builder.Environment);

//use encryption and multiple Database connections and their respective DbContexts
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddUserBasedDbContext();
builder.Services.AddVersionInfo();
builder.Services.AddEnvironmentInfo();

#region Injecting a dependency service to read MusicWebApi
builder.Services.AddHttpClient(name: "MusicWebApi", configureClient: options =>
{
    options.BaseAddress = new Uri(builder.Configuration["DataService:WebApiBaseUri"]);
    options.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
            mediaType: "application/json",
            quality: 1.0));
});


//Inject DbRepos and Services
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();
builder.Services.AddScoped<ArtistsDbRepos>();

builder.Services.AddScoped<IAdminService, AdminServiceDb>();
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();
builder.Services.AddScoped<IArtistsService, ArtistsServiceDb>();

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    //https://en.wikipedia.org/wiki/HTTP_Strict_Transport_Security
    //https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl
    app.UseHsts();
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute(); // enables conventional routing: {controller=Home}/{action=Index}/{id?}

//Route aliases
app.MapGet("/version", (IOptions<VersionOptions> versionOptions) => Results.Json(versionOptions.Value));
app.MapGet("/static", () => Results.Redirect("/index.html"));

app.Run();

