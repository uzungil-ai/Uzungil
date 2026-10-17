using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    [Authorize(Roles = "Admin,Editor")]
    public class AltTextsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public AltTextsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public Destination? Destination { get; set; }
        public List<DestinationImage>? Images { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!.FirstOrDefaultAsync(d => d.Id == id);
            if (Destination == null) return NotFound();
            Images = await _db.DestinationImages!.Where(di => di.DestinationId == id).ToListAsync();
            return Page();
        }

        public class UpdateRequest { public int ImageId { get; set; } public string? AltText { get; set; } }

        public async Task<IActionResult> OnPostUpdateAsync([FromBody] UpdateRequest req)
        {
            if (req == null) return BadRequest();
            var img = await _db.DestinationImages!.FirstOrDefaultAsync(i => i.Id == req.ImageId);
            if (img == null) return NotFound();
            img.AltText = req.AltText;
            _db.DestinationImages.Update(img);
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostBulkUpdateAsync([FromBody] List<UpdateRequest>? items)
        {
            if (items == null || items.Count == 0) return BadRequest();
            var ids = items.Select(i => i.ImageId).ToList();
            var imgs = await _db.DestinationImages!.Where(i => ids.Contains(i.Id)).ToListAsync();
            foreach (var it in items)
            {
                var img = imgs.FirstOrDefault(x => x.Id == it.ImageId);
                if (img != null)
                {
                    img.AltText = it.AltText;
                }
            }
            _db.DestinationImages.UpdateRange(imgs);
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true, updated = imgs.Count });
        }
    }
}