using MultiProjectSample.Core.Contracts;

namespace MultiProjectSample.Core.Services;

public sealed class MessageFormatter
{
    public string Format(Message message) => $"[{message.Text}]";
}
