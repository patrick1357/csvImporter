using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Collections.Generic;

namespace cvsimporter
{
    public static class DatabaseHelper
    {
        /// <summary>
        /// Erstellt eine neue SQLite-Datenbankdatei mit der benötigten Tabellenstruktur.
        /// </summary>
        public static void CreateDatabase(string path)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            // Customers (mit Email)
            var createCustomersTable = @"
                CREATE TABLE IF NOT EXISTS Customers (
                    CustomerID INTEGER PRIMARY KEY,
                    FirstName VARCHAR(100),
                    LastName VARCHAR(100),
                    Email VARCHAR(255) -- NEU
                );";
            using (var cmd = new SqliteCommand(createCustomersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Instruments
            var createInstrumentsTable = @"
                CREATE TABLE IF NOT EXISTS Instruments (
                    SerialNumber VARCHAR(100) PRIMARY KEY,
                    Instrument VARCHAR(150)
                );";
            using (var cmd = new SqliteCommand(createInstrumentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Rentals (mit IsEnded, EndDate, OrderNumber)
            var createRentalsTable = @"
                CREATE TABLE IF NOT EXISTS Rentals (
                    RentalID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerID INTEGER,
                    SerialNumber VARCHAR(50),
                    RentalStart DATE,
                    RentalEnd DATE,
                    RentalPrice DECIMAL(10, 2),
                    IsEnded BOOLEAN DEFAULT 0, -- NEU
                    EndDate DATE,              -- NEU
                    OrderNumber VARCHAR(50),   -- NEU, kann NULL sein
                    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID),
                    FOREIGN KEY (SerialNumber) REFERENCES Instruments(SerialNumber)
                );";
            using (var cmd = new SqliteCommand(createRentalsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Invoices
            var createInvoicesTable = @"
                CREATE TABLE IF NOT EXISTS Invoices (
                    InvoiceID INTEGER PRIMARY KEY,
                    CustomerID INTEGER,
                    InvoiceDate DATE,
                    TotalAmount DECIMAL(10, 2),
                    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
                );";
            using (var cmd = new SqliteCommand(createInvoicesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // RentalInvoices
            var createRentalInvoicesTable = @"
                CREATE TABLE IF NOT EXISTS RentalInvoices (
                    ID INTEGER PRIMARY KEY,
                    RentalID INTEGER,
                    InvoiceID INTEGER,
                    FOREIGN KEY (RentalID) REFERENCES Rentals(RentalID),
                    FOREIGN KEY (InvoiceID) REFERENCES Invoices(InvoiceID)
                );";
            using (var cmd = new SqliteCommand(createRentalInvoicesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Payments
            var createPaymentsTable = @"
                CREATE TABLE IF NOT EXISTS Payments (
                    PaymentID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RentalID INTEGER,
                    ReceiptNumber VARCHAR(50), -- NEU, kann NULL sein
                    PaymentDate DATE,
                    Amount DECIMAL(10, 2),
                    FOREIGN KEY (RentalID) REFERENCES Rentals(RentalID)
                );";
            using (var cmd = new SqliteCommand(createPaymentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // RentalComments (NEU: Mehrere Kommentare pro Miete)
            var createRentalCommentsTable = @"
                CREATE TABLE IF NOT EXISTS RentalComments (
                    CommentID INTEGER PRIMARY KEY AUTOINCREMENT,
                    RentalID INTEGER,
                    Comment TEXT,
                    CreatedAt DATE DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RentalID) REFERENCES Rentals(RentalID)
                );";
            using (var cmd = new SqliteCommand(createRentalCommentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Fügt einen Kunden ein (oder ignoriert, falls schon vorhanden).
        /// </summary>
        public static void InsertCustomer(SqliteConnection connection, SqliteTransaction transaction, int customerId, string firstName, string lastName)
        {
            var insertCustomer = @"
                INSERT OR IGNORE INTO Customers (CustomerID, FirstName, LastName)
                VALUES ($CustomerID, $FirstName, $LastName);";
            using var cmd = new SqliteCommand(insertCustomer, connection, transaction);
            cmd.Parameters.AddWithValue("$CustomerID", customerId);
            cmd.Parameters.AddWithValue("$FirstName", firstName);
            cmd.Parameters.AddWithValue("$LastName", lastName);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Fügt ein Instrument ein (oder ignoriert, falls schon vorhanden).
        /// </summary>
        public static void InsertInstrument(SqliteConnection connection, SqliteTransaction transaction, string serialNumber, string instrument)
        {
            var insertInstrument = @"
                INSERT OR IGNORE INTO Instruments (SerialNumber, Instrument)
                VALUES ($SerialNumber, $Instrument);";
            using var cmd = new SqliteCommand(insertInstrument, connection, transaction);
            cmd.Parameters.AddWithValue("$SerialNumber", serialNumber);
            cmd.Parameters.AddWithValue("$Instrument", instrument);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Prüft, ob die Datenbankdatei existiert, und legt sie ggf. neu an.
        /// </summary>
        public static void CheckOrCreateDatabase(string databasePath)
        {
            if (!File.Exists(databasePath))
            {
                CreateDatabase(databasePath);
                System.Windows.MessageBox.Show("database.db nicht gefunden. Es wird eine neue Datenbank erstellt. Anschließend kann auch eine alte Datenbankdatei geladen werden.", "Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Öffnet einen Dialog zum Auswählen einer Datenbankdatei und erstellt sie bei Bedarf.
        /// </summary>
        public static string SelectAndPrepareDatabase(Window owner, string currentDatabasePath)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SQLite-Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog(owner) == true)
            {
                string selectedPath = openFileDialog.FileName;
                if (!File.Exists(selectedPath))
                {
                    CreateDatabase(selectedPath);
                    MessageBox.Show(owner, "Die ausgewählte Datenbank existierte nicht und wurde neu erstellt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(owner, $"Datenbank '{selectedPath}' wird nun verwendet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return selectedPath;
            }
            return currentDatabasePath;
        }

        /// <summary>
        /// Importiert Kunden- und Instrumentendaten aus einer CSV-Datei und fügt sie der Datenbank hinzu.
        /// </summary>
        public static void ImportInitialData(Window owner, string databasePath)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog(owner) != true)
                return;

            var lines = File.ReadAllLines(openFileDialog.FileName);
            if (lines.Length < 2)
            {
                MessageBox.Show(owner, "Die CSV-Datei enthält keine Daten.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                for (int i = 1; i < lines.Length; i++) // Überspringe Header
                {
                    var parts = lines[i].Split(';');
                    if (parts.Length < 5) continue;

                    string lastName = parts[0].Trim();
                    string firstName = parts[1].Trim();
                    if (!int.TryParse(parts[2].Trim(), out int customerId))
                        throw new Exception($"CustomerID '{parts[2]}' ist keine gültige Zahl (Zeile {i + 1})");
                    string instrument = parts[3].Trim();
                    string serialNumber = parts[4].Trim();

                    InsertCustomer(connection, transaction, customerId, firstName, lastName);
                    InsertInstrument(connection, transaction, serialNumber, instrument);
                }

                transaction.Commit();
                MessageBox.Show(owner, "Import erfolgreich abgeschlossen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show(owner, "Fehler beim Import: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Importiert Zahlungsdaten aus einer CSV-Datei und fügt sie der Datenbank hinzu.
        /// </summary>
        public static void ImportPayments(Window owner, string databasePath)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog(owner) != true)
                return;

            var lines = File.ReadAllLines(openFileDialog.FileName);
            if (lines.Length < 2)
            {
                MessageBox.Show(owner, "Die CSV-Datei enthält keine Daten.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var header = lines[0].Split(';');
            int firstMonthCol = 6; // Index der ersten Monatszahlungsspalte

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(';');
                    if (parts.Length < firstMonthCol) continue;

                    if (!int.TryParse(parts[0].Trim(), out int customerId))
                        throw new Exception($"CustomerID '{parts[0]}' ist keine gültige Zahl (Zeile {i + 1})");
                    string instrument = parts[1].Trim();
                    string serialNumber = parts[2].Trim();
                    if (!int.TryParse(parts[3].Trim(), out int counterPayments))
                        counterPayments = 0;
                    if (!decimal.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rentalPrice))
                        rentalPrice = 0;
                    string startDate = parts[5].Trim();

                    // Rentals-Eintrag anlegen
                    var insertRental = @"
                        INSERT INTO Rentals (CustomerID, SerialNumber, RentalStart, RentalPrice)
                        VALUES ($CustomerID, $SerialNumber, $RentalStart, $RentalPrice);
                        SELECT last_insert_rowid();";
                    long rentalId;
                    using (var cmd = new SqliteCommand(insertRental, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("$CustomerID", customerId);
                        cmd.Parameters.AddWithValue("$SerialNumber", serialNumber);
                        cmd.Parameters.AddWithValue("$RentalStart", DateTime.ParseExact(startDate, "dd.MM.yyyy", CultureInfo.InvariantCulture));
                        cmd.Parameters.AddWithValue("$RentalPrice", rentalPrice);
                        rentalId = (long)cmd.ExecuteScalar();
                    }

                    // Zahlungen eintragen
                    for (int col = firstMonthCol; col < parts.Length && col < header.Length; col++)
                    {
                        string paymentValue = parts[col].Trim();
                        if (string.IsNullOrEmpty(paymentValue)) continue;

                        DateTime paymentDate;
                        if (!DateTime.TryParseExact(header[col], "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out paymentDate))
                            continue;

                        decimal amount;
                        if (paymentValue.ToLower() == "x")
                            amount = rentalPrice;
                        else if (!decimal.TryParse(paymentValue, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                            continue;

                        var insertPayment = @"
                            INSERT INTO Payments (RentalID, PaymentDate, Amount)
                            VALUES ($RentalID, $PaymentDate, $Amount);";
                        using (var cmd = new SqliteCommand(insertPayment, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("$RentalID", rentalId);
                            cmd.Parameters.AddWithValue("$PaymentDate", paymentDate);
                            cmd.Parameters.AddWithValue("$Amount", amount);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                transaction.Commit();
                MessageBox.Show(owner, "Import der Zahlungen erfolgreich abgeschlossen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show(owner, "Fehler beim Import: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gibt eine Liste von ausstehenden Zahlungen bis zu einem bestimmten Datum zurück.
        /// </summary>
        public static List<OutstandingPaymentInfo> GetOutstandingPayments(string databasePath, DateTime bisDatum)
        {
            var result = new List<OutstandingPaymentInfo>();
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var sql = @"
        SELECT
            r.RentalID,
            r.CustomerID,
            c.FirstName || ' ' || c.LastName AS CustomerName,
            i.Instrument,
            r.RentalStart,
            r.RentalPrice,
            SUM(CASE WHEN p.PaymentDate <= $BisDatum THEN p.Amount ELSE 0 END) AS Paid,
            MAX(CASE WHEN p.PaymentDate <= $BisDatum THEN p.PaymentDate ELSE NULL END) AS LastPaymentDate,
            COUNT(CASE WHEN p.PaymentDate <= $BisDatum THEN 1 ELSE NULL END) AS PaymentCount
        FROM Rentals r
        JOIN Customers c ON r.CustomerID = c.CustomerID
        JOIN Instruments i ON r.SerialNumber = i.SerialNumber
        LEFT JOIN Payments p ON r.RentalID = p.RentalID
        GROUP BY r.RentalID, r.CustomerID, c.FirstName, c.LastName, i.Instrument, r.RentalStart, r.RentalPrice
    ";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("$BisDatum", bisDatum);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var rentalStart = reader.GetDateTime(4);
                int months = ((bisDatum.Year - rentalStart.Year) * 12 + bisDatum.Month - rentalStart.Month) + 1;
                if (months < 1) continue;
                decimal expected = months * reader.GetDecimal(5);

                decimal paid = !reader.IsDBNull(6) ? reader.GetDecimal(6) : 0;
                string letzterMonat = "-";
                if (!reader.IsDBNull(7))
                {
                    letzterMonat = reader.GetDateTime(7).ToString("MM.yyyy");
                }
                int anzahlZahlungen = !reader.IsDBNull(8) ? reader.GetInt32(8) : 0;

                if (paid < expected)
                {
                    result.Add(new OutstandingPaymentInfo
                    {
                        CustomerName = reader.GetString(2),
                        CustomerId = reader.GetInt32(1),
                        Instrument = reader.GetString(3),
                        OffenerBetrag = expected - paid,
                        LetzterZahlungsmonat = letzterMonat,
                        InsgesamtBezahlt = paid,
                        AnzahlZahlungen = anzahlZahlungen,
                        ErwarteteZahlungen = expected
                    });
                }
            }
            return result;
        }

        public static List<PaymentInfo> GetAllPayments(string databasePath)
        {
            var result = new List<PaymentInfo>();
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            var cmd = new SqliteCommand(@"
        SELECT c.FirstName || ' ' || c.LastName AS CustomerName, c.CustomerID, i.Instrument, p.PaymentDate, p.Amount, p.ReceiptNumber
        FROM Payments p
        JOIN Rentals r ON p.RentalID = r.RentalID
        JOIN Customers c ON r.CustomerID = c.CustomerID
        JOIN Instruments i ON r.SerialNumber = i.SerialNumber
        ORDER BY p.PaymentDate DESC
    ", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new PaymentInfo
                {
                    CustomerName = reader.GetString(0),
                    CustomerId = reader.GetInt32(1),
                    Instrument = reader.GetString(2),
                    PaymentDate = reader.GetDateTime(3),
                    Amount = reader.GetDecimal(4),
                    ReceiptNumber = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
            return result;
        }

        public static List<MultipleRentalCustomer> GetCustomersWithMultipleRentals(string databasePath)
        {
            var result = new List<MultipleRentalCustomer>();
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            var cmd = new SqliteCommand(@"
        SELECT c.FirstName || ' ' || c.LastName AS CustomerName, c.CustomerID, COUNT(r.SerialNumber) AS InstrumentCount
        FROM Rentals r
        JOIN Customers c ON r.CustomerID = c.CustomerID
        GROUP BY c.CustomerID, c.FirstName, c.LastName
        HAVING COUNT(r.SerialNumber) > 1
        ORDER BY CustomerName
    ", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new MultipleRentalCustomer
                {
                    CustomerName = reader.GetString(0),
                    CustomerId = reader.GetInt32(1),
                    InstrumentCount = reader.GetInt32(2)
                });
            }
            return result;
        }

        public static List<CustomerInfo> SearchCustomers(string databasePath, string search)
        {
            var result = new List<CustomerInfo>();
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            var cmd = new SqliteCommand(@"
        SELECT CustomerID, FirstName, LastName FROM Customers
        WHERE LOWER(FirstName) LIKE $search OR LOWER(LastName) LIKE $search OR CAST(CustomerID AS TEXT) LIKE $search
        OR CustomerID IN (SELECT CustomerID FROM Rentals WHERE OrderNumber LIKE $search)
    ", connection);
            cmd.Parameters.AddWithValue("$search", $"%{search}%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new CustomerInfo
                {
                    CustomerId = reader.GetInt32(0),
                    Display = $"{reader.GetString(1)} {reader.GetString(2)} (ID: {reader.GetInt32(0)})"
                });
            }
            return result;
        }



        public static bool InvoiceNumberExists(string databasePath, string invoiceNumber)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            var cmd = new SqliteCommand("SELECT COUNT(*) FROM Invoices WHERE InvoiceID = $invoiceNumber", connection);
            cmd.Parameters.AddWithValue("$invoiceNumber", invoiceNumber);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public static void InsertManualPayment(string databasePath, int rentalId, DateTime paymentDate, decimal amount, string receiptNumber)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            if (!string.IsNullOrWhiteSpace(receiptNumber))
            {
                // Prüfe, ob die Belegnummer bereits existiert
                var checkCmd = new SqliteCommand(@"
            SELECT c.FirstName || ' ' || c.LastName AS CustomerName, p.PaymentDate
            FROM Payments p
            JOIN Rentals r ON p.RentalID = r.RentalID
            JOIN Customers c ON r.CustomerID = c.CustomerID
            WHERE p.ReceiptNumber = $ReceiptNumber
            LIMIT 1
        ", connection);
                checkCmd.Parameters.AddWithValue("$ReceiptNumber", receiptNumber);

                using var reader = checkCmd.ExecuteReader();
                if (reader.Read())
                {
                    string customerName = reader.GetString(0);
                    DateTime existingDate = reader.GetDateTime(1);
                    MessageBox.Show(
                        $"Die Belegnummer '{receiptNumber}' existiert bereits bei Kunde '{customerName}'. Zahldatum {existingDate:dd.MM.yyyy}.",
                        "Belegnummer bereits vorhanden",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
            }

            // Einfügen, wenn die Belegnummer noch nicht existiert
            var cmd = new SqliteCommand(@"
        INSERT INTO Payments (RentalID, PaymentDate, Amount, ReceiptNumber)
        VALUES ($RentalID, $PaymentDate, $Amount, $ReceiptNumber)", connection);
            cmd.Parameters.AddWithValue("$RentalID", rentalId);
            cmd.Parameters.AddWithValue("$PaymentDate", paymentDate);
            cmd.Parameters.AddWithValue("$Amount", amount);
            cmd.Parameters.AddWithValue("$ReceiptNumber", receiptNumber ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        public static List<RentalInfo> GetRentalsForCustomer(string databasePath, int customerId)
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                var rentals = new List<RentalInfo>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT r.RentalID, i.Instrument, r.RentalStart, r.EndDate
                FROM Rentals r
                JOIN Instruments i ON r.SerialNumber = i.SerialNumber
                WHERE r.CustomerID = @CustomerID";
                    command.Parameters.AddWithValue("@CustomerID", customerId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rentals.Add(new RentalInfo
                            {
                                RentalID = reader.GetInt32(0),
                                Instrument = reader.GetString(1),
                                StartDate = reader.GetDateTime(2),
                                EndDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3)
                            });
                        }
                    }
                }
                return rentals;
            }
        }
        public static List<AllRentalInfo> GetAllRentalsWithOutstanding(string databasePath)
        {
            var result = new List<AllRentalInfo>();
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var sql = @"
        SELECT
            r.RentalID,
            c.FirstName || ' ' || c.LastName AS CustomerName,
            c.CustomerID,
            r.OrderNumber,
            r.RentalStart,
            r.EndDate,
            r.IsEnded,
            r.RentalPrice,
            IFNULL(SUM(p.Amount), 0) AS Paid
        FROM Rentals r
        JOIN Customers c ON r.CustomerID = c.CustomerID
        LEFT JOIN Payments p ON r.RentalID = p.RentalID
        GROUP BY r.RentalID, c.FirstName, c.LastName, c.CustomerID, r.OrderNumber, r.RentalStart, r.EndDate, r.IsEnded, r.RentalPrice
    ";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var rentalStart = reader.GetDateTime(4);
                var rentalPrice = reader.GetDecimal(7);
                var months = ((DateTime.Now.Year - rentalStart.Year) * 12 + DateTime.Now.Month - rentalStart.Month) + 1;
                if (months < 1) months = 0;
                var expected = months * rentalPrice;
                var paid = !reader.IsDBNull(8) ? reader.GetDecimal(8) : 0;
                result.Add(new AllRentalInfo
                {
                    CustomerName = reader.GetString(1),
                    CustomerId = reader.GetInt32(2),
                    OrderNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                    RentalStart = rentalStart,
                    RentalEnd = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                    IsEnded = reader.GetBoolean(6),
                    OffeneZahlungen = expected - paid
                });
            }
            return result;

        }
        public static void ImportCsvPaymentsWithOrderNumber(Window owner, string databasePath, string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 3) return; // Header überspringen

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

            int imported = 0;
            int skipped = 0;
            int alreadyExists = 0;
            int noRental = 0;
            int userSkipped = 0;
            List<string> details = new();

            for (int i = 2; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 10)
                {
                    skipped++;
                    details.Add($"Zeile {i + 1}: Zu wenig Spalten.");
                    continue;
                }

                string datumStr = parts[0].Trim();
                string kundenNrStr = parts[1].Trim();
                string bestellNr = parts[2].Trim();
                string belegNr = parts[4].Trim();
                string name = parts[7].Trim();
                string betragStr = parts[9].Replace("\"", "").Replace("€", "").Trim();

                if (!int.TryParse(kundenNrStr, out int kundenNr))
                {
                    skipped++;
                    details.Add($"Zeile {i + 1}: Ungültige Kundennummer.");
                    continue;
                }
                if (!DateTime.TryParseExact(datumStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime zahlungsDatum))
                {
                    skipped++;
                    details.Add($"Zeile {i + 1}: Ungültiges Zahlungsdatum.");
                    continue;
                }
                if (!decimal.TryParse(betragStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal betrag))
                {
                    skipped++;
                    details.Add($"Zeile {i + 1}: Ungültiger Betrag.");
                    continue;
                }

                // 1. Suche Miete per Bestellnummer
                int? rentalId = null;
                if (!string.IsNullOrWhiteSpace(bestellNr))
                {
                    var cmd = new SqliteCommand("SELECT RentalID FROM Rentals WHERE OrderNumber = $OrderNumber", connection, transaction);
                    cmd.Parameters.AddWithValue("$OrderNumber", bestellNr);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        rentalId = Convert.ToInt32(result);
                    }
                }

                // 2. Wenn keine Miete gefunden, suche alle Mieten zum Kunden
                if (rentalId == null)
                {
                    var cmd = new SqliteCommand(
                        @"SELECT RentalID, OrderNumber, RentalStart, EndDate, 
                (SELECT Instrument FROM Instruments WHERE SerialNumber = r.SerialNumber) 
                AS Instrument, RentalPrice
                FROM Rentals r 
                WHERE CustomerID = $CustomerID",
                        connection, transaction);
                    cmd.Parameters.AddWithValue("$CustomerID", kundenNr);
                    var reader = cmd.ExecuteReader();
                    var mieten = new List<(int RentalID, string OrderNumber, DateTime RentalStart, DateTime? EndDate, string Instrument, decimal RentalPrice)>();
                    while (reader.Read())
                    {
                        mieten.Add((
                            reader.GetInt32(0),
                            reader.IsDBNull(1) ? null : reader.GetString(1),
                            reader.GetDateTime(2),
                            reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            reader.IsDBNull(4) ? "" : reader.GetString(4),
                            reader.GetDecimal(5)
                        ));
                    }
                    reader.Close();

                    if (mieten.Count == 0)
                    {
                        noRental++;
                        details.Add($"Zeile {i + 1}: Keine Miete für Kunde {kundenNr} gefunden.");
                        continue;
                    }
                    else if (mieten.Count == 1)
                    {
                        rentalId = mieten[0].RentalID;
                        // Falls Bestellnummer in CSV vorhanden, aber nicht in DB, jetzt speichern
                        if (!string.IsNullOrWhiteSpace(bestellNr) && string.IsNullOrWhiteSpace(mieten[0].OrderNumber))
                        {
                            var updateCmd = new SqliteCommand("UPDATE Rentals SET OrderNumber = $OrderNumber WHERE RentalID = $RentalID", connection, transaction);
                            updateCmd.Parameters.AddWithValue("$OrderNumber", bestellNr);
                            updateCmd.Parameters.AddWithValue("$RentalID", rentalId.Value);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                    else if (mieten.Count > 1)
                    {
                        // Auswahlfenster anzeigen
                        var auswahl = new SelectRentalWindow(mieten, bestellNr, name, zahlungsDatum, betrag, belegNr);
                        if (auswahl.ShowDialog() == true)
                        {
                            if (auswahl.SplitPayment && auswahl.SelectedRentalIds != null && auswahl.SelectedRentalIds.Any())
                            {
                                foreach (var rid in auswahl.SelectedRentalIds)
                                {
                                    var mietpreis = mieten.First(x => x.RentalID == rid).RentalPrice;

                                    // Prüfe, ob Zahlung mit dieser Belegnummer für diese Miete schon existiert
                                    var checkCmd = new SqliteCommand(
                                        "SELECT COUNT(*) FROM Payments WHERE RentalID = $RentalID AND ReceiptNumber = $ReceiptNumber",
                                        connection, transaction);
                                    checkCmd.Parameters.AddWithValue("$RentalID", rid);
                                    checkCmd.Parameters.AddWithValue("$ReceiptNumber", string.IsNullOrWhiteSpace(belegNr) ? (object)DBNull.Value : belegNr);
                                    var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                                    if (exists)
                                    {
                                        alreadyExists++;
                                        details.Add($"Zeile {i + 1}: Zahlung für Miete {rid} mit Belegnummer {belegNr} existiert bereits.");
                                        continue;
                                    }

                                    var insertCmd = new SqliteCommand(@"
                                INSERT INTO Payments (RentalID, PaymentDate, Amount, ReceiptNumber)
                                VALUES ($RentalID, $PaymentDate, $Amount, $ReceiptNumber)", connection, transaction);
                                    insertCmd.Parameters.AddWithValue("$RentalID", rid);
                                    insertCmd.Parameters.AddWithValue("$PaymentDate", zahlungsDatum);
                                    insertCmd.Parameters.AddWithValue("$Amount", mietpreis);
                                    insertCmd.Parameters.AddWithValue("$ReceiptNumber", string.IsNullOrWhiteSpace(belegNr) ? (object)DBNull.Value : belegNr);
                                    insertCmd.ExecuteNonQuery();
                                    imported++;
                                }
                                continue; // nächste Zahlung
                            }
                            else if (auswahl.SelectedRentalId.HasValue)
                            {
                                rentalId = auswahl.SelectedRentalId.Value;
                            }
                        }
                        else if (auswahl.SkipPayment)
                        {
                            userSkipped++;
                            details.Add($"Zeile {i + 1}: Zahlung vom Nutzer übersprungen.");
                            continue; // Zahlung überspringen
                        }
                        else
                        {
                            userSkipped++;
                            details.Add($"Zeile {i + 1}: Keine Auswahl getroffen, Zahlung übersprungen.");
                            continue;
                        }
                    }
                }

                // Zahlung eintragen (Einzelfall)
                if (rentalId.HasValue)
                {
                    var checkCmd = new SqliteCommand(
                        "SELECT COUNT(*) FROM Payments WHERE RentalID = $RentalID AND ReceiptNumber = $ReceiptNumber",
                        connection, transaction);
                    checkCmd.Parameters.AddWithValue("$RentalID", rentalId.Value);
                    checkCmd.Parameters.AddWithValue("$ReceiptNumber", string.IsNullOrWhiteSpace(belegNr) ? (object)DBNull.Value : belegNr);
                    var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                    if (exists)
                    {
                        alreadyExists++;
                        details.Add($"Zeile {i + 1}: Zahlung für Miete {rentalId.Value} mit Belegnummer {belegNr} existiert bereits.");
                        continue;
                    }

                    var insertCmd = new SqliteCommand(@"
                INSERT INTO Payments (RentalID, PaymentDate, Amount, ReceiptNumber)
                VALUES ($RentalID, $PaymentDate, $Amount, $ReceiptNumber)", connection, transaction);
                    insertCmd.Parameters.AddWithValue("$RentalID", rentalId.Value);
                    insertCmd.Parameters.AddWithValue("$PaymentDate", zahlungsDatum);
                    insertCmd.Parameters.AddWithValue("$Amount", betrag);
                    insertCmd.Parameters.AddWithValue("$ReceiptNumber", string.IsNullOrWhiteSpace(belegNr) ? (object)DBNull.Value : belegNr);
                    insertCmd.ExecuteNonQuery();
                    imported++;
                }
            }

            transaction.Commit();

            // Statistik anzeigen
            int total = lines.Length - 2;
            int notImported = skipped + alreadyExists + noRental + userSkipped;
            string message =
                $"Import abgeschlossen.\n\n" +
                $"Gesamt: {total}\n" +
                $"Importiert: {imported}\n" +
                $"Nicht importiert: {notImported}\n" +
                $"- Davon bereits vorhanden: {alreadyExists}\n" +
                $"- Keine Miete gefunden: {noRental}\n" +
                $"- Ungültige Daten: {skipped}\n" +
                $"- Vom Nutzer übersprungen: {userSkipped}\n\n" +
                $"Details:\n" +
                string.Join("\n", details.Take(30)) + (details.Count > 30 ? "\n... (weitere Details gekürzt)" : "");

            MessageBox.Show(owner, message, "Import-Statistik", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
public class OutstandingPaymentInfo
{
    public string CustomerName { get; set; }
    public int CustomerId { get; set; }
    public string Instrument { get; set; }
    public decimal OffenerBetrag { get; set; }
    public string LetzterZahlungsmonat { get; set; }
    public decimal InsgesamtBezahlt { get; set; }
    public int AnzahlZahlungen { get; set; }
    public decimal ErwarteteZahlungen { get; set; } // NEU
}

public class PaymentInfo
{
    public string CustomerName { get; set; }
    public int CustomerId { get; set; }
    public string Instrument { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string ReceiptNumber { get; set; }
}


