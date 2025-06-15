using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Windows;

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

            // Customers
            var createCustomersTable = @"
                CREATE TABLE IF NOT EXISTS Customers (
                    CustomerID INTEGER PRIMARY KEY,
                    FirstName VARCHAR(100),
                    LastName VARCHAR(100)
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

            // Rentals (jetzt mit RentalPrice und RentalStart)
            var createRentalsTable = @"
                CREATE TABLE IF NOT EXISTS Rentals (
                    RentalID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerID INTEGER,
                    SerialNumber VARCHAR(50),
                    RentalStart DATE,
                    RentalEnd DATE,
                    RentalPrice DECIMAL(10, 2),
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
                    PaymentDate DATE,
                    Amount DECIMAL(10, 2),
                    FOREIGN KEY (RentalID) REFERENCES Rentals(RentalID)
                );";
            using (var cmd = new SqliteCommand(createPaymentsTable, connection))
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

            var rentalsCmd = @"
                SELECT r.RentalID, r.CustomerID, r.SerialNumber, r.RentalStart, r.RentalPrice,
                       c.FirstName, c.LastName, i.Instrument
                FROM Rentals r
                JOIN Customers c ON r.CustomerID = c.CustomerID
                JOIN Instruments i ON r.SerialNumber = i.SerialNumber
            ";
            using var cmd = new SqliteCommand(rentalsCmd, connection);
            using var reader = cmd.ExecuteReader();
            var rentals = new List<(long RentalID, int CustomerID, string FirstName, string LastName, string Instrument, string SerialNumber, DateTime RentalStart, decimal RentalPrice)>();
            while (reader.Read())
            {
                rentals.Add((
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    reader.GetString(5) + " " + reader.GetString(6),
                    reader.GetString(5),
                    reader.GetString(7),
                    reader.GetString(2),
                    reader.GetDateTime(3),
                    reader.GetDecimal(4)
                ));
            }
            reader.Close();

            foreach (var rental in rentals)
            {
                int months = ((bisDatum.Year - rental.RentalStart.Year) * 12 + bisDatum.Month - rental.RentalStart.Month) + 1;
                if (months < 1) continue;
                decimal expected = months * rental.RentalPrice;

                // Summe und Anzahl aller Zahlungen bis bisDatum
                var sumCmd = new SqliteCommand(
                    "SELECT SUM(Amount), MAX(PaymentDate), COUNT(*) FROM Payments WHERE RentalID = $RentalID AND PaymentDate <= $BisDatum",
                    connection);
                sumCmd.Parameters.AddWithValue("$RentalID", rental.RentalID);
                sumCmd.Parameters.AddWithValue("$BisDatum", bisDatum);

                decimal paid = 0;
                string letzterMonat = "-";
                int anzahlZahlungen = 0;
                using (var sumReader = sumCmd.ExecuteReader())
                {
                    if (sumReader.Read())
                    {
                        paid = !sumReader.IsDBNull(0) ? sumReader.GetDecimal(0) : 0;
                        if (!sumReader.IsDBNull(1))
                        {
                            var dt = sumReader.GetDateTime(1);
                            letzterMonat = dt.ToString("MM.yyyy");
                        }
                        anzahlZahlungen = !sumReader.IsDBNull(2) ? sumReader.GetInt32(2) : 0;
                    }
                }

                if (paid < expected)
                {
                    result.Add(new OutstandingPaymentInfo
                    {
                        CustomerName = rental.FirstName,
                        CustomerId = rental.CustomerID,
                        Instrument = rental.Instrument,
                        OffenerBetrag = expected - paid,
                        LetzterZahlungsmonat = letzterMonat,
                        InsgesamtBezahlt = paid,
                        AnzahlZahlungen = anzahlZahlungen,
                        ErwarteteZahlungen = expected // NEU
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
        SELECT c.FirstName || ' ' || c.LastName AS CustomerName, c.CustomerID, i.Instrument, p.PaymentDate, p.Amount
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
                    Amount = reader.GetDecimal(4)
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
    }
}