using GSB_project.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Event> Events { get; }

        DbSet<Participant> Participants { get; }

        DbSet<Registration> Registrations { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
