using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Application.Registrations.Queries.GetRegistrationById
{
    // Request
    public record GetRegistrationByIdQuery(int Id) : IRequest<Registration?>;

    // Handler
    public class GetRegistrationByIdQueryHandler(IApplicationDbContext context)
        : IRequestHandler<GetRegistrationByIdQuery, Registration?>
    {
        public async Task<Registration?> Handle(GetRegistrationByIdQuery request, CancellationToken cancellationToken)
        {
            return await context.Registrations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        }
    }
}
