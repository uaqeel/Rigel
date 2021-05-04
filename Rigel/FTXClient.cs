using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using FtxApi;
using FtxApi.Enums;


namespace Rigel
{
    public class FTXClient
    {
        private string apiKey;
        private string apiSecret;
        public List<string> tokens;
        public Dictionary<string, Future> futures;
        public Dictionary<string, double> fundingRates;

        private Client client;
        private FtxRestApi restApi;
        private FtxWebSocketApi wsApi;

        public FTXClient(string apiKey, string apiSecret)
        {
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;

            client = new Client(apiKey, apiSecret);
            restApi = new FtxRestApi(client);
            wsApi = new FtxWebSocketApi("wss://ftx.com/ws/");
        }

        public async Task Initialize()
        {
            List<Task<dynamic>> tasks = new List<Task<dynamic>>();
            tasks.Add(restApi.GetCoinsAsync());
            tasks.Add(restApi.GetAllFuturesAsync());


            var results = await Task.WhenAll(tasks);
            JObject jj = JObject.Parse(results[0]);
            tokens = jj["result"].Values<JObject>()
                        .Select(x => x["id"].ToString())
                        .ToList();

            JObject jj2 = JObject.Parse(results[1]);
            futures = new Dictionary<string, Future>();
            foreach (var f in jj2["result"].Values<JObject>())
            {
                Future ff = new Future(f);
                futures.Add(ff.name, ff);
            }

            UpdateFundingRatesAsync();
        }

        internal async Task<Tuple<DateTime, Market[]>> GetMultipleMarketsAsync(List<string> selectedContracts)
        {
            List<Task<Market>> tasks = new List<Task<Market>>();
            for (int i = 0; i < selectedContracts.Count; i++)
            {
                if (i == 0)
                {
                    tasks.Add(GetMarketAsync(selectedContracts[0]));
                }
                else
                {
                    tasks.Add(futures[selectedContracts[i]].GetMarketAsync(this));
                }
            }

            var results = await Task.WhenAll(tasks);

            Tuple<DateTime, Market[]> ret = new Tuple<DateTime, Market[]>(DateTime.Now, new Market[selectedContracts.Count]);
            int j = 0;
            foreach (var r in results)
                ret.Item2[j++] = r;

            return ret;
        }

        public async Task<Market> GetMarketAsync(string ss)
        {
            var response = await restApi.GetSingleMarketsAsync(ss);
            JObject results = JObject.Parse(response.ToString())["result"];

            double bid = Double.Parse(results["bid"].ToString());
            double ask = Double.Parse(results["ask"].ToString());
            double last = Double.Parse(results["last"].ToString());

            return new Market(bid, ask, last);
        }

        public async Task<List<Tuple<DateTime, double>[]>> GetMultipleHistoricalMarketsAsync(List<string> selectedContracts, int res, DateTime start, DateTime end)
        {
            List<Tuple<DateTime, double>[]> ret = new List<Tuple<DateTime, double>[]>();
            List<Task<Tuple<DateTime, double>[]>> tasks = new List<Task<Tuple<DateTime, double>[]>>();

            foreach (var s in selectedContracts)
            {
                tasks.Add(GetHistoricalMarketAsync(s, res, start, end));
            }

            var results = await Task.WhenAll(tasks);
            int i = 0;
            foreach (var s in selectedContracts)
            {
                ret.Add(results[i]);
                i++;
            }

            return ret;
        }

        public async Task<Tuple<DateTime, double>[]> GetHistoricalMarketAsync(string ss, int resolutionInSecs, DateTime startDate, DateTime endDate)
        {
            var response = await restApi.GetHistoricalPricesAsync(ss, resolutionInSecs, 10000, startDate, endDate);
            JArray results = JObject.Parse(response.ToString())["result"];

            Tuple<DateTime, double>[] ret = new Tuple<DateTime, double>[results.Count];
            int i = 0;
            foreach (var r in results)
            {
                ret[i] = new Tuple<DateTime, double>(DateTime.Parse(r["startTime"].ToString()), Double.Parse(r["open"].ToString()));
                i++;
            }

            return ret;

        }

        public async Task UpdateFundingRatesAsync()
        {
            var results = await restApi.GetFundingRatesAsync();

            JObject jj3 = JObject.Parse(results);
            fundingRates = new Dictionary<string, double>();
            foreach (var f in jj3["result"].Values<JObject>())
            {
                string future = f["future"].ToString();
                if (!fundingRates.ContainsKey(future))
                    fundingRates.Add(f["future"].ToString(), Double.Parse(f["rate"].ToString()));
            }
        }

        public async Task<List<Tuple<DateTime, double>[]>> GetHistoricalFundingRatesAsync(List<string> selectedContracts, List<Tuple<DateTime, double>[]> historicalPrices)
        {
            int n = historicalPrices[0].Length;
            int x = 0;
            List<Tuple<DateTime, double>[]> ret = new List<Tuple<DateTime, double>[]>();
            foreach (var s in selectedContracts)
            {
                Tuple<DateTime, double>[] ret2;
                if (x == 0)
                {
                    ret2 = new Tuple<DateTime, double>[0];
                }
                else if (futures[s].isPerpetual)
                {
                    var results = await restApi.GetFundingRatesAsync(s, historicalPrices[0][0].Item1, historicalPrices[0].Last().Item1);

                    List<Tuple<DateTime, double>> tempRet = new List<Tuple<DateTime, double>>();
                    var fundingRates = JObject.Parse(results.ToString());
                    foreach (var f in fundingRates["result"].Values<JObject>())
                    {
                        tempRet.Add(new Tuple<DateTime, double>(DateTime.Parse(f["time"].ToString()), 100 * 24 * 365.25 * (double)f["rate"]));
                    }

                    tempRet.Reverse();
                    ret2 = tempRet.ToArray();
                }
                else
                {
                    ret2 = new Tuple<DateTime, double>[n];

                    for (int i = 0; i < n; i++)
                    {
                        double spot = historicalPrices[0][i].Item2;
                        double impliedRate = 100 * Utils.ImpliedFundingRate(spot, historicalPrices[x][i].Item2, historicalPrices[x][i].Item1, futures[s].expiry);

                        ret2[i] = new Tuple<DateTime, double>(historicalPrices[x][i].Item1, impliedRate);
                    }
                }

                ret.Add(ret2);
                x++;
            }

            return ret;
        }
    }

    public class Future
    {
        JObject rawObject;

        public string name;
        public string underlying;
        public DateTime expiry;
        public bool isPerpetual;
        public Market market;

        public double bid { get { return market.bid; } }
        public double ask { get { return market.ask; } }
        public double last { get { return market.last; } }

        public Future (JObject fut)
        {
            rawObject = fut;

            name = fut["name"].ToString();
            underlying = fut["underlying"].ToString();
            expiry = fut["expiry"].ToString() == "" ? DateTime.MinValue : DateTime.Parse(fut["expiry"].ToString());
            isPerpetual = Boolean.Parse(fut["perpetual"].ToString());   // null
        }

        public async Task<Market> GetMarketAsync(FTXClient client)
        {
            market = await client.GetMarketAsync(name);
            return market;
        }

        public double YearFraction(DateTime now)
        {
            if (isPerpetual)
            {
                expiry = now.Add(new TimeSpan(0, 0, 59 - now.Minute, 59 - now.Second, 1000 - now.Millisecond));
            }

            return Utils.YearFraction(now, expiry);
        }

        public double ImpliedFundingRate(double spot, DateTime now)
        {
            if (isPerpetual)
            {
                expiry = now.Add(new TimeSpan(0, 0, 59 - now.Minute, 59 - now.Second, 1000 - now.Millisecond));
            }

            return Utils.ImpliedFundingRate(spot, last, now, expiry);
        }
    }

    public class Market
    {
        public double bid;
        public double ask;
        public double last;

        public Market (double b, double a, double l)
        {
            bid = b;
            ask = a;
            last = l;
        }
    }

    public class Utils
    {
        public static double ImpliedFundingRate(double spot, double future, DateTime now, DateTime expiry)
        {
            double yield = (future - spot) / spot;
            yield /= Utils.YearFraction(now, expiry);

            return yield;
        }

        public static double YearFraction(DateTime now, DateTime expiry)
        {
            return (expiry - now).TotalSeconds / (365.25 * 86400);
        }
    }
}
