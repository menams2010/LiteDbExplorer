using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using LiteDbExplorer.Framework;

namespace LiteDbExplorer.Wpf.Framework.Shell
{
    public abstract class Document : LayoutItemBase, IDocument
    {
        public virtual string InstanceId { get; protected set; }

        public string GroupId { get; set; }

        public string GroupDisplayName { get; set; }

        public bool GroupDisplayNameIsVisible { get; set; }

        public GroupDisplayVisibility GroupDisplayVisibility { get; set; } = GroupDisplayVisibility.Auto;

        public SolidColorBrush GroupDisplayBackground { get; set; }

        public string DialogHostIdentifier => $"Dialog_{Id}";

        private ICommand _closeCommand;
        public override ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(async p => await TryCloseAsync(null), p => true)); }
        }

        public void UpdateGroupDisplay()
        {
            UpdateGroupDisplayRequest?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<EventArgs> UpdateGroupDisplayRequest;
    }

    public abstract class Document<T> : Document, IDocument<T> where T : IReferenceId
    {
        public abstract void Init(T item);
    }

    public abstract class DocumentConductor<T, TItem> : Conductor<TItem>, IDocument<T> where T : IReferenceId where TItem : class, IScreen
    {
        [Browsable(false)]
        public Guid Id { get; } = Guid.NewGuid();

        [Browsable(false)]
        public string ContentId => Id.ToString();

        [Browsable(false)]
        public bool IsSelected { get; set; }

        [Browsable(false)]
        public virtual bool ShouldReopenOnStart => false;

        public virtual object IconContent { get; set; }

        public string GroupId { get; set; }

        public string GroupDisplayName { get; set; }

        public bool GroupDisplayNameIsVisible { get; set; }

        public GroupDisplayVisibility GroupDisplayVisibility { get; set; } = GroupDisplayVisibility.Auto;

        public SolidColorBrush GroupDisplayBackground { get; set; }

        public virtual string InstanceId { get; protected set; }

        public string DialogHostIdentifier => $"Dialog_{Id}";

        private ICommand _closeCommand;
        public virtual ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(async p => await TryCloseAsync(null), p => true)); }
        }

        public abstract void Init(T item);

        public void UpdateGroupDisplay()
        {
            UpdateGroupDisplayRequest?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<EventArgs> UpdateGroupDisplayRequest;
    }

    public abstract class DocumentConductorOneActive<T> : Conductor<IScreen>.Collection.OneActive, IDocument<T> where T : IReferenceId
    {
        [Browsable(false)]
        public Guid Id { get; } = Guid.NewGuid();

        [Browsable(false)]
        public string ContentId => Id.ToString();

        [Browsable(false)]
        public bool IsSelected { get; set; }

        [Browsable(false)]
        public virtual bool ShouldReopenOnStart => false;

        public virtual object IconContent { get; set; }

        public string GroupId { get; set; }

        public string GroupDisplayName { get; set; }

        public bool GroupDisplayNameIsVisible { get; set; }

        public GroupDisplayVisibility GroupDisplayVisibility { get; set; } = GroupDisplayVisibility.Auto;

        public SolidColorBrush GroupDisplayBackground { get; set; }

        public virtual string InstanceId { get; protected set; }

        public string DialogHostIdentifier => $"Dialog_{Id}";

        private ICommand _closeCommand;
        public virtual ICommand CloseCommand
        {
            get { return _closeCommand ?? (_closeCommand = new RelayCommand(async p => await TryCloseAsync(null), p => true)); }
        }

        public abstract void Init(T item);

        public void UpdateGroupDisplay()
        {
            UpdateGroupDisplayRequest?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<EventArgs> UpdateGroupDisplayRequest;
    }
}