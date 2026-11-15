using GSB_project.Application.Common.Interfaces;
using GSB_project.Application.Events.Commands.CreateEvent;
using GSB_project.Application.Participants.Commands.CreateParticipant;
using GSB_project.Application.Registrations.Commands.CreateRegistration;
using GSB_project.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GSB_project.Tests;

public class CommandHandlerTests
{
    [Fact]
    public async Task Create_event_maps_fields_saves_once_and_returns_generated_id()
    {
        // Arrange: veritabani yerine mock kullanilir.
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var events = new Mock<DbSet<Event>>();
        
        Event? added = null;
        events.Setup(set => set.Add(It.IsAny<Event>()))
            .Callback<Event>(entity => added = entity);
        context.SetupGet(db => db.Events).Returns(events.Object);

        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        context.Setup(db => db.SaveChangesAsync(token)).ReturnsAsync(() =>
        {
            added!.Id = 42; // EF'nin kaydetme sirasinda kimlik atamasini temsil eder.
            return 1;
        });

        var command = new CreateEventCommand("Spor", DateTime.UtcNow.AddDays(1), 20);

        // Act
        var id = await new CreateEventCommandHandler(context.Object).Handle(command, token);

        // Assert
        Assert.Equal(42, id);
        Assert.NotNull(added);
        Assert.Equal(command.Name, added.Name);
        Assert.Equal(command.StartAt, added.StartAt);
        Assert.Equal(command.Capacity, added.Capacity);
        Assert.Equal(0, added.RegisteredCount);
        events.Verify(set => set.Add(It.IsAny<Event>()), Times.Once);
        context.Verify(db => db.SaveChangesAsync(token), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("05551234567")]
    public async Task Create_participant_preserves_contact_fields_and_returns_generated_id(string? phone)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var participants = new Mock<DbSet<Participant>>();

        Participant? added = null;
        participants.Setup(set => set.Add(It.IsAny<Participant>()))
            .Callback<Participant>(entity => added = entity);
        context.SetupGet(db => db.Participants).Returns(participants.Object);

        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        context.Setup(db => db.SaveChangesAsync(token)).ReturnsAsync(() =>
        {
            added!.Id = 17;
            return 1;
        });

        var command = new CreateParticipantCommand("Ada Yilmaz", "ada@example.com", phone);

        var id = await new CreateParticipantCommandHandler(context.Object).Handle(command, token);

        Assert.Equal(17, id);
        Assert.NotNull(added);
        Assert.Equal(command.FullName, added.FullName);
        Assert.Equal(command.Email, added.Email);
        Assert.Equal(phone, added.Phone);
        participants.Verify(set => set.Add(It.IsAny<Participant>()), Times.Once);
        context.Verify(db => db.SaveChangesAsync(token), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_registration_preserves_references_sets_utc_time_and_saves_once()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var registrations = new Mock<DbSet<Registration>>();

        Registration? added = null;
        registrations.Setup(set => set.Add(It.IsAny<Registration>()))
            .Callback<Registration>(entity => added = entity);
        context.SetupGet(db => db.Registrations).Returns(registrations.Object);

        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        context.Setup(db => db.SaveChangesAsync(token)).ReturnsAsync(() =>
        {
            added!.Id = 8;
            return 1;
        });

        var before = DateTime.UtcNow;

        var id = await new CreateRegistrationCommandHandler(context.Object)
            .Handle(new CreateRegistrationCommand(12, 34), token);

        Assert.Equal(8, id);
        Assert.NotNull(added);
        Assert.Equal(12, added.EventId);
        Assert.Equal(34, added.ParticipantId);
        Assert.False(added.IsCancelled);
        Assert.Equal(DateTimeKind.Utc, added.RegisteredAt.Kind);
        Assert.InRange(added.RegisteredAt, before, DateTime.UtcNow);
        registrations.Verify(set => set.Add(It.IsAny<Registration>()), Times.Once);
        context.Verify(db => db.SaveChangesAsync(token), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Save_failure_is_propagated_without_retrying()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var events = new Mock<DbSet<Event>>();

        context.SetupGet(db => db.Events).Returns(events.Object);

        var failure = new DbUpdateException("Save failed");
        context.Setup(db => db.SaveChangesAsync(CancellationToken.None)).ThrowsAsync(failure);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            new CreateEventCommandHandler(context.Object).Handle(
                new CreateEventCommand("Spor", DateTime.UtcNow.AddDays(1), 20), CancellationToken.None));

        Assert.Same(failure, exception);
        events.Verify(set => set.Add(It.IsAny<Event>()), Times.Once);
        context.Verify(db => db.SaveChangesAsync(CancellationToken.None), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Save_cancellation_is_propagated_with_original_token()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var events = new Mock<DbSet<Event>>();

        context.SetupGet(db => db.Events).Returns(events.Object);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        
        var token = cancellation.Token;
        context.Setup(db => db.SaveChangesAsync(token)).ThrowsAsync(new OperationCanceledException(token));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new CreateEventCommandHandler(context.Object).Handle(
                new CreateEventCommand("Spor", DateTime.UtcNow.AddDays(1), 20), token));

        Assert.Equal(token, exception.CancellationToken);
        events.Verify(set => set.Add(It.IsAny<Event>()), Times.Once);
        context.Verify(db => db.SaveChangesAsync(token), Times.Once);
        context.VerifyNoOtherCalls();
    }
}
