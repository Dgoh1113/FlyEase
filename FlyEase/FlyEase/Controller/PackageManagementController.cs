using FlyEase.Controllers;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Services;
using System.Linq;

namespace FlyEase.Controllers
{
    [Authorize(Roles = "Admin, Staff")]
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PackageManagementController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // 1. Main Page Load
        public async Task<IActionResult> PackageManagement(int page = 1)
        {
            return View(await LoadViewModelAsync(page));
        }

        // 2. AJAX: Get Single Package for Editing
        [HttpGet]
        public async Task<IActionResult> GetPackage(int id)
        {
            var p = await _context.Packages
                .Include(x => x.PackageInclusions)
                .Include(x => x.Itinerary)
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.PackageID == id);

            if (p == null) return NotFound();

            int displayCategoryId = p.CategoryID;
            if (p.Category != null)
            {
                var canonicalId = await _context.PackageCategories
                    .Where(c => c.CategoryName == p.Category.CategoryName)
                    .OrderBy(c => c.CategoryID)
                    .Select(c => c.CategoryID)
                    .FirstOrDefaultAsync();

                if (canonicalId > 0) displayCategoryId = canonicalId;
            }

            return Json(new
            {
                p.PackageID,
                p.PackageName,
                CategoryID = displayCategoryId,
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
            });
        }

        // 3. AJAX: Refresh List (UPDATED)
        [HttpGet]
        public async Task<IActionResult> GetPackageList(int page = 1, string search = "", int? category = null)
        {
            int pageSize = 4;
            var query = _context.Packages
                .Include(p => p.Category)
                .Include(p => p.Bookings)
                .AsQueryable();

            // Apply Search Filter (Package Name OR Booking ID)
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.ToLower().Trim();
                query = query.Where(p => p.PackageName.ToLower().Contains(searchLower)
                                      || p.Bookings.Any(b => b.BookingID.ToString().Contains(searchLower)));
            }

            // Apply Category Filter
            if (category.HasValue && category.Value > 0)
            {
                query = query.Where(p => p.CategoryID == category.Value);
            }

            query = query.OrderByDescending(p => p.PackageID);

            int totalItems = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var vm = new PackageManagementViewModel
            {
                Packages = items,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                PageSize = pageSize,
                SearchTerm = search,
                SelectedCategoryId = category
            };

            return PartialView("_PackageListPartial", vm);
        }

        // 4. Create Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                                       .ToDictionary(k => k.Key, v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray());

                if (IsAjaxRequest()) return Json(new { success = false, message = "Please correct the highlighted errors.", errors = errors });
                return View("PackageManagement", await LoadViewModelAsync());
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

                if (request.Inclusions != null)
                {
                    foreach (var inc in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = package.PackageID, InclusionItem = inc.Trim() });
                }

                if (request.Itinerary != null)
                {
                    foreach (var item in request.Itinerary)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Title))
                            _context.PackageItineraries.Add(new PackageItinerary { PackageID = package.PackageID, DayNumber = item.DayNumber, Title = item.Title, ActivityDescription = item.ActivityDescription });
                    }
                }

                await _context.SaveChangesAsync();

                if (IsAjaxRequest()) return Json(new { success = true, message = "Package created successfully!" });
                return RedirectToAction("PackageManagement");
            }
            catch (Exception ex)
            {
                if (IsAjaxRequest()) return Json(new { success = false, message = "Error: " + ex.Message });
                return View("PackageManagement", await LoadViewModelAsync());
            }
        }

        // 5. Update Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(UpdatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                                       .ToDictionary(k => k.Key, v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray());

                if (IsAjaxRequest()) return Json(new { success = false, message = "Please correct the highlighted errors.", errors = errors });
                return RedirectToAction("PackageManagement");
            }

            try
            {
                var existingPackage = await _context.Packages.Include(p => p.PackageInclusions).Include(p => p.Itinerary).FirstOrDefaultAsync(p => p.PackageID == request.PackageID);
                if (existingPackage == null) return NotFound();

                List<string> currentImages;
                if (Request.Form.ContainsKey("OrderedExistingImages"))
                {
                    var orderedPaths = Request.Form["OrderedExistingImages"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    var currentDbImages = existingPackage.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
                    currentImages = orderedPaths.Where(p => currentDbImages.Contains(p)).ToList();
                }
                else
                {
                    currentImages = string.IsNullOrEmpty(existingPackage.ImageURL) ? new List<string>() : existingPackage.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                if (request.DeleteImagePaths != null)
                {
                    foreach (var path in request.DeleteImagePaths)
                    {
                        if (currentImages.Contains(path)) { currentImages.Remove(path); DeleteImageFile(path); }
                    }
                }

                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    if (currentImages.Count == 1 && currentImages[0].Contains("default-package.jpg")) currentImages.Clear();
                    foreach (var file in request.ImageFiles) currentImages.Add(await HandleImageUpload(file));
                }

                if (currentImages.Count == 0) currentImages.Add("/img/default-package.jpg");

                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);

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

        // 6. Delete Action & Helpers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var package = await _context.Packages.Include(p => p.Bookings).FirstOrDefaultAsync(p => p.PackageID == id);
                if (package != null)
                {
                    if (!string.IsNullOrEmpty(package.ImageURL))
                        foreach (var img in package.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries)) DeleteImageFile(img);
                    if (package.Bookings != null && package.Bookings.Any())
                        _context.Bookings.RemoveRange(package.Bookings);
                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();
                }
                if (IsAjaxRequest()) return Json(new { success = true, message = "Deleted successfully" });
                return RedirectToAction("PackageManagement");
            }
            catch (Exception ex)
            {
                if (IsAjaxRequest()) return Json(new { success = false, message = "Error deleting: " + ex.Message });
                return RedirectToAction("PackageManagement");
            }
        }

        private async Task<PackageManagementViewModel> LoadViewModelAsync(int page = 1)
        {
            int pageSize = 4;
            var query = _context.Packages.Include(p => p.Category).Include(p => p.PackageInclusions).Include(p => p.Bookings).OrderByDescending(p => p.PackageID);
            int totalItems = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var allCategories = await _context.PackageCategories.OrderBy(c => c.CategoryName).ToListAsync();
            var distinctCategories = allCategories.GroupBy(c => c.CategoryName).Select(g => g.OrderBy(c => c.CategoryID).First()).OrderBy(c => c.CategoryName).ToList();
            return new PackageManagementViewModel { Packages = items, Categories = distinctCategories, CurrentPage = page, TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize), PageSize = pageSize };
        }

        private async Task<int> GetOrCreateCategoryId(int? categoryId, string? newCategoryName)
        {
            if (categoryId.HasValue && categoryId > 0) return categoryId.Value;
            if (!string.IsNullOrWhiteSpace(newCategoryName))
            {
                var existing = await _context.PackageCategories.Where(c => c.CategoryName.ToLower() == newCategoryName.Trim().ToLower()).OrderBy(c => c.CategoryID).FirstOrDefaultAsync();
                if (existing != null) return existing.CategoryID;
                var newCat = new PackageCategory { CategoryName = newCategoryName.Trim() };
                _context.PackageCategories.Add(newCat);
                await _context.SaveChangesAsync();
                return newCat.CategoryID;
            }
            return 0;
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
            using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create)) await imageFile.CopyToAsync(stream);
            return $"/img/{fileName}";
        }

        private void DeleteImageFile(string? imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.Contains("default-package.jpg"))
            {
                try { var path = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace("/", "\\")); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
            }
        }

        private bool IsAjaxRequest() { return Request.Headers["X-Requested-With"] == "XMLHttpRequest"; }
    }
}