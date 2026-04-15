using HqApi.Data;
using HqApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HqApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly HqDbContext _db;

    public MenuController(HqDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _db.Menus.OrderBy(m => m.Id).ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMenuRequest req)
    {
        var menu = new Menu
        {
            Name = req.Name,
            Category = req.Category,
            Price = req.Price,
            Cost = req.Cost,
            IsAvailable = true,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = menu.Id }, menu);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMenuRequest req)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu is null) return NotFound();

        if (req.Price.HasValue)       menu.Price = req.Price.Value;
        if (req.IsAvailable.HasValue) menu.IsAvailable = req.IsAvailable.Value;
        menu.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(menu);
    }
}

public record CreateMenuRequest(string Name, string Category, decimal Price, decimal Cost);
public record UpdateMenuRequest(decimal? Price, bool? IsAvailable);
