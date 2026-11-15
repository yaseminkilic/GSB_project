using Microsoft.AspNetCore.Authorization;
using GSB_project.WebApi.Security;
using GSB_project.Application.Registrations.Commands.CreateRegistration;
using GSB_project.Application.Registrations.Queries.GetRegistrationById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchTemplate.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = ApiSecurity.Manage)]
public class RegistrationsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRegistrationByIdQuery(id), cancellationToken);

        if (result is null)
            return NotFound(new { Message = $"{id} ID'li kayıt bulunamadı." });

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRegistrationCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }
}

