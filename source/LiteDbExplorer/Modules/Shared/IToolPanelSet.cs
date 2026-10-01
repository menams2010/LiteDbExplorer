using System.Threading;
using System.Threading.Tasks;
using LiteDbExplorer.Wpf.Framework.Shell;

namespace LiteDbExplorer.Modules.Shared
{
    public interface IToolPanelSet
    {
        Task ActivateItemAsync(IToolPanel item, CancellationToken cancellationToken = default(CancellationToken));
        Task DeactivateItemAsync(IToolPanel item, bool close, CancellationToken cancellationToken = default(CancellationToken));
    }
}