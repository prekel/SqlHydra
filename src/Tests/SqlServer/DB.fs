﻿module SqlServer.DB

open Microsoft.Data.SqlClient

let server = "mssql"                // devcontainer
//let server = "localhost,12019"    // localhost

let connectionString = $@"Server={server};Database=AdventureWorksLT2019;User=sa;Password=Password#123;"

let getConnection() = 
    new SqlConnection(connectionString)

let openConnection() = 
    let conn = getConnection()
    conn.Open()
    conn

let openMaster() = 
    let conn = new SqlConnection($@"Server={server};Database=master;User=sa;Password=Password#123;")
    conn.Open()
    conn

let toSql (query: SqlKata.Query) = 
    let compiler = SqlKata.Compilers.SqlServerCompiler()
    compiler.Compile(query).Sql
