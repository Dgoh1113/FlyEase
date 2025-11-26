using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace FlyEase.Controllers
{
    [Route("StaffDashboard")]
    // [Authorize(Roles = "Staff")] // Uncomment to secure
    public class StaffDashboardController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public StaffDashboardController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ... (Dashboard, Users, Bookings methods - kept same as before) ...
        // (Including them briefly so the file is complete)

        [HttpGet("StaffDashboard")]
        public async Task<IActionResult> StaffDashboard()
        {
            var vm = new StaffDashboardVM
            {
                TotalUsers = await _context.Users.CountAsync(u => u.Role == "User"),
                TotalBookings = await _context.Bookings.CountAsync(),
                PendingBookings = await _context.Bookings.CountAsync(b => b.BookingStatus == "Pending"),
                TotalRevenue = await _context.Payments.Where(p => p.PaymentStatus == "Completed").SumAsync(p => p.AmountPaid),
                RecentBookings = await _context.Bookings.Include(b => b.User).Include(b => b.Package).OrderByDescending(b => b.BookingDate).Take(5).ToListAsync(),
                LowStockPackages = await _context.Packages.Where(p => p.AvailableSlots < 10).OrderBy(p => p.AvailableSlots).Take(5).ToListAsync()
            };
            return View(vm);
        }

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
            var booking = await _context.Bookings.FindAsync(input.BookingID);
            if (booking != null)
            {
                booking.BookingStatus = input.Status;
                booking.TravelDate = input.TravelDate;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Booking updated!";
            }
            return RedirectToAction(nameof(Bookings));
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
        // 3. PACKAGES MANAGEMENT (FIXED)
        // ==========================================
        [HttpGet("Packages")]
        public async Task<IActionResult> Packages()
        {
            var vm = new PackagesPageVM
            {
                Packages = await _context.Packages
                    .Include(p => p.Category)
                    .Include(p => p.PackageInclusions)
                    .OrderByDescending(p => p.PackageID)
                    .ToListAsync(),
                Categories = await _context.PackageCategories.ToListAsync()
            };
            return View(vm);
        }

        [HttpPost("SavePackage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePackage(PackagesPageVM model)
        {
            var input = model.CurrentPackage;
            int categoryId = await GetOrCreateCategoryId(input.CategoryID, input.NewCategoryName);

            // --- EDIT PACKAGE ---
            if (input.PackageID.HasValue && input.PackageID > 0)
            {
                var pkg = await _context.Packages
                    .Include(p => p.PackageInclusions)
                    .FirstOrDefaultAsync(p => p.PackageID == input.PackageID);

                if (pkg != null)
                {
                    // 1. Load current images from DB into a List
                    var currentImages = string.IsNullOrEmpty(pkg.ImageURL)
                        ? new List<string>()
                        : pkg.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                    // 2. Remove images the user deleted in the modal
                    if (input.DeleteImagePaths != null && input.DeleteImagePaths.Any())
                    {
                        foreach (var path in input.DeleteImagePaths)
                        {
                            if (currentImages.Contains(path))
                            {
                                currentImages.Remove(path);
                                DeletePhysicalFile(path); // Clean up disk
                            }
                        }
                    }

                    // 3. Add new uploads
                    if (input.ImageFiles != null)
                    {
                        foreach (var file in input.ImageFiles)
                        {
                            currentImages.Add(await HandleImageUpload(file));
                        }
                    }

                    // 4. If empty, set default
                    if (currentImages.Count == 0) currentImages.Add("/img/default-package.jpg");

                    // 5. Save
                    pkg.ImageURL = string.Join(";", currentImages.Distinct());

                    // Update other fields
                    pkg.PackageName = input.PackageName;
                    pkg.CategoryID = categoryId;
                    pkg.Destination = input.Destination;
                    pkg.Price = input.Price;
                    pkg.StartDate = input.StartDate;
                    pkg.EndDate = input.EndDate;
                    pkg.AvailableSlots = input.AvailableSlots;
                    pkg.Description = input.Description;

                    // Update Inclusions
                    _context.PackageInclusions.RemoveRange(pkg.PackageInclusions);
                    if (input.Inclusions != null)
                    {
                        foreach (var inc in input.Inclusions.Where(x => !string.IsNullOrWhiteSpace(x)))
                        {
                            _context.PackageInclusions.Add(new PackageInclusion { PackageID = pkg.PackageID, InclusionItem = inc });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Package updated successfully!";
                }
            }
            // --- CREATE PACKAGE ---
            else
            {
                var newImages = new List<string>();
                if (input.ImageFiles != null)
                {
                    foreach (var file in input.ImageFiles)
                    {
                        newImages.Add(await HandleImageUpload(file));
                    }
                }
                if (newImages.Count == 0) newImages.Add("/img/default-package.jpg");

                var pkg = new Package
                {
                    PackageName = input.PackageName,
                    CategoryID = categoryId,
                    Destination = input.Destination,
                    Price = input.Price,
                    StartDate = input.StartDate,
                    EndDate = input.EndDate,
                    AvailableSlots = input.AvailableSlots,
                    Description = input.Description,
                    ImageURL = string.Join(";", newImages)
                };

                _context.Packages.Add(pkg);
                await _context.SaveChangesAsync();

                if (input.Inclusions != null)
                {
                    foreach (var inc in input.Inclusions.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = pkg.PackageID, InclusionItem = inc });
                    }
                    await _context.SaveChangesAsync();
                }
                TempData["Success"] = "Package created successfully!";
            }

            return RedirectToAction(nameof(Packages));
        }

        [HttpPost("DeletePackage")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var p = await _context.Packages.FindAsync(id);
            if (p != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.PackageID == id))
                {
                    TempData["Error"] = "Cannot delete: Active bookings exist.";
                }
                else
                {
                    // Delete images
                    if (!string.IsNullOrEmpty(p.ImageURL))
                    {
                        var images = p.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var img in images) DeletePhysicalFile(img);
                    }

                    _context.Packages.Remove(p);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Package deleted.";
                }
            }
            return RedirectToAction(nameof(Packages));
        }

        // Helpers
        private async Task<int> GetOrCreateCategoryId(int? categoryId, string? newCategoryName)
        {
            if (categoryId.HasValue && categoryId > 0) return categoryId.Value;
            if (!string.IsNullOrWhiteSpace(newCategoryName))
            {
                var existing = await _context.PackageCategories.FirstOrDefaultAsync(c => c.CategoryName == newCategoryName.Trim());
                if (existing != null) return existing.CategoryID;
                var newCat = new PackageCategory { CategoryName = newCategoryName.Trim() };
                _context.PackageCategories.Add(newCat);
                await _context.SaveChangesAsync();
                return newCat.CategoryID;
            }
            return 1; // Default Fallback
        }

        private async Task<string> HandleImageUpload(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return "";

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var ext = Path.GetExtension(imageFile.FileName).ToLower();
            var fileName = Guid.NewGuid().ToString() + ext;
            var path = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            return $"/img/{fileName}";
        }

        private void DeletePhysicalFile(string url)
        {
            if (string.IsNullOrEmpty(url) || url.Contains("default-package")) return;
            try
            {
                var path = Path.Combine(_environment.WebRootPath, url.TrimStart('/').Replace("/", "\\"));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch { }
        }
    }
}