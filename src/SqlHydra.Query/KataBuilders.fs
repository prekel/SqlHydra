﻿/// LINQ builders for SqlKata.Query
[<AutoOpen>]
module SqlHydra.Query.KataBuilders

open System
open System.Linq.Expressions
open SqlKata

type SelectExpressionBuilder<'Output>() =

    let getQueryOrDefault (state: QuerySource<'Result>) = // 'Result allows 'T to vary as the result of joins
        match state with
        | :? QuerySource<'Result, Query> as qs -> qs.Query
        | _ -> Query()            

    let mergeTableMappings (a: Map<FQ.FQName, TableMapping>, b: Map<FQ.FQName, TableMapping>) =
        Map (Seq.concat [ (Map.toSeq a); (Map.toSeq b) ])

    member this.For (state: QuerySource<'T>, f: 'T -> QuerySource<'T>) =
        let tbl = state.GetOuterTableMapping()
        let query = state |> getQueryOrDefault
        QuerySource<'T, Query>(
            query.From(match tbl.Schema with Some schema -> $"{schema}.{tbl.Name}" | None -> tbl.Name), 
            state.TableMappings)

    member this.Yield _ =
        QuerySource<'T>(Map.empty)

    // Prevents errors while typing join statement if rest of query is not filled in yet.
    member this.Zero _ = 
        QuerySource<'T>(Map.empty)

    /// Sets the WHERE condition
    [<CustomOperation("where", MaintainsVariableSpace = true)>]
    member this.Where (state:QuerySource<'T>, [<ProjectionParameter>] whereExpression) = 
        let query = state |> getQueryOrDefault
        let where = LinqExpressionVisitors.visitWhere<'T> whereExpression (FQ.fullyQualifyColumn state.TableMappings)
        QuerySource<'T, Query>(query.Where(fun w -> where), state.TableMappings)

    /// Sets the SELECT statement and filters the query to include only the selected tables
    [<CustomOperation("select", MaintainsVariableSpace = true)>]
    member this.Select (state: QuerySource<'T>, [<ProjectionParameter>] selectExpression: Expression<Func<'T, 'Transform>>) =
        let query = state |> getQueryOrDefault

        let selections = LinqExpressionVisitors.visitSelect<'T,'Transform> selectExpression

        let queryWithSelectedColumns =
            selections
            |> List.fold (fun (q: Query) -> function
                | LinqExpressionVisitors.SelectedTable tbl -> 
                    // Select all columns in table
                    q.Select($"%s{FQ.fullyQualifyTable state.TableMappings tbl}.*")
                | LinqExpressionVisitors.SelectedColumn col -> 
                    // Select a single column
                    q.Select(FQ.fullyQualifyColumn state.TableMappings col)
                | LinqExpressionVisitors.SelectedAggregateColumn (aggType, col) -> 
                    // Use SelectRaw as a workaround until SqlKata supports multiple aggregates
                    // https://github.com/sqlkata/querybuilder/pull/504
                    q.SelectRaw($"{aggType}({FQ.fullyQualifyColumn state.TableMappings col})")
            ) query
                  
        QuerySource<'Transform, Query>(queryWithSelectedColumns, state.TableMappings)

    /// Sets the ORDER BY for single column
    [<CustomOperation("orderBy", MaintainsVariableSpace = true)>]
    member this.OrderBy (state:QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let orderedQuery = 
            LinqExpressionVisitors.visitOrderByPropertySelector<'T, 'Prop> propertySelector
            |> function 
                | LinqExpressionVisitors.OrderByColumn p -> 
                    query.OrderBy(FQ.fullyQualifyColumn state.TableMappings p)
                | LinqExpressionVisitors.OrderByAggregateColumn (aggType, p) -> 
                    query.OrderByRaw($"{aggType}({FQ.fullyQualifyColumn state.TableMappings p})")        
        QuerySource<'T, Query>(orderedQuery, state.TableMappings)

    /// Sets the ORDER BY for single column
    [<CustomOperation("thenBy", MaintainsVariableSpace = true)>]
    member this.ThenBy (state:QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let orderedQuery = 
            LinqExpressionVisitors.visitOrderByPropertySelector<'T, 'Prop> propertySelector
            |> function 
                | LinqExpressionVisitors.OrderByColumn p -> 
                    query.OrderBy(FQ.fullyQualifyColumn state.TableMappings p)
                | LinqExpressionVisitors.OrderByAggregateColumn (aggType, p) -> 
                    query.OrderByRaw($"{aggType}({FQ.fullyQualifyColumn state.TableMappings p})")        
        QuerySource<'T, Query>(orderedQuery, state.TableMappings)

    /// Sets the ORDER BY DESC for single column
    [<CustomOperation("orderByDescending", MaintainsVariableSpace = true)>]
    member this.OrderByDescending (state:QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let orderedQuery = 
            LinqExpressionVisitors.visitOrderByPropertySelector<'T, 'Prop> propertySelector
            |> function 
                | LinqExpressionVisitors.OrderByColumn p -> 
                    query.OrderByDesc(FQ.fullyQualifyColumn state.TableMappings p)
                | LinqExpressionVisitors.OrderByAggregateColumn (aggType, p) -> 
                    query.OrderByRaw($"{aggType}({FQ.fullyQualifyColumn state.TableMappings p}) DESC")        
        QuerySource<'T, Query>(orderedQuery, state.TableMappings)

    /// Sets the ORDER BY DESC for single column
    [<CustomOperation("thenByDescending", MaintainsVariableSpace = true)>]
    member this.ThenByDescending (state:QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let orderedQuery = 
            LinqExpressionVisitors.visitOrderByPropertySelector<'T, 'Prop> propertySelector
            |> function 
                | LinqExpressionVisitors.OrderByColumn p -> 
                    query.OrderByDesc(FQ.fullyQualifyColumn state.TableMappings p)
                | LinqExpressionVisitors.OrderByAggregateColumn (aggType, p) -> 
                    query.OrderByRaw($"{aggType}({FQ.fullyQualifyColumn state.TableMappings p}) DESC")        
        QuerySource<'T, Query>(orderedQuery, state.TableMappings)

    /// Sets the SKIP value for query
    [<CustomOperation("skip", MaintainsVariableSpace = true)>]
    member this.Skip (state:QuerySource<'T>, skip) = 
        let query = state |> getQueryOrDefault
        QuerySource<'T, Query>(query.Skip(skip), state.TableMappings)
    
    /// Sets the TAKE value for query
    [<CustomOperation("take", MaintainsVariableSpace = true)>]
    member this.Take (state:QuerySource<'T>, take) = 
        let query = state |> getQueryOrDefault
        QuerySource<'T, Query>(query.Take(take), state.TableMappings)

    /// INNER JOIN table on one or more columns
    [<CustomOperation("join", MaintainsVariableSpace = true, IsLikeJoin = true, JoinConditionWord = "on")>]
    member this.Join (outerSource: QuerySource<'TOuter>, 
                      innerSource: QuerySource<'TInner>, 
                      outerKeySelector: Expression<Func<'TOuter,'Key>>, 
                      innerKeySelector: Expression<Func<'TInner,'Key>>, 
                      resultSelector: Expression<Func<'TOuter,'TInner,'Result>> ) = 

        let mergedTables = mergeTableMappings (outerSource.TableMappings, innerSource.TableMappings)
        let outerProperties = LinqExpressionVisitors.visitJoin<'TOuter, 'Key> outerKeySelector
        let innerProperties = LinqExpressionVisitors.visitJoin<'TInner, 'Key> innerKeySelector

        let outerQuery = outerSource |> getQueryOrDefault
        let innerTableName = 
            innerProperties 
            |> Seq.map (fun p -> mergedTables.[FQ.fqName p.DeclaringType])
            |> Seq.map (fun tbl -> 
                match tbl.Schema with
                | Some schema -> sprintf "%s.%s" schema tbl.Name
                | None -> tbl.Name
            )
            |> Seq.head
        
        let joinOn = 
            let fq = FQ.fullyQualifyColumn mergedTables
            List.zip outerProperties innerProperties
            |> List.fold (fun (j: Join) (outerProp, innerProp) -> j.On(fq outerProp, fq innerProp)) (Join())
            
        QuerySource<'Result, Query>(outerQuery.Join(innerTableName, fun j -> joinOn), mergedTables)

    /// LEFT JOIN table on one or more columns
    [<CustomOperation("leftJoin", MaintainsVariableSpace = true, IsLikeJoin = true, JoinConditionWord = "on")>]
    member this.LeftJoin (outerSource: QuerySource<'TOuter>, 
                          innerSource: QuerySource<'TInner>, 
                          outerKeySelector: Expression<Func<'TOuter,'Key>>, 
                          innerKeySelector: Expression<Func<'TInner option,'Key>>, 
                          resultSelector: Expression<Func<'TOuter,'TInner option,'Result>> ) = 

        let mergedTables = mergeTableMappings (outerSource.TableMappings, innerSource.TableMappings)
        let outerProperties = LinqExpressionVisitors.visitJoin<'TOuter, 'Key> outerKeySelector
        let innerProperties = LinqExpressionVisitors.visitJoin<'TInner option, 'Key> innerKeySelector

        let outerQuery = outerSource |> getQueryOrDefault
        let innerTableName = 
            innerProperties 
            |> Seq.map (fun p -> mergedTables.[FQ.fqName p.DeclaringType])
            |> Seq.map (fun tbl -> 
                match tbl.Schema with
                | Some schema -> sprintf "%s.%s" schema tbl.Name
                | None -> tbl.Name
            )
            |> Seq.head

        let joinOn = 
            let fq = FQ.fullyQualifyColumn mergedTables
            List.zip outerProperties innerProperties
            |> List.fold (fun (j: Join) (outerProp, innerProp) -> j.On(fq outerProp, fq innerProp)) (Join())
            
        QuerySource<'Result, Query>(outerQuery.LeftJoin(innerTableName, fun j -> joinOn), mergedTables)

    /// Sets the GROUP BY for one or more columns.
    [<CustomOperation("groupBy", MaintainsVariableSpace = true)>]
    member this.GroupBy (state:QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let properties = LinqExpressionVisitors.visitGroupBy<'T, 'Prop> propertySelector (FQ.fullyQualifyColumn state.TableMappings)
        QuerySource<'T, Query>(query.GroupBy(properties |> List.toArray), state.TableMappings)

    /// Sets the HAVING condition.
    [<CustomOperation("having", MaintainsVariableSpace = true)>]
    member this.Having (state:QuerySource<'T>, [<ProjectionParameter>] havingExpression) = 
        let query = state |> getQueryOrDefault
        let having = LinqExpressionVisitors.visitHaving<'T> havingExpression (FQ.fullyQualifyColumn state.TableMappings)
        QuerySource<'T, Query>(query.Having(fun w -> having), state.TableMappings)

    /// COUNT aggregate function
    [<CustomOperation("count", MaintainsVariableSpace = true)>]
    member this.Count (state:QuerySource<'T>) = 
        let query = state |> getQueryOrDefault
        QuerySource<int, Query>(query.AsCount(), state.TableMappings)

    /// Sets query to return DISTINCT values
    [<CustomOperation("distinct", MaintainsVariableSpace = true)>]
    member this.Distinct (state:QuerySource<'T>) = 
        let query = state |> getQueryOrDefault        
        QuerySource<'T, Query>(query.Distinct(), state.TableMappings)

    /// Transforms the query
    member this.Run (state: QuerySource<'T>) =
        let query = getQueryOrDefault state
        SelectQuery<'T>(query)
        
type DeleteExpressionBuilder<'T>() =

    let getQueryOrDefault (state: QuerySource<'Result>) =
        match state with
        | :? QuerySource<'Result, Query> as qs -> qs.Query
        | _ -> Query()            

    member this.For (state: QuerySource<'T>, f: 'T -> QuerySource<'T>) =
        let tbl = state.GetOuterTableMapping()
        let query = state |> getQueryOrDefault
        QuerySource<'T, Query>(
            query.From(match tbl.Schema with Some schema -> $"{schema}.{tbl.Name}" | None -> tbl.Name), 
            state.TableMappings)

    member this.Yield _ =
        QuerySource<'T>(Map.empty)

    /// Sets the WHERE condition
    [<CustomOperation("where", MaintainsVariableSpace = true)>]
    member this.Where (state:QuerySource<'T>, [<ProjectionParameter>] whereExpression) = 
        let query = state |> getQueryOrDefault
        let where = LinqExpressionVisitors.visitWhere<'T> whereExpression (FQ.fullyQualifyColumn state.TableMappings)
        QuerySource<'T, Query>(query.Where(fun w -> where), state.TableMappings)

    /// Deletes all records in the table (only when there are is no where clause)
    [<CustomOperation("deleteAll", MaintainsVariableSpace = true)>]
    member this.DeleteAll (state:QuerySource<'T>) = 
        state :?> QuerySource<'T, Query>

    /// Unwraps the query
    member this.Run (state: QuerySource<'T>) =
        let query = state |> getQueryOrDefault
        DeleteQuery(query.AsDelete())

type InsertExpressionBuilder<'T, 'InsertReturn when 'InsertReturn : struct>() =

    let getQueryOrDefault (state: QuerySource<'T>) =
        match state with
        | :? QuerySource<'T, InsertQuerySpec<'T, 'IdentityReturn>> as qs -> qs.Query
        | _ -> InsertQuerySpec.Default

    member this.For (state: QuerySource<'T>, f: 'T -> QuerySource<'T>) =
        let tbl = state.GetOuterTableMapping()
        let query = state |> getQueryOrDefault
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>(
            { query with Table = match tbl.Schema with Some schema -> $"{schema}.{tbl.Name}" | None -> tbl.Name }
            , state.TableMappings)

    /// Sets the TABLE name for query.
    [<CustomOperation("into")>]
    member this.Into (state: QuerySource<'T>, table: QuerySource<'T>) =
        let tbl = table.GetOuterTableMapping()
        let query = state |> getQueryOrDefault
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>(
            { query with Table = match tbl.Schema with Some schema -> $"{schema}.{tbl.Name}" | None -> tbl.Name }
            , state.TableMappings)

    member this.Yield _ =
        QuerySource<'T>(Map.empty)

    /// Sets the single value for INSERT
    [<CustomOperation("entity", MaintainsVariableSpace = true)>]
    member this.Entity (state:QuerySource<'T>, value: 'T) = 
        let query = state |> getQueryOrDefault
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>(
            { query with Entity = value |> Some}
            , state.TableMappings)

    /// Includes a column in the insert query.
    [<CustomOperation("includeColumn", MaintainsVariableSpace = true)>]
    member this.IncludeColumn (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let prop = (propertySelector |> LinqExpressionVisitors.visitPropertySelector<'T, 'Prop>).Name
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>({ query with Fields = query.Fields @ [ prop ] }, state.TableMappings)

    /// Excludes a column from the insert query.
    [<CustomOperation("excludeColumn", MaintainsVariableSpace = true)>]
    member this.ExcludeColumn (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let prop = LinqExpressionVisitors.visitPropertySelector<'T, 'Prop> propertySelector
        let newQuery =
            query.Fields
            |> function
                | [] -> FSharp.Reflection.FSharpType.GetRecordFields(typeof<'T>) |> Array.map (fun x -> x.Name) |> Array.toList
                | fields -> fields
            |> List.filter (fun f -> f <> prop.Name)
            |> (fun x -> { query with Fields = x })
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>(newQuery, state.TableMappings)
    
    /// Sets the identity field that should be returned from the insert and excludes it from the insert columns.
    [<CustomOperation("getId", MaintainsVariableSpace = true)>]
    member this.GetId (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        // Exclude the identity column from the query
        let state = this.ExcludeColumn(state, propertySelector)
        
        // Set the identity property and the 'InsertReturn type
        let spec = state.Query
        let prop = LinqExpressionVisitors.visitPropertySelector<'T, 'InsertReturn> propertySelector :?> Reflection.PropertyInfo
        let identitySpec = { Table = spec.Table; Entity = spec.Entity; Fields = spec.Fields; IdentityField = Some prop.Name }
        
        // Sets both the identity field name (prop.Name) and its type ('InsertReturn)
        QuerySource<'T, InsertQuerySpec<'T, 'InsertReturn>>(identitySpec, state.TableMappings)

    member this.Run (state: QuerySource<'T>) =
        let spec = getQueryOrDefault state
        InsertQuery<'T, 'InsertReturn>(spec)

type UpdateExpressionBuilder<'T>() =
    
    let getQueryOrDefault (state: QuerySource<'Result>) =
        match state with
        | :? QuerySource<'Result, UpdateQuerySpec<'T>> as qs -> qs.Query
        | _ -> UpdateQuerySpec.Default

    member this.For (state: QuerySource<'T>, f: 'T -> QuerySource<'T>) =
        let tbl = state.GetOuterTableMapping()
        let query = state |> getQueryOrDefault
        QuerySource<'T, UpdateQuerySpec<'T>>(
            { query with Table = match tbl.Schema with Some schema -> $"{schema}.{tbl.Name}" | None -> tbl.Name }
            , state.TableMappings)

    member this.Yield _ =
        QuerySource<'T>(Map.empty)

    /// Sets the emtore entity ('T) to be updated
    [<CustomOperation("entity", MaintainsVariableSpace = true)>]
    member this.Entity (state: QuerySource<'T>, value: 'T) = 
        let query = state |> getQueryOrDefault
        QuerySource<'T, UpdateQuerySpec<'T>>(
            { query with Entity = value |> Some}
            , state.TableMappings)

    /// Sets a property of the entity ('T) to be updated
    [<CustomOperation("set", MaintainsVariableSpace = true)>]
    member this.Set (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector: Expression<Func<'T, 'Prop>>, value: 'Prop) = 
        let query = state |> getQueryOrDefault
        let prop = LinqExpressionVisitors.visitPropertySelector<'T, 'Prop> propertySelector :?> Reflection.PropertyInfo
        QuerySource<'T, UpdateQuerySpec<'T>>(
            { query with SetValues = query.SetValues @ [ prop.Name, box value ] }
            , state.TableMappings)

    /// Includes a column in the insert query.
    [<CustomOperation("includeColumn", MaintainsVariableSpace = true)>]
    member this.IncludeColumn (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let prop = (propertySelector |> LinqExpressionVisitors.visitPropertySelector<'T, 'Prop>).Name
        QuerySource<'T, UpdateQuerySpec<'T>>({ query with Fields = query.Fields @ [ prop ] }, state.TableMappings)

    /// Excludes a column from the insert query.
    [<CustomOperation("excludeColumn", MaintainsVariableSpace = true)>]
    member this.ExcludeColumn (state: QuerySource<'T>, [<ProjectionParameter>] propertySelector) = 
        let query = state |> getQueryOrDefault
        let prop = LinqExpressionVisitors.visitPropertySelector<'T, 'Prop> propertySelector
        let newQuery =
            query.Fields
            |> function
                | [] -> FSharp.Reflection.FSharpType.GetRecordFields(typeof<'T>) |> Array.map (fun x -> x.Name) |> Array.toList
                | fields -> fields
            |> List.filter (fun f -> f <> prop.Name)
            |> (fun x -> { query with Fields = x })
        QuerySource<'T, UpdateQuerySpec<'T>>(newQuery, state.TableMappings)

    /// Sets the WHERE condition
    [<CustomOperation("where", MaintainsVariableSpace = true)>]
    member this.Where (state: QuerySource<'T>, [<ProjectionParameter>] whereExpression) = 
        let query = state |> getQueryOrDefault
        let where = LinqExpressionVisitors.visitWhere<'T> whereExpression (FQ.fullyQualifyColumn state.TableMappings)
        QuerySource<'T, UpdateQuerySpec<'T>>({ query with Where = Some where }, state.TableMappings)

    /// A safeguard that verifies that all records in the table should be updated.
    [<CustomOperation("updateAll", MaintainsVariableSpace = true)>]
    member this.UpdateAll (state:QuerySource<'T>) = 
        let query = state |> getQueryOrDefault
        QuerySource<'T, UpdateQuerySpec<'T>>({ query with UpdateAll = true }, state.TableMappings)

    /// Unwraps the query
    member this.Run (state: QuerySource<'T>) =
        let spec = state |> getQueryOrDefault
        if spec.Where = None && spec.UpdateAll = false
        then failwith "An `update` expression must either contain a `where` clause or `updateAll`."
        UpdateQuery<'T>(spec)
        

let select<'T> = SelectExpressionBuilder<'T>()
let delete<'T> = DeleteExpressionBuilder<'T>()
let insert<'T, 'InsertReturn when 'InsertReturn : struct> = InsertExpressionBuilder<'T, 'InsertReturn>()
let update<'T> = UpdateExpressionBuilder<'T>()

