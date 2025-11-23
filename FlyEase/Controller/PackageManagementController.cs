using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.Models;
using Microsoft.AspNetCore.Hosting;

namespace FlyEase.Controllers
{
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PackageManagementController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> PackageManagement()
        {
            var viewModel = await LoadViewModelAsync();
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Please fix validation errors";
                viewModel.IsSuccess = false;
                return View("PackageManagement", viewModel);
            }

            try
            {
                // Handle category - check if new category or existing
                int categoryId;

                if (request.CategoryID.HasValue && request.CategoryID > 0)
                {
                    // Use existing category
                    categoryId = request.CategoryID.Value;
                }
                else if (!string.IsNullOrWhiteSpace(request.NewCategoryName))
                {
                    // Check if category already exists (case insensitive)
                    var existingCategory = await _context.PackageCategories
                        .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == request.NewCategoryName.Trim().ToLower());

                    if (existingCategory != null)
                    {
                        // Use existing category
                        categoryId = existingCategory.CategoryID;
                    }
                    else
                    {
                        // Create new category
                        var newCategory = new PackageCategory
                        {
                            CategoryName = request.NewCategoryName.Trim()
                        };
                        _context.PackageCategories.Add(newCategory);
                        await _context.SaveChangesAsync();
                        categoryId = newCategory.CategoryID;
                    }
                }
                else
                {
                    var PackageManagementviewModel = await LoadViewModelAsync();
                    PackageManagementviewModel.Message = "Please select or enter a category";
                    PackageManagementviewModel.IsSuccess = false;
                    return View(PackageManagementviewModel);
                }

                // Handle image upload
                string imagePath = await HandleImageUpload(request.ImageFile);

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
                    ImageURL = imagePath
                };

                _context.Packages.Add(package);
                await _context.SaveChangesAsync();

                // Add inclusions
                foreach (var inclusion in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                {
                    var packageInclusion = new PackageInclusion
                    {
                        PackageID = package.PackageID,
                        InclusionItem = inclusion.Trim()
                    };
                    _context.PackageInclusions.Add(packageInclusion);
                }

                await _context.SaveChangesAsync();

                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Package created successfully!";
                viewModel.IsSuccess = true;
                return View("PackageManagement", viewModel);
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error creating package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("PackageManagement", viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var viewModel = await LoadViewModelAsync();
            var package = await _context.Packages
                .Include(p => p.PackageInclusions)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package != null)
            {
                viewModel.Package = package;
                viewModel.EditingPackageId = id;
                viewModel.SelectedCategoryId = package.CategoryID;
                viewModel.Inclusions = package.PackageInclusions
                    .Select(pi => pi.InclusionItem)
                    .ToList();
            }
            else
            {
                viewModel.Message = "Package not found!";
                viewModel.IsSuccess = false;
            }

            return View("PackageManagement", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(UpdatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Please fix validation errors";
                viewModel.IsSuccess = false;
                return View("PackageManagement", viewModel);
            }

            try
            {
                var existingPackage = await _context.Packages
                    .Include(p => p.PackageInclusions)
                    .FirstOrDefaultAsync(p => p.PackageID == request.PackageID);

                if (existingPackage != null)
                {
                    // Handle category - same logic as Create
                    int categoryId;

                    if (request.CategoryID.HasValue && request.CategoryID > 0)
                    {
                        categoryId = request.CategoryID.Value;
                    }
                    else if (!string.IsNullOrWhiteSpace(request.NewCategoryName))
                    {
                        var existingCategory = await _context.PackageCategories
                            .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == request.NewCategoryName.Trim().ToLower());

                        if (existingCategory != null)
                        {
                            categoryId = existingCategory.CategoryID;
                        }
                        else
                        {
                            var newCategory = new PackageCategory
                            {
                                CategoryName = request.NewCategoryName.Trim()
                            };
                            _context.PackageCategories.Add(newCategory);
                            await _context.SaveChangesAsync();
                            categoryId = newCategory.CategoryID;
                        }
                    }
                    else
                    {
                        var PackageManagementviewModel = await LoadViewModelAsync();
                        PackageManagementviewModel.Message = "Please select or enter a category";
                        PackageManagementviewModel.IsSuccess = false;
                        return View( PackageManagementviewModel);
                    }

                    // Handle image upload
                    string imagePath = existingPackage.ImageURL; // Keep existing by default
                    if (request.ImageFile != null && request.ImageFile.Length > 0)
                    {
                        // Delete old image if it exists and is not default
                        if (!string.IsNullOrEmpty(existingPackage.ImageURL) &&
                            !existingPackage.ImageURL.Contains("default-package.jpg"))
                        {
                            var oldImagePath = Path.Combine(_environment.WebRootPath, existingPackage.ImageURL.Replace("/", "\\"));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }
                        imagePath = await HandleImageUpload(request.ImageFile);
                    }

                    // Update package properties
                    existingPackage.PackageName = request.PackageName;
                    existingPackage.CategoryID = categoryId;
                    existingPackage.Description = request.Description;
                    existingPackage.Destination = request.Destination;
                    existingPackage.Price = request.Price;
                    existingPackage.StartDate = request.StartDate;
                    existingPackage.EndDate = request.EndDate;
                    existingPackage.AvailableSlots = request.AvailableSlots;
                    existingPackage.ImageURL = imagePath;

                    // Update inclusions
                    var existingInclusions = existingPackage.PackageInclusions.ToList();
                    _context.PackageInclusions.RemoveRange(existingInclusions);

                    foreach (var inclusion in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        var packageInclusion = new PackageInclusion
                        {
                            PackageID = existingPackage.PackageID,
                            InclusionItem = inclusion.Trim()
                        };
                        _context.PackageInclusions.Add(packageInclusion);
                    }

                    await _context.SaveChangesAsync();

                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package updated successfully!";
                    viewModel.IsSuccess = true;
                    return View("PackageManagement", viewModel);
                }
                else
                {
                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package not found!";
                    viewModel.IsSuccess = false;
                    return View("PackageManagement", viewModel);
                }
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error updating package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("PackageManagement", viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var package = await _context.Packages
                    .Include(p => p.PackageInclusions)
                    .FirstOrDefaultAsync(p => p.PackageID == id);

                if (package != null)
                {
                    // Check if there are any bookings for this package
                    var hasBookings = await _context.Bookings.AnyAsync(b => b.PackageID == id);
                    if (hasBookings)
                    {
                        var packageManagementVM = await LoadViewModelAsync();
                        packageManagementVM.Message = "Cannot delete package that has existing bookings!";
                        packageManagementVM.IsSuccess = false;
                        return View("PackageManagement", packageManagementVM);
                    }

                    // Delete associated image file if it exists and is not default
                    if (!string.IsNullOrEmpty(package.ImageURL) &&
                        !package.ImageURL.Contains("default-package.jpg"))
                    {
                        var imagePath = Path.Combine(_environment.WebRootPath, package.ImageURL.Replace("/", "\\"));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }

                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();

                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package deleted successfully!";
                    viewModel.IsSuccess = true;
                    return View("PackageManagement", viewModel);
                }
                else
                {
                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package not found!";
                    viewModel.IsSuccess = false;
                    return View("PackageManagement", viewModel);
                }
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error deleting package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("PackageManagement", viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            var viewModel = await LoadViewModelAsync();
            return View("PackageManagement", viewModel);
        }

        private async Task<string> HandleImageUpload(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return "/img/default-package.jpg"; // Return default image path
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException("Invalid file type. Only JPG, JPEG, PNG, GIF, and WebP files are allowed.");
            }

            // Validate file size (max 5MB)
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size too large. Maximum size is 5MB.");
            }

            // Create unique filename
            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "img");

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return the relative path for database storage - this will be "/img/filename.jpg"
            return $"/img/{fileName}";
        }

        private async Task<PackageManagementViewModel> LoadViewModelAsync()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .Include(p => p.PackageInclusions)
                .OrderByDescending(p => p.PackageID)
                .ToListAsync();

            var categories = await _context.PackageCategories
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            return new PackageManagementViewModel
            {
                Packages = packages,
                Categories = categories
            };
        }
    }
}