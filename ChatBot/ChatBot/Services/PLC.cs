using ChatBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S7.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ChatBot.Services
{
    public enum LineType { Line1 = 0, Line2 = 1 } //Line Defind


    public class PLC : IDisposable
    {
        private readonly Plc _plc;
        private readonly HttpClient _http;
        private readonly System.Timers.Timer _timer;
        private readonly string _apiBase;
        //NextOrrder Button Press?
        private bool _prev1 = false, _prev2 = false; // trạng thái trước đó của nút NEXTORDER  
        private bool _prevChatsend = false; //trạng thái trước đó của nút send requets trong chatbot
        //ErrorLog
        private readonly Dictionary<string, bool> _prevError = new Dictionary<string, bool>(); //trạng thái trước đó của biến Error
        private readonly Dictionary<string, DateTime> _errorStartTime = new Dictionary<string, DateTime>(); // thời gian bắt đầu lỗi
        private readonly Dictionary<string, string> _errorOrderId = new Dictionary<string, string>(); //order bị lỗi
        #region Maping cho biến O_state
        //Mapping O_State
        private readonly Dictionary<string, string> _stateMap = new Dictionary<string, string>
        {
            // Line1
            ["Blowmolder1"] = "DB2.DBW48",
            ["Washer1"] = "DB4.DBW48",
            ["Filler1"] = "DB7.DBW48",
            ["Capper1"] = "DB9.DBW48",
            ["Labeler1"] = "DB11.DBW48",
            ["Printer1"] = "DB13.DBW48",
            ["Packer1"] = "DB15.DBW48",
            // Line2
            ["Blowmolder2"] = "DB21.DBW48",
            ["Washer2"] = "DB23.DBW48",
            ["Filler2"] = "DB25.DBW48",
            ["Capper2"] = "DB27.DBW48",
            ["Labeler2"] = "DB29.DBW48",
            ["Printer2"] = "DB31.DBW48",
            ["Packer2"] = "DB33.DBW48",
        };
        #endregion



        // Define Index of datablock in OEEVlaue
        #region Datablock cho các trạm của cả 2 line
        private static readonly Dictionary<LineType, (string Name, int Db)[]> _stations =
    new[]
    {
        // Line1
        (LineType.Line1, new []
        {
            ("Blowmolder", 36),
            ("Washer",     37),
            ("Filler",     38),
            ("Capper",     39),
            ("Labeler",    40),
            ("Printer",    41),
            ("Packer",     42),
        }),
        // Line2
        (LineType.Line2, new []
        {
            ("Blowmolder", 43),
            ("Washer",     44),
            ("Filler",     45),
            ("Capper",     46),
            ("Labeler",    47),
            ("Printer",    48),
            ("Packer",     49),
        })
    }
    .ToDictionary(x => x.Item1, x => x.Item2);
        #endregion


        // DB numbers and offsets for each line
        // Order datablock in tiaport (ORDER LINE 1 IN DB1, ORDER LINE 2 IN DB220)
        private readonly int[] _dbNums = { 1, 20 };
        // offset in orderblock (START IN 262)
        private readonly int[] _baseOffsets = { 262, 262 };
        //1 order is 54 offsets
        private const int StructSize = 54; // Struct Size
        private const int MaxSlots = 5; // maximum slot in HMI


        private const string MasterUrl = "api/Order"; // Oder collection 
        private const string Intermediate1 = "api/order1";  //order1 save maximum 5 order in order 1
        private const string Intermediate2 = "api/order2";  //order2 save maximum 5 order in order 2


        #region Connect to the PLC
        public PLC(string ip, string name, string apiBaseUrl)
        {
            _apiBase = apiBaseUrl.TrimEnd('/') + "/";
            _http = new HttpClient { BaseAddress = new Uri(_apiBase) };

            // 1) Open PLC connection
            _plc = new Plc(CpuType.S71500, ip, 0, 1);
            try
            {
                _plc.Open();
                Console.WriteLine($"[{name}] PLC Connected: {_plc.IsConnected}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{name}] PLC connection error: {ex.Message}");
            }

            foreach (var key in _stateMap.Keys) //init prevError = false for all Station
                _prevError[key] = false;

            // 2) Seed & write initial 5+5 orders
            _ = SeedAndWriteInitialAsync();

            // 3) Start polling NextOrder every 100ms
            _timer = new System.Timers.Timer(100);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }
        #endregion
        #region write 5 order to Order1 and order2
        private async Task SeedAndWriteInitialAsync()
        {
            // clear intermediates
            await _http.DeleteAsync("api/order1");
            await _http.DeleteAsync("api/order2");

            // fetch first 10 master orders
            var resp = await _http.GetAsync("api/Order?skip=0&limit=10");
            resp.EnsureSuccessStatusCode();
            var all10 = JsonConvert
                .DeserializeObject<List<Order>>(await resp.Content.ReadAsStringAsync())
                ?? new List<Order>();

            // split into two sets of 5
            var part1 = all10.Take(5).ToList();
            var part2 = all10.Skip(5).Take(5).ToList();

            // seed intermediates
            foreach (var o in part1) await PostIntermediateAsync("order1", o);
            foreach (var o in part2) await PostIntermediateAsync("order2", o);

            // write each batch into PLC DB blocks
            await WriteBatchToPlcAsync(LineType.Line1, part1);
            await WriteBatchToPlcAsync(LineType.Line2, part2);

            Console.WriteLine("[PLC] Seeded & wrote initial 5 orders to each line");
        }
        #endregion
        #region  hàm viết vào Order 1 và 2
        private Task PostIntermediateAsync(string col, Order o)
        {
            var body = new StringContent(
                JsonConvert.SerializeObject(o),
                Encoding.UTF8, "application/json"
            );
            return _http.PostAsync($"api/{col}", body);
        }
        private async Task WriteBatchToPlcAsync(LineType line, List<Order> list)
        {
            int idx = (int)line;
            int db = _dbNums[idx];
            int off = _baseOffsets[idx];

            for (int i = 0; i < 5; i++)
            {
                int start = off + i * StructSize;
                if (i < list.Count)
                    WriteStruct(db, start, list[i]);
                else
                    ClearSlot(db, start);
            }
        }
        #endregion
        private void ClearSlot(int db, int start) // xóa khi mới khởi chạy
        {
            _plc.WriteBytes(DataType.DataBlock, db, start, new byte[StructSize]);
        }

        private void WriteStruct(int db, int start, Order o)
        {
            // OrderID (String[20])
            WriteString(db, start + 0, 20, o.OrderID);
            // FinalMaterial (String[20])
            WriteString(db, start + 22, 20, o.FinalMaterial);
            // PlannedQTY (WORD)
            _plc.Write(DataType.DataBlock, db, start + 44, (ushort)o.PlannedQTY);
            // UoM (String[5])
            WriteString(db, start + 46, 5, o.UoM);
        }

        private void WriteString(int db, int pos, int len, string val)
        {
            byte[] raw = new byte[len];
            raw[0] = (byte)(len - 2);
            byte[] data = Encoding.ASCII.GetBytes(val ?? "");
            int actual = Math.Min(data.Length, len - 2);
            raw[1] = (byte)actual;
            Array.Copy(data, 0, raw, 2, actual);
            _plc.WriteBytes(DataType.DataBlock, db, pos, raw);
        }
        private string ReadDbString(int db, int startByte, int maxLen)
        {
            // đọc maxLen byte
            var raw = (byte[])_plc.ReadBytes(
                DataType.DataBlock,
                db,
                startByte,
                maxLen
            );
            int actual = raw[1];             // byte1 = độ dài thực
            return Encoding.ASCII.GetString(raw, 2, actual);
        }

        private async void Timer_Elapsed(object _, ElapsedEventArgs __) // timer check
        {
            _timer.Enabled = false;
            if (!_plc.IsConnected) return;

            // check xem next order của line 1 có được nhấn không
            bool cur1 = (bool)_plc.Read("DB1.DBX0.0");
            if (cur1 && !_prev1) _ = NextOrderAsync(LineType.Line1);
            _prev1 = cur1;

            // check xem next order của line 2 có được check xem
            bool cur2 = (bool)_plc.Read("DB20.DBX0.0");
            if (cur2 && !_prev2) _ = NextOrderAsync(LineType.Line2);
            _prev2 = cur2;

            //===CHATBOT SEND REQUEST===
            bool send = (bool)_plc.Read("DB50.DBX512.0");   // check xem nút send request có được nhấn không
            if (send && !_prevChatsend)
            {
                await HandleChatAsync();
            }
            #region Báo Lỗi
            //Update Error
            foreach (var kv in _stateMap)
            {
                string stationKey = kv.Key;
                string addr = kv.Value;

                // read INT
                int state = (ushort)_plc.Read(addr);
                bool isErr = (state == 5);   // State = 5 is error
                bool wasErr = _prevError[stationKey];
                //Console.WriteLine($"{isErr} {wasErr}");
                if (isErr && !wasErr)
                {
                    // rising edge → start error
                    _errorStartTime[stationKey] = DateTime.Now;
                    // lấy OrderID từ slot tương ứng
                    var line = stationKey.EndsWith("1") ? LineType.Line1 : LineType.Line2;
                    string orderId = ReadOrderIdForStation(line);
                    _errorOrderId[stationKey] = orderId;
                    //_prevError[stationKey] = isErr;
                    //Console.WriteLine(_errorOrderId[stationKey]);
                }
                else if (!isErr && wasErr)
                {
                    // falling edge → log error
                    var start = _errorStartTime[stationKey];
                    var dur = DateTime.Now - start;
                    var orderId = _errorOrderId[stationKey];
                    //Console.WriteLine(orderId);
                    if (!string.IsNullOrWhiteSpace(orderId))
                    {
                        var log = new ErrorLog
                        {
                            OrderID = orderId,
                            Station = stationKey,
                            ErrorStart = start,
                            DurationSec = dur.TotalSeconds
                        };
                        Console.WriteLine(log.OrderID);
                        Console.WriteLine(log.ErrorStart);
                        Console.WriteLine(log.Station);
                        Console.WriteLine(log.DurationSec);
                        await PostErrorLogAsync(log);
                    }
                    else
                    {
                        Console.WriteLine($"[Warning] Station {stationKey} had error but no OrderID read; skipping log.");
                    }

                    _errorStartTime.Remove(stationKey);
                    _errorOrderId.Remove(stationKey);
                }
                #endregion
                _prevError[stationKey] = isErr;
                _prevChatsend = send;
                _timer.Enabled = true;
            }
        }

        private string ReadOrderIdForStation(LineType line)
        {
            int db = _dbNums[(int)line];         // 1 hoặc 20
            int start = _baseOffsets[(int)line];    // luôn 262
            return ReadDbString(db, start, 20);
        }
        //Post ERROR LOG
        private async Task PostErrorLogAsync(ErrorLog log)
        {
            /*
            var json = JsonConvert.SerializeObject(log);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("api/ErrorLog", content);
            if (!resp.IsSuccessStatusCode)
                Console.WriteLine($"ErrorLog POST failed: {resp.StatusCode}");
            */
            var json = JsonConvert.SerializeObject(log, Formatting.Indented);
            Console.WriteLine("[ErrorLog] Payload to send:");
            Console.WriteLine(json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("api/ErrorLog", content);
            var respBody = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[ErrorLog] HTTP {(int)resp.StatusCode}: {resp.StatusCode}");
            Console.WriteLine(respBody);
        }

        private async Task HandleChatAsync()
        {
            const int db = 50;
            const int promOff = 0;
            const int promSize = 256;
            string prompt = ReadDbString(db, promOff, promSize);
            Console.WriteLine($"[Chat] Prompt from PLC: {prompt}");
            var reqObj = new { Prompt = prompt };
            var json = JsonConvert.SerializeObject(reqObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage resp;
            try
            {
                resp = await _http.PostAsync("api/Chat/ask", content);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chat] HTTP error: {ex.Message}");
                return;
            }

            var body = await resp.Content.ReadAsStringAsync();
            var reply = JObject.Parse(body)["reply"]?.ToString() ?? "";
            Console.WriteLine($"[Chat] Reply: {reply}");

            // 3) write reply in DB50 offset 256, maxLen = 256
            //const int respOff = 256;
            //const int respSize = 256;
            //WriteString(db, respOff, respSize, reply);

            // 4) Store Text in to Folder in Tiaport project
            var folder = @"D:\@MyUniver\@PROJECT\Project\TextReponse";
            Directory.CreateDirectory(folder);
            var fname = "Response.txt";
            var path = Path.Combine(folder, fname);
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            await File.WriteAllTextAsync(path, reply, utf8WithBom);
            Console.WriteLine($"[Chat] Saved to {path}");
        }

        #region OEE value
        private (int, int, TimeSpan, TimeSpan, TimeSpan, double, double, double, double)
        ReadOeeBlock(int db)
        {
            // GoodCount, ScrapCount
            int good = (ushort)_plc.Read($"DB{db}.DBW0");
            int scrap = (ushort)_plc.Read($"DB{db}.DBW2");

            // Runtime, Planned, CycleTime (TIME is 32-bit integer of 100ns ticks)
            uint rtRaw = (uint)_plc.Read($"DB{db}.DBD4");
            uint plRaw = (uint)_plc.Read($"DB{db}.DBD8");
            uint ctRaw = (uint)_plc.Read($"DB{db}.DBD12");

            var runTime = TimeSpan.FromMilliseconds(rtRaw / 10.0);
            var planned = TimeSpan.FromMilliseconds(plRaw / 10.0);
            var cycleTime = TimeSpan.FromMilliseconds(ctRaw / 10.0);

            // Availability, Performance, Quality, OEE (REAL = float32)
            double avl = (float)_plc.Read(DataType.DataBlock, db, 16, VarType.Real, 1);
            double prf = (float)_plc.Read(DataType.DataBlock, db, 20, VarType.Real, 1);
            double qul = (float)_plc.Read(DataType.DataBlock, db, 24, VarType.Real, 1);
            double oee = (float)_plc.Read(DataType.DataBlock, db, 28, VarType.Real, 1);

            return (good, scrap, runTime, planned, cycleTime, avl, prf, qul, oee);
        }

        private async Task PostOeeAsync(string orderId, LineType line)
        {
            var stations = new List<OEEStation>();
            var defs = _stations[line];
            // DB block
            foreach (var (station, db) in defs)
            {
                var (good, scrap, runTime, planned, cycle, avail, perf, qual, oee) =
            ReadOeeBlock(db);

                stations.Add(new OEEStation
                {
                    Station = station,
                    GoodCount = good,
                    ScrapCount = scrap,
                    RunTime = runTime,
                    Planned = planned,
                    CycleTime = cycle,
                    Availability = avail,
                    Performance = perf,
                    Quality = qual,
                    OEE = oee
                });
                var batch = new OEEValue
                {
                    OrderID = orderId,
                    Line = line == LineType.Line1 ? "Line1" : "Line2",
                    Stations = stations,
                    TimeStamp = DateTime.Now
                };


                // call API
                var json = JsonConvert.SerializeObject(batch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync("api/OEEValue", content);
                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine($"Failed OEE post for {orderId}: {resp.StatusCode}");
            }
        }
        #endregion
        #region hàm thực hiện khi nhấn nút next order
        private async Task NextOrderAsync(LineType line)
        {
            string col = line == LineType.Line1 ? "order1" : "order2";
            string otherCol = line == LineType.Line1 ? "order2" : "order1";
            string masterUrl = "api/Order";
            string intUrl = $"api/{col}";

            try
            {
                // === OLD ORDER ===
                // GET OLD ORDER DELETE IN MASTER AND INTERMEDIATE
                var oldResp = await _http.GetAsync($"{intUrl}?skip=0&limit=1");
                oldResp.EnsureSuccessStatusCode();
                var oldList = JsonConvert.DeserializeObject<List<Order>>(await oldResp.Content.ReadAsStringAsync())
                              ?? new List<Order>();
                if (oldList.Count == 1)
                {
                    var oldOrder = oldList[0];
                    await _http.DeleteAsync($"{intUrl}/{oldOrder.OrderID}");
                    await _http.DeleteAsync($"{masterUrl}/{oldOrder.OrderID}");
                    // WRITE log...
                    var log = new OrderLog
                    {
                        OrderID = oldOrder.OrderID,
                        Line = line == LineType.Line1 ? "Line 1" : "Line 2",
                        FinalMaterial = oldOrder.FinalMaterial,
                        PlannedQTY = oldOrder.PlannedQTY,
                        UoM = oldOrder.UoM,
                        TimeStamp = DateTime.Now,
                        Status = "Done"
                    };
                    var jsonLog = JsonConvert.SerializeObject(log);
                    var contentLog = new StringContent(jsonLog, Encoding.UTF8, "application/json");
                    var respLog = await _http.PostAsync("api/OrderLog", contentLog);
                    if (!respLog.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[{line}] Failed logging {oldOrder.OrderID}: {respLog.StatusCode}");
                    }
                    else
                    {
                        Console.WriteLine($"[{line}] Logged {oldOrder.OrderID}");
                    }
                    Console.WriteLine($"[{line}] Logged  {oldOrder.OrderID}");
                    await PostOeeAsync(oldOrder.OrderID, line);


                }
                //WRITE OEE VALUE
                //await PostOeeAsync(popped.OrderID, line);
                //await PostOeeAsync(oldOrder.OrderID, line);
                //  REFILL 5 SLOT

                // GET 2 INTERMEDIATE
                async Task<List<Order>> FetchInt(string collection)
                {
                    var r = await _http.GetAsync($"api/{collection}?skip=0&limit={MaxSlots}");
                    r.EnsureSuccessStatusCode();
                    return JsonConvert.DeserializeObject<List<Order>>(await r.Content.ReadAsStringAsync())
                           ?? new List<Order>();
                }

                var currentThis = await FetchInt(col);
                var currentOther = await FetchInt(otherCol);
                var usedIds = new HashSet<string>(
                    currentThis.Select(o => o.OrderID)
                    .Concat(currentOther.Select(o => o.OrderID))
                );

                int needed = MaxSlots - currentThis.Count;
                if (needed > 0)
                {
                    // FILLTER
                    var candResp = await _http.GetAsync($"{masterUrl}?skip=0&limit={needed * 2}");
                    candResp.EnsureSuccessStatusCode();
                    var candidates = JsonConvert
                        .DeserializeObject<List<Order>>(await candResp.Content.ReadAsStringAsync())
                        ?? new List<Order>();

                    // CHOOSE RIGHT ID
                    var toInsert = candidates
                        .Where(o => !usedIds.Contains(o.OrderID))
                        .Take(needed)
                        .ToList();

                    //INSERT TO INTERMEDIATE AND DELETE TO MASTER
                    foreach (var ord in toInsert)
                    {
                        var body = new StringContent(
                           JsonConvert.SerializeObject(ord),
                           Encoding.UTF8, "application/json"
                        );
                        var postR = await _http.PostAsync(intUrl, body);
                        if (postR.IsSuccessStatusCode)
                        {
                            await _http.DeleteAsync($"{masterUrl}/{ord.OrderID}");
                            Console.WriteLine($"[{line}] Refilled intermediate with {ord.OrderID}");
                        }
                        else
                        {
                            Console.WriteLine($"[{line}] Failed to refill {ord.OrderID}: {postR.StatusCode}");
                        }
                    }
                }

                // WRITE DOWN TO PLC
                var finalInt = await FetchInt(col);
                await WriteBatchToPlcAsync(line, finalInt);
                Console.WriteLine($"[{line}] Wrote {finalInt.Count}/{MaxSlots} slots to PLC");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{line}] Error in NextOrderAsync: {ex}");
            }
        }
        #endregion
        // check timer to stop
        public void Dispose()
        {
            _timer?.Stop();
            _plc?.Close();
            _http?.Dispose();
        }
    }
}