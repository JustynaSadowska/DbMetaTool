using FirebirdSql.Data.FirebirdClient;
using System;
using System.Data.Common;
using System.IO;
using System.Text;

namespace DbMetaTool
{
    public static class Program
    {
        // Przykładowe wywołania:
        // DbMetaTool build-db --db-dir "C:\db\fb5" --scripts-dir "C:\scripts"
        // DbMetaTool export-scripts --connection-string "..." --output-dir "C:\out"
        // DbMetaTool update-db --connection-string "..." --scripts-dir "C:\scripts"
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Użycie:");
                Console.WriteLine("  build-db --db-dir <ścieżka> --scripts-dir <ścieżka>");
                Console.WriteLine("  export-scripts --connection-string <connStr> --output-dir <ścieżka>");
                Console.WriteLine("  update-db --connection-string <connStr> --scripts-dir <ścieżka>");
                return 1;
            }

            try
            {
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "build-db":
                        {
                            string dbDir = GetArgValue(args, "--db-dir");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            BuildDatabase(dbDir, scriptsDir);
                            Console.WriteLine("Baza danych została zbudowana pomyślnie.");
                            return 0;
                        }

                    case "export-scripts":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string outputDir = GetArgValue(args, "--output-dir");

                            ExportScripts(connStr, outputDir);
                            Console.WriteLine("Skrypty zostały wyeksportowane pomyślnie.");
                            return 0;
                        }

                    case "update-db":
                        {
                            string connStr = GetArgValue(args, "--connection-string");
                            string scriptsDir = GetArgValue(args, "--scripts-dir");

                            UpdateDatabase(connStr, scriptsDir);
                            Console.WriteLine("\nBaza danych została zaktualizowana pomyślnie.");
                            return 0;
                        }

                    default:
                        Console.WriteLine($"Nieznane polecenie: {command}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd: " + ex.Message);
                return -1;
            }
        }

        private static string GetArgValue(string[] args, string name)
        {
            int idx = Array.IndexOf(args, name);
            if (idx == -1 || idx + 1 >= args.Length)
                throw new ArgumentException($"Brak wymaganego parametru {name}");
            return args[idx + 1];
        }

        /// <summary>
        /// Buduje nową bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void BuildDatabase(string databaseDirectory, string scriptsDirectory)
        {
            // TODO:
            // 1) Utwórz pustą bazę danych FB 5.0 w katalogu databaseDirectory.
            // 2) Wczytaj i wykonaj kolejno skrypty z katalogu scriptsDirectory
            //    (tylko domeny, tabele, procedury).
            // 3) Obsłuż błędy i wyświetl raport.

            if (!Directory.Exists(databaseDirectory)) 
                Directory.CreateDirectory(databaseDirectory);//Utwórz katalog, jeśli nie istnieje

            var dbFile = Path.Combine(databaseDirectory, "database.fdb");

            if (File.Exists(dbFile))
                throw new InvalidOperationException($"Plik bazy danych '{dbFile}' już istnieje. Operacja przerwana.");

            var csb = new FbConnectionStringBuilder
            {
                Database = dbFile,
                DataSource = "localhost",
                UserID = "SYSDBA",
                Password = "masterkey",
                ServerType = FbServerType.Default
            };
            FbConnection.CreateDatabase(csb.ToString());

            var scripts = Directory.GetFiles(scriptsDirectory, "*.sql"); //Wczytaj pliki *.sql z katalogu scriptsDirectory
            
            using var conn = new FbConnection(csb.ToString());

            conn.Open();

            var successScripts = new List<string>();
            var failedScripts = new Dictionary<string, string>();

            foreach (var scriptFile in scripts)
            {
                Console.WriteLine($"Wykonywanie skryptu: {Path.GetFileName(scriptFile)}");
                var sql = File.ReadAllText(scriptFile);
                try
                {
                    using var cmd = new FbCommand(sql, conn);
                    cmd.CommandTimeout = 0;
                    cmd.ExecuteNonQuery();//wykonuje skrypt (bez zwracania danych
                    successScripts.Add(Path.GetFileName(scriptFile));
                }
                catch (Exception ex)
                {
                    failedScripts[Path.GetFileName(scriptFile)] = ex.Message;
                    Console.WriteLine($"Błąd przy wykonywaniu skryptu: {Path.GetFileName(scriptFile)} -> {ex.Message}");
                }
            }

            Console.WriteLine("\n=== Raport wykonania skryptów ===");
            Console.WriteLine($"Poprawnie wykonane: {successScripts.Count}");
            foreach (var s in successScripts)
                Console.WriteLine(" " + s);

            Console.WriteLine($"Niepowodzenia: {failedScripts.Count}");
            foreach (var f in failedScripts)
                Console.WriteLine($" {f.Key}: {f.Value}");
        }

        /// <summary>
        /// Generuje skrypty metadanych z istniejącej bazy danych Firebird 5.0.
        /// </summary>
        public static void ExportScripts(string connectionString, string outputDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Pobierz metadane domen, tabel (z kolumnami) i procedur.
            // 3) Wygeneruj pliki .sql / .json / .txt w outputDirectory.

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            using var conn = new FbConnection(connectionString);
            conn.Open();

            string SafeGetString(FbDataReader reader, int index) =>
                reader.IsDBNull(index) ? "" : reader.GetString(index).Trim();

            short SafeGetInt16(FbDataReader reader, int index) =>
                reader.IsDBNull(index) ? (short)0 : reader.GetInt16(index);

            //domeny
            using (var cmd = new FbCommand(
                    @"SELECT F.RDB$FIELD_NAME, F.RDB$FIELD_TYPE, F.RDB$FIELD_SUB_TYPE, F.RDB$FIELD_LENGTH, F.RDB$FIELD_SCALE
                        FROM RDB$FIELDS F
                        LEFT JOIN RDB$RELATION_FIELDS RF ON F.RDB$FIELD_NAME = RF.RDB$FIELD_SOURCE
                        WHERE F.RDB$SYSTEM_FLAG = 0
                          AND RF.RDB$FIELD_SOURCE IS NULL
                        ORDER BY F.RDB$FIELD_NAME;
                        ", conn)) 
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string domainName = SafeGetString(reader, 0);
                    short fieldType = SafeGetInt16(reader, 1);
                    short fieldSubType = SafeGetInt16(reader, 2);
                    int fieldLength = SafeGetInt16(reader, 3);
                    short fieldScale = SafeGetInt16(reader, 4);

                    string sqlType = fieldType switch
                    {
                        7 => fieldSubType == 2 ? $"NUMERIC({fieldLength}, {Math.Abs(fieldScale)})" : "SMALLINT",
                        8 => "INTEGER",
                        10 => "FLOAT",
                        12 => "DATE",
                        13 => "TIME",
                        14 => $"CHAR({fieldLength})",
                        16 => "BIGINT",
                        27 => "DOUBLE PRECISION",
                        35 => "TIMESTAMP",
                        37 => $"VARCHAR({fieldLength})",
                        40 => $"CSTRING({fieldLength})", 
                        45 => "BLOB_ID",
                        261 => "BLOB",
                        _ => "UNKNOWN"
                    };

                    string domainSql = $"CREATE DOMAIN {domainName} {sqlType};";
                    File.WriteAllText(Path.Combine(outputDirectory, $"{domainName}.sql"), domainSql, Encoding.UTF8);
                }
            }

            //tabele
            using (var cmd = new FbCommand(
                @"SELECT rf.RDB$RELATION_NAME, rf.RDB$FIELD_NAME, f.RDB$FIELD_TYPE, f.RDB$FIELD_SUB_TYPE, f.RDB$FIELD_LENGTH, f.RDB$FIELD_SCALE
                  FROM RDB$RELATION_FIELDS rf
                  JOIN RDB$FIELDS f ON rf.RDB$FIELD_SOURCE = f.RDB$FIELD_NAME
                  WHERE rf.RDB$SYSTEM_FLAG = 0
                  ORDER BY rf.RDB$RELATION_NAME, rf.RDB$FIELD_POSITION", conn))

            using (var reader = cmd.ExecuteReader())
            {
                string? currentTable = null;
                StringBuilder tableSql = new StringBuilder();

                while (reader.Read())
                {
                    string tableName = SafeGetString(reader, 0);
                    string fieldName = SafeGetString(reader, 1);
                    short fieldType = SafeGetInt16(reader, 2);
                    short fieldSubType = SafeGetInt16(reader, 3);
                    int fieldLength = SafeGetInt16(reader, 4);
                    short fieldScale = SafeGetInt16(reader, 5);

                    string sqlType = fieldType switch
                    {
                        7 => fieldSubType == 2 ? $"NUMERIC({fieldLength}, {Math.Abs(fieldScale)})" : "SMALLINT",
                        8 => "INTEGER",
                        10 => "FLOAT",
                        12 => "DATE",
                        13 => "TIME",
                        14 => $"CHAR({fieldLength})",
                        16 => "BIGINT",
                        27 => "DOUBLE PRECISION",
                        35 => "TIMESTAMP",
                        37 => $"VARCHAR({fieldLength})",
                        40 => $"CSTRING({fieldLength})", 
                        45 => "BLOB_ID",
                        261 => "BLOB",
                        _ => "UNKNOWN"
                    };

                    if (currentTable != tableName)
                    {
                        if (currentTable != null)
                        {
                            tableSql.AppendLine(");");
                            File.WriteAllText(Path.Combine(outputDirectory, $"{currentTable}.sql"), tableSql.ToString(), Encoding.UTF8);
                        }

                        currentTable = tableName;
                        tableSql.Clear();
                        tableSql.AppendLine($"CREATE TABLE {tableName} (");
                        tableSql.AppendLine($"    {fieldName} {sqlType}");
                    }
                    else
                    {
                        tableSql.AppendLine($",    {fieldName} {sqlType}");
                    }
                }

                if (currentTable != null)
                {
                    tableSql.AppendLine(");");
                    File.WriteAllText(Path.Combine(outputDirectory, $"{currentTable}.sql"), tableSql.ToString(), Encoding.UTF8);
                }
            }

            // procedury
            using (var cmd = new FbCommand(
                @"SELECT RDB$PROCEDURE_NAME, RDB$PROCEDURE_SOURCE
                  FROM RDB$PROCEDURES
                  WHERE RDB$SYSTEM_FLAG = 0", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string procName = SafeGetString(reader, 0);
                    string procSource = SafeGetString(reader, 1);

                    if (!string.IsNullOrEmpty(procSource))
                    {
                        string content = $"CREATE OR ALTER PROCEDURE {procName}" + Environment.NewLine;
                        content += "AS" + Environment.NewLine;

                        content += procSource.Trim() + Environment.NewLine + Environment.NewLine;

                        File.WriteAllText(Path.Combine(outputDirectory, $"{procName}.sql"), content, Encoding.UTF8);
                    }
                }
            }
            Console.WriteLine($"Eksport metadanych zakończony. Pliki zapisane w: {outputDirectory}");

        }

        /// <summary>
        /// Aktualizuje istniejącą bazę danych Firebird 5.0 na podstawie skryptów.
        /// </summary>
        public static void UpdateDatabase(string connectionString, string scriptsDirectory)
        {
            // TODO:
            // 1) Połącz się z bazą danych przy użyciu connectionString.
            // 2) Wykonaj skrypty z katalogu scriptsDirectory (tylko obsługiwane elementy).
            // 3) Zadbaj o poprawną kolejność i bezpieczeństwo zmian.

            if (!Directory.Exists(scriptsDirectory))
                throw new DirectoryNotFoundException($"Katalog skryptów nie istnieje: {scriptsDirectory}");

            var scriptFiles = Directory.GetFiles(scriptsDirectory, "*.sql");

            var groups = new Dictionary<string, List<string>>
            {
                ["DOMAIN"] = [],
                ["TABLE"] = [],
                ["PROCEDURE"] = [],
            };

            var allowedScripts = new Dictionary<string, string>
            {
                ["CREATE DOMAIN"] = "DOMAIN",
                ["CREATE TABLE"] = "TABLE",
                ["ALTER TABLE"] = "TABLE",
                ["CREATE OR ALTER TABLE"] = "TABLE",
                ["CREATE PROCEDURE"] = "PROCEDURE",
                ["ALTER PROCEDURE"] = "PROCEDURE",
                ["CREATE OR ALTER PROCEDURE"] = "PROCEDURE"
            };

            foreach (var file in scriptFiles)
            {
                string script = File.ReadAllText(file).ToUpperInvariant();

                string? matchedGroup = null;

                foreach (var prefix in allowedScripts.Keys)
                {
                    if (script.StartsWith(prefix))
                    {
                        matchedGroup = allowedScripts[prefix];
                        break;
                    }
                }

                if (matchedGroup != null)
                    groups[matchedGroup].Add(file);
                else
                    Console.WriteLine($"Pomijanie nieobsługiwanego skryptu: {Path.GetFileName(file)}");
            }

            using var conn = new FbConnection(connectionString);
            
            conn.Open();

            using var transaction = conn.BeginTransaction();

            try
            {
                ExecuteGroup(conn, transaction, groups["DOMAIN"], "DOMAIN");
                ExecuteGroup(conn, transaction, groups["TABLE"], "TABLE");
                ExecuteGroup(conn, transaction, groups["PROCEDURE"], "PROCEDURE");

                transaction.Commit();
                Console.WriteLine("Aktualizacja bazy danych zakończona pomyślnie (COMMIT)");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"\nWystąpił błąd – wszystkie zmiany zostały cofnięte (ROLLBACK)\n");
                throw new Exception(ex.Message);
            }
        }

        private static void ExecuteGroup(FbConnection conn, FbTransaction transaction, List<string> files, string groupName)
        {
            if (files.Count == 0) return;

            Console.WriteLine($"\n=== Wykonywanie grupy: {groupName} ===");

            foreach (var file in files.OrderBy(f => f))
            {
                string script = File.ReadAllText(file);
                string fileName = Path.GetFileName(file);

                Console.WriteLine($"Wykonywanie skryptu: {fileName}");

                using var cmd = new FbCommand(script, conn, transaction);

                try
                {
                    cmd.ExecuteNonQuery();
                    Console.WriteLine($"Skrypt {fileName} wykonany poprawnie.");
                }
                catch (FbException ex)
                {
                    throw new Exception($"Błąd w skrypcie {fileName}: {ex.Message}");
                }
            }
        }
    }
}
