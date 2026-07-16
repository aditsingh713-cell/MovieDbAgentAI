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
