using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("MedallionOData")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("MedallionOData")]
[assembly: AssemblyCopyright("Copyright © Michael Adelson 2014")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("3d51ab59-72e8-43c5-9804-a97ad7536ede")]

// 2015-03-28 Version 1.4.2 optimizes server-side pagination and improves ODataEntity error messages
// 2015-01-31 Version 1.4.1 adds missing support for numeric division and negation
// 2014-09-21 Version 1.4.0 adds direct support for SQL-based OData services. These services can have fully dynamic schemas, unlike the static typing imposed by providers like EntityFramework
// 2014-09-20 Version 1.3.1 fixes a bug where Skip([large #]).Count() could return negative values
// 2014-09-11 Version 1.3.0 adds support for fully dynamic services based on in-memory ODataEntity collections
[assembly: AssemblyInformationalVersion("1.4.2")]
[assembly: AssemblyVersion("1.4.2.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

[assembly: InternalsVisibleTo("MedallionOData.Tests")]