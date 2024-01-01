namespace MhuoProxy;

/// <summary>
/// 代理转发应用
/// </summary>
internal static class Program
{
    private const string Title = "Mhuo Proxy";

    private static ProxyService? _sProxyService;
    
    private static void Main(string[] args)
    {
        Console.Title = Title;
        
        _sProxyService = new ProxyService("127.0.0.1", 8888);
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnProcessExit;
        
        Thread.Sleep(-1);
    }
    
    private static void OnProcessExit(object? sender, EventArgs args)
    {
        _sProxyService?.Shutdown();
    }
}