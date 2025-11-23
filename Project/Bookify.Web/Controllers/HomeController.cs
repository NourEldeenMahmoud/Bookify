using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
