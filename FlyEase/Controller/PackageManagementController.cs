using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlyEase.Controllers
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
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PackageViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Your existing create logic...
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating package: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
            }

            // Ensure all ViewBag data is loaded
            await LoadViewBagData();
            return View("PackageManagement", viewModel);
        }
        // GET: PackageManagement/Create
        public async Task<IActionResult> Create()
        {
            await LoadViewBagData();
            var viewModel = new PackageViewModel
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                AvailableSlots = 10,
                Price = 0
            };
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
                        ModelState.AddModelError("NewCategoryName", "Category name is required when creating new category.");
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
            ViewBag.Categories = categories; // This was missing in some flows
        }
        // Helper method to get existing category ID or create new one
        private async Task<int> GetOrCreateCategoryId(PackageViewModel viewModel)
        {
            if (viewModel.CategoryID > 0)
            {
                var existingCategory = await _context.PackageCategories
                    .FirstOrDefaultAsync(c => c.CategoryID == viewModel.CategoryID);
                if (existingCategory != null)
                {
                    return viewModel.CategoryID;
                }
            }

            if (!string.IsNullOrWhiteSpace(viewModel.NewCategoryName))
            {
                var normalizedName = viewModel.NewCategoryName.Trim().ToLower();
                var existingCategory = await _context.PackageCategories
                    .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == normalizedName);

                if (existingCategory != null)
                {
                    return existingCategory.CategoryID;
                }
                else
                {
                    var newCategory = new PackageCategory
                    {
                        CategoryName = viewModel.NewCategoryName.Trim()
                    };

                    _context.PackageCategories.Add(newCategory);
                    await _context.SaveChangesAsync();

                    return newCategory.CategoryID;
                }
            }

            return 0;
        }

        // Handle image file upload
        private async Task<string> HandleImageUpload(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return "/img/default-package.jpg"; // Default image
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new Exception("Invalid file type. Only JPG, JPEG, PNG, GIF, and WebP files are allowed.");
            }

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                throw new Exception("File size too large. Maximum size is 5MB.");
            }

            // Create unique filename
            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var imagesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img");

            // Ensure img directory exists
            if (!Directory.Exists(imagesFolder))
            {
                Directory.CreateDirectory(imagesFolder);
            }

            var filePath = Path.Combine(imagesFolder, fileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return the path that will be stored in database
            return $"/img/{fileName}";
        }

        // Delete image file when package is deleted
        private void DeleteImageFile(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("/img/") || imageUrl == "/img/default-package.jpg")
                return;

            try
            {
                var fileName = Path.GetFileName(imageUrl);
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "img", fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw, as package deletion should continue
                Console.WriteLine($"Error deleting image file: {ex.Message}");
            }
        }
    }
}