     WHERE "Id" = @p24;
      UPDATE public."Labors" SET "Hectares" = @p25
      WHERE "Id" = @p26;
fail: GestorOT.Api.Middleware.GlobalExceptionMiddleware[0]
      Unhandled exception: The database operation was expected to affect 1 row(s), but actually affected 0 row(s); data may have been modified or deleted since 
entities were loaded. See https://go.microsoft.com/fwlink/?LinkId=527962 for information on understanding and handling optimistic concurrency exceptions.       
      Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s); data may have been modified or deleted since entities were loaded. See https://go.microsoft.com/fwlink/?LinkId=527962 for information on understanding and handling optimistic concurrency exceptions.
         at Npgsql.EntityFrameworkCore.PostgreSQL.Update.Internal.NpgsqlModificationCommandBatch.ThrowAggregateUpdateConcurrencyExceptionAsync(RelationalDataReader reader, Int32 commandIndex, Int32 expectedRowsAffected, Int32 rowsAffected, CancellationToken cancellationToken)
         at Npgsql.EntityFrameworkCore.PostgreSQL.Update.Internal.NpgsqlModificationCommandBatch.Consume(RelationalDataReader reader, Boolean async, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Update.Internal.BatchExecutor.ExecuteAsync(IEnumerable`1 commandBatches, IRelationalConnection connection, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalDatabase.SaveChangesAsync(IList`1 entries, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(IList`1 entriesToSave, CancellationToken cancellationToken)     
         at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChangesAsync(StateManager stateManager, Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.<>c__DisplayClass30_0`2.<<ExecuteAsync>b__0>d.MoveNext()
      --- End of stack trace from previous location ---
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState 
state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteImplementationAsync[TState,TResult](Func`4 operation, Func`4 verifySucceeded, TState 
state, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(Boolean acceptAllChangesOnSuccess, CancellationToken cancellationToken)
         at GestorOT.Api.Controllers.LaborsController.UpdateLabor(Guid id, LaborDto dto) in C:\Users\HWLScuffi\workspace\Gestor-OT\src\GestorOT.Api\Controllers\LaborsController.cs:line 254
         at lambda_method3538(Closure, Object)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.AwaitableObjectResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeInnerFilterAsync>g__Awaited|13_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)
         at Program.<>c.<<<Main>$>b__0_2>d.MoveNext() in C:\Users\HWLScuffi\workspace\Gestor-OT\src\GestorOT.Api\Program.cs:line 61
      --- End of stack trace from previous location ---
         at Microsoft.AspNetCore.Diagnostics.StatusCodePagesMiddleware.Invoke(HttpContext context)
         at GestorOT.Api.Middleware.GlobalExceptionMiddleware.InvokeAsync(HttpContext context) in C:\Users\HWLScuffi\workspace\Gestor-OT\src\GestorOT.Api\Middleware\GlobalExceptionMiddleware.cs:line 22
