using CodeBridge.Core.Attributes;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map endpoints with CodeBridge attributes
app.MapWeatherEndpoints();
app.MapUserEndpoints();

app.Run();

public static class WeatherEndpoints
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weather/forecast", GetWeatherForecast)
            .WithName("GetWeatherForecast")
            .WithSummary("Get weather forecast")
            .WithDescription("Returns a 5-day weather forecast")
            .Produces<WeatherForecast[]>(StatusCodes.Status200OK);

        app.MapGet("/weather/current", GetCurrentWeather)
            .WithName("GetCurrentWeather")
            .WithSummary("Get current weather")
            .WithDescription("Returns current weather information")
            .Produces<WeatherForecast>(StatusCodes.Status200OK);

        app.MapPost("/weather/report", SubmitWeatherReport)
            .WithName("SubmitWeatherReport")
            .WithSummary("Submit weather report")
            .WithDescription("Submit a user weather report")
            .Accepts<WeatherReport>("application/json")
            .Produces<object>(StatusCodes.Status200OK);

        [GenerateSdk(Group = "Weather", Summary = "Get 5-day weather forecast")]
        static IResult GetWeatherForecast()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
            return Results.Ok(forecast);
        }

        [GenerateSdk(Group = "Weather", Summary = "Get current weather")]
        static IResult GetCurrentWeather()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var current = new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            );
            return Results.Ok(current);
        }

        [GenerateSdk(Group = "Weather", Summary = "Submit weather report")]
        static IResult SubmitWeatherReport(WeatherReport report)
        {
            return Results.Ok(new { Message = "Weather report submitted successfully", Id = Guid.NewGuid() });
        }
    }
}

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/users", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Get all users")
            .WithDescription("Returns list of all users")
            .Produces<User[]>(StatusCodes.Status200OK);

        app.MapGet("/users/{id:int}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Returns a specific user by their ID")
            .Produces<User>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/users", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create new user")
            .WithDescription("Creates a new user with the provided information")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<User>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        [GenerateSdk(Group = "Users", Summary = "Get all users")]
        static IResult GetUsers()
        {
            var users = new[]
            {
                new User(1, "John Doe", "john@example.com"),
                new User(2, "Jane Smith", "jane@example.com")
            };
            return Results.Ok(users);
        }

        [GenerateSdk(Group = "Users", Summary = "Get user by ID")]
        static IResult GetUserById(int id)
        {
            if (id <= 0)
                return Results.BadRequest("Invalid user ID");

            var user = new User(id, $"User {id}", $"user{id}@example.com");
            return Results.Ok(user);
        }

        [GenerateSdk(Group = "Users", Summary = "Create new user")]
        static IResult CreateUser(CreateUserRequest request)
        {
            var user = new User(Random.Shared.Next(1000, 9999), request.Name, request.Email);
            return Results.Created($"/users/{user.Id}", user);
        }
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherReport(string Location, int TemperatureC, string Conditions, string ReporterName);

public record User(int Id, string Name, string Email);

public record CreateUserRequest(string Name, string Email);
