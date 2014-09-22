using Medallion.OData.Client;
using Medallion.OData.Service;
using Medallion.OData.Service.Sql;
using Medallion.OData.Tests.Integration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Medallion.OData.Samples.Web.Controllers
{
    public class ODataMvcController : Controller
    {
        [HttpGet]
        public ActionResult Companies()
        {
            var service = new ODataService();
            var result = service.Execute(Company.GetCompanies(), HttpUtility.ParseQueryString(this.Request.Url.Query));
            return this.Content(result.Results.ToString(), "application/json");
        }

        [HttpGet]
        public ActionResult DataTable()
        {
            return this.View("DataTable");
        }

        [HttpGet]
        public ActionResult Data(int id)
        {
            using (var context = new CustomersContext()) 
            {
                context.Database.Initialize(force: false);

                var schema = GenerateRandomSchema(id);
                var sql = string.Join(
                    Environment.NewLine + "UNION ALL ",
                    Enumerable.Range(0, count: schema.Values.First().Count)
                        .Select(i => "SELECT " + string.Join(", ", schema.Select(kvp => string.Format("'{0}' AS {1}", kvp.Value[i], kvp.Key))))
                );

                var sqlContext = new ODataSqlContext(
                    new SqlServerSyntax(SqlServerSyntax.Version.Sql2012), 
                    new DefaultSqlExecutor(() => new SqlConnection(context.Database.Connection.ConnectionString))
                );
                var query = sqlContext.Query<ODataEntity>("(" + sql + ")");

                var service = new ODataService();
                // hack on OData v4 handling
                var queryParameters = HttpUtility.ParseQueryString(this.Request.Url.Query);
                //if (bool.Parse(queryParameters["$count"] ?? bool.FalseString)) 
                //{
                //    queryParameters["$inlinecount"] = "allpages";
                //}
                var result = service.Execute(query, queryParameters);
                var resultJObject = JObject.Parse(result.Results.ToString());
                //JToken count;
                //if (resultJObject.TryGetValue("odata.count", out count))
                //{
                //    resultJObject["@odata.count"] = count;
                //}
                return this.Content(resultJObject.ToString(), "application/json");
            }
        }

        public static IReadOnlyDictionary<string, IReadOnlyList<string>> GenerateRandomSchema(int seed)
        {
            var alphabet = string.Join(string.Empty, Enumerable.Range('a', 26).Select(i => (char)i));

            var random = new Random(seed);
            var rowCount = random.Next(20, 65);
            var result = Enumerable.Range(0, random.Next(3, 8))
                .Select(i => new { index = i, word = string.Join(string.Empty, Enumerable.Range(0, random.Next(1, 8)).Select(_ => alphabet[random.Next(0, alphabet.Length)])) })
                .ToDictionary(
                    t => "col_" + t.index + "_" + t.word,
                    t => (IReadOnlyList<string>)Enumerable.Range(0, rowCount).Select(i => string.Join(string.Empty, t.word.OrderBy(_ => random.Next()))).ToArray()
                );
            return result;
        }
	}

    public class ODataController : System.Web.Http.ApiController
    {
        [System.Web.Http.HttpGet]
        public HttpResponseMessage Companies(HttpRequestMessage request)
        {
            var service = new ODataService();
            var result = service.Execute(Company.GetCompanies(), request.GetQueryNameValuePairs());

            var response = request.CreateResponse();
            response.Content = new StringContent(result.Results.ToString(), Encoding.UTF8, "application/json");
            return response;

            // alternatively, we could change the return type to object and do:
            // return JObject.Parse(result.Results.ToString());
            // this is necessary (for now) since WebApi wants to be the one to do the serialization
        }
    }

    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Sales { get; set; }

        public static IQueryable<Company> GetCompanies()
        {
            return new[]
            {
                new Company { Id = 1, Name = "Greg's Grocer", Sales = 1000 },
                new Company { Id = 2, Name = "Phil's Pharmacy", Sales = 1250 },
                new Company { Id = 3, Name = "Hal's Hardware", Sales = 900 },
            }
            .AsQueryable();
        }
    }
}