using FlyEase.Data;
using FlyEase.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Globalization;
using FlyEase.Model;
using Microsoft.AspNetCore.StaticFiles; // 1. ADD THIS NAMESPACE
var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Stripe Settings
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// 1. REGISTER SERVICES
// ====================================================================
builder.Services.AddScoped<StripeService>();
builder.Services.AddTransient<EmailService>();
builder.Services.AddScoped<IEmailService, ForgetEmailService>();
// ====================================================================

string path = builder.Environment.ContentRootPath;
AppDomain.CurrentDomain.SetData("DataDirectory", path);

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// === 1. ADD LOCALIZATION SERVICES (Updated) ===
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Configure DbContext
builder.Services.AddDbContext<FlyEaseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "FlyEase.Session";
});

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FlyEase.Auth";
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// === 2. CONFIGURE AUTOMATIC LOCALIZATION MIDDLEWARE (Updated) ===
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("ms"),
    new CultureInfo("zh-CN")
};

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"), // Default if no other match found
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

// This enables Automatic switching via QueryString, Cookie, and Browser Header
app.UseRequestLocalization(localizationOptions);


// === 3. CONFIGURE STATIC FILES FOR AVIF (Updated) ===
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".avif"] = "image/avif";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();