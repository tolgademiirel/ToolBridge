using System;
using System.Printing;
using MusicShell.Infrastructure;
using MusicShell.Models;

namespace MusicShell.ViewModels;

public sealed partial class MainViewModel
{
    private bool IsDuplexPrintRequested()
    {
        var side = SelectedPrintSide ?? string.Empty;
        var normalized = NormalizePrinterSearchKey(side);

        if (normalized.Contains("TEK", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("ONE", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("SIMPLEX", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Contains("CIFT", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("DUPLEX", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("DOUBLE", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("IKI", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("TWO", StringComparison.OrdinalIgnoreCase);
    }

    private string GetPjlDuplexMode()
    {
        return IsDuplexPrintRequested() ? "ON" : "OFF";
    }

    private string GetPjlSidesMode()
    {
        return IsDuplexPrintRequested() ? "TWO-SIDED-LONG-EDGE" : "ONE-SIDED";
    }

    private string GetSumatraSideMode()
    {
        return IsDuplexPrintRequested() ? "duplex" : "simplex";
    }

    private void TryApplySelectedPrintTicketToQueue(PrinterDeviceItem printer)
    {
        try
        {
            var printerQueueName = ResolveWindowsPrinterName(printer, out _);
            if (string.IsNullOrWhiteSpace(printerQueueName))
            {
                return;
            }

            using var printServer = new LocalPrintServer();
            using var queue = printServer.GetPrintQueue(printerQueueName);
            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
            var requestedTicket = ticket.Clone();
            ApplyPrintTicketSettings(requestedTicket);

            try
            {
                var validation = queue.MergeAndValidatePrintTicket(ticket, requestedTicket);
                queue.UserPrintTicket = validation.ValidatedPrintTicket;
            }
            catch
            {
                queue.UserPrintTicket = requestedTicket;
            }

            queue.Commit();
            AppLogger.Log($"Print queue ticket applied. Printer={printerQueueName}; Side={(IsDuplexPrintRequested() ? "Duplex" : "OneSided")}; Color={SelectedPrintColor}; Paper={SelectedPaperSize}");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Print queue ticket could not be applied", ex);
        }
    }
}
