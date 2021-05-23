# SqlHydra
SqlHydra is a collection of [Myriad](https://github.com/MoiraeSoftware/myriad) plugins that generate F# records for a given database provider.

### Benefits of Myriad
* Myriad is fast and has a low impact on your build
* Generated types are records which provide algebraic type safety for your data layer
* Generated types can be used outside of project
* Generated types can be checked into source control (build server friendly)


## SqlHydra.SqlServer [![NuGet version (SqlHydra.SqlServer)](https://img.shields.io/nuget/v/SqlHydra.SqlServer.svg?style=flat-square)](https://www.nuget.org/packages/SqlHydra.SqlServer/)

### Setup

1) Install `SqlHydra.SqlServer` and `Myriad.Sdk` from NuGet.

2) Add a `myriad.toml` configuration file with the following parameters (_TOML requires that backslashes must be escaped_):

```toml
[sqlserver]
namespace = "AdventureWorks"
connection = "Data Source=localhost\\SQLEXPRESS;Initial Catalog=AdventureWorksLT;Integrated Security=SSPI"
```

3) Add an `ItemGroup` to your .fsproj file and specifiy an output file:

```xml
    <ItemGroup>
         <!-- OUTPUT: the .fs output file (to be generated by Myriad) -->
        <Compile Include="AdventureWorks.fs">
            <!-- INPUT: none needed, so use myriad.toml -->
            <MyriadFile>myriad.toml</MyriadFile>
        </Compile>
    </ItemGroup>
```

4) Build your project to generate the .fs file.

5) Your new file should now be populated (along with a `schema.json` file which you can check-in or ignore).

### Regenerating Records
Myriad caches the input file and only runs again when changes are detected.

To rebuild, simply change the myriad.toml file and then Rebuild the project. (Rebuilding ensures that the the project is built even when no code changes are detected).

Alternatively, you can delete the output file and then Build the project.  
_NOTE: If you delete the file, be sure to delete it from the file system, not from within the project, because that will also remove the `<MyriadFile>` element from the project._

## SqlHydra.SSDT

SqlHydra started out as a single Myriad plugin that used a SQL Server SSDT .dacpac file as an input. 
However, it is recommended that you use SqlHydra.SqlServer instead because it provides a slightly better schema translation due to the fact that .dacpac files do not provide data types for User Defined DataTypes or computed columns. 

For those reasons, the original SqlHydra NuGet package is now deprecated. 
With that said, if there is still a demand for the SSDT provider, I will add it back as a new SqlHydra.SSDT package.



## Officially Recommended ORM: Dapper.FSharp!

After creating SqlHydra, I was trying to find the perfect ORM to complement SqlHyda's generated records.
Ideally, I wanted to find a library with 
- First-class support for F# records, option types, etc.
- LINQ queries (to take advantage of strongly typed SqlHydra generated records)

[FSharp.Dapper](https://github.com/Dzoukr/Dapper.FSharp) met the first critera with flying colors. 
As the name suggests, Dapper.FSharp was written specifically for F# with simplicity and ease-of-use as the driving design priorities.
FSharp.Dapper features custom F# Computation Expressions for selecting, inserting, updating and deleting, and support for F# Option types and records (no need for `[<CLIMutable>]` attributes!).

If only it had Linq queries, it would be the _perfect_ complement to SqlHydra...

So I submitted a [PR](https://github.com/Dzoukr/Dapper.FSharp/pull/26) to Dapper.FSharp that adds Linq query expressions (now in v2.0+)!

The result is that it is now the _perfect_ complement to SqlHydra!
Between the two, you can have strongly typed access to your database:

```fsharp
module DapperFSharpExample
open System.Data
open System.Data.SqlClient
open Dapper.FSharp.LinqBuilders
open Dapper.FSharp.MSSQL
open AdventureWorks // Generated Types

Dapper.FSharp.OptionTypes.register()
    
// Tables
let customerTable =         table<Customer>         |> inSchema (nameof SalesLT)
let customerAddressTable =  table<CustomerAddress>  |> inSchema (nameof SalesLT)
let addressTable =          table<SalesLT.Address>  |> inSchema (nameof SalesLT)

let getAddressesForCity(conn: IDbConnection) (city: string) = 
    select {
        for a in addressTable do
        where (a.City = city)
    } |> conn.SelectAsync<SalesLT.Address>
    
let getCustomersWithAddresses(conn: IDbConnection) =
    select {
        for c in customerTable do
        leftJoin ca in customerAddressTable on (c.CustomerID = ca.CustomerID)
        leftJoin a  in addressTable on (ca.AddressID = a.AddressID)
        where (isIn c.CustomerID [30018;29545;29954;29897;29503;29559])
        orderBy c.CustomerID
    } |> conn.SelectAsyncOption<Customer, CustomerAddress, Address>

```

