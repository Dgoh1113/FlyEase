using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.Models;

namespace FlyEase.Controllers
{
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // 100 MB = 104,857,600 bytes
        private const long MAX_UPLOAD_BYTES = 104857600;

        public PackageManagementController(FlyEaseDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<IActionResult> PackageManagement()
        {
            return View(await LoadViewModelAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MAX_UPLOAD_BYTES)] // Sets the max request size for this action
        [RequestFormLimits(MultipartBodyLengthLimit = MAX_UPLOAD_BYTES)] // Specifically for multipart form data
        public async Task<IActionResult> Create(CreatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var vm = await LoadViewModelAsync();
                vm.Message = "Please fix validation errors";
                vm.IsSuccess = false;
                return View("PackageManagement", vm);
            }

            try
            {
                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);
                if (categoryId == 0) return View("PackageManagement", await LoadViewModelAsync());

                // 1. Process Uploaded Images
                var imagePaths = new List<string>();
                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    foreach (var file in request.ImageFiles)
                    {
                        // Upload each file and add path to list
                        imagePaths.Add(await HandleImageUpload(file));
                    }
                }
                else
                {
                    imagePaths.Add("/img/default-package.jpg");
                }

                // 2. Join into one string for DB
                string combinedImages = string.Join(";", imagePaths);

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
                    ImageURL = combinedImages
                };

                _context.Packages.Add(package);
                await _context.SaveChangesAsync();

                // Inclusions
                if (request.Inclusions != null)
                {
                    foreach (var inc in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = package.PackageID, InclusionItem = inc.Trim() });
                    }
                    await _context.SaveChangesAsync();
                }

                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Package created successfully!";
                viewModel.IsSuccess = true;
                return View("PackageManagement", viewModel);
            }
            catch (Exception ex)
            {
                var vm = await LoadViewModelAsync();
                vm.Message = $"Error: {ex.Message}";
                vm.IsSuccess = false;
                return View("PackageManagement", vm);
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
                viewModel.Inclusions = package.PackageInclusions.Select(pi => pi.InclusionItem).ToList();
            }
            return View("PackageManagement", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MAX_UPLOAD_BYTES)] // Sets the max request size for this action
        [RequestFormLimits(MultipartBodyLengthLimit = MAX_UPLOAD_BYTES)] // Specifically for multipart form data
        public async Task<IActionResult> Update(UpdatePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                var vm = await LoadViewModelAsync();
                vm.Message = "Validation Failed";
                vm.IsSuccess = false;
                return View("PackageManagement", vm);
            }

            try
            {
                var existingPackage = await _context.Packages
                    .Include(p => p.PackageInclusions)
                    .FirstOrDefaultAsync(p => p.PackageID == request.PackageID);

                if (existingPackage == null) return NotFound();

                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);

                // === IMAGE HANDLING LOGIC ===

                // 1. Split current DB string into a List
                var currentImages = string.IsNullOrEmpty(existingPackage.ImageURL)
                    ? new List<string>()
                    : existingPackage.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                // 2. Remove images marked for deletion by the user
                if (request.DeleteImagePaths != null && request.DeleteImagePaths.Any())
                {
                    foreach (var pathToDelete in request.DeleteImagePaths)
                    {
                        if (currentImages.Contains(pathToDelete))
                        {
                            currentImages.Remove(pathToDelete);
                            DeleteImageFile(pathToDelete); // Physically delete file
                        }
                    }
                }

                // 3. Add NEW uploaded images
                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    // If the only existing image is the default one, clear it so we only have the new real photos
                    if (currentImages.Count == 1 && currentImages[0].Contains("default-package.jpg"))
                    {
                        currentImages.Clear();
                    }

                    foreach (var file in request.ImageFiles)
                    {
                        currentImages.Add(await HandleImageUpload(file));
                    }
                }

                // 4. Safety: If list is empty after deletions, put default back
                if (currentImages.Count == 0)
                {
                    currentImages.Add("/img/default-package.jpg");
                }

                // 5. Join back into string and save (Use Distinct to avoid duplicates)
                existingPackage.ImageURL = string.Join(";", currentImages.Distinct());
                // === END IMAGE HANDLING ===

                existingPackage.PackageName = request.PackageName;
                existingPackage.CategoryID = categoryId;
                existingPackage.Description = request.Description;
                existingPackage.Destination = request.Destination;
                existingPackage.Price = request.Price;
                existingPackage.StartDate = request.StartDate;
                existingPackage.EndDate = request.EndDate;
                existingPackage.AvailableSlots = request.AvailableSlots;

                // Update Inclusions
                var existingInclusions = existingPackage.PackageInclusions.ToList();
                _context.PackageInclusions.RemoveRange(existingInclusions);
                if (request.Inclusions != null)
                {
                    foreach (var inc in request.Inclusions.Where(i => !string.IsNullOrWhiteSpace(i)))
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = existingPackage.PackageID, InclusionItem = inc.Trim() });
                    }
                }

                await _context.SaveChangesAsync();

                var viewModel = await LoadViewModelAsync();
                viewModel.Message = "Package updated successfully!";
                viewModel.IsSuccess = true;
                return View("PackageManagement", viewModel);
            }
            catch (Exception ex)
            {
                var vm = await LoadViewModelAsync();
                vm.Message = $"Error: {ex.Message}";
                vm.IsSuccess = false;
                return View("PackageManagement", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package != null)
            {
                var hasBookings = await _context.Bookings.AnyAsync(b => b.PackageID == id);
                if (hasBookings)
                {
                    var vm = await LoadViewModelAsync();
                    vm.Message = "Cannot delete package with bookings.";
                    vm.IsSuccess = false;
                    return View("PackageManagement", vm);
                }

                var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (images != null)
                {
                    foreach (var img in images) DeleteImageFile(img);
                }
                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();

                var successVm = await LoadViewModelAsync();
                successVm.Message = "Deleted successfully";
                successVm.IsSuccess = true;
                return View("PackageManagement", successVm);
            }
            return RedirectToAction("PackageManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            var viewModel = await LoadViewModelAsync();
            return View("PackageManagement", viewModel);
        }

        // --- Helpers ---

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

        private void DeleteImageFile(string? imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.Contains("default-package.jpg"))
            {
                try
                {
                    // This converts "/img/file.jpg" to physical path
                    var path = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace("/", "\\"));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                catch { /* Ignore errors during deletion */ }
            }
        }

        private async Task<string> HandleImageUpload(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return "/img/default-package.jpg";

            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
            {
                switch (imageFile.ContentType.ToLower())
                {
                    case "image/jpeg": ext = ".jpg"; break;
                    case "image/png": ext = ".png"; break;
                    case "image/gif": ext = ".gif"; break;
                    case "image/webp": ext = ".webp"; break;
                    default: ext = ".jpg"; break;
                }
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!allowedExtensions.Contains(ext))
            {
                throw new InvalidOperationException($"Invalid file extension: {ext}");
            }

            // Create unique filename
            var fileName = Guid.NewGuid().ToString() + ext;

            // Define physical path: wwwroot/img/
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Save the file physically
            using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return the WEB path to store in Database
            return $"/img/{fileName}";
        }
        //ok
        private async Task<PackageManagementViewModel> LoadViewModelAsync()
        {
            return new PackageManagementViewModel
            {
                Packages = await _context.Packages.Include(p => p.Category).Include(p => p.PackageInclusions).OrderByDescending(p => p.PackageID).ToListAsync(),
                Categories = await _context.PackageCategories.OrderBy(c => c.CategoryName).ToListAsync()
            };
        }
    }
}