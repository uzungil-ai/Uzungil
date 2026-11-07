using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using Roma.Services;

namespace Roma.Pages.Admin;

[Authorize]
public class ContentEditorModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ContentEditorModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Destination> Destinations { get; set; } = new();
    public List<DestinationImage> DestinationImages { get; set; } = new();
    public List<Attraction> Attractions { get; set; } = new();
    public List<Flavor> Flavors { get; set; } = new();
    public List<Parking> Parkings { get; set; } = new();
    public List<Hotel> Hotels { get; set; } = new();
    public List<Meal> Meals { get; set; } = new();
    public List<DemoChatService> ChatTexts { get; set; } = new();

    public async Task OnGetAsync()
    {
        Destinations = await _db.Destinations!
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        DestinationImages = await _db.DestinationImages!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Order)
            .ToListAsync();

        Attractions = await _db.Attractions!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Name)
            .ToListAsync();

        Flavors = await _db.Flavors!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Name)
            .ToListAsync();

        Parkings = await _db.Parkings!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Name)
            .ToListAsync();

        Hotels = await _db.Hotels!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Name)
            .ToListAsync();

        Meals = await _db.Meals!
            .AsNoTracking()
            .Include(x => x.Destination)
            .OrderBy(x => x.DestinationId)
            .ThenBy(x => x.Name)
            .ToListAsync();

        ChatTexts = await _db.DemoChatServices!
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}