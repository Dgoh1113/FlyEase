using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using FlyEase.ViewModels;
using Microsoft.AspNetCore.Authorization;
using FlyEase.Services;

namespace FlyEase.Controllers
{
    // Allow both Admin and Staff to manage packages
    [Authorize(Roles = "Admin, Staff")]
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly StripeService _stripeService;
        private readonly EmailService _emailService;
        private const long MAX_UPLOAD_BYTES = 104857600;

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MAX_UPLOAD_BYTES)]
        [RequestFormLimits(MultipartBodyLengthLimit = MAX_UPLOAD_BYTES)]
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

                var imagePaths = new List<string>();
                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    foreach (var file in request.ImageFiles) imagePaths.Add(await HandleImageUpload(file));
                }
                else
                {
                    imagePaths.Add("/img/default-package.jpg");
                }

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
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = package.PackageID, InclusionItem = inc.Trim() });
                    }
                }

                if (request.Itinerary != null && request.Itinerary.Any())
                {
                    foreach (var item in request.Itinerary)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Title))
                        {
                            _context.PackageItineraries.Add(new PackageItinerary
                            {
                                PackageID = package.PackageID,
                                DayNumber = item.DayNumber,
                                Title = item.Title,
                                ActivityDescription = item.ActivityDescription
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

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
                .Include(p => p.Itinerary)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package != null)
            {
                viewModel.Package = package;
                viewModel.EditingPackageId = id;
                viewModel.SelectedCategoryId = package.CategoryID;
                viewModel.Inclusions = package.PackageInclusions.Select(pi => pi.InclusionItem).ToList();
                viewModel.Itinerary = package.Itinerary.OrderBy(i => i.DayNumber).ToList();
            }
            return View("PackageManagement", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MAX_UPLOAD_BYTES)]
        [RequestFormLimits(MultipartBodyLengthLimit = MAX_UPLOAD_BYTES)]
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
                    .Include(p => p.Itinerary)
                    .FirstOrDefaultAsync(p => p.PackageID == request.PackageID);

                if (existingPackage == null) return NotFound();

                int categoryId = await GetOrCreateCategoryId(request.CategoryID, request.NewCategoryName);

                var currentImages = string.IsNullOrEmpty(existingPackage.ImageURL)
                    ? new List<string>()
                    : existingPackage.ImageURL.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                if (request.DeleteImagePaths != null)
                {
                    foreach (var path in request.DeleteImagePaths)
                    {
                        if (currentImages.Contains(path))
                        {
                            currentImages.Remove(path);
                            DeleteImageFile(path);
                        }
                    }
                }

                if (request.ImageFiles != null && request.ImageFiles.Count > 0)
                {
                    if (currentImages.Count == 1 && currentImages[0].Contains("default-package.jpg")) currentImages.Clear();
                    foreach (var file in request.ImageFiles) currentImages.Add(await HandleImageUpload(file));
                }

                if (currentImages.Count == 0) currentImages.Add("/img/default-package.jpg");
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
                    {
                        _context.PackageInclusions.Add(new PackageInclusion { PackageID = existingPackage.PackageID, InclusionItem = inc.Trim() });
                    }
                }

                if (existingPackage.Itinerary.Any()) _context.PackageItineraries.RemoveRange(existingPackage.Itinerary);

                if (request.Itinerary != null && request.Itinerary.Any())
                {
                    foreach (var item in request.Itinerary)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Title))
                        {
                            _context.PackageItineraries.Add(new PackageItinerary
                            {
                                PackageID = existingPackage.PackageID,
                                DayNumber = item.DayNumber,
                                Title = item.Title,
                                ActivityDescription = item.ActivityDescription
                            });
                        }
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

        // =========================================================
        // UPDATED DELETE LOGIC: FORCE DELETE WITH REFUND
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var package = await _context.Packages
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.Payments)
                .Include(p => p.Bookings)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(p => p.PackageID == id);

            if (package != null)
            {
                // 1. Process Refunds & Notifications for existing bookings
                if (package.Bookings.Any())
                {
                    // Create a list to iterate over safely while modifying the context
                    var bookingsToDelete = package.Bookings.ToList();

                    foreach (var booking in bookingsToDelete)
                    {
                        decimal refundedAmount = 0;

                        // Refund Payments
                        foreach (var payment in booking.Payments)
                        {
                            if ((payment.PaymentStatus == "Completed" || payment.PaymentStatus == "Deposit")
                                && !string.IsNullOrEmpty(payment.TransactionID))
                            {
                                // Stripe Refund
                                if (payment.PaymentMethod.Contains("Credit Card") || payment.PaymentMethod.Contains("Stripe"))
                                {
                                    try
                                    {
                                        await _stripeService.RefundPaymentAsync(payment.TransactionID);
                                        refundedAmount += payment.AmountPaid;
                                    }
                                    catch
                                    {
                                        // If refund fails, we proceed with delete as per "Force Delete" instruction
                                    }
                                }
                            }
                        }

                        // Send Email Notification
                        if (booking.User != null)
                        {
                            await _emailService.SendRefundNotification(
                                booking.User.Email,
                                booking.User.FullName,
                                package.PackageName,
                                refundedAmount
                            );
                        }

                        // *** CRITICAL FIX: Remove the booking to resolve the Constraint Exception ***
                        // This allows the package to be deleted since the dependent records are gone.
                        _context.Bookings.Remove(booking);
                    }
                }

                // 2. Delete Images
                var images = package.ImageURL?.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (images != null) foreach (var img in images) DeleteImageFile(img);

                // 3. Delete Package
                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();

                var successVm = await LoadViewModelAsync();
                successVm.Message = "Package force deleted. Refunds processed and bookings removed.";
                successVm.IsSuccess = true;
                return View("PackageManagement", successVm);
            }
            return RedirectToAction("PackageManagement");
        }

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
                    var path = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace("/", "\\"));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                catch { }
            }
        }

        private async Task<string> HandleImageUpload(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0) return "/img/default-package.jpg";
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var fileName = Guid.NewGuid().ToString() + ext;
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "img");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }
            return $"/img/{fileName}";
        }

        private async Task<PackageManagementViewModel> LoadViewModelAsync()
        {
            return new PackageManagementViewModel
            {
                Packages = await _context.Packages
                    .Include(p => p.Category)
                    .Include(p => p.PackageInclusions)
                    .Include(p => p.Bookings) // Required for frontend check
                    .OrderByDescending(p => p.PackageID)
                    .ToListAsync(),
                Categories = await _context.PackageCategories.OrderBy(c => c.CategoryName).ToListAsync()
            };
        }
    }
}