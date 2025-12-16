using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using FlyEase.Services;

namespace FlyEase.Controllers
{
    [Authorize(Roles = "Admin, Staff")]
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly StripeService _stripeService;
        private readonly EmailService _emailService;

        public PackageManagementController(FlyEaseDbContext context, IWebHostEnvironment environment, StripeService stripeService, EmailService emailService)
        {
            _context = context;
            _environment = environment;
            _stripeService = stripeService;
            _emailService = emailService;
        }

        public async Task<IActionResult> PackageManagement()
        {
            return View(await LoadViewModelAsync());
        }

        // === 1. NEW: API to get single package data ===
        [HttpGet]
        public async Task<IActionResult> GetPackage(int id)
        {
            var p = await _context.Packages
                .Include(x => x.PackageInclusions)
                .Include(x => x.Itinerary)
                .FirstOrDefaultAsync(x => x.PackageID == id);

            if (p == null) return NotFound();

            // Return pure JSON data
            var data = new
            {
                p.PackageID,
                p.PackageName,
                p.CategoryID,
                p.Description,
                p.Destination,
                p.Price,
                StartDate = p.StartDate.ToString("yyyy-MM-dd"),
                EndDate = p.EndDate.ToString("yyyy-MM-dd"),
                p.AvailableSlots,
                p.Latitude,
                p.Longitude,
                Inclusions = p.PackageInclusions.Select(i => i.InclusionItem).ToList(),
                Itinerary = p.Itinerary.OrderBy(i => i.DayNumber).Select(i => new { i.DayNumber, i.Title, i.ActivityDescription }).ToList(),
                Images = !string.IsNullOrEmpty(p.ImageURL) ? p.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries) : new string[0]
            };
            return Json(data);
        }

        // === 2. NEW: API to get updated list HTML ===
        [HttpGet]
        public async Task<IActionResult> GetPackageList()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            return PartialView("_PackageListPartial", packages);
        }

        // === 3. UPDATED: Create with AJAX support ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                // Return JSON error if AJAX
                if (IsAjaxRequest()) return Json(new { success = false, message = "Validation failed. Please check inputs." });

                // Fallback for normal post
                var vm = await LoadViewModelAsync();
                vm.Message = "Validation Error";
                vm.IsSuccess = false;
                return View("PackageManagement", vm);
            }

            try
            {
                var imagePaths = new List<string>();
                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    foreach (var file in request.ImageFiles) imagePaths.Add(await HandleImageUpload(file));
                }
                else
                {
                    imagePaths.Add("/img/default-package.jpg");
                }

                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);

                var package = new Package
                {
                    PackageName = request.PackageName,
                    CategoryID = categoryId,
                    Description = request.Description,
                    Destination = request.Destination,
                    Price = request.Price,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    AvailableSlots = request.AvailableSlots,
                    ImageURL = string.Join(";", imagePaths),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                _context.Packages.Add(package);
                await _context.SaveChangesAsync();

                // Add Inclusions
                if (request.Inclusions != null)
                {
                    foreach (var inc in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = package.PackageID, InclusionItem = inc.Trim() });
                }

                // Add Itinerary
                if (request.Itinerary != null && request.Itinerary.Any())
                {
                    foreach (var item in request.Itinerary)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Title))
                            _context.PackageItineraries.Add(new PackageItinerary { PackageID = package.PackageID, DayNumber = item.DayNumber, Title = item.Title, ActivityDescription = item.ActivityDescription });
                    }
                }

                await _context.SaveChangesAsync();

                if (IsAjaxRequest()) return Json(new { success = true, message = "Package created successfully!" });

                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Package created successfully!";
                viewModel.IsSuccess = true;
                return View("PackageManagement", viewModel);
            }
            catch (Exception ex)
            {
                if (IsAjaxRequest()) return Json(new { success = false, message = "Error: " + ex.Message });
                var vm = await LoadViewModelAsync();
                vm.Message = ex.Message;
                return View("PackageManagement", vm);
            }
        }

        // === 4. UPDATED: Update with AJAX support ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(UpdatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                if (IsAjaxRequest()) return Json(new { success = false, message = "Validation failed." });
                return RedirectToAction("PackageManagement");
            }

            try
            {
                var existingPackage = await _context.Packages.Include(p => p.PackageInclusions).Include(p => p.Itinerary).FirstOrDefaultAsync(p => p.PackageID == request.PackageID);
                if (existingPackage == null) return NotFound();

                // Image Logic (Simplified for brevity, keep your full logic)
                var currentImages = string.IsNullOrEmpty(existingPackage.ImageURL) ? new List<string>() : existingPackage.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                // Handle Deletions
                if (request.DeleteImagePaths != null)
                {
                    foreach (var path in request.DeleteImagePaths)
                    {
                        if (currentImages.Contains(path)) { currentImages.Remove(path); DeleteImageFile(path); }
                    }
                }
                // Handle Additions
                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    if (currentImages.Count == 1 && currentImages[0].Contains("default-package.jpg")) currentImages.Clear();
                    foreach (var file in request.ImageFiles) currentImages.Add(await HandleImageUpload(file));
                }
                if (currentImages.Count == 0) currentImages.Add("/img/default-package.jpg");

                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);

                // Update Fields
                existingPackage.ImageURL = string.Join(";", currentImages.Distinct());
                existingPackage.PackageName = request.PackageName;
                existingPackage.CategoryID = categoryId;
                existingPackage.Description = request.Description;
                existingPackage.Destination = request.Destination;
                existingPackage.Price = request.Price;
                existingPackage.StartDate = request.StartDate;
                existingPackage.EndDate = request.EndDate;
                existingPackage.AvailableSlots = request.AvailableSlots;
                existingPackage.Latitude = request.Latitude;
                existingPackage.Longitude = request.Longitude;

                // Update Children
                _context.PackageInclusions.RemoveRange(existingPackage.PackageInclusions);
                if (request.Inclusions != null)
                {
                    foreach (var inc in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = existingPackage.PackageID, InclusionItem = inc.Trim() });
                }

                _context.PackageItineraries.RemoveRange(existingPackage.Itinerary);
                if (request.Itinerary != null)
                {
                    foreach (var item in request.Itinerary)
                        if (!string.IsNullOrWhiteSpace(item.Title))
                            _context.PackageItineraries.Add(new PackageItinerary { PackageID = existingPackage.PackageID, DayNumber = item.DayNumber, Title = item.Title, ActivityDescription = item.ActivityDescription });
                }

                await _context.SaveChangesAsync();

                if (IsAjaxRequest()) return Json(new { success = true, message = "Package updated successfully!" });

                return RedirectToAction("PackageManagement");
            }
            catch (Exception ex)
            {
                if (IsAjaxRequest()) return Json(new { success = false, message = "Error: " + ex.Message });
                return RedirectToAction("PackageManagement");
            }
        }

        // KEEP your Delete, HandleImageUpload, DeleteImageFile, methods here (omitted for space, do not delete them)
        // ...

        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }

        // Helper methods... (GetOrCreateCategoryId, LoadViewModelAsync etc)
        // ... (Keep your existing implementations)

        private async Task<int> GetOrCreateCategoryId(int? categoryId, string? newCategoryName)
        {
            if (categoryId.HasValue && categoryId > 0) return categoryId.Value;
            if (!string.IsNullOrWhiteSpace(newCategoryName))
            {
                var existing = await _context.PackageCategories.FirstOrDefaultAsync(c => c.CategoryName.ToLower() == newCategoryName.Trim().ToLower());
                if (existing != null) return existing.CategoryID;
                var newCat = new PackageCategory { CategoryName = newCategoryName.Trim() };
                _context.PackageCategories.Add(newCat);
                await _context.SaveChangesAsync();
                return newCat.CategoryID;
            }
            return 0;
        }

        private async Task<PackageManagementViewModel> LoadViewModelAsync()
        {
            return new PackageManagementViewModel
            {
                Packages = await _context.Packages
                    .Include(p => p.Category)
                    .Include(p => p.PackageInclusions)
                    .Include(p => p.Bookings)
                    .OrderByDescending(p => p.PackageID)
                    .ToListAsync(),
                Categories = await _context.PackageCategories.OrderBy(c => c.CategoryName).ToListAsync()
            };
        }

        // Ensure DeleteImageFile and HandleImageUpload are kept as they were in your code
        private void DeleteImageFile(string? imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.Contains("default-package.jpg"))
            {
                try
                {
                    var path = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace("/", "\\"));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                catch { }
            }
        }

        private async Task<string> HandleImageUpload(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return "/img/default-package.jpg";
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif" };
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) throw new InvalidOperationException($"File type '{ext}' is not allowed.");
            var fileName = Guid.NewGuid().ToString() + ext;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            return $"/img/{fileName}";
        }
    }
}