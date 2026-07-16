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