﻿module SqlServer.DB

open Microsoft.Data.SqlClient

#if LOCALHOST // localhost
let server = "localhost,12019"
#else // devcontainer
let server = "mssql"
#endif

let connectionString = $@"Server={server};Database=AdventureWorks;User=sa;Password=Password#123;"

let getConnection() = 
    new SqlConnection(connectionString)

let openConnection() = 
    let conn = getConnection()
    conn.Open()
    conn

let toSql (query: SqlKata.Query) = 
    let compiler = SqlKata.Compilers.SqlServerCompiler()
    compiler.Compile(query).Sql
