﻿open SqlHydra
open SqlHydra.SqlServer

let sqlHydraId = "SqlHydra.SqlServer"

[<EntryPoint>]
let main argv =
    match argv with
    | [| connectionString; nmspace; outputFilePath |] -> 

        let comment = $"// This code was generated by {sqlHydraId}."
        let formattedCode = 
            SqlServerSchemaProvider.getSchema connectionString
            |> SchemaGenerator.generateModule nmspace
            |> SchemaGenerator.toFormattedCode nmspace comment

        System.IO.File.WriteAllText(outputFilePath, formattedCode)
        0

    | _ ->
        failwith $"{sqlHydraId} expected [| connectionString; nmspace; outputFilePath |] args"
