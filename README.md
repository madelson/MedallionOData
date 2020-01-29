# MedallionOData

MedallionOData is a lightweight, zero-setup .NET library for creating and querying [OData](http://msdn.microsoft.com/en-us/library/ff478141.aspx) and OData-like services. 

[Download the NuGet package](https://www.nuget.org/packages/medallionodata) [![NuGet Status](http://img.shields.io/nuget/v/MedallionOData.svg?style=flat)](https://www.nuget.org/packages/MedallionOData/) ([Release notes](#release-notes))

Keep reading for a quick introduction to the library. For a more detailed walkthrough, check out [this tutorial](https://github.com/steaks/codeducky/blob/master/blogs/IntroducingMedallionOData.md).

## Querying a service

```C#
var context = new ODataQueryContext();
var categories = context.Query(@"http://services.odata.org/v3/odata/odata.svc/Categories");

var foodCategoryId = categories.Where(c => c.Get<string>("Name") == "Food")
    .Select(c => c.Get<int>("ID"))
	// hits http://services.odata.org/v3/odata/odata.svc/Categories?$format=json&$filter=Name eq 'Food'&$select=ID
	.Single();
Console.WriteLine(foodCategoryId); // 0

// we can also perform a more "strongly typed" query using a POCO class
class Category {
	public int ID { get; set; }
	public string Name { get; set; }
}

var categories2 = context.Query<Category>(@"http://services.odata.org/v3/odata/odata.svc/Categories");

var foodCategoryId2 = categories2.Where(c => c.Name == "Food")
	.Select(c => c.ID)
	.Single(); // 0
```

In many cases, you might need to inject custom logic in order to authenticate yourself with a service or otherwise customize the underlying HTTP request. To do this, pass a custom request function to the `ODataQueryContext`:

```C#
var client = new HttpClient();
// customize client
var context = new ODataQueryContext(async url =>
{
	var response = await client.GetAsync(url);
});
	return await response.Content.ReadAsStreamAsync();
```

## Creating a service

The example uses EntityFramework and .NET MVC, but the MedallionOData library doesn't depend on either.

```C#
private static readonly ODataService service = new ODataService();

[Route("Categories")] // any form of mapping the route will do
public ActionResult Categories()
{
	using (var db = new MyDbContext())
	{
		IQueryable<Category> query = db.Categories;
		var result = service.Execute(query, HttpUtility.ParseQueryString(this.Request.Url.Query));
		return this.Content(result.Results.ToString(), "application/json");
	}
}
```

While the typical use-case for OData is to have the shape of the data known at compile time, it is sometimes helpful to be able to build services in a way that allows the schema to be dynamic. MedallionOData supports this use-case. See [this walkthrough](https://github.com/steaks/codeducky/blob/master/blogs/MedallionODataDynamicDataTables.md) for more details.

## Release notes
- 1.7.0 adds support for using `SqlServerSyntax` with `Microsoft.Data.SqlClient`
- 1.6.0 makes it easier to customize the HTTP request layer ([#13](https://github.com/madelson/MedallionOData/issues/13)) and adds a target for .NET Standard 2.0 (supports dynamic ODataEntity queries) 
- 1.5.0 adds support for .NET Core via .NET Standard 1.5. Dynamic ODataEntity queries are not supported in the .NET Standard build
- 1.4.3 fixes parsing bug for empty OData query parameters
- 1.4.2 optimizes server-side pagination and improves ODataEntity error messages
- 1.4.1 adds missing support for numeric division and negation
- 1.4.0 adds direct support for SQL-based OData services. These services can have fully dynamic schemas, unlike the static typing imposed by providers like EntityFramework
- 1.3.1 fixes a bug where Skip([large #]).Count() could return negative values
- 1.3.0 adds support for fully dynamic services based on in-memory ODataEntity collections
- 1.2.0.0 fixes (enables) support for overriding the client query pipeline. It also adds support for creating queries using relative URIs as well as some improved error messages
- [1.0, 1.2) initial non-semantically-versioned releases

