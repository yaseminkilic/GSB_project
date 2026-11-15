using GSB_project.Application.Events.Commands.CreateEvent;

namespace GSB_project.Tests;

public class EventValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_name_is_rejected(string? name)
    {
        var result = await new CreateEventCommandValidator().ValidateAsync(new CreateEventCommand(name!, DateTime.UtcNow.AddDays(1), 1));
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public async Task Name_length_boundary_is_enforced(int length, bool valid)
    {
        var result = await new CreateEventCommandValidator().ValidateAsync(new CreateEventCommand(new string('a', length), DateTime.UtcNow.AddDays(1), 1));
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Capacity_boundary_is_enforced(int capacity, bool valid)
    {
        var result = await new CreateEventCommandValidator().ValidateAsync(new CreateEventCommand("Test", DateTime.UtcNow.AddDays(1), capacity));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task Date_is_compared_with_validation_time()
    {
        var validator = new CreateEventCommandValidator();
        // A date captured after construction is still in the past at validation.
        var command = new CreateEventCommand("Test", DateTime.UtcNow, 1);
        var result = await validator.ValidateAsync(command);
        Assert.Contains(result.Errors, e => e.PropertyName == "StartAt");
    }
}


