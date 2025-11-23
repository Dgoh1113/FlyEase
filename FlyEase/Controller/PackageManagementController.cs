using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.Models;

namespace FlyEase.Controllers
{
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public PackageManagementController(FlyEaseDbContext context)
        {
            _context = context;
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
                return View("Index", viewModel);
            }

            try
            {
                // Format the image URL to be relative to wwwroot/img/
                string formattedImageUrl = FormatImageUrl(request.ImageURL);

                var package = new Package
                {
                    PackageName = request.PackageName,
                    CategoryID = request.CategoryID,
                    Description = request.Description,
                    Destination = request.Destination,
                    Price = request.Price,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    AvailableSlots = request.AvailableSlots,
                    ImageURL = formattedImageUrl
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
                return View("Index", viewModel);
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error creating package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("Index", viewModel);
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
                viewModel.Inclusions = package.PackageInclusions
                    .Select(pi => pi.InclusionItem)
                    .ToList();
            }
            else
            {
                viewModel.Message = "Package not found!";
                viewModel.IsSuccess = false;
            }

            return View("Index", viewModel);
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
                return View("Index", viewModel);
            }

            try
            {
                var existingPackage = await _context.Packages
                    .Include(p => p.PackageInclusions)
                    .FirstOrDefaultAsync(p => p.PackageID == request.PackageID);

                if (existingPackage != null)
                {
                    // Format the image URL to be relative to wwwroot/img/
                    string formattedImageUrl = FormatImageUrl(request.ImageURL);

                    // Update package properties
                    existingPackage.PackageName = request.PackageName;
                    existingPackage.CategoryID = request.CategoryID;
                    existingPackage.Description = request.Description;
                    existingPackage.Destination = request.Destination;
                    existingPackage.Price = request.Price;
                    existingPackage.StartDate = request.StartDate;
                    existingPackage.EndDate = request.EndDate;
                    existingPackage.AvailableSlots = request.AvailableSlots;
                    existingPackage.ImageURL = formattedImageUrl;

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
                    return View("Index", viewModel);
                }
                else
                {
                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package not found!";
                    viewModel.IsSuccess = false;
                    return View("Index", viewModel);
                }
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error updating package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("Index", viewModel);
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
                        var PackageManagementVM = await LoadViewModelAsync();
                        PackageManagementVM.Message = "Cannot delete package that has existing bookings!";
                        PackageManagementVM.IsSuccess = false;
                        return View(PackageManagementVM);
                    }

                    _context.Packages.Remove(package);
                    await _context.SaveChangesAsync();

                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package deleted successfully!";
                    viewModel.IsSuccess = true;
                    return View("Index", viewModel);
                }
                else
                {
                    var viewModel = await LoadViewModelAsync();
                    viewModel.Message = "Package not found!";
                    viewModel.IsSuccess = false;
                    return View("Index", viewModel);
                }
            }
            catch (Exception ex)
            {
                var viewModel = await LoadViewModelAsync();
                viewModel.Message = $"Error deleting package: {ex.Message}";
                viewModel.IsSuccess = false;
                return View("Index", viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            var viewModel = await LoadViewModelAsync();
            return View("Index", viewModel);
        }

        private string FormatImageUrl(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return "/img/default-package.jpg";
            }

            // If it's already a relative path starting with /img/, return as is
            if (imageUrl.StartsWith("/img/"))
            {
                return imageUrl;
            }

            // If it's a full URL or different path, extract filename and format it
            var fileName = Path.GetFileName(imageUrl);
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