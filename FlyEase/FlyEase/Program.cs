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
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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
    });

// Add Authorization
builder.Services.AddAuthorization();

// Add distributed memory cache (required for session)
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Call the seeder here
        DbSeeder.Seed(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// [Add this in Program.cs after var app = builder.Build();]
// ====================================================================
// 3. SEEDING DATABASE
// ====================================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FlyEaseDbContext>();
    context.Database.EnsureCreated();

    // Check if we need to seed data
    if (!context.Packages.Any())
    {
        Console.WriteLine("Seeding database with sample data...");

        // Add sample categories
        var beachCategory = new PackageCategory { CategoryName = "Beach Vacation" };
        var mountainCategory = new PackageCategory { CategoryName = "Mountain Adventure" };
        var cityCategory = new PackageCategory { CategoryName = "City Tour" };

        context.PackageCategories.AddRange(beachCategory, mountainCategory, cityCategory);
        await context.SaveChangesAsync();

        // Add sample packages
        var packages = new List<Package>
        {
            new Package
            {
                PackageName = "Langkawi Island Paradise",
                CategoryID = beachCategory.CategoryID,
                Description = "Beautiful beach resort with crystal clear waters",
                Destination = "Langkawi",
                Price = 1200.00m,
                StartDate = DateTime.Now.AddDays(7),
                EndDate = DateTime.Now.AddDays(14),
                AvailableSlots = 20,
                ImageURL = "/img/default-package.jpg"
            },
            new Package
            {
                PackageName = "Cameron Highlands Retreat",
                CategoryID = mountainCategory.CategoryID,
                Description = "Cool mountain escape with tea plantations",
                Destination = "Cameron Highlands",
                Price = 800.00m,
                StartDate = DateTime.Now.AddDays(14),
                EndDate = DateTime.Now.AddDays(21),
                AvailableSlots = 15,
                ImageURL = "/img/default-package.jpg"
            },
            new Package
            {
                PackageName = "Kuala Lumpur City Tour",
                CategoryID = cityCategory.CategoryID,
                Description = "Explore the vibrant capital city",
                Destination = "Kuala Lumpur",
                Price = 500.00m,
                StartDate = DateTime.Now.AddDays(5),
                EndDate = DateTime.Now.AddDays(8),
                AvailableSlots = 25,
                ImageURL = "/img/default-package.jpg"
            }
        };

        context.Packages.AddRange(packages);
        await context.SaveChangesAsync();

        // Add sample inclusions
        var inclusions = new List<PackageInclusion>
        {
            new PackageInclusion { PackageID = packages[0].PackageID, InclusionItem = "5-star accommodation" },
            new PackageInclusion { PackageID = packages[0].PackageID, InclusionItem = "Breakfast included" },
            new PackageInclusion { PackageID = packages[0].PackageID, InclusionItem = "Airport transfers" },

            new PackageInclusion { PackageID = packages[1].PackageID, InclusionItem = "Mountain resort stay" },
            new PackageInclusion { PackageID = packages[1].PackageID, InclusionItem = "Tea plantation tour" },

            new PackageInclusion { PackageID = packages[2].PackageID, InclusionItem = "City hotel accommodation" },
            new PackageInclusion { PackageID = packages[2].PackageID, InclusionItem = "Guided city tour" }
        };

        context.PackageInclusions.AddRange(inclusions);

        // Add sample discounts
        var discounts = new List<DiscountType>
        {
            new DiscountType { DiscountName = "Early Bird", DiscountRate = 10 },
            new DiscountType { DiscountName = "Bulk Discount", DiscountRate = 15 },
            new DiscountType { DiscountName = "Senior Citizen", DiscountRate = 20 },
            new DiscountType { DiscountName = "Junior Price", DiscountRate = 50 }
        };

        context.DiscountTypes.AddRange(discounts);
        await context.SaveChangesAsync();

        Console.WriteLine("Database seeded successfully!");
    }
}

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