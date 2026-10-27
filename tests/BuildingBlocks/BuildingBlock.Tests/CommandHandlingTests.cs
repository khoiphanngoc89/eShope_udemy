using BuildingBlock.Cqrs;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Tests;

public class CommandHandlingTests
{
    [Fact]
    public async Task Send_WhenHandlerIsRegistered_ReturnsHandlerResult()
    {
        var services = new ServiceCollection();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(PingHandler).Assembly));
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new PingCommand("pong"));

        Assert.Equal("pong", result);
    }

    private sealed record PingCommand(string Message) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> Handle(PingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(command.Message);
    }
}
