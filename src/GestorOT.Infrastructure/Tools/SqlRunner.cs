using Npgsql;
using System;
using System.Threading.Tasks;

namespace GestorOT.Infrastructure.Tools;

public class SqlRunner
{
    public static async Task Main(string[] args)
    {
        string connectionString = "Host=aws-0-us-west-2.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.mstxmgwnkbboycbrsyed;Password=9TWshj5Hm0fVwajl;SslMode=Require;Trust Server Certificate=true";
        
        try 
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            
            Console.WriteLine("ACTUALIZANDO MODELO DE EMPLEADOS (ROLE ENUM)...");
            
            // Agregar la columna Role (int por defecto para enums de C#)
            string sql = "ALTER TABLE \"Employees\" ADD COLUMN IF NOT EXISTS \"Role\" integer NOT NULL DEFAULT 2;";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            
            Console.WriteLine("COLUMNA 'Role' AGREGADA CON EXITO.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
