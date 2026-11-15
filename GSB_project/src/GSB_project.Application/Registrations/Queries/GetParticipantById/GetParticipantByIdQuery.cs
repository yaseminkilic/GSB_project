using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Application.Registrations.Queries.GetParticipantById;

public record GetParticipantByIdQuery(int Id) : IRequest<Participant?>;

public class GetParticipantByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetParticipantByIdQuery, Participant?>
{
    public async Task<Participant?> Handle(GetParticipantByIdQuery request, CancellationToken cancellationToken)
    {
        return await context.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
