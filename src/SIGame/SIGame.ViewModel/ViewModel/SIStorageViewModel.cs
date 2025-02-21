﻿using Services.SI.ViewModel;
using SIGame.ViewModel.PackageSources;
using SIGame.ViewModel.Properties;
using System;
using System.Threading.Tasks;

namespace SIGame.ViewModel
{
    public sealed class SIStorageViewModel: ViewModel<SIStorageNew>, INavigationNode
    {
        public AsyncCommand LoadStorePackage { get; internal set; }

        internal event Action<PackageSource> AddPackage;

        public bool IsProgress => Model.IsLoading || Model.IsLoadingPackages || IsLoading;

        private bool _isLoading;

        public bool IsLoading
        {
            get { return _isLoading; }
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(nameof(IsProgress)); } }
        }

        public SIStorageViewModel(UserSettings userSettings)
            : base(new SIStorageNew())
        {
            _model.CurrentRestriction = userSettings.Restriction;

            _model.DefaultPublisher = userSettings.Publisher;
            _model.DefaultTag = userSettings.Tag;

            Model.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(SIStorageNew.CurrentRestriction):
                        userSettings.Restriction = Model.CurrentRestriction;
                        break;

                    case nameof(SIStorageNew.CurrentPublisher):
                        userSettings.Publisher = Model.CurrentPublisher.Name;
                        break;

                    case nameof(SIStorageNew.CurrentTag):
                        userSettings.Tag = Model.CurrentTag.Name;
                        break;

                    case nameof(SIStorageNew.CurrentPackage):
                        LoadStorePackage.CanBeExecuted = Model.CurrentPackage != null;
                        break;

                    case nameof(SIStorageNew.IsLoading):
                        OnPropertyChanged(nameof(IsProgress));
                        break;

                    case nameof(SIStorageNew.IsLoadingPackages):
                        OnPropertyChanged(nameof(IsProgress));
                        break;
                }
            };

            Model.Error += exc =>
            {
                PlatformSpecific.PlatformManager.Instance.ShowMessage(Resources.SIStorageError + ": " + exc.Message, PlatformSpecific.MessageType.Warning);
            };

            LoadStorePackage = new AsyncCommand(LoadStorePackage_Executed) { CanBeExecuted = false };
        }

        private async Task LoadStorePackage_Executed(object arg)
        {
            try
            {
                IsLoading = true;

                var packageInfo = Model.CurrentPackage;
                var uri = await Model.LoadSelectedPackageUriAsync();

                var packageSource = new SIStoragePackageSource(uri, packageInfo.ID, packageInfo.Description, packageInfo.Guid);

                AddPackage?.Invoke(packageSource);
                
                IsLoading = false;
            }
            catch (Exception exc)
            {
                IsLoading = false;
                PlatformSpecific.PlatformManager.Instance.ShowMessage(Resources.SIStorageCallError + ": " + exc.Message, PlatformSpecific.MessageType.Warning);
            }
            finally
            {
                OnClose();
            }
        }

        public event Action Close;

        private void OnClose()
        {
            Close?.Invoke();
        }

        private bool _isInitialized = false;

        internal void Init()
        {
            if (_isInitialized)
            {
                return;
            }

            Model.Open();
            _isInitialized = true;
        }
    }
}
