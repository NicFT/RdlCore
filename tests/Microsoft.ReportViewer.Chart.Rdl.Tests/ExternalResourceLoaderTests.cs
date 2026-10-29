using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReportingServices.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ReportViewer.Chart.Rdl.Tests
{
    /// <summary>
    /// Covers ExternalResourceLoader.GetExternalResource's HttpClient-based rewrite
    /// (tasks/webrequest-httpclient-migration.md) - the file:// branch (File.OpenRead)
    /// and the http:// branch (HttpClient.Send), including the abort-polling path,
    /// exercised against a real in-process HttpListener rather than mocked.
    /// </summary>
    [TestClass]
    public class ExternalResourceLoaderTests
    {
        [TestMethod]
        public void GetExternalResource_FileUri_ReturnsFileBytes()
        {
            string path = Path.GetTempFileName();
            try
            {
                byte[] content = Encoding.UTF8.GetBytes("hello from disk");
                File.WriteAllBytes(path, content);

                byte[] result = ExternalResourceLoader.GetExternalResource(new Uri(path).AbsoluteUri, impersonate: false, null, null, null, 30, ExternalResourceLoader.MaxResourceSizeUnlimited, null, out string mimeType, out bool exceeded);

                CollectionAssert.AreEqual(content, result);
                Assert.IsFalse(exceeded);
                Assert.IsNotNull(mimeType);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void GetExternalResource_HttpUri_ReturnsResponseBodyAndContentType()
        {
            using var server = new LoopbackHttpServer(request =>
            {
                request.Response.ContentType = "text/plain";
                byte[] body = Encoding.UTF8.GetBytes("hello from http");
                request.Response.OutputStream.Write(body, 0, body.Length);
            });

            byte[] result = ExternalResourceLoader.GetExternalResource(server.Url, impersonate: false, null, null, null, 30, ExternalResourceLoader.MaxResourceSizeUnlimited, null, out string mimeType, out bool exceeded);

            Assert.AreEqual("hello from http", Encoding.UTF8.GetString(result));
            Assert.AreEqual("text/plain", mimeType);
            Assert.IsFalse(exceeded);
        }

        [TestMethod]
        public void GetExternalResource_ResourceExceedsMaxSize_SetsExceededFlagAndTruncates()
        {
            using var server = new LoopbackHttpServer(request =>
            {
                byte[] body = Encoding.UTF8.GetBytes(new string('x', 100));
                request.Response.OutputStream.Write(body, 0, body.Length);
            });

            byte[] result = ExternalResourceLoader.GetExternalResource(server.Url, impersonate: false, null, null, null, 30, 10, null, out _, out bool exceeded);

            Assert.IsTrue(exceeded);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetExternalResource_AbortHelperAlreadyAborted_ThrowsBeforeCompletingSlowRequest()
        {
            using var gate = new ManualResetEventSlim(false);
            using var server = new LoopbackHttpServer(request =>
            {
                gate.Wait(TimeSpan.FromSeconds(10));
                byte[] body = Encoding.UTF8.GetBytes("too slow");
                request.Response.OutputStream.Write(body, 0, body.Length);
            });

            var abortHelper = new ExternalResourceAbortHelper();
            abortHelper.Abort(default(ProcessingStatus));

            try
            {
                Assert.ThrowsExactly<TaskCanceledException>(() =>
                    ExternalResourceLoader.GetExternalResource(server.Url, impersonate: false, null, null, null, 30, ExternalResourceLoader.MaxResourceSizeUnlimited, abortHelper, out _, out _));
            }
            finally
            {
                gate.Set();
            }
        }

        [TestMethod]
        public void IsValidResourceSize_UnlimitedOrWithinBound_ReturnsTrue()
        {
            Assert.IsTrue(ExternalResourceLoader.IsValidResourceSize(ExternalResourceLoader.MaxResourceSizeUnlimited, new byte[1000]));
            Assert.IsTrue(ExternalResourceLoader.IsValidResourceSize(10, new byte[10]));
            Assert.IsFalse(ExternalResourceLoader.IsValidResourceSize(10, new byte[11]));
        }

        private sealed class LoopbackHttpServer : IDisposable
        {
            private readonly HttpListener m_listener;
            private readonly Thread m_thread;

            public string Url { get; }

            public LoopbackHttpServer(Action<HttpListenerContext> handleRequest)
            {
                int port = GetFreeTcpPort();
                Url = $"http://127.0.0.1:{port}/";
                m_listener = new HttpListener();
                m_listener.Prefixes.Add(Url);
                m_listener.Start();
                m_thread = new Thread(() =>
                {
                    try
                    {
                        HttpListenerContext context = m_listener.GetContext();
                        handleRequest(context);
                        context.Response.OutputStream.Close();
                    }
                    catch (HttpListenerException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                });
                m_thread.IsBackground = true;
                m_thread.Start();
            }

            private static int GetFreeTcpPort()
            {
                using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }

            public void Dispose()
            {
                m_listener.Stop();
                m_listener.Close();
            }
        }
    }
}
