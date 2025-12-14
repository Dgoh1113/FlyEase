using FlyEase.Data;
using FlyEase.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Microsoft.AspNetCore.Localization; // Added for Localization
using System.Globalization; // Added for Localization

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add this line after builder creation
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// ====================================================================
// 1. DYNAMIC PATH SETUP (No App_Data folder)
// ====================================================================
string path = builder.Environment.ContentRootPath;
AppDomain.CurrentDomain.SetData("DataDirectory", path);

// ====================================================================
// 2. REGISTER SERVICES (Modified for Localization)
// ====================================================================

builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
// A. Add Localization Service (Points to "Resources" folder)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// B. Update ControllersWithViews to support View Localization
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

// Configure DbContext
builder.Services.AddDbContext<FlyEaseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, ForgetEmailService>();
// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5); // Shorter for reset links
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
        options.ExpireTimeSpan = TimeSpan.FromHours(1); // Default session length
        options.SlidingExpiration = true;
        
        // Important: Set cookie options
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });


// Add Authorization
builder.Services.AddAuthorization();

// Add distributed memory cache (required for session)
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();



// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ====================================================================
// 4. ADD LOCALIZATION MIDDLEWARE (Multi-Language Support)
// ====================================================================
var supportedCultures = new[] { "en", "ms", "zh-CN" }; // English, Malay, Chinese
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
// ====================================================================

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Add session middleware
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();