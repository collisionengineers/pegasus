namespace CollisionDocNet.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        int interruptCount = 0;
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            if (Interlocked.Increment(ref interruptCount) == 1)
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            }
            else
            {
                eventArgs.Cancel = false;
            }
        };
        Console.CancelKeyPress += handler;
        try
        {
            return await CliApplication.RunAsync(
                args,
                Console.OpenStandardInput(),
                Console.Out,
                Console.Error,
                PhysicalCliFileSystem.Instance,
                cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
