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
using System.Text;

namespace PlateReader
{
    public class Startup
    {
        private WebSocket _websocket;
        private WebSocket[] _websockets;
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

        private async void lpr_OnCarReceived(object source, KarabinEmbeddedLPR.CarReceivedEventArgs e)
        {
            var plateNumber = e.GetPlate();
            await SendPlateNumber(_websocket, plateNumber);
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
                    _websocket = await context.WebSockets.AcceptWebSocketAsync();
                    _websockets.Append(_websocket);
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

        }

        public async Task SendPlateNumber(WebSocket webSocket, string plateNumber)
        {
            var token = CancellationToken.None;
            var plateData = Encoding.UTF8.GetBytes(plateNumber);
            var buffer = new ArraySegment<byte>(plateData);
            // send to all opened websocket
            await Task.WhenAll(_websockets.Where(s => s.State == WebSocketState.Open)
                       .Select(s => s.SendAsync(buffer, WebSocketMessageType.Text, true, token)));
        }
    }
}
