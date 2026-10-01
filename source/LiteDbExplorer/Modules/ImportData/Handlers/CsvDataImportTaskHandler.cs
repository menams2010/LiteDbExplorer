using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using CsvHelper;
using CsvHelper.Configuration;
using Forge.Forms;
using Forge.Forms.Annotations;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Presentation.Behaviors;
using LiteDbExplorer.Wpf.Behaviors;

namespace LiteDbExplorer.Modules.ImportData.Handlers
{
    [Form(Mode = DefaultFields.None)]
    [Export(typeof(IDataImportTaskHandler))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class CsvDataImportTaskHandler : Screen, IDataImportTaskHandler, IStepsScreen, IOwnerViewLocator
    {
        private readonly SourceOptions _sourceOptions;

        [ImportingConstructor]
        public CsvDataImportTaskHandler(Lazy<IApplicationInteraction> lazyApplicationInteraction)
        {
            DisplayName = "Import CSV Options";

            _sourceOptions = new SourceOptions(lazyApplicationInteraction);
        }

        public string HandlerDisplayName => "CSV";

        public int HandlerDisplayOrder => 30;

        public object SourceOptionsContext => _sourceOptions;

        public bool CanContentScroll => true;

        public bool HasNext => true;

        public Task<object> Next()
        {
            return _sourceOptions.Next();
        }

        public bool Validate()
        {
            return ModelState.Validate(SourceOptionsContext);
        }

        public UIElement GetOwnView(object context)
        {
            return _sourceOptions.GetOwnView(context);
        }


        [Form(Grid = new[] { 1d, 1d }, Mode = DefaultFields.None)]
        [Heading("Source Options")]
        public class SourceOptions : ImportSourceFromFileScreen, IStepsScreen, IOwnerViewLocator
        {
            private readonly DataGrid _dataGrid;
            private Maybe<string> _maybeFilePath;

            public SourceOptions(Lazy<IApplicationInteraction> lazyApplicationInteraction) 
                : base(lazyApplicationInteraction)
            {
                Delimiter = DelimiterOptions.Keys.FirstOrDefault();

                _dataGrid = new DataGrid
                {
                    AutoGenerateColumns = true,
                    CanUserAddRows = false,
                    IsReadOnly = true,
                    SelectionUnit = DataGridSelectionUnit.FullRow,
                    GridLinesVisibility = DataGridGridLinesVisibility.All,
                    EnableRowVirtualization = true,
                    EnableColumnVirtualization = true,
                };

                Interaction.GetBehaviors(_dataGrid).Add(new EscapeAccessKeyColumnHeaderBehavior());
            }

            public bool CanContentScroll => false;

            public bool HasNext => true;

            public IReadOnlyDictionary<string, string> DelimiterOptions => new Dictionary<string, string>
            {
                {",","Comma (,)"},
                {";","Semicolon (;)"},
                {"\t","Tab"},
                {" ","Space"},
                {".","Dot (.)"},
                {"&","Ampersand (&)"},
                {"", "Other"}
            };

            /*[Field(Row = "0")]
            public int SkipFirstLines { get; set; }*/

            [Field(Row = "0")]
            public bool FileContainsHeaders { get; set; } = true;

            [Field(Row = "1")]
            [Value(Must.NotBeNull)]
            [SelectFrom("{Binding DelimiterOptions}", DisplayPath = "Value", ValuePath = "Key")]
            public string Delimiter { get; set; }

            [Field(Row = "1", IsVisible = "{Binding Delimiter|IsEmpty}")]
            public string OtherDelimiter { get; set; }

            public DataTable ItemsSource { get; private set; }

            public object DataPreview => _dataGrid;

            protected override (string title, string filter) GetFileFilter()
            {
                return ("Open CSV File", "CSV File|*.csv");
            }

            protected override Task OnFileOpen(Maybe<string> maybeFilePath)
            {
                return ProcessFile(maybeFilePath);
            }

            public Task<object> Next()
            {
                var targetOptions = new TargetOptions(this);
                return Task.FromResult<object>(targetOptions);
            }

            public bool Validate()
            {
                return ModelState.Validate(this);
            }

            public Task ProcessFile(Maybe<string> maybeFilePath)
            {
                _maybeFilePath = maybeFilePath;

                ItemsSource = maybeFilePath.HasValue ? ToDataTable(maybeFilePath.Value, this) : null;

                RefreshPreview(ItemsSource);

                PropertyChanged -= OnOptionsPropertyChanged;
                
                if (_maybeFilePath.HasValue)
                {
                    PropertyChanged += OnOptionsPropertyChanged;
                }

                return Task.CompletedTask;
            }

            private void OnOptionsPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (new[] {nameof(Delimiter), nameof(FileContainsHeaders), nameof(OtherDelimiter)}.Contains(e.PropertyName))
                {
                    ItemsSource = _maybeFilePath.HasValue ? ToDataTable(_maybeFilePath.Value, this) : null;
                    RefreshPreview(ItemsSource);
                }
            }

            public void RefreshPreview(DataTable itemsSource)
            {
                _dataGrid.ItemsSource = itemsSource?.DefaultView;
            }

            public UIElement GetOwnView(object context)
            {
                return new DynamicFormGrid(
                    new DynamicFormGrid.Item(this, GridLength.Auto),
                    new DynamicFormGrid.Item(DataPreview, new GridLength(1, GridUnitType.Star), true)
                );
            }

            public static DataTable ToDataTable(string path, SourceOptions options)
            {
                var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = options.Delimiter,
                };

                if (string.IsNullOrEmpty(options.Delimiter) && !string.IsNullOrEmpty(options.OtherDelimiter))
                {
                    configuration.Delimiter = options.OtherDelimiter;
                }

                var createColumns = !options.FileContainsHeaders;
                var dt = new DataTable();

                using (var reader = new StreamReader(path))
                using (var csv = new CsvReader(reader, configuration))
                {
                    // Track: https://github.com/JoshClose/CsvHelper/issues/1240
                    if (createColumns)
                    {
                        while (csv.Read())
                        {
                            if (createColumns)
                            {
                                for (var i = 0; i < csv.Parser.Record.Length; i++)
                                {
                                    dt.Columns.Add($"Column_{i}");
                                }
                                createColumns = false;
                            }

                            var row = dt.NewRow();
                            for (var i = 0; i < csv.Parser.Record.Length; i++)
                            {
                                row[i] = csv.Parser.Record[i];
                            }

                            dt.Rows.Add(row);
                        }
                    }
                    else
                    {
                        // Do any configuration to `CsvReader` before creating CsvDataReader.
                        using (var dr = new CsvDataReader(csv))
                        {	
                            dt.Load(dr);
                        }   
                    }
                }

                return dt;
            }
        }

        [Form(Grid = new[] { 1d, 1d }, Mode = DefaultFields.None)]
        [Heading("Target Options")]
        public class TargetOptions : ImportTargetSelectorScreen, IStepsScreen, IOwnerViewLocator
        {
            private readonly SourceOptions _sourceOptions;

            public TargetOptions(SourceOptions sourceOptions)
            {
                _sourceOptions = sourceOptions;
            }

            public bool CanContentScroll => true;

            public bool HasNext => true;

            public Task<object> Next()
            {
                var viewModel = IoC.Get<DocumentMapperViewModel>();

                IEnumerable<string> sourceFields = null;

                if (_sourceOptions.ItemsSource != null)
                {
                    sourceFields = _sourceOptions.ItemsSource.Columns.OfType<DataColumn>().Select(p => p.ColumnName);
                }

                var collectionReference = GetTargetCollection();

                viewModel.Init(sourceFields, collectionReference);

                return Task.FromResult<object>(viewModel);
            }

            public bool Validate()
            {
                return ModelState.Validate(this);
            }

            public UIElement GetOwnView(object context)
            {
                return new DynamicFormView(this);
            }
        }

        /*[Heading("Preview")]
        [Form(Mode = DefaultFields.None)]
        public class DataPreviewHolder : PropertyChangedBase
        {
            private readonly DataGrid _dataGrid;

            public DataPreviewHolder()
            {
                _dataGrid = new DataGrid
                {
                    AutoGenerateColumns = true,
                    CanUserAddRows = false,
                    IsReadOnly = true,
                    SelectionUnit = DataGridSelectionUnit.FullRow,
                    GridLinesVisibility = DataGridGridLinesVisibility.All,
                    MaxHeight = 200
                };

                DataPreview = new ViewContentProxy(_dataGrid);
            }

            [Field]
            [DirectContent]
            public ViewContentProxy DataPreview { get; set; }

            public void RefreshView(DataTable itemsSource)
            {
                _dataGrid.ItemsSource = itemsSource?.DefaultView;
            }
        }*/

    }
}