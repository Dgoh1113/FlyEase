using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Linq;
using FlyEase.Data;

namespace FlyEase.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly FlyEaseDbContext _context;

        public ChatbotController(IWebHostEnvironment env, FlyEaseDbContext context)
        {
            _env = env;
            _context = context;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult GetResponse([FromBody] BotRequest request)
        {
            // 1. Validate Input
            if (request == null || string.IsNullOrEmpty(request.Message))
                return Json(new { response = "Please type a question." });

            string msg = request.Message.ToLower().Trim();

            // === STRATEGY 1: CHECK DATABASE (#101) ===
            if (msg.Contains("#"))
            {
                string idString = new string(msg.Where(char.IsDigit).ToArray());
                if (int.TryParse(idString, out int bookingId))
                {
                    var booking = _context.Bookings.FirstOrDefault(b => b.BookingID == bookingId);

                    if (booking != null)
                    {
                        return Json(new { response = $"Found it! 🎫<br>Booking ID: #{bookingId}<br>Status: <b>{booking.BookingStatus}</b>" });
                    }
                    else
                    {
                        return Json(new { response = $"I checked the system, but Booking #{bookingId} does not exist." });
                    }
                }
            }

            // === STRATEGY 2: THE BRAIN (Internal List) ===
            var rules = new List<BotRule>
            {
                new BotRule {
                    Keywords = new List<string> { "hello", "hi", "hey" },
                    Response = "Hello! 👋 I can help you navigate FlyEase. Try asking: <br>• 'How to book?' <br>• 'Check status' <br>• 'Refund policy'"
                },
                new BotRule {
                    Keywords = new List<string> { "how to book", "booking", "package" },
                    Response = "<b>How to Book:</b><br>1. Browse our <a href='/Package/Index'>Packages</a>.<br>2. Click 'View Details' on a trip.<br>3. Click 'Book Now'."
                },
                new BotRule {
                    Keywords = new List<string> { "status", "check booking" },
                    Response = "<b>Check Status:</b><br>Go to your <a href='/User/Profile'>Profile</a> or just type your Booking ID here (e.g., <b>#101</b>)."
                },
                new BotRule {
                    Keywords = new List<string> { "refund", "cancel", "money" },
                    Response = "<b>Cancellation Policy:</b><br>✅ > 7 days: Full Refund.<br>⚠️ < 3 days: No Refund.<br>Visit <a href='/User/Profile'>Profile</a> to cancel."
                },
                new BotRule {
                    Keywords = new List<string> { "pay", "payment", "card" },
                    Response = "We accept Visa, Mastercard, and FPX Online Banking."
                },
                // === 👇 ADDED MISSING PASSWORD RULE 👇 ===
                new BotRule {
                    Keywords = new List<string> { "password", "reset", "forgot" },
                    Response = "No worries! Go to the <a href='/Account/Login'>Login Page</a> and click <b>'Forgot Password'</b> to reset it."
                },
                // ===========================================
                new BotRule {
                    Keywords = new List<string> { "discount", "promo", "code" },
                    Response = "Check out our <a href='/Discount/Index'>Discounts Page</a> for the latest offers! 🎁"
                },
                new BotRule {
                    Keywords = new List<string> { "contact", "support", "human", "help" },
                    Response = "Need help? Call us at <b>+60 11-1685 8138</b> or email support@flyease.com."
                },
                new BotRule {
                    Keywords = new List<string> { "location", "office", "address" },
                    Response = "<b>FlyEase HQ:</b><br>Level 5, Menara FlyEase,<br>Jalan Tun Razak, Kuala Lumpur."
                }
            };

            // === STRATEGY 3: FIND MATCH ===
            foreach (var rule in rules)
            {
                if (rule.Keywords.Any(k => msg.Contains(k)))
                {
                    return Json(new { response = rule.Response });
                }
            }

            // === STRATEGY 4: FALLBACK ===
            return Json(new { response = "I'm not sure about that. Try asking 'How to book' or 'Refund policy'." });
        }
    }

    public class BotRequest { public string Message { get; set; } }
    public class BotRule { public List<string> Keywords { get; set; } public string Response { get; set; } }
}