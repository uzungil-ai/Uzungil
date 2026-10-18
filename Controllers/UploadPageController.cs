using Microsoft.AspNetCore.Mvc;

namespace Roma.Controllers
{
    // Bu controller Razor view'i sunmak için kullanılır.
    // /Upload adresine GET isteği yapıldığında Views/Upload/Index.cshtml döner.
    public class UploadPageController : Controller
    {
        [HttpGet("/Upload")]
        public IActionResult Index()
        {
            // Açık yol ile view döndürerek view'in konumunu kesinleştiriyoruz.
            return View("~/Views/Upload/Index.cshtml");
        }
    }
}
