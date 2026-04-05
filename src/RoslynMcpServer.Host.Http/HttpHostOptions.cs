namespace RoslynMcpServer.Host.Http;

internal sealed record HttpHostOptions(string[] Urls)
{
    public static HttpHostOptions Parse(string[] args)
    {
        var urls = new List<string> { "http://localhost:3001" };

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--urls", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                urls = args[++i]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }
        }

        return new HttpHostOptions(urls.ToArray());
    }
}
