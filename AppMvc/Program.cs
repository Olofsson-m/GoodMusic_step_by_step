using Services;
using Configuration.Extensions;
using DbContext.Extensions;
using DbRepos;
using Models.Authorization;
using Microsoft.AspNetCore.Authorization;
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

//Add IdentityServices to DbContext.MainDbContext
builder.Services.AddDefaultIdentity<User>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<DbContext.MainDbContext>();

// Configure cookie authentication to redirect to AccountController Login action
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Allow both HTTP and HTTPS in development
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "GoodMusicAuth";
});

#region Injecting a dependency service to read MusicWebApi
builder.Services.AddHttpClient(name: "MusicWebApi", configureClient: options =>
{
    options.BaseAddress = new Uri(builder.Configuration["DataService:WebApiBaseUri"]);
    options.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
            mediaType: "application/json",
            quality: 1.0));
});

//Model Authorization
builder.Services.AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>();

//Used for Identity email verification
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailService>();

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
app.MapGet("/rock", () => Results.Redirect("/Group/ListOfGroups?search=Rock"));
app.MapGet("/queen", () => Results.Redirect("/Group/ListOfGroups?search=Queen"));

app.Run();

