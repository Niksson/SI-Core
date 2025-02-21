﻿using Microsoft.AspNetCore.SignalR;
using SI.GameServer.Client;
using SICore.Connections;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SIGame.ViewModel.Implementation
{
    internal sealed class GameServerConnection : ConnectionBase
    {
        private readonly IGameServerClient _gameServerClient;
        private bool _isDisposed;

        public GameServerConnection(
            IGameServerClient gameServerClient)
        {
            _gameServerClient = gameServerClient;
            _gameServerClient.IncomingMessage += OnMessageReceived;
            _gameServerClient.Reconnecting += GameServerClient_Reconnecting;
            _gameServerClient.Reconnected += GameServerClient_Reconnected;
        }

        private Task GameServerClient_Reconnecting(Exception arg)
        {
            OnReconnecting();
            return Task.CompletedTask;
        }

        private Task GameServerClient_Reconnected(string arg)
        {
            OnReconnected();
            return Task.CompletedTask;
        }

        public override string RemoteAddress => throw new NotImplementedException();

        public override ValueTask SendMessageAsync(Message m)
        {
            if (_isDisposed)
            {
                OnError(new InvalidOperationException("Connection was closed"), true);
                return new ValueTask();
            }

            try
            {
                return new ValueTask(_gameServerClient.SendMessageAsync(m));
            }
            catch (TaskCanceledException exc)
            {
                OnError(exc, true);
            }
            catch (InvalidDataException exc)
            {
                OnError(exc, true);
            }
            catch (HubException exc)
            {
                OnError(exc, true);
            }

            return new ValueTask();
        }

        protected override ValueTask DisposeAsync(bool disposing)
        {
            _gameServerClient.IncomingMessage -= OnMessageReceived;
            _gameServerClient.Reconnecting -= GameServerClient_Reconnecting;
            _gameServerClient.Reconnected -= GameServerClient_Reconnected;

            _isDisposed = true;

            return _gameServerClient.DisposeAsync();
        }
    }
}
