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