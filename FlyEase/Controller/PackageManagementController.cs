using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace FlyEase.Controller
{
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PackageManagementController(FlyEaseDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: PackageManagement/PackageManagement - Main CRUD page
        public async Task<IActionResult> PackageManagement()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            var categories = await _context.PackageCategories.ToListAsync();

            var viewModel = new PackageViewModel
            {
                Categories = categories,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                AvailableSlots = 10,
                Price = 0
            };

            ViewBag.Packages = packages;
            await LoadViewBagData();
            return View(viewModel);
        }

        // POST: PackageManagement/Create - Handle form submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PackageViewModel viewModel)
        {
            // Model validation (DataAnnotations + IValidatableObject) runs before here
            if (ModelState.IsValid)
            {
                try
                {
                    // Handle category creation/selection
                    int categoryId = await GetOrCreateCategoryId(viewModel);

                    if (categoryId == 0)
                    {
                        // Provide a precise error message attached to both fields
                        ModelState.AddModelError(nameof(viewModel.CategoryID), "Please select an existing category or enter a new category name.");
                        ModelState.AddModelError(nameof(viewModel.NewCategoryName), "Please select an existing category or enter a new category name.");
                        await LoadViewBagData();
                        return View("PackageManagement", viewModel);
                    }

                    // Handle image upload
                    string imageUrl = await HandleImageUpload(viewModel.ImageFile);

                    var package = new Package
                    {
                        PackageName = viewModel.PackageName,
                        CategoryID = categoryId,
                        Description = viewModel.Description,
                        Destination = viewModel.Destination,
                        Price = viewModel.Price,
                        StartDate = viewModel.StartDate,
                        EndDate = viewModel.EndDate,
                        AvailableSlots = viewModel.AvailableSlots,
                        ImageURL = imageUrl
                    };

                    _context.Add(package);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Package created successfully!";
                    return RedirectToAction(nameof(PackageManagement));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating package: {ex.Message}";
                }
            }
            else
            {
                // Make ModelState errors more visible by putting the first few errors into TempData for flash display (optional)
                // This code is for debugging/user feedback only
                var firstErrors = ModelState.Where(kvp => kvp.Value.Errors.Count > 0)
                                            .Select(kvp => new { Key = kvp.Key, Errors = kvp.Value.Errors.Select(e => e.ErrorMessage + (e.Exception?.Message ?? "")) })
                                            .ToList();
                // Optionally log or show one combined message:
                TempData["ErrorMessage"] = "Please fix the validation errors shown on the form.";
            }

            await LoadViewBagData();
            return View("PackageManagement", viewModel);
        }

        // GET: PackageManagement/Edit - Load package data into form
        public async Task<IActionResult> Edit(int id)
        {
            var package = await _context.Packages
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package == null)
            {
                TempData["ErrorMessage"] = "Package not found!";
                return RedirectToAction(nameof(PackageManagement));
            }

            var viewModel = new PackageViewModel
            {
                PackageID = package.PackageID,
                PackageName = package.PackageName,
                CategoryID = package.CategoryID,
                Description = package.Description,
                Destination = package.Destination,
                Price = package.Price,
                StartDate = package.StartDate,
                EndDate = package.EndDate,
                AvailableSlots = package.AvailableSlots,
                ImageURL = package.ImageURL
            };

            await LoadViewBagData();
            ViewBag.EditingId = id;

            return View("PackageManagement", viewModel);
        }

        // POST: PackageManagement/Edit - Update package
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PackageViewModel viewModel)
        {
            if (id != viewModel.PackageID)
            {
                TempData["ErrorMessage"] = "Package ID mismatch!";
                return RedirectToAction(nameof(PackageManagement));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle category creation/selection
                    int categoryId = await GetOrCreateCategoryId(viewModel);

                    if (categoryId == 0)
                    {
                        ModelState.AddModelError(nameof(viewModel.CategoryID), "Please select an existing category or enter a new category name.");
                        ModelState.AddModelError(nameof(viewModel.NewCategoryName), "Please select an existing category or enter a new category name.");
                        await LoadViewBagData();
                        ViewBag.EditingId = id;
                        return View("PackageManagement", viewModel);
                    }

                    var package = await _context.Packages.FindAsync(id);
                    if (package == null)
                    {
                        TempData["ErrorMessage"] = "Package not found!";
                        return RedirectToAction(nameof(PackageManagement));
                    }

                    // Handle image upload if new file is provided
                    if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
                    {
                        string imageUrl = await HandleImageUpload(viewModel.ImageFile);
                        package.ImageURL = imageUrl;
                    }

                    // Update package properties
                    package.PackageName = viewModel.PackageName;
                    package.CategoryID = categoryId;
                    package.Description = viewModel.Description;
                    package.Destination = viewModel.Destination;
                    package.Price = viewModel.Price;
                    package.StartDate = viewModel.StartDate;
                    package.EndDate = viewModel.EndDate;
                    package.AvailableSlots = viewModel.AvailableSlots;

                    _context.Update(package);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Package updated successfully!";
                    return RedirectToAction(nameof(PackageManagement));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PackageExists(viewModel.PackageID))
                    {
                        TempData["ErrorMessage"] = "Package not found!";
                        return RedirectToAction(nameof(PackageManagement));
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating package: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
            }

            await LoadViewBagData();
            ViewBag.EditingId = id;
            return View("PackageManagement", viewModel);
        }

        // POST: PackageManagement/Delete - Delete package
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var package = await _context.Packages.FindAsync(id);
                if (package != null)
                {
                    // Delete associated image file if exists and it's not the default image
                    if (!string.IsNullOrEmpty(package.ImageURL) && package.ImageURL != "/img/default-package.jpg")
                    {
                        DeleteImageFile(package.ImageURL);
                    }

                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Package deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Package not found!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting package: {ex.Message}";
            }

            return RedirectToAction(nameof(PackageManagement));
        }

        private bool PackageExists(int id)
        {
            return _context.Packages.Any(e => e.PackageID == id);
        }

        private async Task LoadViewBagData()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            var categories = await _context.PackageCategories.ToListAsync();

            ViewBag.Packages = packages;
            ViewBag.Categories = categories;
        }

        // Helper method to get existing category ID or create new one
        private async Task<int> GetOrCreateCategoryId(PackageViewModel viewModel)
        {
            // Use selected existing category if provided and valid
            if (viewModel.CategoryID.HasValue && viewModel.CategoryID.Value > 0)
            {
                var existingCategory = await _context.PackageCategories.FindAsync(viewModel.CategoryID.Value);
                if (existingCategory != null)
                {
                    return existingCategory.CategoryID;
                }
                // selected ID didn't match DB; treat as not selected
            }

            // Otherwise, if user typed a new category name, validate and create or return existing match
            if (!string.IsNullOrWhiteSpace(viewModel.NewCategoryName))
            {
                var trimmed = viewModel.NewCategoryName.Trim();

                // Validate pattern server-side to ensure matches client-side regex
                var regex = new Regex(@"^[A-Za-z0-9\s\-]{1,100}$");
                if (!regex.IsMatch(trimmed))
                {
                    // invalid category name format
                    ModelState.AddModelError(nameof(viewModel.NewCategoryName), "Category name must be 1-100 characters and contain only letters, numbers, spaces or hyphens.");
                    return 0;
                }

                // Try to find case-insensitive match
                var existing = await _context.PackageCategories
                    .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == trimmed.ToLower());
                if (existing != null)
                {
                    return existing.CategoryID;
                }

                // Create the category (this modifies DB but not DB schema)
                var cat = new PackageCategory
                {
                    CategoryName = trimmed
                };
                _context.PackageCategories.Add(cat);
                await _context.SaveChangesAsync();

                return cat.CategoryID;
            }

            // Neither selected nor typed -> indicate failure
            return 0;
        }

        // Image handling (unchanged logic, simplified for inclusion)
        private async Task<string> HandleImageUpload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "/img/default-package.jpg";
            }

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
            {
                throw new Exception("Invalid image type.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new Exception("File too large.");
            }

            var uploads = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "packages");
            if (!Directory.Exists(uploads))
            {
                Directory.CreateDirectory(uploads);
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/uploads/packages/" + fileName;
        }

        private void DeleteImageFile(string imageUrl)
        {
            try
            {
                var rootRelative = imageUrl.TrimStart('/');
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, rootRelative);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch
            {
                // ignore deletion failures
            }
        }
    }
}