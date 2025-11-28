// [file name]: PaymentController.cs
using Microsoft.AspNetCore.Mvc;
using FlyEase.ViewModels;
using System;

namespace FlyEase.Controllers
{
    public class PaymentController : Controller
    {
        // STEP 1: Customer Information (GET)
        [HttpGet]
        public IActionResult CustomerInfo(int packageId = 1, int people = 1)
        {
            var vm = new BookingViewModel
            {
                PackageID = packageId,
                PackageName = "Langkawi Island Paradise",
                PackagePrice = 1200.00m,
                NumberOfPeople = people,
                TravelDate = DateTime.Now.AddDays(14),
                BasePrice = 1200.00m * people,
                CurrentStep = 1
            };

            CalculateDiscounts(vm);
            return View(vm);
        }

        // STEP 1: Customer Information (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CustomerInfo(BookingViewModel model)
        {
            model.CurrentStep = 1;
            CalculateDiscounts(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            HttpContext.Session.SetString("BookingData", System.Text.Json.JsonSerializer.Serialize(model));
            return RedirectToAction("PaymentDetails");
        }

        // STEP 2: Payment Details (GET)
        [HttpGet]
        public IActionResult PaymentDetails()
        {
            var bookingData = HttpContext.Session.GetString("BookingData");
            if (string.IsNullOrEmpty(bookingData))
            {
                return RedirectToAction("CustomerInfo");
            }

            var model = System.Text.Json.JsonSerializer.Deserialize<BookingViewModel>(bookingData);
            model.CurrentStep = 2;
            CalculateDiscounts(model);

            return View(model);
        }

        // STEP 2: Payment Details (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PaymentDetails(BookingViewModel model)
        {
            var bookingData = HttpContext.Session.GetString("BookingData");
            if (string.IsNullOrEmpty(bookingData))
            {
                return RedirectToAction("CustomerInfo");
            }

            var baseModel = System.Text.Json.JsonSerializer.Deserialize<BookingViewModel>(bookingData);

            // Update with payment details
            baseModel.PaymentMethod = model.PaymentMethod;
            baseModel.CardNumber = model.CardNumber;
            baseModel.CardHolderName = model.CardHolderName;
            baseModel.ExpiryDate = model.ExpiryDate;
            baseModel.CVV = model.CVV;
            baseModel.CurrentStep = 2;

            if (!ModelState.IsValid)
            {
                CalculateDiscounts(baseModel);
                return View(baseModel);
            }

            HttpContext.Session.SetString("BookingData", System.Text.Json.JsonSerializer.Serialize(baseModel));
            return RedirectToAction("Confirmation");
        }

        // STEP 3: Confirmation (GET)
        [HttpGet]
        public IActionResult Confirmation()
        {
            var bookingData = HttpContext.Session.GetString("BookingData");
            if (string.IsNullOrEmpty(bookingData))
            {
                return RedirectToAction("CustomerInfo");
            }

            var model = System.Text.Json.JsonSerializer.Deserialize<BookingViewModel>(bookingData);
            model.CurrentStep = 3;
            CalculateDiscounts(model);

            return View(model);
        }

        // STEP 3: Process Booking (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessBooking()
        {
            // Clear session after successful booking
            HttpContext.Session.Remove("BookingData");
            return RedirectToAction("Success");
        }

        // STEP 4: Success Page
        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }

        private void CalculateDiscounts(BookingViewModel model)
        {
            decimal discount = 0;
            decimal basePrice = model.PackagePrice * model.NumberOfPeople;

            // Early Bird Discount (30 days in advance)
            if ((model.TravelDate - DateTime.Now).TotalDays >= 30)
            {
                discount += basePrice * 0.10m;
            }

            // Bulk Discount (5+ people)
            if (model.NumberOfPeople >= 5)
            {
                discount += basePrice * 0.15m;
            }

            model.DiscountAmount = discount;
            model.FinalAmount = basePrice - discount;
        }
    }
}