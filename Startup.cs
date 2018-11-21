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
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MD.PersianDateTime.Core;
using System.Globalization;

namespace PlateReader
{
    public class Startup
    {
        private WebSocket _websocket;
        private PlateReaderDb _plateReaderDb;
        // private WebSocket[] _websockets;
        public string message1;
        KarabinEmbeddedLPR lpr;
        private readonly ILogger _logger;
        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            lpr = new KarabinEmbeddedLPR();
            lpr.OnCarReceived += new KarabinEmbeddedLPR.CarReceivedEvent(lpr_OnCarReceived);
            _plateReaderDb = new PlateReaderDb();
            _logger = logger;
            bool result = lpr.Connect("192.168.12.46", "usertest", "123456", 8091);
            _logger.LogInformation(result.ToString());
            Configuration = configuration;
        }

        private async void lpr_OnCarReceived(object source, KarabinEmbeddedLPR.CarReceivedEventArgs e)
        {
            var plateNumber = e.GetPlate();
            await SendPlateNumber(plateNumber);

                        var data = e.GetData();

            string[] words = data.Split('_', '~');
             var date = words[1];
            //  _logger.LogInformation("date:"+ date);

             var CameraCode = words[2];
            //  _logger.LogInformation("CameraCode:"+ CameraCode);

              var UpdateCount = words[4];
            //  _logger.LogInformation("Updatecount:"+ UpdateCount);


            // foreach (var word in words)
            // {
            //     System.Console.WriteLine($"<{word}>");
            // }
            // _logger.LogInformation("data:"+data);

            // var persianDateTime = new PersianDateTime(DateTime.Now);
            //              _logger.LogInformation("farsiDate:"+ persianDateTime);

            DateTime dt = 
                DateTime.ParseExact(date, "yyyy-M-d-H-m-s", CultureInfo.InvariantCulture);
            System.Console.WriteLine(dt);


            PersianDateTime persianDate = new PersianDateTime(dt);
            System.Console.WriteLine("FINALL DATE:"+persianDate);


            _plateReaderDb.Create(new Plate()
            {
                plateNumber = plateNumber,
                date = persianDate.ToString(),
                CameraCode = CameraCode ,
                UpdateCount = UpdateCount
            });
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
                            await Echo(context, _websocket);
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

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        public async Task SendPlateNumber(string plateNumber)
        {
            var token = CancellationToken.None;
            var plateData = Encoding.UTF8.GetBytes(plateNumber);
            var buffer = new ArraySegment<byte>(plateData);
            await _websocket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
        }
    }
    public class Plate
    {
        public ObjectId Id { get; set; }
        // [BsonElement("Id")]
        public string plateNumber { get; set; }
        public String date { get; set; }
        public string CameraCode { get; set; }
        public string UpdateCount { get; set; }
    }
    public class PlateReaderDb
    {
        MongoClient _client;
        MongoDatabase _db;

        public PlateReaderDb()
        {
            var mongoUrl = new MongoUrl("mongodb://10.1.40.28:27017/PlateReader");
            _client = new MongoClient(mongoUrl);
            _db = _client.GetServer().GetDatabase(mongoUrl.DatabaseName);
            // books below is an IMongoCollection
            // var Plate = db.GetCollection<Plate>("Plates");
        }

        public IEnumerable<Plate> GetPlates()
        {
            return _db.GetCollection<Plate>("Plates").FindAll();
        }


        public Plate GetPlate(ObjectId id)
        {
            var res = Query<Plate>.EQ(p => p.Id, id);
            return _db.GetCollection<Plate>("Plates").FindOne(res);
        }

        public Plate Create(Plate p)
        {
            _db.GetCollection<Plate>("Plates").Save(p);
            return p;
        }

        // public void Update(ObjectId id, Plate p)
        // {
        //     p.Id = id;
        //     var res = Query<Plate>.EQ(pd => pd.Id, id);
        //     var operation = Update<Plate>.Replace(p);
        //     _db.GetCollection<Plate>("Plate").Update(res, operation);
        // }
        // public void Remove(ObjectId id)
        // {
        //     var res = Query<Plate>.EQ(e => e.Id, id);
        //     var operation = _db.GetCollection<Plate>("Plate").Remove(res);
        // }
    }
}
