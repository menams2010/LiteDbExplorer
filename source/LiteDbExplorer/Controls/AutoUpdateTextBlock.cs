using System;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using JetBrains.Annotations;

namespace LiteDbExplorer.Controls
{
    public class AutoUpdateTextBlock : TextBlock
    {
        private static readonly IObservable<long> Timer = Observable
            .Interval(TimeSpan.FromSeconds(10)).Publish().RefCount().ObserveOn(new DispatcherSynchronizationContext());

        private IDisposable _disposeToken;

        public AutoUpdateTextBlock()
        {
            _disposeToken = Timer.Subscribe(_ =>
            {
                GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
            });
        }
    }
}