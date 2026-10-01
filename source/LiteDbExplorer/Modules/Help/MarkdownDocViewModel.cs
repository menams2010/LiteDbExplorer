using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using CSharpFunctionalExtensions;
using JetBrains.Annotations;
using Humanizer;
using LiteDbExplorer.Modules.Shared;
using LiteDbExplorer.Wpf.Framework;
using LiteDbExplorer.Wpf.Framework.Shell;
using MaterialDesignThemes.Wpf;

namespace LiteDbExplorer.Modules.Help
{
    [Export(typeof(MarkdownDocViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class MarkdownDocViewModel : Document<IMarkdownDocContext>, INavigationTarget<IMarkdownDocContext>
    {
        private IMarkdownDocContext _markdownDocContext;
        
        public MarkdownDocViewModel()
        {
        }

        public override object IconContent => new PackIcon { Kind = PackIconKind.HelpCircleOutline };

        public bool IsBusy { get; set; }

        public string MarkdownContent { get; set; }

        [UsedImplicitly]
        public string NavigateUrl => _markdownDocContext?.NavigateUrl;

        public override void Init(IMarkdownDocContext item)
        {
            _markdownDocContext = item;

            InstanceId = item.InstanceId;

            DisplayName = item.Title;
        }

        protected override async void OnViewLoaded(object view)
        {
            IsBusy = true;
            var content = await _markdownDocContext.GetContent();
            if (content.IsSuccess)
            {
                MarkdownContent = content.Value;
            }
            else
            {
                MessageBox.Show(content.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            IsBusy = false;
        }

        public void OpenHyperlink(string uri)
        {
            if (_markdownDocContext.TryGetLocalContext(uri, out var context))
            {
                var documentSet = IoC.Get<IDocumentSet>();
                documentSet.OpenDocument<MarkdownDocViewModel, IMarkdownDocContext>(context);
                return;
            }

            Process.Start(_markdownDocContext.ResolveLink(uri));
        }
    }

    public interface IMarkdownDocContext : IReferenceId
    {
        string Title { get; }
        string RootUrl { get; }
        string NavigateUrl { get; }
        string RawUrl { get; }

        Task<Result<string>> GetContent();

        string ResolveLink(string uri);
        bool TryGetLocalContext(string uri, out IMarkdownDocContext context);
    }

    public class GithubWikiMarkdownDocContext : IMarkdownDocContext
    {
        private readonly string _user;
        private readonly string _repo;

        public GithubWikiMarkdownDocContext(string title, string user, string repo, string page)
        {
            _user = user;
            _repo = repo;
            Title = title;
            InstanceId = $"{user}:{repo}:{page}";
            RootUrl = $"https://github.com/{user}/{repo}/wiki";
            RawUrl = $"https://raw.githubusercontent.com/wiki/{user}/{repo}/{page}.md";
            NavigateUrl = $"https://github.com/{user}/{repo}/wiki/{page}";
        }

        public string InstanceId { get; }
        public string Title { get; }
        public string RootUrl { get; }
        public string NavigateUrl { get; }
        public string RawUrl { get; }

        public async Task<Result<string>> GetContent()
        {
            if (string.IsNullOrEmpty(RawUrl))
            {
                return Result.Success(string.Empty);
            }

            try
            {
                string rawContent;
                using (var client = new HttpClient())
                {
                    rawContent = await client.GetStringAsync(RawUrl);
                }

                return Result.Success(rawContent);
            }
            catch (Exception e)
            {
                return Result.Failure<string>(e.Message);
            }
        }

        public string ResolveLink(string uri)
        {
            if (!string.IsNullOrEmpty(uri) && !uri.StartsWith("http"))
            {
                uri = $"{RootUrl.TrimEnd('/')}/{uri}";
            }

            return uri;
        }

        public bool TryGetLocalContext(string uri, out IMarkdownDocContext context)
        {
            context = null;
            if (string.IsNullOrEmpty(uri) || uri.StartsWith("http"))
            {
                return false;
            }

            context = new GithubWikiMarkdownDocContext(uri.Humanize(), _user, _repo, uri);

            return true;
        }
    }
}