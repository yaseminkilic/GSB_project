using Microsoft.AspNetCore.Authorization;
using GSB_project.WebApi.Security;

using GSB_project.Application.Participants.Commands.CreateParticipant;
using GSB_project.Application.Registrations.Queries.GetParticipantById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GSB_project.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = ApiSecurity.Manage)]
public class ParticipantsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetParticipantByIdQuery(id), cancellationToken);

        if (result is null)
            return NotFound(new { Message = $"{id} ID'li katılımcı bulunamadı." });

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateParticipantCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }
}

