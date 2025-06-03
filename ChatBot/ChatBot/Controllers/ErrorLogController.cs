using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ErrorLogController : ControllerBase
    {
        private readonly IMongoCollection<ErrorLog> _col;
        public ErrorLogController(IMongoDatabase db) =>
            _col = db.GetCollection<ErrorLog>("ErrorLog");

        // POST: with no ID key
        [HttpPost]
        public IActionResult Post([FromBody] ErrorLog log)
        {
            if (!ModelState.IsValid)
            {
                // Trả về chi tiết lỗi binding
                var errors = ModelState
                    .Where(kv => kv.Value.Errors.Count > 0)
                    .Select(kv => new {
                        Field = kv.Key,
                        Errors = kv.Value.Errors.Select(e => e.ErrorMessage).ToArray() // Error
                    });
                return BadRequest(new { message = "Validation failed", details = errors });
            }

            _col.InsertOne(log); //INSERT LOG TO ERRORLOG COLLECTION
            return Ok(new { success = true });
        }

        // GET /api/ErrorLog?skip=0&limit=100
        [HttpGet]
        public ActionResult<List<ErrorLog>> Get(int skip = 0, int limit = 100)
        {
            var list = _col.Find(FilterDefinition<ErrorLog>.Empty)
                           .SortByDescending(x => x.ErrorStart)
                           .Skip(skip)
                           .Limit(limit)
                           .ToList();
            return Ok(list);
        }

        [HttpGet("all")]
        public ActionResult<List<ErrorLog>> GetAll()
        {
            return _col.Find(_ => true).ToList();
        }
    }
}