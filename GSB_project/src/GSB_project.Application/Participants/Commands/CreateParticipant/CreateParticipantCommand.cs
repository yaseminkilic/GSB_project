using FluentValidation;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Application.Participants.Commands.CreateParticipant;

public record CreateParticipantCommand(string FullName, string Email, string? Phone) : IRequest<int>;

public class CreateParticipantCommandValidator : AbstractValidator<CreateParticipantCommand>
{
    public CreateParticipantCommandValidator(IApplicationDbContext context)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .MaximumLength(150)
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MustAsync(async (email, ct) => !await context.Participants.AnyAsync(p => p.Email == email, ct))
            .WithMessage("Bu e-posta adresi zaten kayıtlı.");

        RuleFor(x => x.Phone).MaximumLength(15);
    }
}

public class CreateParticipantCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateParticipantCommand, int>
{
    public async Task<int> Handle(CreateParticipantCommand request, CancellationToken cancellationToken)
    {
        var entity = new Participant
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone
        };

        context.Participants.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
