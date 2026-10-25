using Microsoft.AspNetCore.Mvc;

namespace Roma.Controllers
{
    // Bu controller Razor view'i sunmak için kullanılır.
    // /Upload adresine GET isteği yapıldığında Views/Upload/Index.cshtml döner.
    public class UploadPageController : Controller
    {
        [HttpGet("/Upload")]
        [HttpGet("/api/TestUpload")]
        public IActionResult Index()
        {
            // Aynı view'i hem /Upload hem de /api/TestUpload (GET) istekleri için döndürüyoruz.
            // API controller POST işlemlerini yapmaya devam edecek; GET ile view sunmak için ayrı bir MVC action kullanıyoruz.
            return View("~/Views/Upload/Index.cshtml");
        }
    }
}
