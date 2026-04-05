using MultiProjectSample.Core.Contracts;
using MultiProjectSample.Core.Services;

namespace MultiProjectSample.App;

public sealed class AppRunner
{
    private readonly MessageFormatter _formatter = new();

    public string Run(string text) => _formatter.Format(new Message(text));
}
