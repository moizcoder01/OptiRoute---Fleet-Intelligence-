// =============================================================
//  OptiRoute  |  Core/Data/DbConfig.cs
//  Single source of truth for the connection string.
//  Change the server name here once — everywhere else updates.
// =============================================================
namespace OptiRoute.Core.Data
{
    public static class DbConfig
    {
        // ── Change ONLY this value when deploying to Laptop B ─────
        private const string Server = @"LAPTPO\SQLEXPRESS";
        private const string Database = "OptiRoute";

        public static string ConnectionString =>
            $@"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;";
    }
}
