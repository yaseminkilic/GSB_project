using FluentValidation;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace GSB_project.Application.Registrations.Commands.CreateRegistration;

public record CreateRegistrationCommand(int EventId, int ParticipantId) : IRequest<int>;

public class CreateRegistrationCommandValidator : AbstractValidator<CreateRegistrationCommand>
{
    public CreateRegistrationCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .MustAsync(async (eventId, ct) => await context.Events.AnyAsync(e => e.Id == eventId, ct))
            .WithMessage("Belirtilen etkinlik bulunamadı.");

        RuleFor(x => x.ParticipantId)
            .GreaterThan(0)
            .MustAsync(async (participantId, ct) => await context.Participants.AnyAsync(p => p.Id == participantId, ct))
            .WithMessage("Belirtilen katılımcı bulunamadı.");

        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await context.Registrations
                .AnyAsync(r => r.EventId == cmd.EventId && r.ParticipantId == cmd.ParticipantId, ct))
            .WithMessage("Bu katılımcı bu etkinliğe zaten kayıtlı.");
    }
}

public class CreateRegistrationCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateRegistrationCommand, int>
{
    public async Task<int> Handle(CreateRegistrationCommand request, CancellationToken cancellationToken)
    {
        var entity = new Registration
        {
            EventId = request.EventId,
            ParticipantId = request.ParticipantId,
            RegisteredAt = DateTime.UtcNow
        };

        context.Registrations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}