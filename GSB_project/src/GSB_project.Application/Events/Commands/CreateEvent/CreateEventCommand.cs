using FluentValidation;
using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using MediatR;

namespace GSB_project.Application.Events.Commands.CreateEvent;

public record CreateEventCommand(string Name, DateTime StartAt, int Capacity) : IRequest<int>;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Etkinlik başlığı boş olamaz.")
            .MaximumLength(150).WithMessage("Etkinlik başlığı en fazla 150 karakter olabilir.");

        RuleFor(x => x.StartAt)
            .Must(startAt => startAt > DateTime.UtcNow).WithMessage("Etkinlik tarihi gelecekte bir tarih olmalıdır.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("Kapasite 0'dan büyük olmalıdır.");
    }
}

public class CreateEventCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateEventCommand, int>
{
    public async Task<int> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var entity = new Event
        {
            Name = request.Name,
            StartAt = request.StartAt,
            Capacity = request.Capacity
        };

        context.Events.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}

