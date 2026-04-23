using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main()
    {
        string connectionString = "Host=localhost;Port=5432;Database=gestorot;Username=postgres;Password=password;Include Error Detail=true";
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            Console.WriteLine("Connection to local DB successful!");

            string sql = @"
                ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""Priority"" integer NOT NULL DEFAULT 0;
                ALTER TABLE public.""Labors"" ADD COLUMN IF NOT EXISTS ""SupplyWithdrawalNotes"" text NULL;
            ";

            using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("Columns 'Priority' and 'SupplyWithdrawalNotes' added successfully to GestorOT_Prod!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
