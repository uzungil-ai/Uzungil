using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace Roma.Pages.Admin.Destinations
{
    public class GenerateDescriptionModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly Channel<Roma.Services.AiJob> _aiChannel;

        public GenerateDescriptionModel(ApplicationDbContext db, Channel<Roma.Services.AiJob> aiChannel)
        {
            _db = db;
            _aiChannel = aiChannel;
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var dest = await _db.Destinations!.Where(d => d.Id == id).FirstOrDefaultAsync();
            if (dest == null) return NotFound();

            var job = new Roma.Services.AiJob { DestinationId = id, JobType = "Description", Payload = dest.Name + " " + dest.Country };
            await _aiChannel.Writer.WriteAsync(job);

            return new JsonResult(new { success = true });
        }
    }
}
