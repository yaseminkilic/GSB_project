using GSB_project.Domain.Entities;
using GSB_project.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Tests;

public class DatabaseSchemaTests
{
    [Fact]
    public async Task Context_reads_and_writes_existing_singular_table_schema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        // Independent schema: do not generate it from the EF model being tested.
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Event (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL CHECK(length(Name) <= 150),
                StartAt TEXT NOT NULL,
                Capacity INTEGER NOT NULL CHECK(Capacity > 0),
                RegisteredCount INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE Participant (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FullName TEXT NOT NULL, Email TEXT NOT NULL, Phone TEXT NULL);
            CREATE TABLE Registration (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL REFERENCES Event(Id),
                ParticipantId INTEGER NOT NULL REFERENCES Participant(Id),
                RegisteredAt TEXT NOT NULL, IsCancelled INTEGER NOT NULL DEFAULT 0);
            """;
        await command.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        var activity = new Event { Name = new string('a', 150), StartAt = DateTime.UtcNow.AddDays(1), Capacity = 10 };
        var participant = new Participant { FullName = "Test", Email = "test@example.com" };
        db.Events.Add(activity);
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        db.Registrations.Add(new Registration
        {
            EventId = activity.Id, ParticipantId = participant.Id, RegisteredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(activity.Id, (await db.Events.SingleAsync()).Id);
        Assert.Equal(participant.Id, (await db.Participants.SingleAsync()).Id);
        Assert.Equal(activity.Id, (await db.Registrations.SingleAsync()).EventId);
    }
}
