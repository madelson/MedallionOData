using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.OData.Tests.Integration
{
    public class TestServer : IDisposable
    {
        private readonly string _prefix = "http://localhost:2020/" + Guid.NewGuid() + "/";
        private volatile HttpListener _listener;
        private readonly Func<Uri, string> _handler;

        public TestServer(Func<Uri, string> handler)
        {
            this._handler = handler; 
            this._listener = new HttpListener();
            this._listener.Prefixes.Add(this._prefix);
            this._listener.Start();
            Task.Run(() => this.RequestHandler());
        }

        public string Prefix { get { return this._prefix; } }

        private void RequestHandler()
        {
            while (true)
            {
                var listener = this._listener;
                if (listener == null)
                {
                    break;
                }

                var context = listener.GetContext();
                Console.WriteLine("TestServer: Received {0}", context.Request.Url);

                using (var writer = new StreamWriter(context.Response.OutputStream))
                {
                    try
                    {
                        var results = this._handler(context.Request.Url);
                        writer.Write(results);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("TestServer: hit error {0}", ex);
                        context.Response.StatusCode = 500;
                        writer.WriteLine(ex);
                    }
                }

                context.Response.Close();
            }
        }

        void IDisposable.Dispose()
        {
            if (this._listener != null)
            {
                this._listener.Stop();
                this._listener = null;
            }
        }
    }
}
