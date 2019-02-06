using Autofac;
using MahApps.Metro.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using Ui.Wpf.Common.ShowOptions;
using Ui.Wpf.Common.ViewModels;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using IContainer = Autofac.IContainer;

namespace Ui.Wpf.Common
{
    public partial class Shell : ReactiveObject, IShell
    {
        public IContainer Container { get; set; }

        [Reactive] public string Title { get; set; }

        [Reactive] public IView SelectedView { get; set; }

        public void ShowView<TView>(
            ViewRequest viewRequest = null,
            UiShowOptions options = null)
            where TView : class, IView
        {
            var layoutDocument = FindLayoutByViewRequest(DocumentPane, viewRequest);

            if (layoutDocument == null)
            {
                var view = Container.Resolve<TView>();
                if (options != null)
                    view.Configure(options);

                layoutDocument = new LayoutDocument
                {
                    ContentId = viewRequest?.ViewId,
                    Content = view
                };
                if (options != null)
                    layoutDocument.CanClose = options.CanClose;

                AddTitleRefreshing(view, layoutDocument);
                AddWindowBehaviour(view, layoutDocument);
                AddClosingByRequest(view, layoutDocument);

                DocumentPane.Children.Add(layoutDocument);

                InitializeView(view, viewRequest);
            }

            ActivateContent(layoutDocument, viewRequest);
        }

        public void ShowTool<TToolView>(
            ViewRequest viewRequest = null,
            UiShowOptions options = null)
            where TToolView : class, IToolView
        {
            var layoutAnchorable = FindLayoutByViewRequest(ToolsPane, viewRequest);

            if (layoutAnchorable == null)
            {
                var view = Container.Resolve<TToolView>();
                if (options != null)
                    view.Configure(options);

                layoutAnchorable = new LayoutAnchorable
                {
                    ContentId = viewRequest?.ViewId,
                    Content = view,
                    CanAutoHide = false,
                    CanFloat = false,
                };
                view.ViewModel.CanClose = false;
                view.ViewModel.CanHide = false;

                AddTitleRefreshing(view, layoutAnchorable);
                AddWindowBehaviour(view, layoutAnchorable);

                ToolsPane.Children.Add(layoutAnchorable);

                InitializeView(view, viewRequest);
            }

            ActivateContent(layoutAnchorable, viewRequest);
        }

        public void ShowFlyoutView<TView>(
            ViewRequest viewRequest = null,
            UiShowFlyoutOptions options = null)
            where TView : class, IView
        {
            var view = Container.Resolve<TView>();
            if (options != null)
                view.Configure(options);

            options = options ?? new UiShowFlyoutOptions();

            FlyoutsControl.Items.Add(new Flyout
            {
                Position = options.Position,
                IsModal = options.IsModal,
                CloseButtonVisibility =
                    options.CanClose
                        ? Visibility.Visible
                        : Visibility.Collapsed,
                IsPinned = options.IsPinned,
                CloseButtonIsCancel = options.CloseButtonIsCancel,
                CloseCommand = options.CloseCommand,
                CloseCommandParameter = options.CloseCommandParameter,
                Width = options.Width ?? double.NaN,
                Height = options.Height ?? double.NaN,
                Header = options.Title,
                Content = view,
                IsOpen = true
            });

            InitializeView(view, viewRequest);
        }

        public void ShowStartView<TStartWindow, TStartView>(
            UiShowStartWindowOptions options = null)
            where TStartWindow : class
            where TStartView : class, IView
        {
            if (options != null)
            {
                ToolPaneWidth = options.ToolPaneWidth;
                Title = options.Title;
            }

            var startObject = Container.Resolve<TStartWindow>();

            if (startObject == null)
                throw new InvalidOperationException($"You should configure {typeof(TStartWindow)}");

            var window = startObject as Window;
            if (window == null)
                throw new InvalidCastException($"{startObject.GetType()} is not a window");

            ShowView<TStartView>();

            window.Show();
        }

        public void ShowStartView<TStartWindow>(
            UiShowStartWindowOptions options = null)
            where TStartWindow : class
        {
            if (options != null)
            {
                ToolPaneWidth = options.ToolPaneWidth;
                Title = options.Title;
            }

            var startObject = Container.Resolve<TStartWindow>();

            if (startObject == null)
                throw new InvalidOperationException($"You shuld configurate {typeof(TStartWindow)}");

            var window = startObject as Window;
            if (window == null)
                throw new InvalidCastException($"{startObject.GetType()} is not a window");

            window.Show();
        }

        public void AttachDockingManager(DockingManager dockingManager)
        {
            DockingManager = dockingManager;

            var layoutRoot = new LayoutRoot();
            DockingManager.Layout = layoutRoot;

            DocumentPane = layoutRoot.RootPanel.Children[0] as LayoutDocumentPane;

            ToolsPane = new LayoutAnchorablePane();
            layoutRoot.RootPanel.Children.Insert(0, ToolsPane);
            ToolsPane.DockWidth = new GridLength(ToolPaneWidth.GetValueOrDefault(410));
        }

        public void AttachFlyoutsControl(FlyoutsControl flyoutsControl)
        {
            FlyoutsControl = flyoutsControl;
        }

        private static T FindLayoutByViewRequest<T>(LayoutGroup<T> layoutGroup, ViewRequest viewRequest)
            where T : LayoutContent
        {
            return viewRequest?.ViewId != null
                ? layoutGroup.Children.FirstOrDefault(x => x.ContentId == viewRequest.ViewId)
                : null;
        }

        private static void InitializeView(IView view, ViewRequest viewRequest)
        {
            if (view.ViewModel is ViewModelBase vmb)
            {
                vmb.ViewId = viewRequest?.ViewId;
            }

            if (view.ViewModel is IInitializableViewModel initializibleViewModel)
            {
                initializibleViewModel.Initialize(viewRequest);
            }
        }

        private static void ActivateContent(LayoutContent layoutContent, ViewRequest viewRequest)
        {
            layoutContent.IsActive = true;
            if (layoutContent.Content is IView view &&
                view.ViewModel is IActivatableViewModel activatableViewModel)
            {
                activatableViewModel.Activate(viewRequest);
            }
        }

        private static void AddClosingByRequest<TView>(TView view, LayoutContent layoutDocument)
            where TView : class, IView
        {
            if (!(view.ViewModel is ViewModelBase baseViewModel))
                return;

            Observable
                .FromEventPattern<ViewModelCloseQueryArgs>(
                    x => baseViewModel.CloseQuery += x,
                    x => baseViewModel.CloseQuery -= x)
                .Subscribe(x => { layoutDocument.Close(); })
                .DisposeWith(baseViewModel.Disposables);

            Observable
                .FromEventPattern<CancelEventArgs>(
                    x => layoutDocument.Closing += x,
                    x => layoutDocument.Closing -= x)
                .Subscribe(x =>
                {
                    var vcq = new ViewModelCloseQueryArgs {IsCanceled = false};
                    baseViewModel.Closing(vcq);

                    if (vcq.IsCanceled)
                    {
                        x.EventArgs.Cancel = true;
                    }
                })
                .DisposeWith(baseViewModel.Disposables);

            Observable
                .FromEventPattern(
                    x => layoutDocument.Closed += x,
                    x => layoutDocument.Closed -= x)
                .Subscribe(_ => baseViewModel.Closed(new ViewModelCloseQueryArgs {IsCanceled = false}))
                .DisposeWith(baseViewModel.Disposables);
        }

        private static void AddTitleRefreshing<TView>(TView view, LayoutContent layoutDocument)
            where TView : class, IView
        {
            var titleRefreshSubsription = view.ViewModel
                .WhenAnyValue(vm => vm.Title)
                .Subscribe(x => layoutDocument.Title = x);

            Observable
                .FromEventPattern(
                    x => layoutDocument.Closed += x,
                    x => layoutDocument.Closed -= x)
                .Take(1)
                .Subscribe(_ => titleRefreshSubsription.Dispose());
        }

        private static void AddWindowBehaviour<TView>(TView view, LayoutContent layoutContent)
            where TView : class, IView
        {
            if (!(view.ViewModel is ViewModelBase baseViewModel))
                return;

            baseViewModel
                .WhenAnyValue(x => x.IsEnabled)
                .Subscribe(x => layoutContent.IsEnabled = x)
                .DisposeWith(baseViewModel.Disposables);

            baseViewModel
                .WhenAnyValue(x => x.CanClose)
                .Subscribe(x => layoutContent.CanClose = x)
                .DisposeWith(baseViewModel.Disposables);

            if (layoutContent is LayoutAnchorable layoutAnchorable)
            {
                baseViewModel
                    .WhenAnyValue(x => x.CanHide)
                    .Subscribe(x => layoutAnchorable.CanHide = x)
                    .DisposeWith(baseViewModel.Disposables);
            }
        }


        //TODO replace to abstract manager
        private DockingManager DockingManager { get; set; }
        private FlyoutsControl FlyoutsControl { get; set; }

        protected LayoutDocumentPane DocumentPane { get; set; }

        private LayoutAnchorablePane ToolsPane { get; set; }

        private int? ToolPaneWidth { get; set; }
    }
}