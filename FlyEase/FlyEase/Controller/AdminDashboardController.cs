using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlyEase.Services;
using X.PagedList;

namespace FlyEase.Controllers
{
    [Route("AdminDashboard")]
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly EmailService _emailService;
        private readonly StripeService _stripeService;

        public AdminDashboardController(FlyEaseDbContext context, IWebHostEnvironment environment, EmailService emailService, StripeService stripeService)
        {
            _context = context;
            _environment = environment;
            _emailService = emailService;
            _stripeService = stripeService;
        }

        // ==========================================
        // 1. MAIN DASHBOARD SUMMARY
        // ==========================================
        [HttpGet("AdminDashboard")]
        public async Task<IActionResult> AdminDashboard()
        {
            // [UPDATED] Only count users with Role == "User"
            var totalUsers = await _context.Users.CountAsync(u => u.Role == "User");

            var totalBookings = await _context.Bookings.CountAsync();
            var pendingBookings = await _context.Bookings.CountAsync(b => b.BookingStatus == "Pending");
            var totalRevenue = await _context.Payments.Where(p => p.PaymentStatus == "Completed").SumAsync(p => p.AmountPaid);

            var lowStock = await _context.Packages.Where(p => p.AvailableSlots < 10).OrderBy(p => p.AvailableSlots).Take(5).ToListAsync();

            var recentBookings = await _context.Bookings
                .Include(b => b.User).Include(b => b.Package)
                .OrderByDescending(b => b.BookingDate).Take(5).ToListAsync();

            var vm = new AdminDashboardVM
            {
                TotalUsers = totalUsers,
                TotalBookings = totalBookings,
                PendingBookings = pendingBookings,
                TotalRevenue = totalRevenue,
                RecentBookings = recentBookings,
                LowStockPackages = lowStock
            };

            return View(vm);
        }

        // ==========================================
        // 2. USERS MANAGEMENT
        // ==========================================
        [HttpGet("Users")]
        public async Task<IActionResult> Users(string? search = null, string? role = null, int? page = 1)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;

            var query = _context.Users.AsQueryable();

            // Filter: Exclude Admins
            query = query.Where(u => u.Role != "Admin");

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u =>
                    u.UserID.ToString().Contains(search) ||
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search));
            }

            // Filter by role
            if (!string.IsNullOrEmpty(role) && role != "All")
            {
                query = query.Where(u => u.Role == role);
            }

            var totalItems = await query.CountAsync();
            var pagedData = await query
                .OrderByDescending(u => u.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new UsersPageVM
            {
                Users = new StaticPagedList<User>(pagedData, pageNumber, pageSize, totalItems),
                SearchTerm = search,
                RoleFilter = role
            };
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

        [HttpPost("BanUser")]
        public async Task<IActionResult> BanUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Role = "Ban";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"User {user.FullName} has been banned.";
            }
            else
            {
                TempData["Error"] = "User not found.";
            }
            return RedirectToAction(nameof(Users));
        }

        [HttpPost("UnbanUser")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.Role = "User"; // Reset to default role
                await _context.SaveChangesAsync();
                TempData["Success"] = $"User {user.FullName} has been unbanned.";
            }
            else
            {
                TempData["Error"] = "User not found.";
            }
            return RedirectToAction(nameof(Users));
        }

        // ==========================================
        // 3. BOOKINGS MANAGEMENT
        // ==========================================
        [HttpGet("Bookings")]
        public async Task<IActionResult> Bookings(string? search = null, string status = "All", int? page = 1)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;

            var query = _context.Bookings.Include(b => b.User).Include(b => b.Package).AsQueryable();

            if (status != "All" && !string.IsNullOrEmpty(status))
                query = query.Where(b => b.BookingStatus == status);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(b => b.BookingID.ToString().Contains(search) || b.Package.PackageName.Contains(search));

            var totalItems = await query.CountAsync();
            var pagedData = await query
                .OrderByDescending(b => b.BookingDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new BookingsPageVM
            {
                Bookings = new StaticPagedList<Booking>(pagedData, pageNumber, pageSize, totalItems),
                SearchTerm = search,
                StatusFilter = status
            };
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
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking updated!";
            }
            return RedirectToAction(nameof(Bookings));
        }

        // ... inside UpdateBookingStatus method ...

        [HttpPost("UpdateBookingStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(BookingsPageVM model)
        {
            var input = model.CurrentBooking;

            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == input.BookingID);

            if (booking == null)
            {
                return Json(new { success = false, message = "Booking not found." });
            }

            string oldStatus = booking.BookingStatus;
            string newStatus = input.BookingStatus;

            // --- VALIDATION RULES ---

            // 1. Confirmed -> Pending blocked
            if (oldStatus == "Confirmed" && newStatus == "Pending")
            {
                return Json(new { success = false, message = "Action Denied: A 'Confirmed' booking cannot be reverted to 'Pending'." });
            }

            // 2. Editing Completed/Cancelled blocked
            if (oldStatus == "Completed" || oldStatus == "Cancelled")
            {
                return Json(new { success = false, message = $"Cannot modify a booking that is already {oldStatus}." });
            }

            // [REMOVED THE BLOCK PREVENTING "COMPLETED" STATUS] 
            // We now allow manual update to Completed as requested.

            // --- PROCESS UPDATE ---
            booking.BookingStatus = newStatus;

            // If setting to Completed, ensure PaymentStatus is also completed/updated if necessary? 
            // (Optional, but usually if a booking is completed, payment is assumed done. 
            //  For now, we just update the status as requested).

            await _context.SaveChangesAsync();

            string successMsg = "Booking status updated successfully.";

            // --- REFUND LOGIC (If cancelling a Paid/Confirmed booking) ---
            if (newStatus == "Cancelled" && (oldStatus == "Confirmed" || oldStatus == "Deposit"))
            {
                try
                {
                    var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == "Completed");
                    if (payment != null && !string.IsNullOrEmpty(payment.TransactionID))
                    {
                        // Refund via Stripe
                        await _stripeService.RefundPaymentAsync(payment.TransactionID);

                        payment.PaymentStatus = "Refunded";
                        await _context.SaveChangesAsync();

                        // Send Email
                        await _emailService.SendRefundNotification(
                            booking.User.Email,
                            booking.User.FullName,
                            booking.Package.PackageName,
                            payment.AmountPaid
                        );
                        successMsg = "Booking Cancelled & Refunded successfully.";
                    }
                    else
                    {
                        successMsg = "Booking Cancelled. Manual refund required (No Online Transaction found).";
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = true, bookingId = booking.BookingID, newStatus = newStatus, message = "Booking Cancelled, but refund failed: " + ex.Message });
                }
            }

            return Json(new { success = true, bookingId = booking.BookingID, newStatus = newStatus, message = successMsg });
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
        public async Task<IActionResult> Packages(string? search = null, int? page = 1)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;

            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings).ThenInclude(b => b.Feedbacks)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.PackageName.Contains(search));

            var totalItems = await query.CountAsync();
            var pagedData = await query.OrderByDescending(p => p.PackageID).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            foreach (var p in pagedData)
            {
                var ratings = p.Bookings.SelectMany(b => b.Feedbacks).Select(f => f.Rating);
                p.AverageRating = ratings.Any() ? ratings.Average() : 0;
            }

            var vm = new PackagesPageVM
            {
                Packages = new StaticPagedList<Package>(pagedData, pageNumber, pageSize, totalItems),
                Categories = await _context.PackageCategories.ToListAsync(),
                CurrentPackage = new Package(),
                SearchTerm = search
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
                    imagePaths.AddRange(existingPkg.ImageURL.Split(';'));
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
                var existing = await _context.Packages.FirstOrDefaultAsync(p => p.PackageID == input.PackageID);
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
            // Include Bookings to enable cascade delete
            var package = await _context.Packages
                .Include(p => p.Bookings)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package != null)
            {
                // Logic: If user confirmed via the 3-second alert, we assume they want to delete everything.
                if (package.Bookings.Any())
                {
                    _context.Bookings.RemoveRange(package.Bookings);
                }

                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Package and its associated bookings were deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Package not found.";
            }
            return RedirectToAction(nameof(Packages));
        }

        // ==========================================
        // 5. ANALYTICS (ADDED THIS METHOD)
        // ==========================================
        [HttpGet("Analytics")]
        public async Task<IActionResult> Analytics()
        {
            // 1. Fetch All Feedbacks
            var allFeedbacks = await _context.Feedbacks
                .Include(f => f.Booking).ThenInclude(b => b.Package)
                .Include(f => f.User)
                .ToListAsync();

            var vm = new FeedbackAnalyticsViewModel();

            if (allFeedbacks.Any())
            {
                // Basic Stats
                vm.TotalReviews = allFeedbacks.Count;
                vm.AverageRating = allFeedbacks.Average(f => f.Rating);
                vm.PositivePercentage = (double)allFeedbacks.Count(f => f.Rating >= 4) / vm.TotalReviews * 100;
                vm.RatingCounts = allFeedbacks.GroupBy(f => f.Rating).ToDictionary(g => g.Key, g => g.Count());

                // Popular Packages Logic
                var packageStats = allFeedbacks.GroupBy(f => f.Booking.Package.PackageName)
                    .Select(g => new { Name = g.Key, Rating = g.Average(f => f.Rating), Count = g.Count() }).ToList();

                var best = packageStats.OrderByDescending(p => p.Rating).FirstOrDefault();
                var worst = packageStats.OrderBy(p => p.Rating).FirstOrDefault();

                if (best != null) vm.MostPopularPackage = new PopularPackageViewModel { PackageName = best.Name, AverageRating = best.Rating, ReviewCount = best.Count };
                if (worst != null) vm.LeastPopularPackage = new PopularPackageViewModel { PackageName = worst.Name, AverageRating = worst.Rating, ReviewCount = worst.Count };

                // --- CATEGORY LOGIC (Parses [Tag] from Comment) ---
                var categoryData = new List<(string Cat, int Rating)>();

                foreach (var f in allFeedbacks)
                {
                    string cat = "General";
                    string text = f.Comment?.ToLower() ?? "";

                    // Extract tag: "[Food] Comment..."
                    if (f.Comment != null && f.Comment.StartsWith("[") && f.Comment.Contains("]"))
                    {
                        int end = f.Comment.IndexOf("]");
                        cat = f.Comment.Substring(1, end - 1);
                    }
                    else
                    {
                        // Fallback Keywords
                        if (text.Contains("food") || text.Contains("meal")) cat = "Food";
                        else if (text.Contains("service") || text.Contains("staff")) cat = "Service";
                        else if (text.Contains("view") || text.Contains("room")) cat = "Environment";
                    }
                    categoryData.Add((cat, f.Rating));
                }

                var catGroups = categoryData.GroupBy(x => x.Cat);
                foreach (var grp in catGroups)
                {
                    vm.CategoryRatings.Add(grp.Key, grp.Average(x => x.Rating));
                    vm.CategoryCounts.Add(grp.Key, grp.Count());
                }

                // Recent Reviews (Clean comments for display)
                vm.RecentReviews = allFeedbacks.OrderByDescending(f => f.CreatedDate).Take(5).Select(f => {
                    if (f.Comment != null && f.Comment.StartsWith("["))
                    {
                        int end = f.Comment.IndexOf("]");
                        if (end > 0) f.Comment = f.Comment.Substring(end + 1).Trim();
                    }
                    return f;
                }).ToList();
            }

            // --- UNRATED USERS LOGIC ---
            // 1. Get Completed Bookings
            var completedBookings = await _context.Bookings
                .Include(b => b.User).Include(b => b.Package)
                .Where(b => b.BookingStatus == "Completed" || b.BookingStatus == "Confirmed")
                .ToListAsync();

            // 2. Filter out those who already rated
            var ratedBookingIds = allFeedbacks.Select(f => f.BookingID).ToHashSet();

            var unratedList = completedBookings
                .Where(b => !ratedBookingIds.Contains(b.BookingID) && b.TravelDate < DateTime.Now)
                .OrderByDescending(b => b.TravelDate)
                .ToList();

            vm.UnratedCount = unratedList.Count;
            vm.UnratedBookings = unratedList.Take(10).ToList();

            return View(vm);
        }

        // ==========================================
        // 6. DISCOUNTS MANAGEMENT
        // ==========================================

        [HttpGet("Discounts")]
        public async Task<IActionResult> Discounts(string? search = null, int? page = 1)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var query = _context.DiscountTypes.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d => d.DiscountName.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var pagedData = await query
                .OrderByDescending(d => d.IsActive)
                .ThenBy(d => d.DiscountName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new DiscountPageVM
            {
                Discounts = new StaticPagedList<DiscountType>(pagedData, pageNumber, pageSize, totalItems),
                SearchTerm = search,
                CurrentDiscount = new DiscountType()
            };

            return View(vm);
        }

        [HttpPost("SaveDiscount")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDiscount(DiscountPageVM model)
        {
            ModelState.Remove("Discounts");
            ModelState.Remove("SearchTerm");

            var input = model.CurrentDiscount;

            if (string.IsNullOrEmpty(input.DiscountName))
                ModelState.AddModelError("CurrentDiscount.DiscountName", "Discount Name is required.");

            if (input.DiscountRate == null && input.DiscountAmount == null)
                ModelState.AddModelError("CurrentDiscount.DiscountAmount", "Please specify either a Percentage Rate or a Fixed Amount.");

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                                        .SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage));

                TempData["Error"] = "Unable to save: " + errors;
                return RedirectToAction(nameof(Discounts));
            }

            if (input.DiscountTypeID == 0)
            {
                _context.DiscountTypes.Add(input);
                TempData["Success"] = "Discount created successfully!";
            }
            else
            {
                var existing = await _context.DiscountTypes.FindAsync(input.DiscountTypeID);
                if (existing != null)
                {
                    existing.DiscountName = input.DiscountName;
                    existing.DiscountRate = input.DiscountRate;
                    existing.DiscountAmount = input.DiscountAmount;
                    existing.MinPax = input.MinPax;
                    existing.MinSpend = input.MinSpend;
                    existing.StartDate = input.StartDate;
                    existing.EndDate = input.EndDate;
                    existing.IsActive = input.IsActive;

                    existing.AgeLimit = input.AgeLimit;
                    existing.AgeCriteria = input.AgeCriteria;
                    existing.EarlyBirdDays = input.EarlyBirdDays;

                    _context.DiscountTypes.Update(existing);
                    TempData["Success"] = "Discount updated successfully!";
                }
                else
                {
                    TempData["Error"] = "Discount not found.";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Discounts));
        }

        [HttpPost("DeleteDiscount")]
        public async Task<IActionResult> DeleteDiscount(int id)
        {
            var discount = await _context.DiscountTypes.FindAsync(id);
            if (discount != null)
            {
                bool isUsed = await _context.BookingDiscounts.AnyAsync(bd => bd.DiscountTypeID == id);
                if (isUsed)
                {
                    TempData["Error"] = "Cannot delete: Discount is used in existing bookings.";
                }
                else
                {
                    _context.DiscountTypes.Remove(discount);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Discount deleted successfully.";
                }
            }
            return RedirectToAction(nameof(Discounts));
        }
    }
}