using Medallion.OData.Service;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace MedallionOData.Samples.Web.Controllers
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