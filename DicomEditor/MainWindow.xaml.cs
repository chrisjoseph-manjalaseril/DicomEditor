using DicomEditor.Models;
using DicomEditor.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DicomEditor
{
    /// <summary>
    /// Main window for the DICOM Editor application.
    /// Handles UI events and coordinates with the MainViewModel.
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Handle keyboard shortcuts
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, OnOpenFiles));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, OnSave, CanSave));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, OnUndo, CanUndo));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, OnRedo, CanRedo));

            // Custom keyboard shortcuts
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.OpenFolderCommand.Execute(null)),
                Key.O, ModifierKeys.Control | ModifierKeys.Shift));

            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.SaveAllModifiedCommand.Execute(null)),
                Key.S, ModifierKeys.Control | ModifierKeys.Shift));

            // F5 for refresh
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.RefreshCommand.Execute(null)),
                Key.F5, ModifierKeys.None));

            // F2 for edit tag
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.EditTagCommand.Execute(null)),
                Key.F2, ModifierKeys.None));

            // Delete key for tag deletion
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.DeleteTagCommand.Execute(null)),
                Key.Delete, ModifierKeys.None));

            // Ctrl+Shift+A for bulk update
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.BulkUpdateTagCommand.Execute(null)),
                Key.A, ModifierKeys.Control | ModifierKeys.Shift));

            // Ctrl+Shift+D for bulk delete
            InputBindings.Add(new KeyBinding(
                new RelayCommand(() => ViewModel?.BulkDeleteTagCommand.Execute(null)),
                Key.D, ModifierKeys.Control | ModifierKeys.Shift));
        }

        #region Command Handlers

        private void OnOpenFiles(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel?.OpenFilesCommand.Execute(null);
        }

        private void OnSave(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel?.SaveSelectedFileCommand.Execute(null);
        }

        private void CanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel?.HasSelectedFile == true;
        }

        private void OnUndo(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel?.UndoCommand.Execute(null);
        }

        private void CanUndo(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel?.CanUndo == true;
        }

        private void OnRedo(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel?.RedoCommand.Execute(null);
        }

        private void CanRedo(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ViewModel?.CanRedo == true;
        }

        #endregion

        #region Menu Event Handlers

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Check for unsaved changes
            if (ViewModel?.HasModifiedFiles == true)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before exiting?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes)
                {
                    ViewModel.SaveAllModifiedCommand.Execute(null);
                }
            }

            Application.Current.Shutdown();
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "DICOM Editor v1.0.0\n\n" +
                "A high-performance DICOM file editor for medical imaging.\n\n" +
                "Features:\n" +
                "• Load and edit DICOM files and folders\n" +
                "• Search and filter DICOM tags\n" +
                "• Undo/Redo support\n" +
                "• Safe save with backup\n" +
                "• Handles 20,000+ files efficiently",
                "About DICOM Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Prevents editing of non-editable tags (binary data, sequences, etc.)
        /// </summary>
        private void TagsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is DicomTagItem tag)
            {
                // Only allow editing of the Value column
                if (e.Column.Header?.ToString() != "Value")
                {
                    e.Cancel = true;
                    return;
                }

                // Prevent editing of non-editable tags
                if (!tag.IsEditable)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Handles tag value editing in the DataGrid.
        /// </summary>
        private void TagsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit &&
                e.EditingElement is TextBox textBox &&
                e.Row.Item is DicomTagItem tag)
            {
                var oldValue = tag.ValueDisplay;
                var newValue = textBox.Text;

                if (oldValue != newValue)
                {
                    // Defer to ViewModel for validation and undo support
                    ViewModel?.OnTagValueEdited(tag, oldValue, newValue);
                }
            }
        }

        /// <summary>
        /// Handles drag-and-drop of files onto the window.
        /// </summary>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    // Check if it's a folder
                    if (System.IO.Directory.Exists(files[0]) && files.Length == 1)
                    {
                        ViewModel?.OpenFolderCommand.Execute(files[0]);
                    }
                    else
                    {
                        // Load files directly - need to expose this in ViewModel
                        // For now, trigger open dialog behavior
                        ViewModel?.OpenFilesCommand.Execute(null);
                    }
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        #endregion

        /// <summary>
        /// Simple relay command for keyboard bindings.
        /// </summary>
        private class RelayCommand : ICommand
        {
            private readonly Action _execute;

            public RelayCommand(Action execute) => _execute = execute;

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => _execute();
        }
    }
    
    /// <summary>
    /// Converts boolean to Visibility (inverse of built-in converter).
    /// True = Collapsed, False = Visible
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }
}