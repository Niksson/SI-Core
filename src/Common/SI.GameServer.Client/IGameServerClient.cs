﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SICore;
using SICore.Connections;
using SIData;

namespace SI.GameServer.Client
{
    public interface IGameServerClient: IAsyncDisposable
    {
        string ServiceUri { get; }

        event Action<GameInfo> GameCreated;
        event Action<int> GameDeleted;
        event Action<GameInfo> GameChanged;

        event Action<string> Joined;
        event Action<string> Leaved;
        event Action<string, string> Receieve;

        event Func<Exception, Task> Reconnecting;
        event Func<string, Task> Reconnected;

        event Func<Exception, Task> Closed;

        event Action<int> UploadProgress;
        event Action<Message> IncomingMessage;

        Task OpenAsync(string userName, CancellationToken token = default);
        Task<Contract.HostInfo> GetGamesHostInfoAsync(CancellationToken cancellationToken = default);
        Task<string> GetNewsAsync(CancellationToken cancellationToken = default);
        Task<string[]> GetUsersAsync(CancellationToken cancellationToken = default);

        Task<Contract.Slice<GameInfo>> GetGamesAsync(int fromId, CancellationToken cancellationToken = default);

        Task<bool> HasPackageAsync(PackageKey packageKey, CancellationToken cancellationToken = default);

        Task UploadPackageAsync(FileKey packageHash, Stream stream, CancellationToken cancellationToken = default);

        Task<Contract.GameCreationResult> CreateGameAsync(
            GameSettingsCore<AppSettingsCore> gameSettings,
            PackageKey packageKey,
            ComputerAccountInfo[] computerAccounts,
            CancellationToken cancellationToken = default);

        Task<string> HasImageAsync(FileKey imageKey, CancellationToken cancellationToken = default);
        Task<string> UploadImageAsync(FileKey imageKey, Stream data, CancellationToken cancellationToken = default);

        Task SayAsync(string message);

        Task<Contract.GameCreationResult> JoinGameAsync(
            int gameId,
            GameRole role,
            bool isMale,
            string password,
            CancellationToken cancellationToken = default);

        Task SendMessageAsync(Message message, CancellationToken cancellationToken = default);

        Task<Contract.GameCreationResult> CreateAndJoinGameAsync(
            GameSettingsCore<AppSettingsCore> gameSettings,
            PackageKey packageKey,
            ComputerAccountInfo[] computerAccounts,
            bool isMale,
            CancellationToken cancellationToken = default);

        Task LeaveGameAsync(CancellationToken cancellationToken = default);
    }
}
