﻿using SICore;
using SICore.BusinessLogic;
using SICore.Network;
using SICore.Network.Configuration;
using SICore.Network.Servers;
using SICore.Special;
using SIData;
using SIGame.ViewModel.Data;
using SIGame.ViewModel.PackageSources;
using SIGame.ViewModel.PlatformSpecific;
using SIGame.ViewModel.Properties;
using SIGame.ViewModel.Web;
using SIPackages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SIGame.ViewModel
{
    public sealed class GameSettingsViewModel: ViewModelWithNewAccount<GameSettings>, INavigatable
    {
        private static readonly Random Random = new Random();

        private string unique = null;

        internal CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Номер за столом в роли игрока
        /// </summary>
        public int PlayerNumber => 0;

        /// <summary>
        /// Роль организатора в игре
        /// </summary>
        public GameRole Role
        {
            get { return _model.Role; }
            set
            {
                if (_model.Role != value)
                {
                    _model.Role = value;
                    OnPropertyChanged();
                    UpdateRoleTrigger();
                    NetworkGameOrRoleChanged(true, false);
                }
            }
        }

        /// <summary>
        /// Будет ли игра сетевой
        /// </summary>
        public bool NetworkGame
        {
            get { return _model.NetworkGame; }
            set
            {
                _model.NetworkGame = value;
                OnPropertyChanged();
                UpdateRoleTrigger();
                SetErrorMessage();
                NetworkGameOrRoleChanged(false, true);
            }
        }

        /// <summary>
        /// Тип сетевой игры
        /// </summary>
        public NetworkGameType NetworkGameType
        {
            get { return _model.NetworkGameType; }
            set
            {
                _model.NetworkGameType = value;
                OnPropertyChanged();
                SetErrorMessage();
            }
        }

        /// <summary>
        /// Название сетевой игры
        /// </summary>
        public string NetworkGameName
        {
            get { return _model.NetworkGameName; }
            set
            {
                _model.NetworkGameName = value;
                OnPropertyChanged();
                SetErrorMessage();
            }
        }

        /// <summary>
        /// Пароль сетевой игры
        /// </summary>
        public string NetworkGamePassword
        {
            get { return _model.NetworkGamePassword; }
            set
            {
                _model.NetworkGamePassword = value;
                OnPropertyChanged();
            }
        }

        public int PlayersCount
        {
            get { return _model.PlayersCount; }
            set 
            {
                var oldValue = _model.PlayersCount;
                if (oldValue != value)
                {
                    _model.PlayersCount = value;
                    OnPropertyChanged();
                    if (oldValue < value)
                    {
                        do
                        {
                            Players.Add(new GameAccount(this) { AccountType = AccountTypes.Computer });
                            oldValue++;
                        } while (oldValue < value);
                    }
                    else
                    {
                        do
                        {
                            Players.RemoveAt(oldValue - 1);
                            oldValue--;
                        } while (oldValue > value);
                    }
                }
            }
        }

        /// <summary>
        /// Разрешить допуск зрителей в сетевую игру
        /// </summary>
        public bool AllowViewers
        {
            get { return _model.AllowViewers; }
            set { _model.AllowViewers = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Порт сетевой игры
        /// </summary>
        public int NetworkPort
        {
            get { return _model.NetworkPort; }
            set { _model.NetworkPort = value; OnPropertyChanged(); SetErrorMessage(); }
        }

        private GameAccount _showman;

        public GameAccount Showman
        {
            get
            {
                return _showman;
            }
            set
            {
                if (_showman != value)
                {
                    if (_showman != null)
                        _showman.PropertyChanged -= Showman_PropertyChanged;

                    _showman = value;

                    if (_showman != null)
                        _showman.PropertyChanged += Showman_PropertyChanged;

                    OnPropertyChanged();
                    UpdateShowman();
                }
            }
        }

        private readonly ComputerAccount[] _defaultComputerPlayers = StoredPersonsRegistry.GetDefaultPlayers(
            new Localizer(Thread.CurrentThread.CurrentUICulture.Name),
            Global.PhotoUri);

        private readonly ComputerAccount[] _defaultComputerShowmans = StoredPersonsRegistry.GetDefaultShowmans(
            new Localizer(Thread.CurrentThread.CurrentUICulture.Name),
            Global.PhotoUri);

        private readonly ComputerAccount _newPlayerAccount = new ComputerAccount(Resources.New + "…", true);
        private readonly ComputerAccount _newShowmanAccount = new ComputerAccount(Resources.New + "…", true);

        private ComputerAccount[] _computerPlayers;
        private ComputerAccount[] _computerShowmans;

        public void UpdateComputerPlayers()
        {
            var result = new List<ComputerAccount>(_defaultComputerPlayers);
            result.AddRange(_commonSettings.CompPlayers2);
            result.Add(_newPlayerAccount);
            _computerPlayers = result.ToArray();

            foreach (var player in Players)
            {
                if (player.AccountType == AccountTypes.Computer)
                {
                    player.SelectionList = _computerPlayers;
                }
            }
        }

        public void UpdateComputerShowmans()
        {
            var result = new List<ComputerAccount>(_defaultComputerShowmans);
            result.AddRange(_commonSettings.CompShowmans2);
            result.Add(_newShowmanAccount);
            _computerShowmans = result.ToArray();

            if (_showman != null && _showman.AccountType == AccountTypes.Computer)
            {
                _showman.SelectionList = _computerShowmans;
            }
        }

        /// <summary>
        /// Игроки
        /// </summary>
        public ObservableCollection<GameAccount> Players { get; } = new ObservableCollection<GameAccount>();
        /// <summary>
        /// Зрители
        /// </summary>
        public ObservableCollection<SimpleAccount<HumanAccount>> Viewers { get; } = new ObservableCollection<SimpleAccount<HumanAccount>>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _errorMessage = "";

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private PackageSource _package;

        /// <summary>
        /// Игровой пакет
        /// </summary>
        public PackageSource Package
        {
            get { return _package; }
            set
            {
                _package = value;
                _model.PackageKey = value.Key;
                _model.RandomSpecials = value.RandomSpecials;
                OnPropertyChanged();
                SetErrorMessage();
            }
        }

        public SIStorageViewModel StorageInfo { get; private set; }

        public IAsyncCommand BeginGame { get; private set; }

        public ICommand RemoveComputerAccount { get; private set; }
        public ICommand EditComputerAccount { get; private set; }

        public ICommand RemoveShowmanAccount { get; private set; }

        private void NewComputerAccount_Add(ComputerAccount origin, ComputerAccount account)
        {
            _commonSettings.CompPlayers2.Add(account);
            UpdateComputerPlayers();

            var gamePlayers = Players;
            foreach (var player in gamePlayers)
            {
                if (player.SelectedAccount == _computerPlayers.Last())
                {
                    player.SelectedAccount = account;
                }
            }
        }

        private void NewComputerAccount_Edit(ComputerAccount origin, ComputerAccount account)
        {
            var players = _commonSettings.CompPlayers2;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == origin)
                {
                    var oldAccount = players[i];
                    GameAccount playerAcc = null;

                    var gamePlayers = Players;
                    foreach (var player in gamePlayers)
                    {
                        if (player.SelectedAccount == oldAccount)
                        {
                            playerAcc = player;
                            break;
                        }
                    }

                    players[i] = account;
                    UpdateComputerPlayers();
                    if (playerAcc != null)
                        playerAcc.SelectedAccount = account;
                }
            }
        }

        private void NewShowmanAccount_Add(ComputerAccount account)
        {
            _commonSettings.CompShowmans2.Add(account);
            UpdateComputerShowmans();

            Showman.SelectedAccount = account;

            Content = null;
        }

        private readonly ExtendedCommand _selectPackage;

        public ICommand SelectPackage => _selectPackage;

        private ICommand _closeNewShowman;
        private ICommand _closeNewPlayer;

        private readonly CommonSettings _commonSettings;

        internal event Func<GameSettings, PackageSource, Task<Tuple<SlaveServer, IViewerClient>>> CreateGame;
        public event Action<ContentBox> Navigate;

        private bool _isProgress;

        public bool IsProgress
        {
            get { return _isProgress; }
            set { _isProgress = value; OnPropertyChanged(); }
        }

        private string _message;

        public string Message
        {
            get { return _message; }
            set { _message = value; OnPropertyChanged(); }
        }

        public GameSettingsViewModel(
            GameSettings gameSettings,
            CommonSettings commonSettings,
            UserSettings userSettings,
            bool isNetworkGame = false)
            : base(gameSettings)
        {
            NetworkGame = isNetworkGame;
            _commonSettings = commonSettings;

            UpdateComputerPlayers();
            UpdateComputerShowmans();

            StorageInfo = new SIStorageViewModel(userSettings);
            StorageInfo.AddPackage += StorageInfo_AddPackage;

            gameSettings.Updated += GameSettings_Updated;

            _selectPackage = new ExtendedCommand(SelectPackage_Executed);

            _selectPackage.ExecutionArea.Add(PackageSourceTypes.Local);
            _selectPackage.ExecutionArea.Add(PackageSourceTypes.SIStorage);
            _selectPackage.ExecutionArea.Add(PackageSourceTypes.VK);

            var packageDirExists = Directory.Exists(Global.PackagesUri);

            if (packageDirExists)
                _selectPackage.ExecutionArea.Add(PackageSourceTypes.Random);

            if (NetworkGame && NetworkGameType == NetworkGameType.GameServer)
                _selectPackage.ExecutionArea.Add(PackageSourceTypes.RandomServer);

            if (packageDirExists)
                _selectPackage.ExecutionArea.Add(PackageSourceTypes.Next);

            if (_package == null)
            {
                if (_model.PackageKey != null && _selectPackage.ExecutionArea.Contains(_model.PackageKey.Type))
                {
                    switch (_model.PackageKey.Type)
                    {
                        case PackageSourceTypes.Next:
                            Package = new NextPackageSource();
                            break;

                        case PackageSourceTypes.Random:
                            Package = new RandomPackageSource();
                            break;

                        case PackageSourceTypes.RandomServer:
                            Package = new RandomServerPackageSource();
                            break;

                        case PackageSourceTypes.Local:
                            Package = new CustomPackageSource(_model.PackageKey.Data);
                            break;

                        case PackageSourceTypes.SIStorage:
                            var key = _model.PackageKey;
                            Uri uri;
                            if (Uri.TryCreate(key.Data, UriKind.Absolute, out uri))
                                Package = new SIStoragePackageSource(uri, key.ID, key.Name, key.PackageID);
                            break;
                    }
                }
                else if (packageDirExists)
                {
                    Package = new RandomPackageSource();
                }
            }
        }

        private void GameSettings_Updated() => OnPropertyChanged(nameof(NetworkPort));

        protected override void Initialize()
        {
            base.Initialize();

            Players.CollectionChanged += Players_CollectionChanged;
            Viewers.CollectionChanged += Viewers_CollectionChanged;

            BeginGame = new AsyncCommand(BeginGame_ExecutedAsync);

            RemoveComputerAccount = new CustomCommand(RemoveComputerAccount_Executed);
            RemoveShowmanAccount = new CustomCommand(RemoveShowmanAccount_Executed);
            EditComputerAccount = new CustomCommand(EditComputerAccount_Executed);

            _closeNewShowman = new CustomCommand(CloseNewShowman_Executed);
            _closeNewPlayer = new CustomCommand(CloseNewPlayer_Executed);

            SetErrorMessage();
        }

        private void CloseNewShowman_Executed(object arg)
        {
            _showman.SelectedAccount = _computerShowmans[0];
            _closeContent.Execute(arg);
        }

        private void CloseNewPlayer_Executed(object arg)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].SelectedAccount == _computerPlayers.Last())
                {
                    UpdatePlayer(Players[i], true);
                }
            }

            _closeContent.Execute(arg);
        }

        private void SelectPackage_Executed(object arg)
        {
            var code = (PackageSourceTypes)arg;
            switch (code)
            {
                case PackageSourceTypes.Next:
                    Package = new NextPackageSource();
                    break;

                case PackageSourceTypes.Random:
                    Package = new RandomPackageSource();
                    break;

                case PackageSourceTypes.RandomServer:
                    Package = new RandomServerPackageSource();
                    break;

                case PackageSourceTypes.Local:
                    var packagePath = PlatformManager.Instance.SelectLocalPackage();
                    if (packagePath != null)
                    {
                        Package = new CustomPackageSource(packagePath);
                    }
                    break;

                case PackageSourceTypes.SIStorage:
                    var contentBox = new ContentBox { Data = StorageInfo, Title = Resources.SIStorage };
                    StorageInfo.Init();
                    Navigate?.Invoke(contentBox);
                    break;

                case PackageSourceTypes.VK:
                    try
                    {
                        Process.Start(Resources.ThemesLink);
                    }
                    catch (Exception exc)
                    {
                        PlatformManager.Instance.ShowMessage(string.Format(Resources.VKThemesError + "\r\n{1}", Resources.ThemesLink, exc.Message), MessageType.Error);
                    }
                    break;
            }
        }

        public void OnNavigatedFrom(object data)
        {
            if (data is ShowmanViewModel)
            {
                _showman.SelectedAccount = _computerShowmans[0];
            }
            else if (data is ComputerAccountViewModel)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].SelectedAccount == _computerPlayers.Last())
                    {
                        UpdatePlayer(Players[i], true);
                    }
                }
            }
        }

        private void RemoveShowmanAccount_Executed(object account)
        {
            var computerAccount = account as ComputerAccount;
            if (!PlatformManager.Instance.Ask(string.Format(Resources.ShowmanDeleteConfirm, computerAccount.Name)))
                return;

            _commonSettings.CompShowmans2.Remove(computerAccount);
            UpdateComputerShowmans();

            Showman.SelectedAccount = _computerShowmans.FirstOrDefault();
        }

        private void RemoveComputerAccount_Executed(object account)
        {
            var computerAccount = account as ComputerAccount;
            if (!PlatformManager.Instance.Ask(string.Format(Resources.PlayerDeleteConfirm, computerAccount.Name)))
                return;

            _commonSettings.CompPlayers2.Remove(computerAccount);
            UpdateComputerPlayers();

            foreach (var player in Players)
            {
                if (player.SelectedAccount == null)
                {
                    UpdatePlayer(player, true);
                    break;
                }
            }
        }

        private void EditComputerAccount_Executed(object arg)
        {
            var computerAccount = (ComputerAccount)arg;
            var newComputerAccount = new ComputerAccountViewModel(computerAccount.Clone(), computerAccount);
            newComputerAccount.Add += NewComputerAccount_Edit;
            var contentBox = new ContentBox { Data = newComputerAccount, Title = Resources.ComputerPlayer };
            Navigate?.Invoke(contentBox);
        }

        private async Task BeginGame_ExecutedAsync(object arg)
        {
            IsProgress = true;
            BeginGame.CanBeExecuted = false;

            ErrorMessage = null;
            FullError = null;

            try
            {
                _model.RandomSpecials = _package.RandomSpecials;

                if (CreateGame != null)
                {
                    var info = await CreateGame(_model, _package);
                    if (info == null)
                    {
                        return;
                    }

                    MoveToGame(info.Item1, info.Item2, null);
                }
                else
                {
                    var (document, path) = await BeginNewGameAsync();
                    BeginNewGameCompleted(document, path);
                }
            }
            catch (PortIsUsedException)
            {
                ErrorMessage = string.Format(Resources.PortIsUsedError, _model.AppSettings.MultimediaPort);
            }
            catch (TargetInvocationException exc) when (exc.InnerException != null)
            {
                ErrorMessage = exc.InnerException.Message;
                FullError = exc.ToString();
            }
            catch (Exception exc)
            {
                ErrorMessage = exc.Message;
                FullError = exc.ToString();
            }
            finally
            {
                IsProgress = false;
                BeginGame.CanBeExecuted = true;
            }
        }

        private async Task<(SIDocument, string)> BeginNewGameAsync()
        {
            try
            {
                var (packageFile, isTemp) = await Package.GetPackageFileAsync();

                var tempDir = Path.Combine(Path.GetTempPath(), CommonSettings.AppNameEn, Guid.NewGuid().ToString());
                ZipHelper.ExtractToDirectory(packageFile, tempDir);

                if (isTemp)
                {
                    File.Delete(packageFile);
                }

                return (SIDocument.Load(tempDir), tempDir);
            }
            finally
            {
                IsProgress = false;
                BeginGame.CanBeExecuted = true;
            }
        }

        private void BeginNewGameCompleted(SIDocument document, string documentPath)
        {
            var localizer = new NetworkLocalizer(Thread.CurrentThread.CurrentUICulture.Name);

            Server server;
            if (NetworkGame)
            {
                server = new TcpMasterServer(NetworkPort, ServerConfiguration.Default, localizer);
                ((TcpMasterServer)server).StartListen();
            }
            else
            {
                server = new BasicServer(ServerConfiguration.Default, localizer);
            }

            server.Error += Server_Error;

            _model.NetworkGamePassword = "";
            _model.AppSettings.Culture = Thread.CurrentThread.CurrentUICulture.Name;
            _model.HumanPlayerName = Human.Name;

            var (host, _) = new GameRunner(
                server,
                _model,
                document,
                BackLink.Default,
                new WebManager(_model.AppSettings.MultimediaPort),
                _computerPlayers.ToArray(),
                _computerShowmans.ToArray())
                .Run();

            if (!NetworkGame)
            {
                host.MyData.IsChatOpened = false;
                host.MyData.AutoReady = true;
            }

            MoveToGame(server, host, documentPath);
        }

        private void MoveToGame(Server server, IViewerClient host, string tempDocFolder)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _model.ShowmanType = Showman.AccountType;
            _model.PlayersTypes = Players.Select(p => p.AccountType).ToArray();

            OnStartGame(server, host, NetworkGame, false, tempDocFolder, NetworkPort);
        }

        private void Server_Error(Exception exc, bool isWarning)
        {
            PlatformManager.Instance.ShowMessage($"{Resources.GameEngineError}: {exc.Message} {exc.InnerException}", isWarning ? MessageType.Warning : MessageType.Error, true);
        }

        private void StorageInfo_AddPackage(PackageSource package)
        {
            Package = package;
        }

        private void NetworkGameOrRoleChanged(bool roleChanged, bool networkGameChanged)
        {
            if (_showman != null)
            {
                if (Role == GameRole.Showman)
                {
                    _showman.AccountType = AccountTypes.Human;
                    if (roleChanged)
                    {
                        UpdateShowman();
                    }
                }
                else if (!NetworkGame)
                {
                    _showman.AccountType = AccountTypes.Computer;
                }
                else
                {
                    UpdateShowman();
                }
            }
        }

        private void Showman_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameAccount.AccountType))
            {
                UpdateShowman();
                return;
            }
            
            if (e.PropertyName == nameof(GameAccount.SelectedAccount))
            {
                if (_showman.SelectedAccount == _computerShowmans.Last()) // Новый ведущий
                {
                    var account = new ComputerAccount { CanBeDeleted = true };
                    var newShowmanAccount = new ShowmanViewModel(account);
                    newShowmanAccount.Add += NewShowmanAccount_Add;
                    var contentBox = new ContentBox { Data = newShowmanAccount, Title = Resources.NewShowman, Cancel = _closeNewShowman };
                    Navigate?.Invoke(contentBox);

                    return;
                }

                _model.Showman = _showman.SelectedAccount;
                CheckUniqueAccounts();
            }
        }

        void Players_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (GameAccount item in e.NewItems)
                    {
                        UpdatePlayer(item);
                        item.PropertyChanged += Item_PropertyChanged;                        
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (GameAccount item in e.OldItems)
                    {
                        item.PropertyChanged -= Item_PropertyChanged;
                    }
                    break;
            }

            _model.Players = Players.Select(acc => acc.SelectedAccount).ToArray();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var acc = (GameAccount)sender;
            if (e.PropertyName == nameof(GameAccount.AccountType))
            {
                UpdatePlayer(acc);
            }
            else if (e.PropertyName == nameof(GameAccount.SelectedAccount))
            {                
                var index = Players.IndexOf(acc);

                if (index > -1 && index < _model.Players.Length)
                    _model.Players[index] = acc.SelectedAccount;

                if (acc.SelectedAccount == _computerPlayers.Last())
                {
                    var account = new ComputerAccount() { CanBeDeleted = true };

                    // Зададим ему рандомные характеристики
                    account.Randomize();

                    var newComputerAccount = new ComputerAccountViewModel(account, null);
                    newComputerAccount.Add += NewComputerAccount_Add;
                    var contentBox = new ContentBox { Data = newComputerAccount, Title = Resources.NewPlayer, Cancel = _closeNewPlayer };
                    Navigate?.Invoke(contentBox);
                }

                CheckUniqueAccounts();
            }
        }

        private void Viewers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (INotifyPropertyChanged item in e.NewItems)
                    {
                        item.PropertyChanged += Viewer_PropertyChanged;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (INotifyPropertyChanged item in e.OldItems)
                    {
                        item.PropertyChanged -= Viewer_PropertyChanged;
                    }
                    break;
            }

            _model.Viewers = Viewers.Select(acc => acc.SelectedAccount).ToArray();
        }

        void Viewer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SimpleAccount<HumanAccount>.SelectedAccount))
            {
                var acc = (SimpleAccount<HumanAccount>)sender;
                var index = Viewers.IndexOf(acc);
                _model.Viewers[index] = acc.SelectedAccount;
                CheckUniqueAccounts();
            }
        }

        private void CheckUniqueAccounts()
        {
            var accounts = new List<Account>();
            accounts.AddRange(Players.Select(acc => acc.SelectedAccount));
            accounts.AddRange(Viewers.Select(acc => acc.SelectedAccount));
            if (Showman != null)
                accounts.Add(Showman.SelectedAccount);

            for (int i = 0; i < accounts.Count; i++)
            {
                if (accounts[i] == null || accounts[i].Name == Constants.FreePlace)
                    continue;

                for (int j = i + 1; j < accounts.Count; j++)
                {
                    if (accounts[i] == accounts[j])
                    {
                        unique = accounts[i].Name;
                        SetErrorMessage();
                        return;
                    }
                }
            }

            unique = null;
            SetErrorMessage();
        }

        protected override void OnHumanChanged()
        {
            base.OnHumanChanged();

            UpdateRoleTrigger();
        }

        private void UpdateRoleTrigger()
        {
            if (Human == null)
                return;

            int player = -1;
            if (Role == GameRole.Player)
            {
                player = PlayerNumber;
                if (PlayerNumber > -1 && PlayerNumber < Players.Count)
                {
                    Players[PlayerNumber].AccountType = AccountTypes.Human;
                }
            }

            for (int i = 0; i < Players.Count; i++)
            {
                if (i != player && !_model.NetworkGame)
                    Players[i].AccountType = AccountTypes.Computer;
                else
                    UpdatePlayer(Players[i]);
            }

            if (Role == GameRole.Viewer)
            {
                if (Viewers.Count == 0)
                    Viewers.Add(new SimpleAccount<HumanAccount>() { SelectedAccount = Human });
                else
                    Viewers[0].SelectedAccount = Human;
            }
            else if (Viewers.Count > 0)
            {
                Viewers.Clear();
                CheckUniqueAccounts();
            }
        }

        private void UpdateShowman()
        {
            if (_showman == null)
                return;

            if (_showman.AccountType == AccountTypes.Human)
            {
                if (!_model.NetworkGame || _model.Role == GameRole.Showman)
                {
                    _showman.SelectionList = new List<Account>(new Account[] { Human });
                    _showman.SelectedAccount = Human;
                }
                else
                {
                    var oneAccount = new HumanAccount { Name = Constants.FreePlace, IsHuman = true };
                    _showman.SelectionList = new Account[] { oneAccount };
                    _showman.SelectedAccount = oneAccount;
                }
            }
            else if (_showman.SelectionList != _computerShowmans)
            {
                _showman.SelectionList = _computerShowmans;
                _showman.SelectedAccount = _computerShowmans[0];
            }
        }

        private void UpdatePlayer(GameAccount player, bool force = false)
        {
            if (player == null)
            {
                return;
            }

            if (player.AccountType == AccountTypes.Human)
            {
                if (!_model.NetworkGame || _model.Role == GameRole.Player && _model.PlayerNumber > -1
                    && _model.PlayerNumber < Players.Count && Players[_model.PlayerNumber] == player)
                {
                    player.SelectionList = new List<Account>(new Account[] { Human });
                    player.SelectedAccount = Human;
                }
                else
                {
                    var oneAccount = new HumanAccount { Name = Constants.FreePlace, IsHuman = true };
                    player.SelectionList = new Account[] { oneAccount };
                    player.SelectedAccount = oneAccount;
                }
            }
            else if (force || player.SelectionList != _computerPlayers)
            {
                var visited = new List<int>();

                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i] != player && Players[i].AccountType == AccountTypes.Computer)
                    {
                        visited.Add(Array.IndexOf(_computerPlayers, (ComputerAccount)Players[i].SelectedAccount));
                    }
                }

                var index = Random.Next(_computerPlayers.Length - visited.Count - 1);
                while (visited.Contains(index))
                {
                    index++;
                }

                if (!force)
                {
                    player.SelectionList = _computerPlayers;
                }

                player.SelectedAccount = _computerPlayers[index];
            }
        }

        private void SetErrorMessage()
        {
            if (_package == null)
            {
                ErrorMessage = " ";
                BeginGame.CanBeExecuted = false;
            }
            else if (unique != null)
            {
                ErrorMessage = string.Format(Resources.IsDouble, unique);
                BeginGame.CanBeExecuted = false;
            }
            else if (NetworkGame && NetworkGameType == NetworkGameType.GameServer && string.IsNullOrEmpty(NetworkGameName))
            {
                ErrorMessage = Resources.GameNameRequired;
                BeginGame.CanBeExecuted = false;
            }
            else if (NetworkGame && NetworkGameType == NetworkGameType.DirectConnection && NetworkPort == 0)
            {
                ErrorMessage = Resources.PortNumberRequired;
                BeginGame.CanBeExecuted = false;
            }
            else
            {
                ErrorMessage = "";
                FullError = null;
                BeginGame.CanBeExecuted = true;
            }
        }

        protected override void LoadNewSettings(UserSettings settings)
        {
            Set(settings.GameSettings);
            base.LoadNewSettings(settings);
        }

        internal void Set(GameSettings gameSettings)
        {
            _model.AppSettings.Set(gameSettings.AppSettings);
            NetworkPort = gameSettings.NetworkPort;
            AllowViewers = gameSettings.AllowViewers;
        }

        internal void PrepareForGame()
        {
            Viewers.Clear();
            if (Role == GameRole.Viewer)
                Viewers.Add(new SimpleAccount<HumanAccount> { SelectedAccount = Human });

            Players.Clear();

            var playersCount = PlayersCount;

            var anyAccount = new HumanAccount { Name = Constants.FreePlace, CanBeDeleted = false };

            var playerTypes = Model.PlayersTypes;
            for (int i = 0; i < playersCount; i++) // Рандомные игроки
            {
                if (Role == GameRole.Player && i == PlayerNumber)
                {
                    Players.Add(new GameAccount(this) { AccountType = AccountTypes.Human, SelectedAccount = Human });
                }
                else if (!NetworkGame || playerTypes == null
                    || i >= playerTypes.Length || playerTypes[i] == AccountTypes.Computer)
                {
                    Players.Add(new GameAccount(this) { AccountType = AccountTypes.Computer });
                }
                else
                {
                    Players.Add(new GameAccount(this) { AccountType = AccountTypes.Human, SelectedAccount = anyAccount });
                }
            }

            if (Showman == null)
            {
                if (Role == GameRole.Showman)
                    Showman = new GameAccount(this) { AccountType = AccountTypes.Human, SelectedAccount = Human };
                else if (!NetworkGame || _model.ShowmanType == AccountTypes.Computer)
                    Showman = new GameAccount(this) { AccountType = AccountTypes.Computer, SelectedAccount = _computerShowmans[0] };
                else
                    Showman = new GameAccount(this) { AccountType = AccountTypes.Human, SelectedAccount = anyAccount };
            }
        }
    }
}
