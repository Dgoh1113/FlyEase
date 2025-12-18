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

        [HttpPost("UpdateBookingStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBookingStatus(BookingsPageVM model)
        {
            var input = model.CurrentBooking;

            // Include Payments to process refunds
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.BookingID == input.BookingID);

            if (booking != null)
            {
                string oldStatus = booking.BookingStatus;
                string newStatus = input.BookingStatus;

                // --- 1. PREVENT MANUALLY SETTING TO 'COMPLETED' ---
                if (newStatus == "Completed")
                {
                    TempData["Error"] = "You cannot manually mark a booking as Completed. This is done automatically.";
                    return RedirectToAction("Bookings");
                }

                // --- 2. PREVENT EDITING COMPLETED OR CANCELLED BOOKINGS ---
                if (oldStatus == "Completed" || oldStatus == "Cancelled")
                {
                    TempData["Error"] = $"Cannot modify a booking that is already {oldStatus}.";
                    return RedirectToAction("Bookings");
                }

                // --- 3. UPDATE STATUS ---
                booking.BookingStatus = newStatus;
                await _context.SaveChangesAsync();

                // --- 4. LOGIC FOR CANCELLED -> REFUND & EMAIL ---
                if (newStatus == "Cancelled" && oldStatus != "Cancelled")
                {
                    // Only auto-refund if confirmed/deposit
                    if (oldStatus == "Confirmed" || oldStatus == "Deposit")
                    {
                        try
                        {
                            // Find a completed payment to refund
                            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == "Completed");

                            // Check if transaction ID exists (Implies Stripe/Online Payment)
                            if (payment != null && !string.IsNullOrEmpty(payment.TransactionID))
                            {
                                // A. Process Refund via Stripe
                                await _stripeService.RefundPaymentAsync(payment.TransactionID);

                                // B. Update Payment Record
                                payment.PaymentStatus = "Refunded";
                                await _context.SaveChangesAsync();

                                // C. Send Refund Email
                                await _emailService.SendRefundNotification(
                                    booking.User.Email,
                                    booking.User.FullName,
                                    booking.Package.PackageName,
                                    payment.AmountPaid
                                );

                                TempData["Success"] = "Booking Cancelled, Payment Refunded (Stripe), and Email Sent.";
                            }
                            else
                            {
                                // Cash or Manual Payment (No Transaction ID)
                                TempData["Warning"] = "Booking Cancelled. Manual refund required (Cash/No Online Transaction Found).";
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["Error"] = "Error processing refund: " + ex.Message;
                        }
                    }
                    else
                    {
                        // e.g. "Pending" bookings don't get refunds
                        TempData["Success"] = $"Booking Cancelled. No refund processed (Previous status was '{oldStatus}').";
                    }
                }
                // --- 5. LOGIC FOR CONFIRMED ---
                else if (newStatus == "Confirmed")
                {
                    TempData["Success"] = "Booking Confirmed successfully.";
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
            var package = await _context.Packages.FindAsync(id);
            if (package != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.PackageID == id))
                    TempData["Error"] = "Cannot delete package: Active bookings exist.";
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
        // 5. ANALYTICS
        // ==========================================
        [HttpGet("Analytics")]
        public async Task<IActionResult> Analytics()
        {
            var allFeedback = await _context.Feedbacks.Include(f => f.User).Include(f => f.Booking).ThenInclude(b => b.Package).OrderByDescending(f => f.CreatedDate).ToListAsync();
            if (!allFeedback.Any()) return View(new FeedbackAnalyticsViewModel());

            var totalReviews = allFeedback.Count;
            var averageRating = allFeedback.Average(f => f.Rating);
            var positiveCount = allFeedback.Count(f => f.Rating >= 4);
            var positivePercentage = totalReviews > 0 ? (double)positiveCount / totalReviews * 100 : 0;

            var packageStats = allFeedback.GroupBy(f => f.Booking.Package.PackageName)
                .Select(g => new PopularPackageViewModel { PackageName = g.Key, AverageRating = g.Average(f => f.Rating), ReviewCount = g.Count() }).ToList();

            var mostPopular = packageStats.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.ReviewCount).FirstOrDefault();
            var leastPopular = packageStats.OrderBy(p => p.AverageRating).ThenByDescending(p => p.ReviewCount).FirstOrDefault();

            var ratingCounts = new Dictionary<int, int> { { 5, allFeedback.Count(f => f.Rating == 5) }, { 4, allFeedback.Count(f => f.Rating == 4) }, { 3, allFeedback.Count(f => f.Rating == 3) }, { 2, allFeedback.Count(f => f.Rating == 2) }, { 1, allFeedback.Count(f => f.Rating == 1) } };

            var viewModel = new FeedbackAnalyticsViewModel { AverageRating = averageRating, TotalReviews = totalReviews, PositivePercentage = positivePercentage, RatingCounts = ratingCounts, RecentReviews = allFeedback.Take(10).ToList(), MostPopularPackage = mostPopular, LeastPopularPackage = leastPopular };
            return View(viewModel);
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