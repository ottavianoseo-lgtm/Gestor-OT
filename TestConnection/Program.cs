using System;
using Npgsql;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing Port 5432...");
        Test("Host=aws-0-us-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.mstxmgwnkbboycbrsyed;Password=9TWshj5Hm0fVwajl;SslMode=Require;Trust Server Certificate=true;CommandTimeout=5;");
        
        Console.WriteLine("Testing Port 6543...");
        Test("Host=aws-0-us-west-2.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.mstxmgwnkbboycbrsyed;Password=9TWshj5Hm0fVwajl;SslMode=Require;Trust Server Certificate=true;CommandTimeout=5;Pooling=false;");
    }

    static void Test(string connString)
    {
        try
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();
            Console.WriteLine("  -> Success!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Failed: {ex.Message}");
        }
    }
}
