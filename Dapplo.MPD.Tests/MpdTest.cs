//  Dapplo - building blocks for desktop applications
//  Copyright (C) 2016 Dapplo
// 
//  For more information see: http://dapplo.net/
//  Dapplo repositories are hosted on GitHub: https://github.com/dapplo
// 
//  This file is part of Dapplo.MPD
// 
//  Dapplo.MPD is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  Dapplo.MPD is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
// 
//  You should have a copy of the GNU Lesser General Public License
//  along with Dapplo.MPD. If not, see <http://www.gnu.org/licenses/lgpl.txt>.

#region using

using System;
using System.Linq;
using System.Threading.Tasks;
using Dapplo.Log;
using Dapplo.Log.XUnit;
using Dapplo.MPD.Client;
using Xunit;
using Xunit.Abstractions;

#endregion

namespace Dapplo.MPD.Tests
{
    public class MpdTest : IAsyncLifetime
    {
        private static int _port;
        private static string _host;

        public MpdTest(ITestOutputHelper testOutputHelper)
        {
            LogSettings.RegisterDefaultLogger<XUnitLogger>(LogLevels.Verbose, testOutputHelper);
        }

        public async Task InitializeAsync()
        {
            await Task.Run(async () =>
            {
                var mpdInstances = (await MpdSocketClient.FindByZeroConfAsync()).ToList();
                if (mpdInstances.Any())
                {
                    _port = mpdInstances.First().Value.Port;
                    _host = mpdInstances.First().Value.Host;
                }
                else
                {
                    var portFromEnv = Environment.GetEnvironmentVariable("MPD_PORT");
                    var hostFromEnv = Environment.GetEnvironmentVariable("MPD_HOST") ?? throw new ArgumentException(
                        "Configuration not found by ZeroConf and MPD_HOST not set in Environment");
                    _port = portFromEnv == null ? 6600 : int.Parse(portFromEnv);
                    _host = hostFromEnv;
                }
            });
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task TestConnectAsync()
        {
            using (var client = await MpdSocketClient.CreateAsync(_host, _port))
            {
                var status = await client.SendCommandAsync("status");
                Assert.True(status.IsOk);

                // Send unknown command
                status = await client.SendCommandAsync("dapplo");
                Assert.False(status.IsOk);
            }
        }

        [Fact]
        public async Task TestIdleAsync()
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            using (var statusClient = await MpdStateMonitor.CreateAsync(_host, _port))
            {
                statusClient.StateChanged += (sender, args) => { taskCompletionSource.SetResult(true); };
                using (var controlClient = await MpdClient.CreateAsync(_host, _port))
                {
                    var status = await controlClient.StatusAsync();
                    await controlClient.PauseAsync(status.PlayState == PlayStates.Playing);
                    status = await controlClient.StatusAsync();
                    await controlClient.PauseAsync(status.PlayState == PlayStates.Playing);
                }

                // Using the delay with the token causes a TaskCanceledException
                await taskCompletionSource.Task;
            }
        }

        [Fact]
        public async Task TestStatusAsync()
        {
            using (var client = await MpdClient.CreateAsync(_host, _port))
            {
                var status = await client.StatusAsync();
                Assert.NotNull(status.Audioformat);
                Assert.Equal("44100:24:2", status.Audioformat);
            }
        }
    }
}