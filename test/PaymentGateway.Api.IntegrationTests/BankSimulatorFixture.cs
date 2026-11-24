using System.Net;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace PaymentGateway.Api.IntegrationTests;

public class BankSimulatorFixture : IAsyncLifetime
{
    private const int ImposterPort = 8080;

    private readonly IContainer _container = new ContainerBuilder("bbyars/mountebank:2.8.1")
        .WithBindMount(FindImpostersDirectory(), "/imposters")
        .WithCommand("--configfile", "/imposters/bank_simulator.ejs", "--allowInjection")
        .WithPortBinding(ImposterPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(ImposterPort)
            .ForStatusCode(HttpStatusCode.BadRequest)))
        .Build();

    public string BaseUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ImposterPort)}/";

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private static string FindImpostersDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var imposters = Path.Combine(directory.FullName, "imposters");
            if (File.Exists(Path.Combine(imposters, "bank_simulator.ejs")))
            {
                return imposters;
            }
        }

        throw new DirectoryNotFoundException("Could not find the imposters directory above the test output folder.");
    }
}
