      SELECT l."Id", l."CampaignLotId", l."ContactId", l."CreatedAt", l."EffectiveArea", l."ErpActivityId", l."EstimatedDate", l."EvidencePhotosJson", l."ExecutionDate", l."Hectares", l."IsExternalBilling", l."LaborTypeId", l."LotId", l."MachineryUsedId", l."MetadataExterna", l."Mode", l."Notes", l."PlannedDose", l."PlannedLaborId", l."PrescriptionMapUrl", l."Priority", l."Rate", l."RateUnit", l."RealizedDose", l."Status", l."SupplyWithdrawalNotes", l."TenantId", l."WeatherLogJson", l."WorkOrderId"
          FROM public."Labors" AS l
          WHERE @ef_filter__p0 OR l."TenantId" = @ef_filter__CurrentTenantId
      ) AS l0 ON w0."Id" = l0."WorkOrderId"
      ORDER BY w0."Id"
fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'GestorOT.Infrastructure.Data.ApplicationDbContext'.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
      An unhandled exception has occurred while executing the request.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at GestorOT.Api.Controllers.LaborsController.ValidateLaborAsync(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 651
         at GestorOT.Api.Controllers.LaborsController.CreateLabor(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 95
         at lambda_method2908(Closure, Object)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.AwaitableObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeInnerFilterAsync>g__Awaited|13_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Program.<>c.<<<Main>$>b__0_2>d.MoveNext() in /src/src/GestorOT.Api/Program.cs:line 64
      --- End of stack trace from previous location ---
         at Microsoft.AspNetCore.Diagnostics.StatusCodePagesMiddleware.Invoke(HttpContext context)
         at Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddlewareImpl.<Invoke>g__Awaited|10_0(ExceptionHandlerMiddlewareImpl middleware, HttpContext context, Task task)
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
CreateLabor: CampaignLotId=0b01a971-cd9c-449e-af81-7ef0f8fc16f5, LotId=b7c0fcab-12c7-4d05-a259-d7698889fc4f
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (0ms) [Parameters=[@ef_filter__p0='?' (DbType = Boolean), @ef_filter__CurrentTenantId='?' (DbType = Guid), @dto_WorkOrderId_Value='?' (DbType = Guid)], CommandType='Text', CommandTimeout='300']
      SELECT w0."Id", w0."AcceptsMultipleDates", w0."AcceptsMultiplePeople", w0."AssignedTo", w0."CampaignId", w0."ContactId", w0."ContractorId", w0."Description", w0."DueDate", w0."ExpirationDate", w0."FieldId", w0."IsExternalBilling", w0."LotId", w0."OTNumber", w0."PlannedDate", w0."Status", w0."StockReserved", w0."TenantId", w0."WorkOrderStatusId", l0."Id", l0."CampaignLotId", l0."ContactId", l0."CreatedAt", l0."EffectiveArea", l0."ErpActivityId", l0."EstimatedDate", l0."EvidencePhotosJson", l0."ExecutionDate", l0."Hectares", l0."IsExternalBilling", l0."LaborTypeId", l0."LotId", l0."MachineryUsedId", l0."MetadataExterna", l0."Mode", l0."Notes", l0."PlannedDose", l0."PlannedLaborId", l0."PrescriptionMapUrl", l0."Priority", l0."Rate", l0."RateUnit", l0."RealizedDose", l0."Status", l0."SupplyWithdrawalNotes", l0."TenantId", l0."WeatherLogJson", l0."WorkOrderId"
      FROM (
          SELECT w."Id", w."AcceptsMultipleDates", w."AcceptsMultiplePeople", w."AssignedTo", w."CampaignId", w."ContactId", w."ContractorId", w."Description", w."DueDate", w."ExpirationDate", w."FieldId", w."IsExternalBilling", w."LotId", w."OTNumber", w."PlannedDate", w."Status", w."StockReserved", w."TenantId", w."WorkOrderStatusId"
          FROM public."WorkOrders" AS w
          WHERE (@ef_filter__p0 OR w."TenantId" = @ef_filter__CurrentTenantId) AND w."Id" = @dto_WorkOrderId_Value
          LIMIT 1
      ) AS w0
      LEFT JOIN (
          SELECT l."Id", l."CampaignLotId", l."ContactId", l."CreatedAt", l."EffectiveArea", l."ErpActivityId", l."EstimatedDate", l."EvidencePhotosJson", l."ExecutionDate", l."Hectares", l."IsExternalBilling", l."LaborTypeId", l."LotId", l."MachineryUsedId", l."MetadataExterna", l."Mode", l."Notes", l."PlannedDose", l."PlannedLaborId", l."PrescriptionMapUrl", l."Priority", l."Rate", l."RateUnit", l."RealizedDose", l."Status", l."SupplyWithdrawalNotes", l."TenantId", l."WeatherLogJson", l."WorkOrderId"
          FROM public."Labors" AS l
          WHERE @ef_filter__p0 OR l."TenantId" = @ef_filter__CurrentTenantId
      ) AS l0 ON w0."Id" = l0."WorkOrderId"
      ORDER BY w0."Id"
fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'GestorOT.Infrastructure.Data.ApplicationDbContext'.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
      An unhandled exception has occurred while executing the request.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at GestorOT.Api.Controllers.LaborsController.ValidateLaborAsync(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 651
         at GestorOT.Api.Controllers.LaborsController.CreateLabor(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 95
         at lambda_method2908(Closure, Object)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.AwaitableObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeInnerFilterAsync>g__Awaited|13_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Program.<>c.<<<Main>$>b__0_2>d.MoveNext() in /src/src/GestorOT.Api/Program.cs:line 64
      --- End of stack trace from previous location ---
         at Microsoft.AspNetCore.Diagnostics.StatusCodePagesMiddleware.Invoke(HttpContext context)
         at Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddlewareImpl.<Invoke>g__Awaited|10_0(ExceptionHandlerMiddlewareImpl middleware, HttpContext context, Task task)
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
CreateLabor: CampaignLotId=0b01a971-cd9c-449e-af81-7ef0f8fc16f5, LotId=b7c0fcab-12c7-4d05-a259-d7698889fc4f
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (1ms) [Parameters=[@ef_filter__p0='?' (DbType = Boolean), @ef_filter__CurrentTenantId='?' (DbType = Guid), @dto_WorkOrderId_Value='?' (DbType = Guid)], CommandType='Text', CommandTimeout='300']
      SELECT w0."Id", w0."AcceptsMultipleDates", w0."AcceptsMultiplePeople", w0."AssignedTo", w0."CampaignId", w0."ContactId", w0."ContractorId", w0."Description", w0."DueDate", w0."ExpirationDate", w0."FieldId", w0."IsExternalBilling", w0."LotId", w0."OTNumber", w0."PlannedDate", w0."Status", w0."StockReserved", w0."TenantId", w0."WorkOrderStatusId", l0."Id", l0."CampaignLotId", l0."ContactId", l0."CreatedAt", l0."EffectiveArea", l0."ErpActivityId", l0."EstimatedDate", l0."EvidencePhotosJson", l0."ExecutionDate", l0."Hectares", l0."IsExternalBilling", l0."LaborTypeId", l0."LotId", l0."MachineryUsedId", l0."MetadataExterna", l0."Mode", l0."Notes", l0."PlannedDose", l0."PlannedLaborId", l0."PrescriptionMapUrl", l0."Priority", l0."Rate", l0."RateUnit", l0."RealizedDose", l0."Status", l0."SupplyWithdrawalNotes", l0."TenantId", l0."WeatherLogJson", l0."WorkOrderId"
      FROM (
          SELECT w."Id", w."AcceptsMultipleDates", w."AcceptsMultiplePeople", w."AssignedTo", w."CampaignId", w."ContactId", w."ContractorId", w."Description", w."DueDate", w."ExpirationDate", w."FieldId", w."IsExternalBilling", w."LotId", w."OTNumber", w."PlannedDate", w."Status", w."StockReserved", w."TenantId", w."WorkOrderStatusId"
          FROM public."WorkOrders" AS w
          WHERE (@ef_filter__p0 OR w."TenantId" = @ef_filter__CurrentTenantId) AND w."Id" = @dto_WorkOrderId_Value
          LIMIT 1
      ) AS w0
      LEFT JOIN (
          SELECT l."Id", l."CampaignLotId", l."ContactId", l."CreatedAt", l."EffectiveArea", l."ErpActivityId", l."EstimatedDate", l."EvidencePhotosJson", l."ExecutionDate", l."Hectares", l."IsExternalBilling", l."LaborTypeId", l."LotId", l."MachineryUsedId", l."MetadataExterna", l."Mode", l."Notes", l."PlannedDose", l."PlannedLaborId", l."PrescriptionMapUrl", l."Priority", l."Rate", l."RateUnit", l."RealizedDose", l."Status", l."SupplyWithdrawalNotes", l."TenantId", l."WeatherLogJson", l."WorkOrderId"
          FROM public."Labors" AS l
          WHERE @ef_filter__p0 OR l."TenantId" = @ef_filter__CurrentTenantId
      ) AS l0 ON w0."Id" = l0."WorkOrderId"
      ORDER BY w0."Id"
fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'GestorOT.Infrastructure.Data.ApplicationDbContext'.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
fail: Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware[1]
      An unhandled exception has occurred while executing the request.
      Npgsql.PostgresException (0x80004005): 42703: column l.Priority does not exist

      POSITION: 1683
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Query.ShapedQueryCompilingExpressionVisitor.SingleOrDefaultAsync[TSource](IAsyncEnumerable`1 asyncEnumerable, CancellationToken cancellationToken)
         at GestorOT.Api.Controllers.LaborsController.ValidateLaborAsync(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 651
         at GestorOT.Api.Controllers.LaborsController.CreateLabor(LaborDto dto) in /src/src/GestorOT.Api/Controllers/LaborsController.cs:line 95
         at lambda_method2908(Closure, Object)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.AwaitableObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeInnerFilterAsync>g__Awaited|13_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Program.<>c.<<<Main>$>b__0_2>d.MoveNext() in /src/src/GestorOT.Api/Program.cs:line 64
      --- End of stack trace from previous location ---
         at Microsoft.AspNetCore.Diagnostics.StatusCodePagesMiddleware.Invoke(HttpContext context)
         at Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddlewareImpl.<Invoke>g__Awaited|10_0(ExceptionHandlerMiddlewareImpl middleware, HttpContext context, Task task)
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column l.Priority does not exist
          Position: 1683
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn