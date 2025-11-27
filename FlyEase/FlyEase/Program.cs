using FlyEase.Data;
using FlyEase.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// 1. DYNAMIC PATH SETUP (No App_Data folder)
// This sets |DataDirectory| to the project's root folder
string path = builder.Environment.ContentRootPath;
AppDomain.CurrentDomain.SetData("DataDirectory", path);
// ====================================================================

// Add services...
// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<EmailService>();

// Configure DbContext
builder.Services.AddDbContext<FlyEaseDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// --- ADD THIS BLOCK TO SEED DATABASE ---
// Now 'app' is declared and can be used here
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<FlyEaseDbContext>();
        context.Database.EnsureCreated(); // Ensures DB exists
        DbSeeder.Seed(services);          // Runs our seeder logic
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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