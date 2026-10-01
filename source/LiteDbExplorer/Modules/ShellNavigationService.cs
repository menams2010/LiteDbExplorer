using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using LiteDbExplorer.Framework;

namespace LiteDbExplorer.Wpf.Framework
{
    public interface IShellNavigationService
    {
        Task<Result> Navigate<T>(object modelParams = null, Action<T> vmCallback = null) where T : class, IScreen;
        Task<Result> Navigate(Type viewModelType, object modelParams = null, Action<object> vmCallback = null);
    }

    // This is just to help with some reflection stuff
    public interface INavigationTarget { }

    public interface INavigationTarget<in T> : INavigationTarget        
    {
        // It contains a single method which will pass arguments to the viewmodel after the nav service has instantiated it from the container
        void Init(T modelParams);
    }

    public class NavigationRequestMessage
    {
        public NavigationRequestMessage(IScreen viewModel)
        {
            ViewModel = viewModel;
        }

        public IScreen ViewModel { get; }
    }

    public interface IViewModelHost
    {
        IScreen FindViewModel(Type viewModelType, IReferenceId referenceId);
    }

    [Export(typeof(IShellNavigationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ShellNavigationService : IShellNavigationService
    {
        // Depends on the aggregator - this is how the shell or any interested VMs will receive
        // notifications that the user wants to navigate to someplace else
        private readonly IEventAggregator _aggregator;

        [ImportingConstructor]
        public ShellNavigationService(IEventAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        [ImportMany(AllowRecomposition = true)]
        public IEnumerable<Lazy<IViewModelHost>> ViewModelHosts { get; protected set; }


        public Task<Result> Navigate<T>(object modelParams = null, Action<T> vmCallback = null) where T : class, IScreen
        {
            if (vmCallback != null)
            {
                return Navigate(typeof(T), modelParams, vm => vmCallback.Invoke(vm as T));
            }
            
            return Navigate(typeof(T), modelParams);
        }

        public async Task<Result> Navigate(Type viewModelType, object modelParams = null, Action<object> vmCallback = null)
        {
            // Resolve the viewmodel type from the container
            var viewModel = ResolveViewModel(viewModelType, modelParams);

            vmCallback?.Invoke(viewModel);

            // Check if the viewmodel implements IViewModelParams and call accordingly
            var interfaces = viewModel.GetType()
                .GetInterfaces()
                .Where(x => typeof(INavigationTarget).IsAssignableFrom(x) && x.IsGenericType);

            // Loop through interfaces and find one that matches the generic signature based on modelParams...
            foreach (var @interface in interfaces)
            {
                var method = @interface.GetMethod(nameof(INavigationTarget<object>.Init));
                if (method == null)
                {
                    continue;
                }

                var argument = @interface.GetGenericArguments()[0];
                var isAwaitable = method.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

                if (argument.IsInstanceOfType(modelParams))
                {
                    // If we found one, invoke the method to run ProcessParameters(modelParams)
                    if (isAwaitable)
                    {
                        await ((Task) method.Invoke(viewModel, new[] {modelParams})).ConfigureAwait(false);
                    }
                    else
                    {
                        method.InvokeOnMainThread(viewModel, modelParams);
                        //method.Invoke(viewModel, new [] { modelParams });
                    }
                }
            }

            // Publish an aggregator event to let the shell/other VMs know to change their active view
            await _aggregator.PublishOnUIThreadAsync(new NavigationRequestMessage(viewModel));

            return Result.Success();
        }

        protected IScreen ResolveViewModel(Type viewModelType, object modelParams)
        {
            if (typeof(IReferenceId).IsAssignableFrom(viewModelType) && modelParams is IReferenceId referenceId)
            {
                foreach (var viewModelHost in ViewModelHosts)
                {
                    var referenceViewModel = viewModelHost.Value.FindViewModel(viewModelType, referenceId);
                    if (referenceViewModel != null)
                    {
                        return referenceViewModel;
                    }
                }
            }

            // Resolve the viewmodel type from the container
            var viewModel = IoC.GetInstance(viewModelType, null) as IScreen;

            // Inject any props by passing through IoC buildup
            IoC.BuildUp(viewModel);

            return viewModel;
        }

    }
}