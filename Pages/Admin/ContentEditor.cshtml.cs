using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public int DestinationImagesCount { get; set; }
    public int AttractionsCount { get; set; }
    public int FlavorsCount { get; set; }
    public int ParkingsCount { get; set; }
    public int HotelsCount { get; set; }
    public int MealsCount { get; set; }
    public int ChatTextsCount { get; set; }

    public async Task OnGetAsync()
    {
        Destinations = await _db.Destinations!
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        var destinationImagesCountTask = _db.DestinationImages!.AsNoTracking().CountAsync();
        var attractionsCountTask = _db.Attractions!.AsNoTracking().CountAsync();
        var flavorsCountTask = _db.Flavors!.AsNoTracking().CountAsync();
        var parkingsCountTask = _db.Parkings!.AsNoTracking().CountAsync();
        var hotelsCountTask = _db.Hotels!.AsNoTracking().CountAsync();
        var mealsCountTask = _db.Meals!.AsNoTracking().CountAsync();
        var chatTextsCountTask = _db.DemoChatServices!.AsNoTracking().CountAsync();

        await Task.WhenAll(
            destinationImagesCountTask,
            attractionsCountTask,
            flavorsCountTask,
            parkingsCountTask,
            hotelsCountTask,
            mealsCountTask,
            chatTextsCountTask
        );

        DestinationImagesCount = destinationImagesCountTask.Result;
        AttractionsCount = attractionsCountTask.Result;
        FlavorsCount = flavorsCountTask.Result;
        ParkingsCount = parkingsCountTask.Result;
        HotelsCount = hotelsCountTask.Result;
        MealsCount = mealsCountTask.Result;
        ChatTextsCount = chatTextsCountTask.Result;
    }

    public async Task<IActionResult> OnGetSectionAsync(string section)
    {
        var key = (section ?? string.Empty).Trim().ToLowerInvariant();

        switch (key)
        {
            case "images":
            {
                var items = await _db.DestinationImages!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Order)
                    .Select(x => new
                    {
                        id = x.Id,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        altText = x.AltText ?? string.Empty,
                        editUrl = Url.Action("Edit", "DestinationImages", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "DestinationImages", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "attractions":
            {
                var items = await _db.Attractions!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Name)
                    .Select(x => new
                    {
                        id = x.Id,
                        name = x.Name,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        editUrl = Url.Action("Edit", "Attractions", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "Attractions", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "flavors":
            {
                var items = await _db.Flavors!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Name)
                    .Select(x => new
                    {
                        id = x.Id,
                        name = x.Name,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        editUrl = Url.Action("Edit", "Flavors", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "Flavors", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "parkings":
            {
                var items = await _db.Parkings!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Name)
                    .Select(x => new
                    {
                        id = x.Id,
                        name = x.Name,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        editUrl = Url.Action("Edit", "Parkings", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "Parkings", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "hotels":
            {
                var items = await _db.Hotels!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Name)
                    .Select(x => new
                    {
                        id = x.Id,
                        name = x.Name,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        editUrl = Url.Action("Edit", "Hotels", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "Hotels", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "meals":
            {
                var items = await _db.Meals!
                    .AsNoTracking()
                    .Include(x => x.Destination)
                    .OrderBy(x => x.DestinationId)
                    .ThenBy(x => x.Name)
                    .Select(x => new
                    {
                        id = x.Id,
                        name = x.Name,
                        destinationName = x.Destination != null ? x.Destination.Name : string.Empty,
                        editUrl = Url.Action("Edit", "Meals", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "Meals", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            case "chattexts":
            {
                var items = await _db.DemoChatServices!
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new
                    {
                        id = x.Id,
                        input = x.Input,
                        reply = x.Reply,
                        editUrl = Url.Action("Edit", "DemoChatServices", new { id = x.Id }) ?? "#",
                        deleteUrl = Url.Action("Delete", "DemoChatServices", new { id = x.Id }) ?? "#"
                    })
                    .ToListAsync();

                return new JsonResult(new { items });
            }
            default:
                return new JsonResult(new { items = Array.Empty<object>() });
        }
    }
}