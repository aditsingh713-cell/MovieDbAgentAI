This solution uses Microsoft.Extensions.AI (the modern, unified .NET AI standard) paired with Dapper for fast, clean data access to your local SQL Server database. It registers local C# methods as tools, allowing the LLM to query your database dynamically based on natural language.


Step 1: Create the Project & Install Dependencies Open your terminal or Package Manager Console and run the following commands to set up the project:
          # Create a new Console Application
          dotnet new console -o MovieDbAgentAI
          cd MovieDbAgentAI
          
          # Add Microsoft AI and OpenAI abstraction packages
          dotnet add package Microsoft.Extensions.AI
          dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease
          
          # Add SQL Server & Lightweight ORM dependencies
          dotnet add package Microsoft.Data.SqlClient
          dotnet add package Dapper

Step 2: Define the Movie Model & Database Service
Create a file named MovieDbService.cs to handle safe, parameterized queries to your local SQL Server.


                    using System.Collections.Generic;
                    using System.Threading.Tasks;
                    using Microsoft.Data.SqlClient;
                    using Dapper;
                    
                    namespace MovieDbAgentAI
                    {
                        public class Movie
                        {
                            public int Id { get; set; }
                            public string Title { get; set; } = string.Empty;
                            public int? ReleaseYear { get; set; }
                            public string? Director { get; set; }
                            public int? GenreId { get; set; }
                        }
                    
                        public class MovieDbService
                        {
                            private readonly string _connectionString;
                    
                            public MovieDbService(string connectionString)
                            {
                                _connectionString = connectionString;
                            }
                    
                            //Safe structured search tool for the AI Agent to consume
                            public async Task<IEnumerable<Movie>> SearchMoviesAsync(string? title, string? director, int? releaseYear)
                            {
                                using var connection = new SqlConnection(_connectionString);
                    
                                var query = @"
                                    SELECT Id, Title, ReleaseYear, Director, GenreId 
                                    FROM [MovieDb].[dbo].[Movies]
                                    WHERE (@Title IS NULL OR Title LIKE '%' + @Title + '%')
                                      AND (@Director IS NULL OR Director LIKE '%' + @Director + '%')
                                      AND (@ReleaseYear IS NULL OR ReleaseYear = @ReleaseYear)";
                    
                                return await connection.QueryAsync<Movie>(query, new
                                {
                                    Title = title,
                                    Director = director,
                                    ReleaseYear = releaseYear
                                });
                            }
                        }
                    }

Step 3: Create the AI Tools Wrapper
Create a file named MovieTools.cs. We will decorate these methods with C# Description attributes. The ChatClientBuilder will automatically read these descriptions to construct the JSON schemas the LLM needs for tool calling.

using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MovieDbAgentAI
{
    public class MovieTools
    {
        // 1. Ensure this is declared ONLY ONCE
        private readonly MovieDbService _dbService;

        // 2. Ensure this constructor is declared ONLY ONCE
        public MovieTools(MovieDbService dbService)
        {
            _dbService = dbService;
        }

        [Description("Searches the local movies database. Use this tool when the user asks to find, list, or filter movies.")]
        public async Task<string> SearchMovies(
            [Description("Optional. Part or all of the movie title (e.g. 'Inception')")] string? title = null,
            [Description("Optional. Name of the director")] string? director = null,
            [Description("Optional. The 4-digit year the movie was released")] int? releaseYear = null)
        {
            try
            {
                var results = await _dbService.SearchMoviesAsync(title, director, releaseYear);
                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (SqlException sqlEx)
            {
                // This will catch and print the exact SQL problem to the console
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[SQL Database Error]: {sqlEx.Message}");
                Console.ResetColor();

                return $"Error querying local database: {sqlEx.Message}";
            }
        }
    }
}
Step 4: Configure the Orchestration (Program.cs)
Now, update your Program.cs to tie everything together.
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using MovieDbAgentAI;

// Explicitly mapping types to prevent compiler collisions
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace MovieDbAgentAI
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Setup local database configuration
            string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=MovieDb;Trusted_Connection=True;TrustServerCertificate=True;";
            var dbService = new MovieDbService(connectionString);
            var movieTools = new MovieTools(dbService);

            // 2. Configure Client Connection
            string apiKey = "github API Key";
            var openAIClient = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") }
            );

            // 3. Build the AI Client with Function Invocation enabled globally
            IChatClient chatClient = new ChatClientBuilder(
                openAIClient.GetChatClient("gpt-4o-mini").AsIChatClient()
            )
            .UseFunctionInvocation() // 👈 Corrected: Parameterless registration
            .Build();

            // 4. Register the actual database tools via ChatOptions
            var chatOptions = new ChatOptions
            {
                Tools = new List<AITool>
                {
                    AIFunctionFactory.Create(movieTools.SearchMovies) // 👈 Tool defined here
                }
            };

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("====================================================================");
            Console.WriteLine("  MovieDbAgentAI - Your Local Database Assistant Online");
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.WriteLine("Ask me database questions like: 'Find movies directed by Christopher Nolan' or 'Are there any movies from 2010?'");
            Console.WriteLine("Type 'exit' to quit.\n");

            List<ChatMessage> chatHistory = new()
            {
                new ChatMessage(ChatRole.System, "You are a helpful database assistant. You have access to a tool to search the 'Movies' table. Always explain your findings clearly based on the data retrieved.")
            };

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You: ");
                Console.ResetColor();

                string? userInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(userInput)) continue;
                if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

                chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nAgent: ");
                Console.ResetColor();

                string responseBuffer = string.Empty;

                // 5. Pass your chatHistory AND the chatOptions (containing the tools) to execute
                await foreach (var chunk in chatClient.GetStreamingResponseAsync(chatHistory, chatOptions))
                {
                    Console.Write(chunk.Text);
                    responseBuffer += chunk.Text;
                }

                chatHistory.Add(new ChatMessage(ChatRole.Assistant, responseBuffer));
                Console.WriteLine("\n");
            }
        }
    }
}

This pipeline wraps an OpenAI-compatible client (using a GitHub Models token, local Ollama instance, or official OpenAI key), registers the MovieTools using UseFunctionInvocation(), and starts an interactive chat loop.
