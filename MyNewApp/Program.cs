using System;
using System.CodeDom.Compiler;
using System.Data;
using System.Security.AccessControl;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());
var app = builder.Build();

app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)","todos/$1"));

app.Use(async (context,next) =>{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();
// app.MapGet("/todos", ()=>todos);
app.MapGet("/todos", (ITaskService service)=>service.GetTodos()); //With ITaskService

app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id,ITaskService service)=>
{
    // var targetTodo = todos.SingleOrDefault(t=>id==t.Id);
     var targetTodo = service.GetTodoById(id);//With ITaskService
    return targetTodo is null
        ? TypedResults.NotFound() 
        : TypedResults.Ok(targetTodo);
});


app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    // todos.Add(task);
    service.AddTodo(task);//With ITaskService
    return TypedResults.Created("/todos/{id}",task);
})
.AddEndpointFilter(async (context, next) =>{
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate),["Can not have the date in the past"]);
        
    }
    if (taskArgument.IsCompleted)
    {
        errors.Add(nameof(Todo.IsCompleted),["Can not add completed todo"]);
    }

    if (errors.Count >0)
    {
        return Results.ValidationProblem(errors);
    }
    return await next(context);
});

app.MapDelete("/todos/{id}", (int id, ITaskService service)=>
{
    // todos.RemoveAll(t=>id==t.Id);
    service.DeleteTodoById(id);//With ITaskService
    return TypedResults.NoContent();
});


app.Run();


public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService
{
    Todo? GetTodoById(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo task);
}

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos= [];

    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(task=>id==task.Id);
    }

    public Todo? GetTodoById(int id)
    {
        return _todos.SingleOrDefault(t=>id==t.Id);
    }

    public List<Todo> GetTodos()
    {
        return _todos;
    }
}
