using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlyEase.Controllers
{
    public class StaffDashboardController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public StaffDashboardController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ==========================================
        // 1. MAIN DASHBOARD
        // View: Views/StaffDashboard/StaffDashboard.cshtml
        // ==========================================
        public async Task<IActionResult> StaffDashboard()
        {
            var vm = new StaffDashboardViewModel
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

        // ==========================================
        // 2. BOOKING MANAGEMENT
        // View: Views/StaffDashboard/Bookings.cshtml
        // ==========================================
        public async Task<IActionResult> Bookings(string status = "All")
        {
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Package)
                .AsQueryable();

            if (status != "All" && !string.IsNullOrEmpty(status))
            {
                query = query.Where(b => b.BookingStatus == status);
            }

            ViewBag.CurrentStatus = status;
            return View(await query.OrderByDescending(b => b.BookingDate).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBookingStatus(int id, string newStatus)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking != null)
            {
                booking.BookingStatus = newStatus;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Bookings));
        }

        // ==========================================
        // 3. PACKAGE MANAGEMENT
        // View: Views/StaffDashboard/Packages.cshtml
        // ==========================================
        public async Task<IActionResult> Packages()
        {
            var vm = new PackageManagementVM
            {
                Packages = await _context.Packages.Include(p => p.Category).OrderByDescending(p => p.PackageID).ToListAsync(),
                Categories = await _context.PackageCategories.OrderBy(c => c.CategoryName).ToListAsync()
            };
            return View(vm);
        }

        // View: Views/StaffDashboard/CreatePackage.cshtml
        [HttpGet]
        public async Task<IActionResult> CreatePackage()
        {
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            return View(new PackageInputModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePackage(PackageInputModel input)
        {
            if (ModelState.IsValid)
            {
                int categoryId = await GetOrCreateCategoryId(input.CategoryID, input.NewCategoryName);
                string combinedImages = await ProcessImages(input.ImageFiles, new List<string>());

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
                    ImageURL = combinedImages
                };

                _context.Packages.Add(pkg);
                await _context.SaveChangesAsync();

                // Add Inclusions
                if (input.Inclusions != null)
                {
                    foreach (var inc in input.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = pkg.PackageID, InclusionItem = inc.Trim() });
                    }
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Packages));
            }

            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            return View(input);
        }

        // View: Views/StaffDashboard/EditPackage.cshtml
        [HttpGet]
        public async Task<IActionResult> EditPackage(int id)
        {
            var p = await _context.Packages.Include(x => x.PackageInclusions).FirstOrDefaultAsync(x => x.PackageID == id);
            if (p == null) return NotFound();

            var model = new PackageInputModel
            {
                PackageID = p.PackageID,
                PackageName = p.PackageName,
                CategoryID = p.CategoryID,
                Destination = p.Destination,
                Price = p.Price,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                AvailableSlots = p.AvailableSlots,
                Description = p.Description,
                Inclusions = p.PackageInclusions.Select(i => i.InclusionItem).ToList()
            };

            ViewBag.ExistingImages = string.IsNullOrEmpty(p.ImageURL) ? new List<string>() : p.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPackage(PackageInputModel input)
        {
            if (ModelState.IsValid)
            {
                var p = await _context.Packages.Include(x => x.PackageInclusions).FirstOrDefaultAsync(x => x.PackageID == input.PackageID);
                if (p == null) return NotFound();

                p.PackageName = input.PackageName;
                p.CategoryID = await GetOrCreateCategoryId(input.CategoryID, input.NewCategoryName);
                p.Destination = input.Destination;
                p.Price = input.Price;
                p.StartDate = input.StartDate;
                p.EndDate = input.EndDate;
                p.AvailableSlots = input.AvailableSlots;
                p.Description = input.Description;

                // Image Handling
                var currentImages = string.IsNullOrEmpty(p.ImageURL) ? new List<string>() : p.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                if (input.DeleteImagePaths != null)
                {
                    foreach (var del in input.DeleteImagePaths) { currentImages.Remove(del); }
                }
                if (input.ImageFiles != null)
                {
                    foreach (var file in input.ImageFiles) { currentImages.Add(await HandleImageUpload(file)); }
                }
                p.ImageURL = string.Join(";", currentImages.Distinct());

                // Inclusions
                _context.PackageInclusions.RemoveRange(p.PackageInclusions);
                if (input.Inclusions != null)
                {
                    foreach (var inc in input.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = p.PackageID, InclusionItem = inc.Trim() });
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Packages));
            }
            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            return View(input);
        }

        [HttpPost]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var p = await _context.Packages.FindAsync(id);
            if (p != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.PackageID == id))
                {
                    TempData["Error"] = "Cannot delete package with active bookings.";
                }
                else
                {
                    _context.Packages.Remove(p);
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Packages));
        }

        // ==========================================
        // 4. USER MANAGEMENT
        // View: Views/StaffDashboard/Users.cshtml
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.OrderByDescending(u => u.CreatedDate).ToListAsync();

            // Pass empty EditUserVM so the form is ready
            var vm = new UsersManagementVM
            {
                Users = users,
                CurrentUser = new UserEditVM()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(UsersManagementVM model)
        {
            // We map from model.CurrentUser because that's what the Modal submits
            var input = model.CurrentUser;

            // Note: We skip ModelState validation check for simplicity in this "same-page" demo
            // or you would need logic to re-open the modal on error.

            var user = await _context.Users.FindAsync(input.UserID);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Update fields
            user.FullName = input.FullName;
            user.Email = input.Email;
            user.Phone = input.Phone;
            user.Role = input.Role;

            await _context.SaveChangesAsync();
            TempData["Success"] = "User updated successfully!";

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (await _context.Bookings.AnyAsync(b => b.UserID == id))
                {
                    TempData["Error"] = "Cannot delete user with existing bookings.";
                }
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
        // HELPERS
        // ==========================================
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
            return 0; // Or throw exception
        }

        private async Task<string> ProcessImages(List<IFormFile> files, List<string> existing)
        {
            var list = new List<string>(existing);
            if (files != null)
            {
                foreach (var f in files) list.Add(await HandleImageUpload(f));
            }
            if (list.Count == 0) list.Add("/img/default-package.jpg");
            return string.Join(";", list);
        }

        private async Task<string> HandleImageUpload(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return "";
            var ext = Path.GetExtension(imageFile.FileName).ToLower();
            var fileName = Guid.NewGuid().ToString() + ext;
            var path = Path.Combine(_environment.WebRootPath, "img", fileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            return $"/img/{fileName}";
        }
    }
}