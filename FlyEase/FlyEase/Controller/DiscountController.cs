// [file name]: DiscountController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlyEase.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FlyEase.Controllers
{
    public class DiscountController : Controller
    {
        private readonly FlyEaseDbContext _context;

        public DiscountController(FlyEaseDbContext context)
        {
            _context = context;
        }

        // GET: /Discount/Discounts
        public async Task<IActionResult> Discounts()
        {
            // Retrieve all discounts from existing database
            var discounts = await _context.DiscountTypes
                .OrderBy(d => d.DiscountName)
                .ToListAsync();

            return View(discounts);
        }

        // GET: /Discount/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var discount = await _context.DiscountTypes
                .FirstOrDefaultAsync(d => d.DiscountTypeID == id);

            if (discount == null)
            {
                return NotFound();
            }

            return View(discount);
        }
    }
}
