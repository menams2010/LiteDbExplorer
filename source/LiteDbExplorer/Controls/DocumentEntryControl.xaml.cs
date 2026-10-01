using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interactivity;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using LiteDB;
using LiteDbExplorer.Core;
using LiteDbExplorer.Framework;
using LiteDbExplorer.Modules;
using LiteDbExplorer.Wpf.Behaviors;
using Action = System.Action;

namespace LiteDbExplorer.Controls
{
    public class DocumentFieldData : INotifyPropertyChanged
    {
        public DocumentFieldData(string name, FrameworkElement editControl, BsonType bsonType, bool isReadonly = false)
        {
            Name = name;
            EditControl = editControl;
            BsonType = bsonType;
            IsReadOnly = isReadonly;
        }

        public string Name { get; set; }

        public FrameworkElement EditControl { get; set; }

        public bool IsReadOnly { get; set; }

        public bool IsEnable => !IsReadOnly;

        public BsonType BsonType { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    ///     Interaction logic for DocumentViewerControl.xaml
    /// </summary>
    public partial class DocumentEntryControl : UserControl
    {
        public static readonly RoutedUICommand PreviousItem = new RoutedUICommand
        (
            "Previous Item",
            nameof(PreviousItem),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.PageUp)
            }
        );

        public static readonly RoutedUICommand NextItem = new RoutedUICommand
        (
            "Next Item",
            nameof(NextItem),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.PageDown)
            }
        );

        public static readonly RoutedUICommand OkCommand = new RoutedUICommand
        (
            "Ok",
            nameof(OkCommand),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.S, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand CancelCommand = new RoutedUICommand
        (
            "Cancel",
            nameof(CancelCommand),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.Escape)
            }
        );

        public static readonly RoutedUICommand NewCommand = new RoutedUICommand
        (
            "New Field",
            nameof(NewCommand),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.N, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand SaveChangesCommand = new RoutedUICommand
        (
            "Save",
            nameof(SaveChangesCommand),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.S, ModifierKeys.Shift | ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand DiscardChangesCommand = new RoutedUICommand
        (
            "Discard",
            nameof(DiscardChangesCommand),
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.Z, ModifierKeys.Shift | ModifierKeys.Control)
            }
        );

        public static readonly Dictionary<BsonType, Func<BsonValue>> FieldTypesMap =
            new Dictionary<BsonType, Func<BsonValue>>
            {
                {BsonType.String, () => new BsonValue(string.Empty)},
                {BsonType.Boolean, () => new BsonValue(false)},
                {BsonType.Double, () => new BsonValue((double) 0)},
                {BsonType.Decimal, () => new BsonValue((decimal) 0.0m)},
                {BsonType.Int32, () => new BsonValue(0)},
                {BsonType.Int64, () => new BsonValue((long) 0)},
                {BsonType.DateTime, () => new BsonValue(DateTime.Now)},
                {BsonType.Guid, () => new BsonValue(Guid.Empty)},
                {BsonType.Array, () => new BsonArray()},
                {BsonType.Document, () => new BsonDocument()},
            };

        public static readonly DependencyProperty DocumentReferenceProperty = DependencyProperty.Register(
            nameof(DocumentReference), typeof(DocumentReference), typeof(DocumentEntryControl),
            new PropertyMetadata(default(DocumentReference), OnDocumentReferenceChanged));

        private readonly string _dialogHostIdentifier;
        private readonly WindowController _windowController;
        private BsonDocument _currentDocument;
        private DocumentReference _documentReference;
        private ObservableCollection<DocumentFieldData> _entryControls;
        private bool _loaded = false;
        private Dictionary<string, BsonType> _allFieldsWithTypes;
        private bool _documentHasChanges;

        public DocumentEntryControl()
        {
            InitializeComponent();

            dialogHost.Identifier = _dialogHostIdentifier = $"DocumentEntryDialogHost_{Guid.NewGuid()}";

            ListItems.Loaded += ListItemsOnLoaded;

            AddNewFieldCommand = new RelayCommand<BsonType?>(async type => await AddNewFieldHandler(type));

            AddExistingFieldCommand = new RelayCommand<KeyValuePair<string, BsonType>?>(async pair => await AddExistingFieldHandler(pair));

            AddAllExistingFieldCommand = new RelayCommand(async _ => await AddAllExistingFieldHandler());

            LoadNewFieldsPicker();

            Interaction.GetBehaviors(DropNewField).Add(new ButtonClickOpenMenuBehavior());

            AddExistingFieldsButton.IsEnabled = false;

            Interaction.GetBehaviors(AddExistingFieldsButton).Add(new ButtonClickOpenMenuBehavior());
        }

        private DocumentEntryControl(WindowController windowController) : this()
        {
            _windowController = windowController;
            _windowController.CanClose = CanDiscardChanges;
        }

        public DocumentEntryControl(DocumentReference document, WindowController windowController = null) : this(
            windowController)
        {
            LoadDocument(document);
        }

        public DocumentEntryControl(BsonDocument document, bool readOnly, WindowController windowController = null) :
            this(windowController)
        {
            IsReadOnly = readOnly;

            LoadDocument(document);

            // ButtonNext.Visibility = Visibility.Collapsed;
            // ButtonPrev.Visibility = Visibility.Collapsed;
            AddExistingFieldsButton.Visibility = Visibility.Collapsed;
            SaveChangesButton.Visibility = Visibility.Collapsed;
            DiscardChangesButton.Visibility = Visibility.Collapsed;
            PagePanel.Visibility = Visibility.Collapsed;

            if (readOnly)
            {
                ButtonClose.Visibility = Visibility.Visible;
                ButtonOK.Visibility = Visibility.Collapsed;
                ButtonCancel.Visibility = Visibility.Collapsed;
                DropNewField.Visibility = Visibility.Collapsed;
            }
        }

        public RelayCommand<BsonType?> AddNewFieldCommand { get; }

        public RelayCommand<KeyValuePair<string, BsonType>?> AddExistingFieldCommand { get; }

        public RelayCommand AddAllExistingFieldCommand { get; }

        public DocumentReference DocumentReference
        {
            get => (DocumentReference) GetValue(DocumentReferenceProperty);
            set => SetValue(DocumentReferenceProperty, value);
        }

        public bool IsReadOnly { get; }

        public bool DialogResult { get; set; }

        public bool DocumentHasChanges
        {
            get => _documentHasChanges;
            private set
            {
                _documentHasChanges = value;
                if (_windowController != null)
                {
                    _windowController.ShowChangeIndicator = _documentHasChanges;
                }
            }
        }

        private void SetDocumentChanged()
        {
            DocumentHasChanges = true;
        }

        private void ListItemsOnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            ListItems.Focus();
        }

        private static void OnDocumentReferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as DocumentEntryControl;
            var documentReference = e.NewValue as DocumentReference;
            control?.LoadDocument(documentReference);
        }

        public void LoadDocument(BsonDocument document)
        {
            DocumentHasChanges = false;

            _currentDocument = document;
            _entryControls = new ObservableCollection<DocumentFieldData>();

            for (var i = 0; i < document.Keys.Count; i++)
            {
                var key = document.Keys.ElementAt(i);
                _entryControls.Add(NewField(key, IsReadOnly));
            }

            ListItems.ItemsSource = _entryControls;
        }

        public void LoadDocument(DocumentReference document)
        {
            DocumentHasChanges = false;

            if (document.Collection is FileCollectionReference reference)
            {
                IsFileCollection = true;
                var fileInfo = reference.GetFileObject(document);
                GroupFile.Visibility = Visibility.Visible;
                FileView.LoadFile(fileInfo);
            }

            _documentReference = document;
            _currentDocument = document.FindFromCollectionRef();
            _entryControls = new ObservableCollection<DocumentFieldData>();

            for (var i = 0; i < document.LiteDocument.Keys.Count; i++)
            {
                var key = document.LiteDocument.Keys.ElementAt(i);
                var fieldData = NewField(key, IsReadOnly);
                _entryControls.Add(fieldData);
            }

            ListItems.ItemsSource = _entryControls;

            LoadExistingFieldsPicker();

            if (_documentReference?.Collection != null)
            {
                var index = _documentReference.Collection.Items.IndexOf(_documentReference);
                var itemsCount = _documentReference.Collection.Items.Count;
                SetPageInfo(index, itemsCount);
            }
        }

        public bool IsFileCollection { get; private set; }

        private void LoadNewFieldsPicker()
        {
            var addNewFieldContextMenu = new ContextMenu
            {
                MinWidth = 160
            };

            foreach (var item in FieldTypesMap)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Key,
                    Command = AddNewFieldCommand,
                    CommandParameter = item.Key
                };
                addNewFieldContextMenu.Items.Add(menuItem);
            }

            DropNewField.ContextMenu = addNewFieldContextMenu;
        }

        private void LoadExistingFieldsPicker()
        {
            AddExistingFieldsButton.IsEnabled = false;
            AddExistingFieldsButton.ContextMenu = null;

            if (_documentReference == null || _currentDocument == null)
            {
                return;
            }

            var currentDocumentKeys = _currentDocument.Keys;

            // Get distinct fields from all entries
            _allFieldsWithTypes = _documentReference.Collection
                .Items
                .SelectMany(p => p.LiteDocument.RawValue)
                .GroupBy(p => p.Key)
                .Select(p => new {p.First().Key, Value = p.First().Value.Type})
                .Where(
                    p => !currentDocumentKeys.Contains(p.Key) &&
                         (p.Value == BsonType.Null || FieldTypesMap.ContainsKey(p.Value))
                )
                .ToDictionary(p => p.Key, p => p.Value);

            if (_allFieldsWithTypes.Any())
            {
                var existingFieldsPickerMenu = new ContextMenu
                {
                    MinWidth = 160
                };

                foreach (var item in _allFieldsWithTypes)
                {
                    var menuItem = new MenuItem
                    {
                        Header = $"{item.Key.Replace("_", "__")}",
                        InputGestureText = $"{item.Value}",
                        Command = AddExistingFieldCommand,
                        CommandParameter = item
                    };
                    existingFieldsPickerMenu.Items.Add(menuItem);
                }

                existingFieldsPickerMenu.Items.Add(new Separator());
                var allAllMenuItem = new MenuItem
                {
                    Header = "Add All Fields",
                    Command = AddAllExistingFieldCommand
                };
                existingFieldsPickerMenu.Items.Add(allAllMenuItem);

                AddExistingFieldsButton.IsEnabled = true;
                AddExistingFieldsButton.ContextMenu = existingFieldsPickerMenu;
            }
        }

        private DocumentFieldData NewField(string key, bool readOnly)
        {
            var expandMode = OpenEditorMode.Inline;
            if (_windowController != null)
            {
                expandMode = OpenEditorMode.Window;
            }

            var bsonValue = _currentDocument[key];

            var editorAllowEditId = Properties.Settings.Default.DocumentEditor_AllowEditId;
            var isReadOnly = readOnly || (bsonValue.IsObjectId && !editorAllowEditId) || (IsFileCollection && key.Equals("_id") && !editorAllowEditId);

            if (bsonValue.IsNull)
            {
                var docTypePicker = GetNullValueEditor(key, isReadOnly, expandMode);
                return new DocumentFieldData(key, docTypePicker, bsonValue.Type);
            }

            var editorContext = new BsonValueEditorContext(expandMode, $"[{key}]", bsonValue, _currentDocument, isReadOnly, key)
            {
                ChangedCallback = SetDocumentChanged
            };

            var valueEdit =
                BsonValueEditor.GetBsonValueEditor(editorContext, UserDefinedCultureFormat.Default);

            return new DocumentFieldData(key, valueEdit, bsonValue.Type, isReadOnly);
        }

        private FrameworkElement GetNullValueEditor(string key, bool readOnly, OpenEditorMode expandMode)
        {
            var docTypePicker = new Button
            {
                Content = "(null)",
                ToolTip = "Select null field type",
                Style = StyleKit.MaterialDesignEntryButtonStyle
            };

            if (readOnly)
            {
                return docTypePicker;
            }

            var fieldToggleMenuItems = GetFieldToggleMenuItems(key, keyValuePair =>
            {
                if (keyValuePair.HasValue)
                {
                    var documentFieldData = _entryControls.First(p => p.Name == keyValuePair.Value.Key);
                    var fieldDefaultValue = GetFieldDefaultValue(keyValuePair.Value.Value);

                    _currentDocument[key] = fieldDefaultValue;

                    var editorContext = new BsonValueEditorContext(expandMode, $"[{key}]", _currentDocument[key], _currentDocument, readOnly, key)
                    {
                        ChangedCallback = SetDocumentChanged
                    };

                    documentFieldData.EditControl =
                        BsonValueEditor.GetBsonValueEditor(editorContext, UserDefinedCultureFormat.Default);
                }
            });

            var typeMenuPicker = new ContextMenu();
            foreach (var menuItem in fieldToggleMenuItems)
            {
                typeMenuPicker.Items.Add(menuItem);
            }

            docTypePicker.ContextMenu = typeMenuPicker;
            Interaction.GetBehaviors(docTypePicker).Add(new ButtonClickOpenMenuBehavior());

            return docTypePicker;
        }

        private IEnumerable<MenuItem> GetFieldToggleMenuItems(string name,
            Action<KeyValuePair<string, BsonType>?> clickAction)
        {
            var result = new List<MenuItem>();
            foreach (var bsonType in FieldTypesMap.Keys)
            {
                var menuItem = new MenuItem
                {
                    Header = bsonType.ToString(),
                    Command = new RelayCommand<KeyValuePair<string, BsonType>?>(clickAction),
                    CommandParameter = new KeyValuePair<string, BsonType>(name, bsonType),
                };
                result.Add(menuItem);
            }

            return result;
        }

        private BsonValue GetFieldDefaultValue(BsonType bsonType)
        {
            if (bsonType == BsonType.Null)
            {
                return new BsonValue();
            }

            if (!FieldTypesMap.ContainsKey(bsonType))
            {
                throw new Exception("Uknown value type.");
            }

            return FieldTypesMap[bsonType]();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string key)
            {
                RemoveField(key);
            }
        }

        public event EventHandler CloseRequested;

        private void Close()
        {
            OnCloseRequested();

            _documentReference = null;
            _currentDocument = null;

            if (_windowController != null)
            {
                _windowController.CanClose = null;
                _windowController.Close(DialogResult);
            }
        }

        

        private void SaveChanges(bool reload)
        {
            // TODO make array and document types use this as well
            foreach (var ctrl in _entryControls)
            {
                var control = ctrl.EditControl;
                var values = control.GetLocalValueEnumerator();
                while (values.MoveNext())
                {
                    var current = values.Current;
                    if (BindingOperations.IsDataBound(control, current.Property))
                    {
                        var binding = control.GetBindingExpression(current.Property);
                        if (binding?.IsDirty == true)
                        {
                            binding.UpdateSource();
                        }
                    }
                }
            }

            if (_documentReference != null && _currentDocument != null)
            {
                _currentDocument.CopyTo(_documentReference.LiteDocument);
                _documentReference.LiteDocument = _currentDocument;
                _documentReference.Collection.UpdateDocument(_documentReference);

                _documentReference.NotifyDocumentChanged();
            }

            DocumentHasChanges = false;

            if (reload)
            {
                Reload();
            }
        }

        private void Reload()
        {
            if (_documentReference != null)
            {
                LoadDocument(_documentReference);
            }
            else if (_currentDocument != null)
            {
                LoadDocument(_currentDocument);
            }

            DocumentHasChanges = false;
        }

        private Result ValidateFieldName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Result.Failure("Name cannot be empty.");
            }

            if (value.Any(char.IsWhiteSpace))
            {
                return Result.Failure("Field name can not contain white spaces.");
            }

            if (_currentDocument.Keys.Contains(value))
            {
                return Result.Failure($"Field \"{value}\" already exists!");
            }

            return Result.Success();
        }

        private async Task AddExistingFieldHandler(KeyValuePair<string, BsonType>? keyValuePair)
        {
            if (keyValuePair.HasValue)
            {
                await AddNewField(keyValuePair.Value.Key, keyValuePair.Value.Value);
            }
        }

        private async Task AddAllExistingFieldHandler()
        {
            if (_allFieldsWithTypes != null)
            {
                var count = _allFieldsWithTypes.Count;
                foreach (var pair in _allFieldsWithTypes)
                {
                    count--;
                    await AddNewField(pair.Key, pair.Value, count == 0);
                }
            }
        }

        private async Task AddNewFieldHandler(BsonType? bsonType)
        {
            if (!bsonType.HasValue)
            {
                return;
            }

            var maybeFieldName =
                await InputDialogView.Show(_dialogHostIdentifier, "Enter name of new field.", "New field name", string.Empty, ValidateFieldName);

            if (maybeFieldName.HasNoValue)
            {
                return;
            }

            var fieldName = maybeFieldName.Value.Trim();
            
            await AddNewField(fieldName, bsonType.Value);
        }

        private async Task AddNewField(string fieldName, BsonType bsonType, bool isLast = true)
        {
            var newValue = GetFieldDefaultValue(bsonType);

            _currentDocument.Add(fieldName, newValue);

            var newField = NewField(fieldName, false);
            
            _entryControls.Add(newField);

            SetDocumentChanged();

            if (isLast)
            {
                LoadExistingFieldsPicker();

                await Task.Delay(150);

                ListItems.ScrollIntoView(newField);
                newField.EditControl.Focus();
            }
        }

        private void RemoveField(string name)
        {
            var documentFieldData = _entryControls.First(a => a.Name == name);
            
            _entryControls.Remove(documentFieldData);

            _currentDocument.Remove(name);

            SetDocumentChanged();

            LoadExistingFieldsPicker();
        }

        private async Task RenameField(string currentName)
        {
            if (string.IsNullOrEmpty(currentName))
            {
                return;
            }

            var maybeFieldName =
                await InputDialogView.Show(_dialogHostIdentifier, "Rename Field", "New field Name", currentName, ValidateFieldName);

            if (maybeFieldName.HasNoValue)
            {
                return;
            }

            var newName = maybeFieldName.Value.Trim();

            RenameField(currentName, newName);
        }

        private void RenameField(string currentName, string newName)
        {
            if (string.IsNullOrEmpty(currentName) || string.IsNullOrEmpty(newName) || newName.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            var documentFieldData = _entryControls.FirstOrDefault(p => p.Name.Equals(currentName));
            var documentFieldIndex = -1;
            if (documentFieldData != null)
            {
                documentFieldIndex = _entryControls.IndexOf(documentFieldData);
                _entryControls.RemoveAt(documentFieldIndex);
            }

            if (documentFieldIndex < 0)
            {
                documentFieldIndex = _entryControls.Count - 1;
            }

            var value = _currentDocument[currentName];
            _currentDocument.Remove(currentName);
            _currentDocument.Add(newName, value);

            var newField = NewField(newName, false);
            _entryControls.Insert(documentFieldIndex, newField);

            SetDocumentChanged();

            LoadExistingFieldsPicker();
        }

        private void DocumentFieldActions_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is DocumentFieldData fieldData))
            {
                return;
            }

            var key = fieldData.Name;
            var bsonValue = _currentDocument[key];
            var convertibleTypes = BsonValueConverter.ConvertibleTypes(bsonValue);
            
            var contextMenu = new ContextMenu
            {
                MinWidth = 160,
                Placement = PlacementMode.Bottom,
                PlacementTarget = element
            };
            contextMenu.Closed += (o, args) =>
            {
                element.ContextMenu = null;
            };

            void ChangeType(Func<BsonValue> getValue)
            {
                var value = getValue();
                fieldData.EditControl = null;
                _currentDocument[key] = value;
                fieldData.BsonType = value.Type;

                var editorContext = new BsonValueEditorContext(OpenEditorMode.Window, $"[{key}]", value, _currentDocument, IsReadOnly, key)
                {
                    ChangedCallback = SetDocumentChanged
                };

                fieldData.EditControl = BsonValueEditor.GetBsonValueEditor(editorContext, UserDefinedCultureFormat.Default);
                
                SetDocumentChanged();

                LoadExistingFieldsPicker();
            }

            var renameMenuItem = new MenuItem
            {
                Header = "Rename",
                Command = new AsyncCommand<string>(RenameField),
                CommandParameter = key
            };
            contextMenu.Items.Add(renameMenuItem);
            
            if (convertibleTypes.Any())
            {
                contextMenu.Items.Add(new Separator());

                foreach (var option in convertibleTypes)
                {
                    if (option.Key == bsonValue.Type)
                    {
                        continue;
                    }

                    Action convertAction = () => ChangeType(option.Value);
                    var menuItem = new MenuItem
                    {
                        Header = $"Convert to {option.Key}",
                        Command = new RelayCommand(param =>
                        {
                            if (param is Action action)
                            {
                                action();
                            }

                            element.ContextMenu = null;
                        }),
                        CommandParameter = convertAction
                    };
                    contextMenu.Items.Add(menuItem);
                }
            }
            
            element.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }

        private bool CanDiscardChanges()
        {
            if (DocumentHasChanges)
            {
                var applicationInteraction = IoC.Get<IApplicationInteraction>();
                var result = applicationInteraction.ShowChangesActionDialog();

                if (result == ChangesActionResult.Save)
                {
                    SaveChanges(true);
                }

                return result != ChangesActionResult.Cancel;
            }

            return true;
        }

        protected virtual void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SetPageInfo(int pageIndex, int count)
        {
            PageInfoText.Text = $"{pageIndex+1} of {count}";
        }

        #region Commamnd Handlers

        private void ReadOnly_CanNotExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !IsReadOnly;
        }

        private void CancelCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (dialogHost.IsOpen)
            {
                dialogHost.CurrentSession?.Close(false);
                e.Handled = true;
                return;    
            }

            DialogResult = false;
            Close();
        }

        private void OkCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (dialogHost.IsOpen)
            {
                dialogHost.CurrentSession?.Close(true);
                e.Handled = true;
                return;
            }

            SaveChanges(false);

            DialogResult = true;

            Close();
        }

        private void NewCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (dialogHost.IsOpen)
            {
                e.Handled = true;
                return;    
            }

            if (DropNewField.ContextMenu != null)
            {
                DropNewField.ContextMenu.IsOpen = true;
                DropNewField.ContextMenu.Focus();
                DropNewField.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault()?.Focus();
            }
        }

        private void NextItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!CanDiscardChanges())
            {
                return;
            }

            var index = _documentReference.Collection.Items.IndexOf(_documentReference);
            var itemsCount = _documentReference.Collection.Items.Count;
            var newIndex = index + 1;
            if (newIndex < itemsCount)
            {
                var newDocument = _documentReference.Collection.Items[newIndex];
                LoadDocument(newDocument);
            }

            SetPageInfo(newIndex, itemsCount);
        }

        private void NextItemCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_documentReference?.Collection == null)
            {
                e.CanExecute = false;
            }
            else
            {
                var index = _documentReference.Collection.Items.IndexOf(_documentReference);
                e.CanExecute = index + 1 < _documentReference.Collection.Items.Count;
            }
        }

        private void PreviousItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!CanDiscardChanges())
            {
                return;
            }

            var index = _documentReference.Collection.Items.IndexOf(_documentReference);
            var itemsCount = _documentReference.Collection.Items.Count;
            var newIndex = index - 1;
            if (index > 0)
            {
                var newDocument = _documentReference.Collection.Items[newIndex];
                LoadDocument(newDocument);
            }
            SetPageInfo(newIndex, itemsCount);
        }

        private void PreviousItemCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_documentReference?.Collection == null)
            {
                e.CanExecute = false;
            }
            else
            {
                var index = _documentReference.Collection.Items.IndexOf(_documentReference);
                e.CanExecute = index > 0;
            }
        }        

        private void SaveChangesCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SaveChanges(true);
        }

        private void SaveChangesCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _currentDocument != null && DocumentHasChanges;
        }

        private void DiscardChangesCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Reload();
        }

        private void DiscardChangesCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _currentDocument != null && DocumentHasChanges;
        }

        #endregion

        
    }
}