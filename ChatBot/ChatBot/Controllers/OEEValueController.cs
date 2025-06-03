using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;

namespace ChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OEEValueController : ControllerBase
    {
        private readonly IMongoCollection<OEEValue> _col;
        public OEEValueController(IMongoDatabase db) =>
            _col = db.GetCollection<OEEValue>("OEEValue");

        // POST /api/OEEValue
        [HttpPost]
        public IActionResult Post([FromBody] OEEValue order)
        {
            if (order == null) return BadRequest();
            var filter = Builders<OEEValue>.Filter.Eq(x => x.OrderID, order.OrderID);
            var options = new ReplaceOptions { IsUpsert = true };
            _col.ReplaceOne(filter, order, options);
            return Ok(new { success = true });
        }

        // GET /api/OEEValue?skip=0&limit=100
        [HttpGet]
        public ActionResult<List<OEEValue>> Get(int skip = 0, int limit = 100)
        {
            var list = _col.Find(FilterDefinition<OEEValue>.Empty)
                           .Skip(skip).Limit(limit)
                           .ToList();
            return Ok(list);
        }

        [HttpGet("all")]
        public ActionResult<List<OEEValue>> GetAll() {
            return _col.Find(_ => true).ToList();
        }
    }
}