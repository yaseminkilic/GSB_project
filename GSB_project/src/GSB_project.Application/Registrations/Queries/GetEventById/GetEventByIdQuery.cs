using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Application.Registrations.Queries.GetEventById
{
    public record GetEventByIdQuery(int Id) : IRequest<Event?>;

    public class GetEventByIdQueryHandler(IApplicationDbContext context)
        : IRequestHandler<GetEventByIdQuery, Event?>
    {
        public async Task<Event?> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
        {
            return await context.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        }
    }
}
