﻿using SI.GameServer.Client;
using SICore;
using SICore.Network;
using SICore.Network.Clients;
using SICore.Network.Configuration;
using SICore.Network.Servers;
using SIData;
using SIGame.ViewModel.Implementation;
using SIGame.ViewModel.Properties;
using SIUI.ViewModel.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIGame.ViewModel
{
    public sealed class SIOnlineViewModel : ConnectionDataViewModel
    {
        private SI.GameServer.Contract.HostInfo _gamesHostInfo;

        public string ServerName => _gamesHostInfo?.Name ?? "SIGame";

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private GameInfo _currentGame = null;

        public GameInfo CurrentGame
        {
            get => _currentGame;
            set
            {
                if (_currentGame != value)
                {
                    _currentGame = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        UpdateJoinCommand(value.Persons);
                    }

                    CheckJoin();
                }
            }
        }

        private bool _canJoin;

        public bool CanJoin
        {
            get => _canJoin;
            set
            {
                if (_canJoin != value)
                {
                    _canJoin = value;
                    OnPropertyChanged();
                }
            }
        }

        private void CheckJoin() =>
            CanJoin = _currentGame != null && (!_currentGame.PasswordRequired || !string.IsNullOrEmpty(_password));

        public CustomCommand Cancel { get; set; }

        public CustomCommand AddEmoji { get; set; }

        public GamesFilter GamesFilter
        {
            get => _userSettings.GamesFilter;
            set
            {
                if (_userSettings.GamesFilter != value)
                {
                    _userSettings.GamesFilter = value;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GamesFilterValue));

                    lock (_serverGamesLock)
                    {
                        RecountGames();
                    }
                }
            }
        }

        public string GamesFilterValue
        {
            get
            {
                var value = "";
                var currentFilter = GamesFilter;

                var onlyNew = (currentFilter & GamesFilter.New) > 0;
                var sport = (currentFilter & GamesFilter.Sport) > 0;
                var tv = (currentFilter & GamesFilter.Tv) > 0;
                var noPassword = (currentFilter & GamesFilter.NoPassword) > 0;

                if ((sport && tv || !sport && !tv) && !onlyNew && !noPassword)
                {
                    value = Resources.GamesFilter_All;
                }
                else
                {
                    if (onlyNew)
                    {
                        value += Resources.GamesFilter_New;
                    }

                    if (sport && !tv)
                    {
                        if (value.Length > 0)
                        {
                            value += ", ";
                        }

                        value += Resources.GamesFilter_Sport;
                    }

                    if (tv && !sport)
                    {
                        if (value.Length > 0)
                        {
                            value += ", ";
                        }

                        value += Resources.GamesFilter_Tv;
                    }

                    if (noPassword)
                    {
                        if (value.Length > 0)
                        {
                            value += ", ";
                        }

                        value += Resources.GamesFilter_NoPassword;
                    }
                }

                return value;
            }
        }

        public bool IsNew
        {
            get => (GamesFilter & GamesFilter.New) > 0;
            set
            {
                if (value)
                    GamesFilter |= GamesFilter.New;
                else
                    GamesFilter &= ~GamesFilter.New;
            }
        }

        public bool IsSport
        {
            get => (GamesFilter & GamesFilter.Sport) > 0;
            set
            {
                if (value)
                    GamesFilter |= GamesFilter.Sport;
                else
                    GamesFilter &= ~GamesFilter.Sport;
            }
        }

        public bool IsTv
        {
            get => (GamesFilter & GamesFilter.Tv) > 0;
            set
            {
                if (value)
                    GamesFilter |= GamesFilter.Tv;
                else
                    GamesFilter &= ~GamesFilter.Tv;
            }
        }

        public bool IsNoPassword
        {
            get => (GamesFilter & GamesFilter.NoPassword) > 0;
            set
            {
                if (value)
                    GamesFilter |= GamesFilter.NoPassword;
                else
                    GamesFilter &= ~GamesFilter.NoPassword;
            }
        }

        protected override bool IsOnline => true;

        public ObservableCollection<GameInfo> ServerGames { get; } = new ObservableCollection<GameInfo>();
        public List<GameInfo> ServerGamesCache { get; private set; } = new List<GameInfo>();

        private readonly object _serverGamesLock = new object();

        private string _password = "";

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    CheckJoin();
                }
            }
        }

        private bool _showSearchBox;

        public bool ShowSearchBox
        {
            get => _showSearchBox;
            set
            {
                if (_showSearchBox != value)
                {
                    _showSearchBox = value;
                    OnPropertyChanged();

                    if (!_showSearchBox)
                    {
                        SearchFilter = null;
                    }
                }
            }
        }

        private string _searchFilter;

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (_searchFilter != value)
                {
                    _searchFilter = value;
                    OnPropertyChanged();

                    lock (_serverGamesLock)
                    {
                        RecountGames();
                    }
                }
            }
        }

        private bool _showProgress;

        public bool ShowProgress
        {
            get => _showProgress;
            set
            {
                if (_showProgress != value)
                {
                    _showProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _uploadProgress;

        public int UploadProgress
        {
            get => _uploadProgress;
            set
            {
                if (_uploadProgress != value)
                {
                    _uploadProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string[] Emoji { get; } = new string[] { "😃", "😁", "😪", "🎄", "🎓", "💥", "🦄", "🍋", "🍄", "🔥", "❤️", "✨", "🎅", "🎁", "☃️", "🦌" };

        private string _chatText;

        public string ChatText
        {
            get => _chatText;
            set
            {
                if (_chatText != value)
                {
                    _chatText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChatShown
        {
            get => _userSettings.GameSettings.AppSettings.IsChatShown;
            set { _userSettings.GameSettings.AppSettings.IsChatShown = value; OnPropertyChanged(); }
        }

        protected override string PackagesPublicBaseUrl => _gamesHostInfo.PackagesPublicBaseUrl;

        protected override string[] ContentPublicBaseUrls => _gamesHostInfo.ContentPublicBaseUrls;

        /// <summary>
        /// Подключение к игровому серверу
        /// </summary>
        private readonly IGameServerClient _gameServerClient;

        public ObservableCollection<string> Users { get; } = new ObservableCollection<string>();

        private readonly object _usersLock = new object();
        
        public SIOnlineViewModel(
            ConnectionData connectionData,
            IGameServerClient gameServerClient,
            CommonSettings commonSettings,
            UserSettings userSettings)
            : base(connectionData, commonSettings, userSettings)
        {
            _gameServerClient = gameServerClient;

            _gameServerClient.GameCreated += GameServerClient_GameCreated;
            _gameServerClient.GameDeleted += GameServerClient_GameDeleted;
            _gameServerClient.GameChanged += GameServerClient_GameChanged;

            _gameServerClient.Joined += GameServerClient_Joined;
            _gameServerClient.Leaved += GameServerClient_Leaved;
            _gameServerClient.Receieve += OnMessage;

            _gameServerClient.Reconnecting += GameServerClient_Reconnecting;
            _gameServerClient.Reconnected += GameServerClient_Reconnected;
            _gameServerClient.Closed += GameServerClient_Closed;

            _gameServerClient.UploadProgress += GameServerClient_UploadProgress;

            ServerAddress = _gameServerClient.ServiceUri;

            AddEmoji = new CustomCommand(AddEmoji_Executed);
        }

        private Task GameServerClient_Reconnecting(Exception exc)
        {
            OnMessage(Resources.App_Name, $"{Resources.ReconnectingMessage} {exc?.Message}");

            return Task.CompletedTask;
        }

        private Task GameServerClient_Reconnected(string message)
        {
            OnMessage(Resources.App_Name, Resources.ReconnectedMessage);

            var cancellationToken = _cancellationTokenSource.Token;

            UI.Execute(
                async () =>
                {
                    await ReloadGamesAsync(cancellationToken);
                    await ReloadUsersAsync(cancellationToken);
                },
                exc =>
                {
                    Error = exc.Message;
                },
                cancellationToken);

            return Task.CompletedTask;
        }

        private void AddEmoji_Executed(object arg) => ChatText += arg.ToString();

        private void GameServerClient_UploadProgress(int progress) => UploadProgress = progress;

        private Task GameServerClient_Closed(Exception exception)
        {
            PlatformSpecific.PlatformManager.Instance.ShowMessage(
                $"{Resources.LostConnection}: {exception?.Message}",
                PlatformSpecific.MessageType.Warning);

            Cancel.Execute(null);

            return Task.CompletedTask;
        }

        public event Action<string, string> Message;

        private void OnMessage(string userName, string message) => Message?.Invoke(userName, message);

        private void GameServerClient_Leaved(string userName)
        {
            lock (_usersLock)
            {
                Users.Remove(userName);
            }
        }

        private void GameServerClient_Joined(string userName)
        {
            lock (_usersLock)
            {
                var inserted = false;

                var length = Users.Count;
                for (int i = 0; i < length; i++)
                {
                    var comparison = Users[i].CompareTo(userName);
                    if (comparison == 0)
                    {
                        inserted = true;
                        break;
                    }

                    if (comparison > 0)
                    {
                        Users.Insert(i, userName);
                        inserted = true;
                        break;
                    }                        
                }

                if (!inserted)
                    Users.Add(userName);
            }
        }

        private void GameServerClient_GameChanged(GameInfo gameInfo)
        {
            lock (_serverGamesLock)
            {
                for (int i = 0; i < ServerGamesCache.Count; i++)
                {
                    if (ServerGamesCache[i].GameID == gameInfo.GameID)
                    {
                        ServerGamesCache[i] = gameInfo;
                        break;
                    }
                }

                RecountGames();
            }
        }

        private void GameServerClient_GameDeleted(int id)
        {
            lock (_serverGamesLock)
            {
                for (int i = 0; i < ServerGamesCache.Count; i++)
                {
                    if (ServerGamesCache[i].GameID == id)
                    {
                        ServerGamesCache.RemoveAt(i);
                        break;
                    }
                }

                RecountGames();
            }
        }

        private bool FilteredOk(GameInfo game) =>
            string.IsNullOrWhiteSpace(SearchFilter) ||
                !SearchFilter.StartsWith(CommonSettings.OnlineGameUrl) &&
                    CultureInfo.CurrentUICulture.CompareInfo.IndexOf(game.GameName, SearchFilter.Trim(), CompareOptions.IgnoreCase) >= 0 ||
                SearchFilter.StartsWith(CommonSettings.OnlineGameUrl) &&
                    int.TryParse(SearchFilter.Substring(CommonSettings.OnlineGameUrl.Length), out int gameId) &&
                    game.GameID == gameId;

        private bool FilterGame(GameInfo gameInfo)
        {
            if ((GamesFilter & GamesFilter.New) > 0 && gameInfo.RealStartTime != DateTime.MinValue)
            {
                return false;
            }

            if ((GamesFilter & GamesFilter.Sport) == 0 && (GamesFilter & GamesFilter.Tv) > 0 && gameInfo.Mode == SIEngine.GameModes.Sport)
            {
                return false;
            }

            if ((GamesFilter & GamesFilter.Sport) > 0 && (GamesFilter & GamesFilter.Tv) == 0 && gameInfo.Mode == SIEngine.GameModes.Tv)
            {
                return false;
            }

            if ((GamesFilter & GamesFilter.NoPassword) > 0 && gameInfo.PasswordRequired)
            {
                return false;
            }

            if (!FilteredOk(gameInfo))
            {
                return false;
            }

            return true;
        }

        private void GameServerClient_GameCreated(GameInfo gameInfo)
        {
            lock (_serverGamesLock)
            {
                ServerGamesCache.Add(gameInfo);
                RecountGames();
            }
        }

        private void InsertGame(GameInfo gameInfo)
        {
            var gameName = gameInfo.GameName;
            var length = ServerGames.Count;

            var inserted = false;

            for (int i = 0; i < length; i++)
            {
                var comparison = ServerGames[i].GameName.CompareTo(gameName);
                if (comparison == 0)
                {
                    inserted = true;
                    break;
                }

                if (comparison > 0)
                {
                    ServerGames.Insert(i, gameInfo);
                    inserted = true;
                    break;
                }
            }

            if (!inserted)
                ServerGames.Add(gameInfo);
        }

        public async Task InitAsync()
        {
            try
            {
                IsProgress = true;

                _gamesHostInfo = await _gameServerClient.GetGamesHostInfoAsync(_cancellationTokenSource.Token);

                OnPropertyChanged(nameof(ServerName));

                await ReloadGamesAsync(_cancellationTokenSource.Token);
                await ReloadUsersAsync(_cancellationTokenSource.Token);

                _avatar = (await UploadAvatarAsync(Human, _cancellationTokenSource.Token)).AvatarUrl;
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception exc)
            {
                PlatformSpecific.PlatformManager.Instance.ShowMessage(exc.ToString(), PlatformSpecific.MessageType.Warning, true);                
                Cancel.Execute(null);
            }
        }

        public async void Load()
        {
            try
            {
                var news = await _gameServerClient.GetNewsAsync(_cancellationTokenSource.Token);

                if (!string.IsNullOrEmpty(news))
                {
                    OnMessage(Resources.News, news);
                }
            }
            catch (Exception exc)
            {
                Error = exc.Message;
                FullError = exc.ToString();
            }
        }

        private async Task ReloadUsersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var users = await _gameServerClient.GetUsersAsync(cancellationToken);
                Array.Sort(users);
                lock (_usersLock)
                {
                    Users.Clear();
                    foreach (var user in users)
                    {
                        Users.Add(user);
                    }
                }
            }
            catch (Exception exc)
            {
                Error = exc.Message;
                FullError = exc.ToString();
            }
        }

        protected override async Task ClearConnectionAsync()
        {
            if (ReleaseConnection)
            {
                await _gameServerClient.DisposeAsync();
            }
            else
            {
                _gameServerClient.GameCreated -= GameServerClient_GameCreated;
                _gameServerClient.GameDeleted -= GameServerClient_GameDeleted;
                _gameServerClient.GameChanged -= GameServerClient_GameChanged;

                _gameServerClient.Joined -= GameServerClient_Joined;
                _gameServerClient.Leaved -= GameServerClient_Leaved;
                _gameServerClient.Receieve -= OnMessage;

                _gameServerClient.Reconnecting -= GameServerClient_Reconnecting;
                _gameServerClient.Reconnected -= GameServerClient_Reconnected;
                _gameServerClient.Closed -= GameServerClient_Closed;

                _gameServerClient.UploadProgress -= GameServerClient_UploadProgress;
            }

            await base.ClearConnectionAsync();
        }

        private async Task ReloadGamesAsync(CancellationToken cancellationToken = default)
        {
            IsProgress = true;
            Error = "";
            try
            {
                lock (_serverGamesLock)
                {
                    ServerGamesCache.Clear();
                    RecountGames();
                }

                SI.GameServer.Contract.Slice<GameInfo> gamesSlice = null;
                var whileGuard = 100;
                do
                {
                    var fromId = gamesSlice != null && gamesSlice.Data.Length > 0 ? gamesSlice.Data.Last().GameID + 1 : 0;
                    gamesSlice = await _gameServerClient.GetGamesAsync(fromId, cancellationToken);

                    lock (_serverGamesLock)
                    {
                        ServerGamesCache.AddRange(gamesSlice.Data);
                        RecountGames();
                    }

                    whileGuard--;
                } while (!gamesSlice.IsLastSlice && whileGuard > 0);
            }
            catch (Exception exc)
            {
                Error = exc.Message;
                FullError = exc.ToString();
            }
            finally
            {
                IsProgress = false;
            }
        }

        private void RecountGames()
        {
            var serverGames = ServerGames.ToArray();
            for (var i = 0; i < serverGames.Length; i++)
            {
                var item = serverGames[i];
                var game = ServerGamesCache.FirstOrDefault(sg => sg.GameID == item.GameID);
                if (game == null || !FilterGame(game))
                    ServerGames.Remove(item);
            }

            serverGames = ServerGames.ToArray();
            for (var i = 0; i < serverGames.Length; i++)
            {
                var item = serverGames[i];
                var game = ServerGamesCache.FirstOrDefault(sg => sg.GameID == item.GameID);
                if (game != null && game != item)
                    ServerGames[i] = game;
            }

            for (int i = 0; i < ServerGamesCache.Count; i++)
            {
                var item = ServerGamesCache[i];

                var game = ServerGames.FirstOrDefault(sg => sg.GameID == item.GameID);
                if (game == null && FilterGame(item))
                    InsertGame(item);
            }

            if (CurrentGame != null && !ServerGames.Contains(CurrentGame))
                CurrentGame = null;

            if (CurrentGame == null && ServerGames.Any())
                CurrentGame = ServerGames[0];
        }

        protected override void Prepare(GameSettingsViewModel gameSettings)
        {
            base.Prepare(gameSettings);

            gameSettings.NetworkGameType = NetworkGameType.GameServer;
            gameSettings.CreateGame += CreateGameAsync;
        }

        private async Task<Tuple<SlaveServer, IViewerClient>> CreateGameAsync(
            GameSettings settings,
            PackageSources.PackageSource packageSource)
        {
            GameSettings.Message = Resources.PackageCheck;

            var cancellationTokenSource = GameSettings.CancellationTokenSource = new CancellationTokenSource();

            var hash = await packageSource.GetPackageHashAsync(cancellationTokenSource.Token);
            var packageKey = new PackageKey
            {
                Name = packageSource.GetPackageName(),
                Hash = hash,
                ID = packageSource.GetPackageId()
            };

            var hasPackage = await _gameServerClient.HasPackageAsync(packageKey, cancellationTokenSource.Token);

            if (!hasPackage)
            {
                var data = await packageSource.GetPackageDataAsync(cancellationTokenSource.Token);
                if (data == null)
                {
                    throw new Exception(Resources.BadPackage);
                }

                using (data)
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        return null;
                    }

                    GameSettings.Message = Resources.SendingPackageToServer;
                    ShowProgress = true;
                    try
                    {
                        await _gameServerClient.UploadPackageAsync(packageKey, data, cancellationTokenSource.Token);
                    }
                    finally
                    {
                        ShowProgress = false;
                    }
                }
            }

            GameSettings.Message = Resources.Preparing;

            var computerAccounts = await ProcessCustomPersonsAsync(settings, cancellationTokenSource.Token);

            GameSettings.Message = Resources.Creating;

            if (_userSettings.UseSignalRConnection)
            {
                var gameCreatingResult2 = await _gameServerClient.CreateAndJoinGameAsync(
                    (GameSettingsCore<AppSettingsCore>)settings,
                    packageKey,
                    computerAccounts.ToArray(),
                    Human.IsMale,
                    cancellationTokenSource.Token);

                if (gameCreatingResult2.Code != SI.GameServer.Contract.GameCreationResultCode.Ok)
                {
                    throw new Exception(GetMessage(gameCreatingResult2.Code));
                }

                await InitServerAndClientNewAsync(cancellationTokenSource.Token);
                await JoinGameCompletedAsync(settings.Role, true, cancellationTokenSource.Token);

                if (_host == null)
                {
                    return null;
                }

                _host.Connector.SetGameID(gameCreatingResult2.GameId);

                return Tuple.Create(_server, _host);
            }

            var gameCreatingResult = await _gameServerClient.CreateGameAsync(
                (GameSettingsCore<AppSettingsCore>)settings,
                packageKey,
                computerAccounts.ToArray(),
                cancellationTokenSource.Token);

            if (gameCreatingResult.Code != SI.GameServer.Contract.GameCreationResultCode.Ok)
            {
                throw new Exception(GetMessage(gameCreatingResult.Code));
            }

            GameSettings.Message = Resources.GameEntering;

            await ConnectToServerAsHostAsync(gameCreatingResult.GameId, settings, cancellationTokenSource.Token);

            if (_host == null)
            {
                return null;
            }

            return Tuple.Create(_server, _host);
        }

        private async Task InitServerAndClientNewAsync(CancellationToken cancellationToken = default)
        {
            _server = new GameServerSlave(
                ServerConfiguration.Default,
                new NetworkLocalizer(Thread.CurrentThread.CurrentUICulture.Name));

            await _server.AddConnectionAsync(new GameServerConnection(_gameServerClient) { IsAuthenticated = true }, cancellationToken);

            _client = new Client(Human.Name);
            _client.ConnectTo(_server);
        }

        private static string GetMessage(SI.GameServer.Contract.GameCreationResultCode gameCreationResultCode) =>
            gameCreationResultCode switch
            {
                SI.GameServer.Contract.GameCreationResultCode.NoPackage => Resources.GameCreationError_NoPackage,
                SI.GameServer.Contract.GameCreationResultCode.TooMuchGames => Resources.GameCreationError_TooManyGames,
                SI.GameServer.Contract.GameCreationResultCode.ServerUnderMaintainance => Resources.GameCreationError_ServerMaintainance,
                SI.GameServer.Contract.GameCreationResultCode.BadPackage => Resources.GameCreationError_BadPackage,
                SI.GameServer.Contract.GameCreationResultCode.GameNameCollision => Resources.GameCreationError_DuplicateName,
                SI.GameServer.Contract.GameCreationResultCode.InternalServerError => Resources.GameCreationError_ServerError,
                SI.GameServer.Contract.GameCreationResultCode.ServerNotReady => Resources.GameCreationError_ServerNotReady,
                SI.GameServer.Contract.GameCreationResultCode.YourClientIsObsolete => Resources.GameCreationError_ObsoleteVersion,
                SI.GameServer.Contract.GameCreationResultCode.UnknownError => Resources.GameCreationError_UnknownReason,
                SI.GameServer.Contract.GameCreationResultCode.JoinError => Resources.GameCreationError_JoinError,
                SI.GameServer.Contract.GameCreationResultCode.WrongGameSettings => Resources.GameCreationError_WrongSettings,
                SI.GameServer.Contract.GameCreationResultCode.TooManyGamesByAddress => Resources.TooManyGames,
                _ => Resources.GameCreationError_UnknownReason,
            };

        private async Task<List<ComputerAccountInfo>> ProcessCustomPersonsAsync(GameSettings settings, CancellationToken cancellationToken)
        {
            var computerAccounts = new List<ComputerAccountInfo>();
            foreach (var player in settings.Players)
            {
                await ProcessCustomPersonAsync(computerAccounts, player, cancellationToken);
            }

            await ProcessCustomPersonAsync(computerAccounts, settings.Showman, cancellationToken);

            return computerAccounts;
        }

        private async Task ProcessCustomPersonAsync(List<ComputerAccountInfo> computerAccounts, Account account, CancellationToken cancellationToken)
        {
            if (!account.IsHuman && account.CanBeDeleted) // Нестандартный игрок, нужно передать его параметры на сервер
            {
                var avatar = (await UploadAvatarAsync(account, cancellationToken)).AvatarUrl;

                var computerAccount = new ComputerAccount((ComputerAccount)account) { Picture = avatar };

                computerAccounts.Add(new ComputerAccountInfo { Account = computerAccount });
            }
        }

        private async Task<(string AvatarUrl, FileKey FileKey)> UploadAvatarAsync(
            Account account,
            CancellationToken cancellationToken = default)
        {
            var avatarUri = account.Picture;
            if (!Uri.TryCreate(avatarUri, UriKind.Absolute, out Uri pictureUri))
            {
                return (null, null);
            }

            if (pictureUri.Scheme != "file" || !File.Exists(avatarUri)) // Это локальный файл, и его нужно отправить на сервер
            {
                return (null, null);
            }

            byte[] fileHash = null;
            using (var stream = File.OpenRead(avatarUri))
            {
                using var sha1 = new System.Security.Cryptography.SHA1Managed();
                fileHash = sha1.ComputeHash(stream);
            }

            var avatarKey = new FileKey { Name = Path.GetFileName(avatarUri), Hash = fileHash };

            // Если файла на сервере нет, загрузим его
            var avatarPath = await _gameServerClient.HasImageAsync(avatarKey, cancellationToken);
            if (avatarPath == null)
            {
                using var stream = File.OpenRead(avatarUri);
                avatarPath = await _gameServerClient.UploadImageAsync(avatarKey, stream, cancellationToken);
            }

            if (avatarPath != null && !Uri.IsWellFormedUriString(avatarPath, UriKind.Absolute))
            {
                // Prepend avatarPath with service content root uri
                var rootAddress = _gamesHostInfo.ContentPublicBaseUrls.FirstOrDefault() ?? _gameServerClient.ServiceUri;
                avatarPath = rootAddress + avatarPath;
            }

            return (avatarPath, avatarKey);
        }

        protected override string GetExtraCredentials() => !string.IsNullOrEmpty(_password) ? $"\n{_password}" : "";

        public override async Task JoinGameCoreAsync(
            GameInfo gameInfo,
            GameRole role,
            bool isHost = false,
            CancellationToken cancellationToken = default)
        {
            gameInfo ??= _currentGame;

            if (!isHost)
            {
                lock (_serverGamesLock)
                {
                    var passwordRequired = gameInfo != null && gameInfo.PasswordRequired;
                    if (passwordRequired && string.IsNullOrEmpty(_password))
                    {
                        IsProgress = false;
                        return;
                    }
                }
            }

            try
            {
                Trace.TraceInformation($"Joining game: UseSignalRConnection = {_userSettings.UseSignalRConnection}");
                if (_userSettings.UseSignalRConnection)
                {
                    var result = await _gameServerClient.JoinGameAsync(gameInfo.GameID, role, Human.IsMale, _password, cancellationToken);
                    if (result.ErrorMessage != null)
                    {
                        Error = result.ErrorMessage;
                        return;
                    }

                    await InitServerAndClientNewAsync(cancellationToken);
                    await JoinGameCompletedAsync(role, isHost, cancellationToken);
                }
                else
                {
                    await InitServerAndClientAsync(_gamesHostInfo.Host ?? new Uri(ServerAddress).Host, _gamesHostInfo.Port);
                    await ConnectCoreAsync(true);
                    var result = await _connector.SetGameIdAsync(gameInfo.GameID);
                    if (!result)
                    {
                        Error = Resources.CreatedGameNotFound;
                        return;
                    }

                    await base.JoinGameCoreAsync(gameInfo, role, isHost, cancellationToken);
                }

                _host.Connector.SetGameID(gameInfo.GameID);
            }
            catch (TaskCanceledException exc)
            {
                Error = Resources.GameConnectionTimeout;
                FullError = exc.ToString();
            }
            catch (Exception exc)
            {
                Error = exc.Message;
                FullError = exc.ToString();
            }
        }

        internal async Task ConnectToServerAsHostAsync(int gameID, GameSettings gameSettings, CancellationToken cancellationToken = default)
        {
            var name = Human.Name;

            _password = gameSettings.NetworkGamePassword;
            var game = new GameInfo { GameID = gameID, Owner = name };

            await JoinGameAsync(game, gameSettings.Role, true, cancellationToken);
        }

        public void Say(string message, bool system = false)
        {
            if (!system)
            {
                _gameServerClient.SayAsync(message);
            }
        }

        protected override void CloseContent_Executed(object arg)
        {
            if (GameSettings != null)
            {
                GameSettings.CancellationTokenSource?.Cancel();
            }

            base.CloseContent_Executed(arg);
        }

        public override ValueTask DisposeAsync()
        {
            if (GameSettings != null)
            {
                GameSettings.CancellationTokenSource?.Cancel();
            }

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            return base.DisposeAsync();
        }
    }
}
