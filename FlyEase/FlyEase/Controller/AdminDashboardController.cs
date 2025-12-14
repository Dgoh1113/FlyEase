using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using FlyEase.Services; // Needed to find EmailService
using Microsoft.AspNetCore.Authorization; // <--- Add this

namespace FlyEase.Controllers
{
    [Route("AdminDashboard")]
    [Authorize(Roles = "Admin")] 
    public class AdminDashboardController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminDashboardController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ==========================================
        // 1. MAIN DASHBOARD SUMMARY (FIXED SQL QUERY)
        // ==========================================
        [HttpGet("AdminDashboard")]
        public async Task<IActionResult> AdminDashboard()
        {
            // --- BASIC STATS ---
            var totalUsers = await _context.Users.CountAsync();
            var totalBookings = await _context.Bookings.CountAsync();
            var pendingBookings = await _context.Bookings.CountAsync(b => b.BookingStatus == "Pending");
            var totalRevenue = await _context.Payments.Where(p => p.PaymentStatus == "Completed").SumAsync(p => p.AmountPaid);

            // --- RECENT & LOW STOCK ---
            var recentBookings = await _context.Bookings
                .Include(b => b.User).Include(b => b.Package)
                .OrderByDescending(b => b.BookingDate).Take(5).ToListAsync();

            var lowStock = await _context.Packages.Where(p => p.AvailableSlots < 10).OrderBy(p => p.AvailableSlots).Take(5).ToListAsync();

            // --- 1. GET REVENUE TREND (FIXED: SPLIT SQL & MEMORY) ---
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);

            // Step A: Fetch raw numbers from DB (SQL can handle this)
            var rawRevenueData = await _context.Payments
                .Where(p => p.PaymentDate >= sixMonthsAgo && p.PaymentStatus == "Completed")
                .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Total = g.Sum(p => p.AmountPaid)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync(); // EXECUTE SQL HERE

            // Step B: Convert numbers to Month Names in Memory (C# logic)
            var revenueMonths = rawRevenueData
                .Select(x => new DateTime(x.Year, x.Month, 1).ToString("MMM"))
                .ToList();

            var revenueValues = rawRevenueData
                .Select(x => x.Total)
                .ToList();

            // --- 2. GET BOOKING STATUS COUNTS ---
            var statusCounts = await _context.Bookings
                .GroupBy(b => b.BookingStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // --- 3. POPULATE VIEWMODEL ---
            var vm = new AdminDashboardVM
            {
                TotalUsers = totalUsers,
                TotalBookings = totalBookings,
                PendingBookings = pendingBookings,
                TotalRevenue = totalRevenue,
                RecentBookings = recentBookings,
                LowStockPackages = lowStock,

                // Assign the processed lists for Charts
                RevenueMonths = revenueMonths,
                RevenueValues = revenueValues,

                PendingBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Pending")?.Count ?? 0,
                CompletedBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Completed")?.Count ?? 0,
                CancelledBookingsCount = statusCounts.FirstOrDefault(x => x.Status == "Cancelled")?.Count ?? 0
            };

            return View(vm);
        }

        // ==========================================
        // 2. USERS MANAGEMENT
        // ==========================================
        [HttpGet("Users")]
        public async Task<IActionResult> Users()
        {
            var vm = new UsersPageVM { Users = await _context.Users.OrderByDescending(u => u.CreatedDate).ToListAsync() };
            return View(vm);
        }

        [HttpPost("SaveUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(UsersPageVM model)
        {
            var input = model.CurrentUser;
            if (input.UserID > 0)
            {
                var user = await _context.Users.FindAsync(input.UserID);
                if (user != null)
                {
                    user.FullName = input.FullName;
                    user.Email = input.Email;
                    user.Phone = input.Phone;
                    user.Role = input.Role;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "User updated successfully!";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost("DeleteUser")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.UserID == id))
                    TempData["Error"] = "Cannot delete user with bookings.";
                else
                {
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "User deleted.";
                }
            }
            return RedirectToAction(nameof(Users));
        }

        // ==========================================
        // 3. BOOKINGS MANAGEMENT
        // ==========================================
        [HttpGet("Bookings")]
        public async Task<IActionResult> Bookings(string status = "All")
        {
            var query = _context.Bookings.Include(b => b.User).Include(b => b.Package).AsQueryable();
            if (status != "All" && !string.IsNullOrEmpty(status)) query = query.Where(b => b.BookingStatus == status);

            var vm = new BookingsPageVM { Bookings = await query.OrderByDescending(b => b.BookingDate).ToListAsync() };
            return View(vm);
        }

        [HttpPost("SaveBooking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBooking(BookingsPageVM model)
        {
            var input = model.CurrentBooking;
            if (input.BookingID == 0) return RedirectToAction(nameof(Bookings));

            var booking = await _context.Bookings.FindAsync(input.BookingID);
            if (booking != null)
            {
                booking.BookingStatus = input.BookingStatus;
                booking.TravelDate = input.TravelDate;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking updated!";
            }
            return RedirectToAction(nameof(Bookings));
        }

        [HttpPost("UpdateBookingStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(BookingsPageVM model)
        {
            var input = model.CurrentBooking;

            // 1. Fetch Booking
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .FirstOrDefaultAsync(b => b.BookingID == input.BookingID);

            if (booking != null)
            {
                // 2. Update Status
                booking.BookingStatus = input.BookingStatus;
                await _context.SaveChangesAsync();

                // 3. TRIGGER: Send Email if Completed
                if (booking.BookingStatus == "Completed")
                {
                    var emailService = new EmailService();
                    try
                    {
                        await emailService.SendReviewInvitation(
                            booking.User.Email,
                            booking.User.FullName,
                            booking.BookingID,
                            booking.Package.PackageName
                        );
                        TempData["Success"] = "Booking marked Completed & Review Email Sent!";
                    }
                    catch
                    {
                        TempData["Warning"] = "Booking saved, but Email failed to send. Check credentials.";
                    }
                }
                else
                {
                    TempData["Success"] = "Booking status updated successfully.";
                }
            }

            return RedirectToAction("Bookings");
        }

        [HttpPost("DeleteBooking")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var b = await _context.Bookings.FindAsync(id);
            if (b != null)
            {
                _context.Bookings.Remove(b);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking deleted.";
            }
            return RedirectToAction(nameof(Bookings));
        }

        // ==========================================
        // 4. PACKAGES MANAGEMENT
        // ==========================================
        [HttpGet("Packages")]
        public async Task<IActionResult> Packages()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings).ThenInclude(b => b.Feedbacks)
                .Include(p => p.Itinerary)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            foreach (var p in packages)
            {
                var feedbacks = p.Bookings.SelectMany(b => b.Feedbacks).ToList();
                p.AverageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;
            }

            var vm = new PackagesPageVM
            {
                Packages = packages,
                Categories = await _context.PackageCategories.ToListAsync(),
                CurrentPackage = new Package()
            };
            return View(vm);
        }

        [HttpPost("SavePackage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePackage(PackagesPageVM model)
        {
            var input = model.CurrentPackage;
            var imagePaths = new List<string>();

            if (input.PackageID > 0)
            {
                var existingPkg = await _context.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.PackageID == input.PackageID);
                if (existingPkg != null && !string.IsNullOrEmpty(existingPkg.ImageURL))
                {
                    imagePaths.AddRange(existingPkg.ImageURL.Split(';'));
                }
            }

            if (input.ImageFiles != null && input.ImageFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in input.ImageFiles)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    using (var fileStream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    imagePaths.Add("/img/" + uniqueFileName);
                }
            }
            input.ImageURL = imagePaths.Count > 0 ? string.Join(";", imagePaths) : null;

            if (input.PackageID == 0)
            {
                _context.Packages.Add(input);
                TempData["Success"] = "Package created successfully!";
            }
            else
            {
                var existing = await _context.Packages
                    .Include(p => p.Itinerary)
                    .FirstOrDefaultAsync(p => p.PackageID == input.PackageID);

                if (existing != null)
                {
                    existing.PackageName = input.PackageName;
                    existing.CategoryID = input.CategoryID;
                    existing.Destination = input.Destination;
                    existing.Price = input.Price;
                    existing.StartDate = input.StartDate;
                    existing.EndDate = input.EndDate;
                    existing.AvailableSlots = input.AvailableSlots;
                    existing.Description = input.Description;
                    existing.ImageURL = input.ImageURL;
                    existing.Latitude = input.Latitude;
                    existing.Longitude = input.Longitude;

                    _context.PackageItineraries.RemoveRange(existing.Itinerary);

                    if (input.Itinerary != null)
                    {
                        foreach (var day in input.Itinerary)
                        {
                            if (!string.IsNullOrWhiteSpace(day.Title)) existing.Itinerary.Add(day);
                        }
                    }
                    _context.Packages.Update(existing);
                    TempData["Success"] = "Package updated successfully!";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Packages));
        }

        [HttpPost("DeletePackage")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.PackageID == id))
                {
                    TempData["Error"] = "Cannot delete package: Active bookings exist.";
                }
                else
                {
                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Package deleted successfully.";
                }
            }
            return RedirectToAction(nameof(Packages));
        }

        // ==========================================
        // 5. REPORT MANAGEMENT
        // ==========================================

        [HttpGet("Report")]
        public IActionResult Report()
        {
            return View();
        }

    }
}