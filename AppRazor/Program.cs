using Services;
using Configuration.Extensions;
using DbContext.Extensions;
using DbRepos;
using Encryption.Extensions;
using Microsoft.Extensions.Options;
using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

//adding support for several secret sources and database sources
//to use either user secrets or azure key vault depending on UseAzureKeyVault tag in appsettings.json
builder.Configuration.AddSecrets(builder.Environment);

//use encryption and multiple Database connections and their respective DbContexts
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddUserBasedDbContext();

// adding version and environment info
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

//using Hsts and https to secure the site
if (!app.Environment.IsDevelopment())
{
    //https://en.wikipedia.org/wiki/HTTP_Strict_Transport_Security
    //https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl
    app.UseHsts();
    app.UseDeveloperExceptionPage();
}
app.UseHttpsRedirection();

//Use static files css, html, and js
app.UseStaticFiles();

//Use endpoint routing
app.UseRouting();

//Use Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

//This is the endpoint, Map Razorpages into Pages folder
app.MapRazorPages();

//Route aliases
app.MapGet("/version", (IOptions<VersionOptions> versionOptions) => Results.Json(versionOptions.Value));
app.MapGet("/static", () => Results.Redirect("/index.html"));
app.MapGet("/rock", () => Results.Redirect("/ListOfGroups?search=Rock"));
app.MapGet("/queen", () => Results.Redirect("/ListOfGroups?search=Queen"));

app.Run();



