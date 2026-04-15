using BranchApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BranchApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationStore _store;

    public NotificationsController(NotificationStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetRecent()
    {
        return Ok(_store.GetRecent());
    }
}
