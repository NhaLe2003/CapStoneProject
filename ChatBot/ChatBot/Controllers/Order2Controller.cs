using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;

namespace ChatBot.Controllers
{
    [ApiController]
    [Route("api/order2")]
    public class Order2Controller : ControllerBase
    {
        private readonly IMongoCollection<Order> _master;      // collection "Order"
        private readonly IMongoCollection<Order> _col2;         // collection "order2"
        private readonly IMongoCollection<Order> _col1;
        public Order2Controller(IMongoDatabase db)
        {
            _master = db.GetCollection<Order>("Order");
            _col2 = db.GetCollection<Order>("order2");
            _col1 = db.GetCollection<Order>("order1");
        }

        // GET /api/order2?skip=0&limit=10
        [HttpGet]
        public ActionResult<List<Order>> Get(int skip = 0, int limit = 10)
        {
            var list = _col2.Find(FilterDefinition<Order>.Empty)
                           .Skip(skip)
                           .Limit(limit)
                           .ToList();
            return Ok(list);
        }

        // POST /api/order2
        // Upsert order in to intermediate
        [HttpPost]
        public IActionResult Post([FromBody] Order order)
        {
            var filter = Builders<Order>.Filter.Eq(o => o.OrderID, order.OrderID);
            var options = new ReplaceOptions { IsUpsert = true };
            _col2.ReplaceOne(filter, order, options);
            return Ok();
        }

        // DELETE /api/order2/{orderId}
        [HttpDelete("{orderId}")]
        public IActionResult Delete(string orderId)
        {
            _col2.DeleteOne(o => o.OrderID == orderId);
            return Ok();
        }

        // DELETE /api/order2
        // delete all intermediate
        [HttpDelete]
        public IActionResult DeleteAll()
        {
            _col2.DeleteMany(FilterDefinition<Order>.Empty);
            return Ok();
        }

        // POST /api/order2/pop
        // Nguyên tử: lấy 1 đơn từ master Order, xóa khỏi master, chèn vào order2 và trả về đơn đó
        [HttpPost("pop")]
        public ActionResult<Order> Pop()
        {
            var excl1 = _col1.Find(_ => true).Project(o => o.OrderID).ToList();
            var excl2 = _col2.Find(_ => true).Project(o => o.OrderID).ToList();
            var excluded = excl1.Concat(excl2).ToList();

            //Pop 1 order master không nằm trong excluded
            var filter = Builders<Order>.Filter.Nin(o => o.OrderID, excluded);
            var sort = Builders<Order>.Sort.Ascending(o => o.TimeStamp);
            var order = _master.FindOneAndDelete(filter, new FindOneAndDeleteOptions<Order, Order>
            {
                Sort = sort
            });

            if (order == null) return NotFound();

            //Chèn vào order2
            _col2.InsertOne(order);

            return Ok(order);
        }
    }
}
