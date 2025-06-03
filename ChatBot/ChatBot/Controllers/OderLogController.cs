using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ChatBot.Models;

[Route("api/[controller]")]
[ApiController]
public class OrderLogController : ControllerBase
{
    private readonly IMongoCollection<OrderLog> _collection;

    public OrderLogController(IConfiguration config)
    {
        var client = new MongoClient(config["MongoConnection"]);
        var db = client.GetDatabase(config["MongoDatabase"]);
        _collection = db.GetCollection<OrderLog>("OrderLogs");
    }

    // GET /api/OrderLog?skip=0&limit=100
    [HttpGet]
    public ActionResult<List<OrderLog>> Get(int skip = 0, int limit = 100)
    {
        var list = _collection
            .Find(FilterDefinition<OrderLog>.Empty)
            .SortByDescending(x => x.TimeStamp)
            .Skip(skip)
            .Limit(limit)
            .ToList();

        return Ok(list);
    }
    [HttpGet("all")]
    public ActionResult<List<OrderLog>> GetAll()
    {
        return _collection.Find(_ => true).ToList();
    }


    [HttpPost]
    public IActionResult Post([FromBody] OrderLog log)
    {
        if (log == null) return BadRequest();
        _collection.InsertOne(log);
        return Ok(new { success = true });
    }
}