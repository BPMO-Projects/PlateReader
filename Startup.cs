using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KarabinEmbeddedLPRLibrary;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using System.Threading;

namespace PlateReader
{
    public class Startup
    {
        public string message1;
        KarabinEmbeddedLPR lpr;
        private readonly ILogger _logger;
        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            lpr = new KarabinEmbeddedLPR();
            lpr.OnCarReceived += new KarabinEmbeddedLPR.CarReceivedEvent(lpr_OnCarReceived);
            _logger = logger;
            bool result = lpr.Connect("192.168.12.46", "usertest", "123456", 8091);
            _logger.LogInformation(result.ToString());
            Configuration = configuration;
        }

        void lpr_OnCarReceived(object source, KarabinEmbeddedLPR.CarReceivedEventArgs e)
        {
            _logger.LogInformation("car received:" + e.GetPlate());
            message1 = e.GetPlate();
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            // config websocket            
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await Echo(webSocket);
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                await next();
            }

        });

            // app.UseHttpsRedirection();
            // app.UseMvc();


        }

        // private async Task Echo(HttpContext context, WebSocket webSocket)
        // {
        //     var buffer = new byte[1024 * 4];
        //     WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //     while (!result.CloseStatus.HasValue)
        //     {
        //         await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);

        //         result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        //     }
        //     await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        // }



        public async Task Echo(WebSocket webSocket)
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            for (; ; )
            {
                var result = await webSocket.ReceiveAsync(
                    buffer,
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    Console.WriteLine("{0}", message1);
                }

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer.Array, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    CancellationToken.None);
            }
        }
    }
}
