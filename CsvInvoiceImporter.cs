using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;

namespace cvsimporter
{
    public static class CsvInvoiceImporter
    {
        /// <summary>
        /// Öffnet einen Dateidialog, liest die CSV und gibt die Liste der InvoiceRecord-Objekte zurück.
        /// </summary>
        public static List<InvoiceRecord>? ImportInvoices(Window owner)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog(owner) != true)
                return null;

            try
            {
                var records = new List<InvoiceRecord>();
                foreach (var line in File.ReadLines(openFileDialog.FileName).Skip(1)) // Überspringt die Kopfzeile
                {
                    var parts = line.Split(';');
                    if (parts.Length < 5) continue;
                    records.Add(new InvoiceRecord
                    {
                        CustomerName = parts[0],
                        CustomerId = parts[1],
                        InvoiceNumber = parts[2],
                        InvoiceDate = DateTime.ParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Paid = parts[4].Trim().ToLower() == "ja"
                    });
                }
                return records;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Fehler beim Importieren der CSV: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static List<InvoiceRecord> LoadCsv(string dateipfad)
        {
            var records = new List<InvoiceRecord>();
            foreach (var line in File.ReadLines(dateipfad).Skip(1)) // Überspringt die Kopfzeile
            {
                var parts = line.Split(';');
                if (parts.Length < 5) continue;
                records.Add(new InvoiceRecord
                {
                    CustomerName = parts[0],
                    CustomerId = parts[1],
                    InvoiceNumber = parts[2],
                    InvoiceDate = DateTime.ParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Paid = parts[4].Trim().ToLower() == "ja"
                });
            }
            return records;
        }
    }
}