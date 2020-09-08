namespace Lavalink4NET.I51Repro
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Discord;
    using Discord.WebSocket;
    using Lavalink4NET.DiscordNet;
    using Lavalink4NET.Player;

    internal class Program
    {
        private const ulong GuildId = 616986381373145099;
        private const string Track1 = "ytsearch:one more time";
        private const string Track2 = "ytsearch:country roads";
        private const ulong VoiceChannelId = 616986382010941488;

        private static async Task Main()
        {
            using var client = new DiscordSocketClient();

            await client.LoginAsync(TokenType.Bot, Secrets.BotToken);
            await client.StartAsync();

            client.Ready += () =>
            {
                // have to run on a different thread
                _ = Task.Run(() => TestAsync(client));
                return Task.CompletedTask;
            };

            await Task.Delay(-1);
        }

        private static async Task TestAsync(DiscordSocketClient client)
        {
            using var audioService = new LavalinkNode(new LavalinkNodeOptions { AllowResuming = false, DisconnectOnStop = false }, new DiscordClientWrapper(client));
            await audioService.InitializeAsync();

            await Task.Delay(2000);

            var track1 = await audioService.GetTrackAsync(Track1);
            var player = await audioService.JoinAsync<QueuedLavalinkPlayer>(GuildId, VoiceChannelId);

            await Task.Delay(2000);

            // Play a track by adding it to the queue using player.Queue.Add(track) and calling
            // PlayTopAsync(track), where track has been dequeued.
            player.Queue.Add(track1);

            if (player.CurrentTrack is null && player.Queue.TryDequeue(out var trackInQueue))
            {
                await player.PlayTopAsync(trackInQueue);
            }

            Debug.Assert(player.State is PlayerState.Playing);
            Debug.Assert(player.CurrentTrack is not null);

            await Task.Delay(8000);

            Debug.Assert(player.State is PlayerState.Playing);
            Debug.Assert(player.CurrentTrack is not null);

            var track2 = await audioService.GetTrackAsync(Track2);

            // Add another track to the queue by using player.Queue.Add(track), but without calling
            // PlayTopAsync() this time or dequeuing the track.
            player.Queue.Add(track2);

            Debug.Assert(player.State is PlayerState.Playing);
            Debug.Assert(player.CurrentTrack is not null);

            var trackEndEventDispatched = false;
            audioService.TrackEnd += async (_, _) => trackEndEventDispatched = true;

            Debug.Assert(!trackEndEventDispatched);

            await Task.Delay(2000);

            // Skip to the next track using player.SkipAsync();
            await player.SkipAsync();

            Debug.Assert(player.State is PlayerState.Playing); // ! Assertion should fail here according to the issue
            Debug.Assert(player.CurrentTrack is not null);

            // give the LavalinkNode some time to dispatch the TrackEndEvent
            await Task.Delay(1000);

            Debug.Assert(trackEndEventDispatched);

            await Task.Delay(4000);

            // In a different method a bit later on, get the player using LavalinkNode.GetPlayer()
            // and check the State property. The player is playing music, the track finished event
            // has not been called, and yet player.State is set to NotPlaying.
            player = audioService.GetPlayer<QueuedLavalinkPlayer>(GuildId);

            Debug.Assert(player is not null);
            Debug.Assert(player.State is PlayerState.Playing); // ! Assertion should fail here according to the issue
            Debug.Assert(player.CurrentTrack is not null);
        }
    }
}
