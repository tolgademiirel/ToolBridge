using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MusicShell.Infrastructure;
using MusicShell.Models;
using MusicShell.ViewModels;

namespace MusicShell;

public partial class MainWindow
{
    private const string ConvertOutputDragFormat = "ToolBridge.ConvertOutputDrag";
    private Point? _convertOutputDragStartPoint;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;

        Closed += (_, _) =>
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };

        SourceInitialized += (_, _) => ApplyNativeWindowTheme(viewModel.IsDarkModeEnabled);
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkModeEnabled) && sender is MainViewModel viewModel)
        {
            ApplyNativeWindowTheme(viewModel.IsDarkModeEnabled);
        }
    }

    private void TransferRecipientItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not FrameworkElement { DataContext: OnlineUserItem user })
        {
            return;
        }

        if (viewModel.SelectTransferRecipientCommand.CanExecute(user))
        {
            viewModel.SelectTransferRecipientCommand.Execute(user);
            e.Handled = true;
        }
    }


    private void IncomingTransferItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: PendingTransferItem transfer } &&
            viewModel.OpenIncomingTransferCommand.CanExecute(transfer))
        {
            viewModel.OpenIncomingTransferCommand.Execute(transfer);
            e.Handled = true;
        }
    }

    private void UploadDropzone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void UploadDropzone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
        {
            viewModel.AddUploadFiles(filePaths);
        }

        e.Handled = true;
    }

    private void ConvertDropzone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ConvertOutputDragFormat))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ConvertDropzone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ConvertOutputDragFormat))
        {
            e.Handled = true;
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
        {
            viewModel.AddConvertFiles(filePaths);
        }

        e.Handled = true;
    }

    private void PdfMergeDropzone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ConvertOutputDragFormat))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void PdfMergeDropzone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ConvertOutputDragFormat))
        {
            e.Handled = true;
            return;
        }

        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
        {
            viewModel.AddPdfMergeFiles(filePaths);
        }

        e.Handled = true;
    }

    private void ConvertTransferDropzone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ConvertTransferDropzone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
        {
            viewModel.AddConvertTransferFiles(filePaths);
        }

        e.Handled = true;
    }

    private void TransferDropzone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TransferDropzone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel viewModel &&
            e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
        {
            viewModel.AddTransferFiles(filePaths);
        }

        e.Handled = true;
    }

    private void RootWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.P && DataContext is MainViewModel printViewModel)
        {
            if (printViewModel.PrintDocumentsCommand.CanExecute(null))
            {
                printViewModel.PrintDocumentsCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && DataContext is MainViewModel viewModel && Clipboard.ContainsFileDropList())
        {
            var filePaths = Clipboard.GetFileDropList().Cast<string>();
            if (viewModel.SelectedPage == "Convert")
            {
                viewModel.AddConvertFiles(filePaths);
            }
            else if (viewModel.SelectedPage == "Transfer")
            {
                viewModel.AddTransferFiles(filePaths);
            }
            else
            {
                viewModel.AddUploadFiles(filePaths);
            }

            e.Handled = true;
        }
    }

    private void ConvertFormatChip_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not Button button)
        {
            return;
        }

        var format = button.Content?.ToString();
        if (!string.IsNullOrWhiteSpace(format) && viewModel.SelectConvertTargetFormatCommand.CanExecute(format))
        {
            viewModel.SelectConvertTargetFormatCommand.Execute(format);
        }

        e.Handled = true;
    }

    private void ConvertOutput_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            _convertOutputDragStartPoint = e.GetPosition(element);
        }

        if (e.ClickCount < 2 || DataContext is not MainViewModel viewModel || sender is not FrameworkElement clickedElement)
        {
            return;
        }

        _convertOutputDragStartPoint = null;

        if (clickedElement.DataContext is ConvertFileItem item && viewModel.OpenConvertResultCommand.CanExecute(item))
        {
            viewModel.OpenConvertResultCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void ConvertOutput_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element || _convertOutputDragStartPoint is not Point startPoint)
        {
            return;
        }

        var currentPoint = e.GetPosition(element);
        if (Math.Abs(currentPoint.X - startPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPoint.Y - startPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (element.DataContext is not ConvertFileItem item)
        {
            return;
        }

        var outputPaths = ResolveConvertOutputDragPaths(item);
        if (outputPaths.Length == 0)
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(DataFormats.FileDrop, outputPaths);
        dataObject.SetData(ConvertOutputDragFormat, true);
        DragDrop.DoDragDrop(element, dataObject, DragDropEffects.Copy);
        _convertOutputDragStartPoint = null;
        e.Handled = true;
    }

    private string[] ResolveConvertOutputDragPaths(ConvertFileItem clickedItem)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return Array.Empty<string>();
        }

        static bool HasUsableOutput(ConvertFileItem item) =>
            !string.IsNullOrWhiteSpace(item.OutputPath) && File.Exists(item.OutputPath);

        var selectedOutputPaths = viewModel.ConvertFiles
            .Where(file => file.IsSelectedForPrintPool && HasUsableOutput(file))
            .Select(file => file.OutputPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clickedItem.IsSelectedForPrintPool && selectedOutputPaths.Length > 0)
        {
            return selectedOutputPaths;
        }

        return HasUsableOutput(clickedItem)
            ? new[] { clickedItem.OutputPath }
            : Array.Empty<string>();
    }


    private void ApplyNativeWindowTheme(bool isDarkModeEnabled)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var value = isDarkModeEnabled ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkMode, ref value, Marshal.SizeOf<int>());
            _ = DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkModeBefore20H1, ref value, Marshal.SizeOf<int>());
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Windows başlık çubuğu tema uygulaması atlandı", ex);
            // DWM desteklenmeyen Windows sürümlerinde standart başlık çubuğuyla devam edilir.
        }
    }

    private const int DwmWindowAttributeUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

}
