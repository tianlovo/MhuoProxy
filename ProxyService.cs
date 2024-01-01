using System.Net;
using System.Net.Security;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace MhuoProxy;

/// <summary>
/// 代理服务类，用于管理代理服务器和请求重定向
/// </summary>
internal class ProxyService
{
    private const string QueryGatewayRequestString = "query_gateway";

    // 需要重定向的域名列表
    private static readonly string[] SRedirectDomains =
    {
        ".bhsr.com",
        ".starrails.com",
        ".hoyoverse.com",
        ".mihoyo.com"
    };

    private readonly ProxyServer _webProxyServer;
    private readonly string _targetRedirectHost;
    private readonly int _targetRedirectPort;

    /// <summary>
    /// 代理服务类的构造函数
    /// </summary>
    /// <param name="targetRedirectHost">目标重定向主机名</param>
    /// <param name="targetRedirectPort">目标重定向端口号</param>
    public ProxyService(string targetRedirectHost, int targetRedirectPort)
    {
        _webProxyServer = new ProxyServer();
        _webProxyServer.CertificateManager.EnsureRootCertificate();

        _webProxyServer.BeforeRequest += BeforeRequest;
        _webProxyServer.ServerCertificateValidationCallback += OnCertValidation;

        _targetRedirectHost = targetRedirectHost;
        _targetRedirectPort = targetRedirectPort;

        SetEndPoint(new ExplicitProxyEndPoint(IPAddress.Any, 8080));
    }

    /// <summary>
    /// 设置代理服务器端点和配置
    /// </summary>
    /// <param name="explicitEp">代理服务器端点和配置</param>
    private void SetEndPoint(ExplicitProxyEndPoint explicitEp)
    {
        explicitEp.BeforeTunnelConnectRequest += BeforeTunnelConnectRequest;

        _webProxyServer.AddEndPoint(explicitEp);
        _webProxyServer.Start();

        // 将代理服务器设置为系统的 HTTP 和 HTTPS 代理
        _webProxyServer.SetAsSystemHttpProxy(explicitEp);
        _webProxyServer.SetAsSystemHttpsProxy(explicitEp);
    }

    /// <summary>
    /// 关闭代理服务
    /// </summary>
    public void Shutdown()
    {
        _webProxyServer.DisableAllSystemProxies();
        _webProxyServer.Stop();
        _webProxyServer.Dispose();
    }

    /// <summary>
    /// SSL 隧道连接请求前的操作
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="args">事件参数</param>
    private static Task BeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs args)
    {
        var hostname = args.HttpClient.Request.RequestUri.Host;
        // Console.WriteLine(hostname);
        
        // 如果要重定向则解密SSL
        args.DecryptSsl = ShouldRedirect(hostname);

        return Task.CompletedTask;
    }

    /// <summary>
    /// SSL 证书验证操作
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="args">事件参数</param>
    private static Task OnCertValidation(object sender, CertificateValidationEventArgs args)
    {
        // 没有错误则通过SSL验证
        if (args.SslPolicyErrors == SslPolicyErrors.None)
            args.IsValid = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 请求处理前的操作
    /// <br/>
    /// 这里检查请求的主机名是否需要重定向，如果需要，会对请求的 URL 进行修改以实现重定向功能
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="args">事件参数</param>
    private Task BeforeRequest(object sender, SessionEventArgs args)
    {
        var hostname = args.HttpClient.Request.RequestUri.Host;

        if (!ShouldRedirect(hostname) && (
                hostname != _targetRedirectHost ||
                !args.HttpClient.Request.RequestUri.AbsolutePath.Contains(QueryGatewayRequestString)
            )
           )
        {
            return Task.CompletedTask;
        }
        
        var requestUrl = args.HttpClient.Request.Url;
        var local = new Uri($"http://{_targetRedirectHost}:{_targetRedirectPort}/");

        var replacedUrl = new UriBuilder(requestUrl)
        {
            Scheme = local.Scheme,
            Host = local.Host,
            Port = local.Port
        }.Uri.ToString();

        Console.WriteLine(hostname + " => 重定向 => " + replacedUrl);
        args.HttpClient.Request.Url = replacedUrl;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断是否需要重定向
    /// <br/>
    /// 检查的依据是主机名是否以特定的域名结尾，如果是则返回 true，表示需要重定向
    /// </summary>
    /// <param name="hostname">要判断的主机名称</param>
    /// <returns>是否需要重定向</returns>
    private static bool ShouldRedirect(string hostname)
    {
        return SRedirectDomains.Any(hostname.EndsWith);
    }
}
