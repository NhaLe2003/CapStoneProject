using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apikey;
    private readonly IMongoDatabase _db;

    public ChatController(IConfiguration config, IMongoDatabase db)
    {
        _httpClient = new HttpClient();
        _apikey = config["GeminiApiKey"]!;
        _db = db;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] Request request)
    {
        //Get OEE Collection in DataBase
        var oeeCol = _db.GetCollection<OEEValue>("OEEValue");
        var oeeList = await oeeCol
            .Find(FilterDefinition<OEEValue>.Empty)
            .SortBy(v => v.TimeStamp)
            .Limit(100)
            .ToListAsync();

        // 2) Lấy OrderLogs
        var logCol = _db.GetCollection<OrderLog>("OrderLogs");
        var logList = await logCol
            .Find(FilterDefinition<OrderLog>.Empty)
            .SortBy(l => l.TimeStamp)
            .Limit(100)
            .ToListAsync();

        // 3) Lấy ErrorLogs
        var errDocs = await _db
          .GetCollection<BsonDocument>("ErrorLog")
          .Find(FilterDefinition<BsonDocument>.Empty)
          .Sort(Builders<BsonDocument>.Sort.Ascending("ErrorStart"))
          .Limit(100)
          .ToListAsync();

        // 4) Tự map vào List<ErrorLog>, bỏ qua _id
        var errList = errDocs.Select(doc => new ErrorLog
        {
            OrderID = doc.GetValue("OrderID", "").AsString,
            Station = doc.GetValue("Station", "").AsString,
            ErrorStart = doc.GetValue("ErrorStart", BsonNull.Value).ToNullableUniversalTime()
                              ?? DateTime.MinValue,
            DurationSec = doc.GetValue("DurationSec", 0).ToDouble()
        }).ToList();

        // 4) Lấy Master Orders
        var orderCol = _db.GetCollection<Order>("Order");
        var orderList = await orderCol
            .Find(FilterDefinition<Order>.Empty)
            .SortBy(o => o.TimeStamp)
            .Limit(100)
            .ToListAsync();


        // merger to 1 json file
        var context = new
        {
            OEEValues = oeeList,
            OrderLogs = logList,
            ErrorLogs = errList,
            Orders = orderList
        };
        var contextJson = JsonConvert.SerializeObject(context, Formatting.Indented);


        //Create Prompt
        //var oeeJson = JsonConvert.SerializeObject(oeeList, Formatting.Indented);

        //Create System Prompt
        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("Bạn là trợ lý OEE sản xuất có quyền truy cập vào các nguồn dữ liệu sau:");
        systemPrompt.AppendLine("1) OEEValues: OEE thời gian thực cho từng trạm."); //Question about orders in line
        systemPrompt.AppendLine("2) OrderLogs: lịch sử các đơn hàng đã xử lý."); // Question about general knowledge
        //systemPrompt.AppendLine("3) Please answer as briefly as possible if the question is related to general knowledge");
        systemPrompt.Append("3) ErrorLogs: ghi lại các lỗi của trạm.");
        systemPrompt.Append("4) Orders: Orders tổng đang chờ xử lý.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("Sau đây là dữ liệu JSON RAW (chỉ sử dụng để tra cứu dữ liệu):");
        systemPrompt.AppendLine("Câu trả lời ở dạng văn bản không được ở dạng khác như Json");
        systemPrompt.AppendLine(contextJson);
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("Câu hỏi: " + request.Prompt);

        //Call API
        var url = $"_API to the Gemini";
        var payload = new
        {
            contents = new[] {
                new {
                    parts = new[] {
                        new { text = systemPrompt.ToString() }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        var resultText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, resultText);

        var j = JObject.Parse(resultText);
        var reply = j["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

        return Ok(new { reply });
    }
}