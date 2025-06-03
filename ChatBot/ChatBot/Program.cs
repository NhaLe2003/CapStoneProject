using ChatBot.Services;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.UseUrls("http://*:5000");


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});


var config = builder.Configuration;
string plcIp = config["PLCIp"]!;
string apiBaseUrl = config["OrderApiBaseUrl"]!;
string mongoConn = config["MongoConnection"]!;
string mongoDbName = config["MongoDatabase"]!;

Console.WriteLine($"[Startup] PLC IP from config: '{plcIp}'");

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(mongoConn));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDbName));


builder.Services.AddSingleton<PLC>(sp =>
    new PLC(
        ip: plcIp,
        name: "PLC_1",
        apiBaseUrl: apiBaseUrl
    ));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();


_ = app.Services.GetRequiredService<PLC>();


app.UseCors("AllowAll");      // Alow all IP address http
app.UseDefaultFiles();        // allow all index.html
app.UseStaticFiles();         // === ===


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();
