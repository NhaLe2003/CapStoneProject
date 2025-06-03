using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;

namespace ChatBot.Controllers
{
    [ApiController]
    [Route("api/order1")]
    public class Order1Controller : ControllerBase
    {
        private readonly IMongoCollection<Order> _master;      // collection "Order"
        private readonly IMongoCollection<Order> _col1;         // collection "order1"
        private readonly IMongoCollection<Order> _col2;         //collection "order2"
        public Order1Controller(IMongoDatabase db)
        {
            _master = db.GetCollection<Order>("Order");
            _col1 = db.GetCollection<Order>("order1");
            _col2 = db.GetCollection<Order>("order2");
        }

        // GET /api/order1?skip=0&limit=10
        [HttpGet]
        public ActionResult<List<Order>> Get(int skip = 0, int limit = 10)
        {
            var list = _col1.Find(FilterDefinition<Order>.Empty)
                           .Skip(skip)
                           .Limit(limit)
                           .ToList();
            return Ok(list);
        }

        // POST /api/order1
        // Upsert in to intermediate
        [HttpPost]
        public IActionResult Post([FromBody] Order order)
        {
            var filter = Builders<Order>.Filter.Eq(o => o.OrderID, order.OrderID);
            var options = new ReplaceOptions { IsUpsert = true };
            _col1.ReplaceOne(filter, order, options);
            return Ok();
        }

        // DELETE /api/order1/{orderId}
        [HttpDelete("{orderId}")]
        public IActionResult Delete(string orderId)
        {
            _col1.DeleteOne(o => o.OrderID == orderId);
            return Ok();
        }

        // DELETE /api/order1
        // delete all intermediate
        [HttpDelete]
        public IActionResult DeleteAll()
        {
            _col1.DeleteMany(FilterDefinition<Order>.Empty);
            return Ok();
        }

        // POST /api/order1/pop
        // Nguyên tử: lấy 1 đơn từ master Order, xóa khỏi master, chèn vào order1 và trả về đơn đó
        [HttpPost("pop")]
        public ActionResult<Order> Pop()
        {
            var excl1 = _col1.Find(_ => true).Project(o => o.OrderID).ToList();
            var excl2 = _col2.Find(_ => true).Project(o => o.OrderID).ToList();
            var excluded = excl1.Concat(excl2).ToList();

            //Lấy & xóa nguyên tử một order từ master mà không thuộc excluded
            var filter = Builders<Order>.Filter.Nin(o => o.OrderID, excluded);
            var sort = Builders<Order>.Sort.Ascending(o => o.TimeStamp);
            var order = _master.FindOneAndDelete(filter, new FindOneAndDeleteOptions<Order, Order>
            {
                Sort = sort
            });

            if (order == null) return NotFound();  // master hết hoặc toàn ID đã dùng

            //Chèn vào order1
            _col1.InsertOne(order);

            return Ok(order);
        }
    }
}
