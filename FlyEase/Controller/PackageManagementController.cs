using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class PackageManagementController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public PackageManagementController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // GET: PackageManagement/PackageManagement - Main CRUD page
        public async Task<IActionResult> PackageManagement()
        {
            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            ViewBag.Packages = packages;

            // Initialize empty package for create form
            var newPackage = new Package();
            return View(newPackage);
        }

        // POST: PackageManagement/Create - Handle form submission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Package package)
        {
            if (ModelState.IsValid)
            {
                _context.Add(package);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Package created successfully!";
                return RedirectToAction(nameof(PackageManagement));
            }

            // If validation fails, return to PackageManagement with errors
            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            ViewBag.Packages = packages;
            return View("PackageManagement", package);
        }

        // GET: PackageManagement/Edit - Load package data into form
        public async Task<IActionResult> Edit(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null)
            {
                TempData["ErrorMessage"] = "Package not found!";
                return RedirectToAction(nameof(PackageManagement));
            }

            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            ViewBag.Packages = packages;
            ViewBag.EditingId = id;

            return View("PackageManagement", package);
        }

        // POST: PackageManagement/Edit - Update package
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Package package)
        {
            if (id != package.PackageID)
            {
                TempData["ErrorMessage"] = "Package ID mismatch!";
                return RedirectToAction(nameof(PackageManagement));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(package);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Package updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PackageExists(package.PackageID))
                    {
                        TempData["ErrorMessage"] = "Package not found!";
                        return RedirectToAction(nameof(PackageManagement));
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(PackageManagement));
            }

            // If validation fails
            var packages = await _context.Packages
                .Include(p => p.Category)
                .ToListAsync();

            ViewBag.Categories = await _context.PackageCategories.ToListAsync();
            ViewBag.Packages = packages;
            ViewBag.EditingId = id;

            return View("PackageManagement", package);
        }

        // POST: PackageManagement/Delete - Delete package
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package != null)
            {
                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Package deleted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Package not found!";
            }
            return RedirectToAction(nameof(PackageManagement));
        }

        private bool PackageExists(int id)
        {
            return _context.Packages.Any(e => e.PackageID == id);
        }
    }
}