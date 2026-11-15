using Microsoft.AspNetCore.Authorization;
using GSB_project.WebApi.Security;
using GSB_project.Application.Events.Commands.CreateEvent;
using GSB_project.Application.Registrations.Queries.GetEventById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GSB_project.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = ApiSecurity.ReadEvents)]
public class EventsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEventByIdQuery(id), cancellationToken);

        if (result is null)
            return NotFound(new { Message = $"{id} ID'li etkinlik bulunamadı." });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = ApiSecurity.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateEventCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }
}

