using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Core.Helpers
{
    /// <summary>
    /// SQL Server adatbázis-műveleteket végző szolgáltatás.
    /// Ez az osztály már paraméterezett lekérdezéseket használ az SQL injekció megelőzésére.
    /// </summary>
    public class SqlDataService : IDataService
    {
        private readonly string _connectionString;

        public SqlDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
            }
            _connectionString = connectionString;
        }

        /// <summary>
        /// Végrehajt egy SQL lekérdezést a megadott paraméterekkel.
        /// </summary>
        /// <param name="query">A végrehajtandó SQL lekérdezés, @paraméter jelölőkkel.</param>
        /// <param name="parameters">A lekérdezés paramétereit tartalmazó szótár.</param>
        /// <returns>Az eredményt tartalmazó DataTable.</returns>
        public DataTable ExecuteQuery(string query, Dictionary<string, object> parameters = null)
        {
            DataTable result = new DataTable();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Paraméterek hozzáadása a parancshoz a biztonságos végrehajtás érdekében.
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            // Biztosítjuk, hogy a paraméter neve @-vel kezdődjön.
                            string paramName = param.Key.StartsWith("@") ? param.Key : "@" + param.Key;

                            // A null értékeket DBNull.Value-ra cseréljük.
                            command.Parameters.AddWithValue(paramName, param.Value ?? DBNull.Value);
                        }
                    }

                    // Adatok lekérése az adapter segítségével.
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(result);
                    }
                }
            }

            return result;
        }

        // A régi, sebezhető metódus megmaradhat a kompatibilitás miatt, de elavultnak kell jelölni.
        // Javasolt a teljes eltávolítása, miután a kód mindenhol az új, paraméteres verziót használja.
        [Obsolete("This method is vulnerable to SQL injection. Use the overload that accepts parameters instead.")]
        public DataTable ExecuteQuery(string query)
        {
            return ExecuteQuery(query, null);
        }
    }
}
