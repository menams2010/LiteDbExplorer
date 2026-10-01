using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using LiteDbExplorer.Core;

namespace LiteDbExplorer.Modules.DbDocument
{
    [Export(typeof(DocumentEntryViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class DocumentEntryViewModel : Screen
    {
        public DocumentEntryViewModel()
        {
            DisplayName = "Document Editor";
        }

        public void Init(DocumentReference document)
        {
            Document = document;
        }

        public DocumentReference Document { get; private set; }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await base.OnDeactivateAsync(close, cancellationToken);
            if (close)
            {
                Document = null;
            }
        }
    }
}