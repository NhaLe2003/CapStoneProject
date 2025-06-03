using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;



namespace ChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IMongoCollection<Order> _col;
        public OrderController(IMongoDatabase db) =>
            _col = db.GetCollection<Order>("Order");

        // GET /api/Order?skip=0&limit=10
        [HttpGet]
        public ActionResult<List<Order>> Get(int skip = 0, int limit = 10)
        {
            var list = _col.Find(FilterDefinition<Order>.Empty)
                           .Skip(skip)
                           .Limit(limit)
                           .ToList();
            return Ok(list);
        }
        [HttpGet("all")]
        public ActionResult<List<Order>> GetAll()
        {
            return _col.Find(_ => true).ToList();
        }

        [HttpDelete("{orderId}")]
        public IActionResult Delete(string orderId)
        {
            _col.DeleteOne(o => o.OrderID == orderId);
            return Ok();
        }
    }
}
