using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Enums;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.PubSub.Models;


namespace ExampleTwitchPubsub
{
    /// <summary>
    /// Represents the example bot
    /// </summary>
    public class Program
    {
        /// <summary>Serilog</summary>
        private static ILogger _logger;
        /// <summary>Settings</summary>
        public static IConfiguration Settings;
        /// <summary>Twitchlib Pubsub</summary>
        public static ITwitchPubSub PubSub;

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">Arguments</param>
        static void Main(string[] args)
        {
            var outputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {Message}{NewLine}{Exception}";

            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File("log/log_.txt", outputTemplate: outputTemplate, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Settings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Settings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            //run in async
            new Program()
                .MainAsync(args)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Async main method
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>the Task</returns>
        private async Task MainAsync(string[] args)
        {
            var channelId = Settings.GetSection("twitch").GetValue<string>("channelId");

            //Set up twitchlib pubsub
            PubSub = new TwitchPubSub();
            PubSub.OnListenResponse += OnListenResponse;
            PubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
            PubSub.OnPubSubServiceClosed += OnPubSubServiceClosed;
            PubSub.OnPubSubServiceError += OnPubSubServiceError;

            //Set up listeners
            ListenToBits(channelId);
            ListenToChatModeratorActions(channelId, channelId);
            ListenToCommerce(channelId);
            ListenToFollows(channelId);
            ListenToLeaderboards(channelId);
            ListenToPredictions(channelId);
            ListenToRaid(channelId);
            ListenToRewards(channelId);
            ListenToSubscriptions(channelId);
            ListenToVideoPlayback(channelId);
            ListenToWhispers(channelId);

            //Connect to pubsub
            PubSub.Connect();

            //Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        #region Whisper Events

        private void ListenToWhispers(string channelId)
        {
            PubSub.OnWhisper += PubSub_OnWhisper;
            PubSub.ListenToWhispers(channelId);
        }

        private void PubSub_OnWhisper(object sender, OnWhisperArgs e)
        {
            _logger.Information($"{e.Whisper.DataObjectWhisperReceived.Recipient.DisplayName} send a whisper {e.Whisper.DataObjectWhisperReceived.Body}");
        }

        #endregion

        #region Video Playback Events

        private void ListenToVideoPlayback(string channelId)
        {
            PubSub.OnStreamUp += PubSub_OnStreamUp;
            PubSub.OnStreamDown += PubSub_OnStreamDown;
            PubSub.OnViewCount += PubSub_OnViewCount;
            //PubSub.OnCommercial += PubSub_OnCommercial;
            PubSub.ListenToVideoPlayback(channelId);
        }

        [Obsolete]
        private void PubSub_OnCommercial(object sender, OnCommercialArgs e)
        {
            _logger.Information($"A commercial has started for {e.Length} seconds");
        }

        private void PubSub_OnViewCount(object sender, OnViewCountArgs e)
        {
            _logger.Information($"Current viewers: {e.Viewers}");
        }

        private void PubSub_OnStreamDown(object sender, OnStreamDownArgs e)
        {
            _logger.Information($"The stream is down");
        }

        private void PubSub_OnStreamUp(object sender, OnStreamUpArgs e)
        {
            _logger.Information($"The stream is up");
        }

        #endregion

        #region Subscription Events

        private void ListenToSubscriptions(string channelId)
        {
            PubSub.OnChannelSubscription += PubSub_OnChannelSubscription;
            PubSub.ListenToSubscriptions(channelId);
        }

        private void PubSub_OnChannelSubscription(object sender, OnChannelSubscriptionArgs e)
        {
            var gifted = e.Subscription.IsGift ?? false;
            if (gifted)
            {
                _logger.Information($"{e.Subscription.DisplayName} gifted a subscription to {e.Subscription.RecipientName}");
            }
            else
            {
                var cumulativeMonths = e.Subscription.CumulativeMonths ?? 0;
                if (cumulativeMonths != 0)
                {
                    _logger.Information($"{e.Subscription.DisplayName} just subscribed (total of {cumulativeMonths} months)");
                }
                else
                {
                    _logger.Information($"{e.Subscription.DisplayName} just subscribed");
                }

            }

        }

        #endregion

        #region Reward Events

        private void ListenToRewards(string channelId)
        {
            PubSub.OnRewardRedeemed += PubSub_OnRewardRedeemed;
            PubSub.OnCustomRewardCreated += PubSub_OnCustomRewardCreated;
            PubSub.OnCustomRewardDeleted += PubSub_OnCustomRewardDeleted;
            PubSub.OnCustomRewardUpdated += PubSub_OnCustomRewardUpdated;
            PubSub.ListenToRewards(channelId);
        }

        private void PubSub_OnCustomRewardUpdated(object sender, OnCustomRewardUpdatedArgs e)
        {
            _logger.Information($"Reward {e.RewardTitle} has been updated");
        }

        private void PubSub_OnCustomRewardDeleted(object sender, OnCustomRewardDeletedArgs e)
        {
            _logger.Information($"Reward {e.RewardTitle} has been removed");
        }

        private void PubSub_OnCustomRewardCreated(object sender, OnCustomRewardCreatedArgs e)
        {
            _logger.Information($"{e.RewardTitle} has been created");
            _logger.Debug($"{e.RewardTitle} (\"{e.RewardId}\")");
        }

        private void PubSub_OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            //Statuses can be:
            // "UNFULFILLED": when a user redeemed the reward
            // "FULFILLED": when a broadcaster or moderator marked the reward as complete
            if (e.Status == "UNFULFILLED")
            {
                _logger.Information($"{e.DisplayName} redeemed: {e.RewardTitle}");
            }

            if (e.Status == "FULFILLED")
            {
                _logger.Information($"Reward from {e.DisplayName} ({e.RewardTitle}) has been marked as complete");
            }
        }

        #endregion

        #region Outgoing Raid Events

        private void ListenToRaid(string channelId)
        {
            PubSub.OnRaidUpdate += PubSub_OnRaidUpdate;
            PubSub.OnRaidUpdateV2 += PubSub_OnRaidUpdateV2;
            PubSub.OnRaidGo += PubSub_OnRaidGo;
            PubSub.ListenToRaid(channelId);
        }

        private void PubSub_OnRaidGo(object sender, OnRaidGoArgs e)
        {
            _logger.Information($"Execute raid for {e.TargetDisplayName}");
        }

        private void PubSub_OnRaidUpdateV2(object sender, OnRaidUpdateV2Args e)
        {
            _logger.Information($"Started raid to {e.TargetDisplayName} with {e.ViewerCount} viewers");
        }

        private void PubSub_OnRaidUpdate(object sender, OnRaidUpdateArgs e)
        {
            _logger.Information($"Started Raid to {e.TargetChannelId} with {e.ViewerCount} viewers will start in {e.RemainingDurationSeconds} seconds");
        }

        #endregion

        #region Prediction Events

        private void ListenToPredictions(string channelId)
        {
            PubSub.OnPrediction += PubSub_OnPrediction;
            PubSub.ListenToPredictions(channelId);
        }

        private void PubSub_OnPrediction(object sender, OnPredictionArgs e)
        {
            //if (e.Type == PredictionType.EventCreated)
            {
                _logger.Information($"A new prediction has started: {e.Title}");
            }

            //if (e.Type == PredictionType.EventUpdated)
            {
                if (e.Status == PredictionStatus.Active)
                {
                    var winningOutcome = e.Outcomes.First(x => e.WinningOutcomeId.Equals(x.Id));
                    _logger.Information($"Prediction: {e.Status}, {e.Title} => winning: {winningOutcome.Title}({winningOutcome.TotalPoints} points by {winningOutcome.TotalUsers} users)");
                }

                if (e.Status == PredictionStatus.Resolved)
                {
                    var winningOutcome = e.Outcomes.First(x => e.WinningOutcomeId.Equals(x.Id));
                    _logger.Information($"Prediction: {e.Status}, {e.Title} => Won: {winningOutcome.Title}({winningOutcome.TotalPoints} points by {winningOutcome.TotalUsers} users)");
                }
            }
        }

        #endregion

        #region Leaderboard Events

        private void ListenToLeaderboards(string channelId)
        {
            PubSub.OnLeaderboardBits += PubSub_OnLeaderboardBits;
            PubSub.OnLeaderboardSubs += PubSub_OnLeaderboardSubs;
            PubSub.ListenToLeaderboards(channelId);
        }

        private void PubSub_OnLeaderboardSubs(object sender, OnLeaderboardEventArgs e)
        {
            _logger.Information($"Gifted Subs leader board");
            foreach (LeaderBoard leaderBoard in e.TopList)
            {
                _logger.Information($"{leaderBoard.Place}) {leaderBoard.UserId} ({leaderBoard.Score})");
            }
        }

        private void PubSub_OnLeaderboardBits(object sender, OnLeaderboardEventArgs e)
        {
            _logger.Information($"Bits leader board");
            foreach (LeaderBoard leaderBoard in e.TopList)
            {
                _logger.Information($"{leaderBoard.Place}) {leaderBoard.UserId} ({leaderBoard.Score})");
            }
        }

        #endregion

        #region Follow Events

        private void ListenToFollows(string channelId)
        {
            PubSub.OnFollow += PubSub_OnFollow;
            PubSub.ListenToFollows(channelId);
        }

        private void PubSub_OnFollow(object sender, OnFollowArgs e)
        {
            _logger.Information($"{e.Username} is now following");
        }

        #endregion

        #region Commerce Events

        private void ListenToCommerce(string channelId)
        {
            PubSub.OnChannelCommerceReceived += PubSub_OnChannelCommerceReceived;
            PubSub.ListenToCommerce(channelId);
        }

        private void PubSub_OnChannelCommerceReceived(object sender, OnChannelCommerceReceivedArgs e)
        {
            _logger.Information($"{e.ItemDescription} => {e.Username}: {e.PurchaseMessage} ");
        }

        #endregion

        #region Moderator Events

        private void ListenToChatModeratorActions(string myTwitchId, string channelId)
        {
            PubSub.OnTimeout += PubSub_OnTimeout;
            PubSub.OnBan += PubSub_OnBan;
            PubSub.OnMessageDeleted += PubSub_OnMessageDeleted;
            PubSub.OnUnban += PubSub_OnUnban;
            PubSub.OnUntimeout += PubSub_OnUntimeout;
            PubSub.OnHost += PubSub_OnHost;
            PubSub.OnSubscribersOnly += PubSub_OnSubscribersOnly;
            PubSub.OnSubscribersOnlyOff += PubSub_OnSubscribersOnlyOff;
            PubSub.OnClear += PubSub_OnClear;
            PubSub.OnEmoteOnly += PubSub_OnEmoteOnly;
            PubSub.OnEmoteOnlyOff += PubSub_OnEmoteOnlyOff;
            PubSub.OnR9kBeta += PubSub_OnR9kBeta;
            PubSub.OnR9kBetaOff += PubSub_OnR9kBetaOff;
            PubSub.ListenToChatModeratorActions(myTwitchId, channelId);
        }

        private void PubSub_OnR9kBetaOff(object sender, OnR9kBetaOffArgs e)
        {
            _logger.Information($"{e.Moderator} disabled R9K mode");
        }

        private void PubSub_OnR9kBeta(object sender, OnR9kBetaArgs e)
        {
            _logger.Information($"{e.Moderator} enabled R9K mode");
        }

        private void PubSub_OnEmoteOnlyOff(object sender, OnEmoteOnlyOffArgs e)
        {
            _logger.Information($"{e.Moderator} disabled emote only mode");
        }

        private void PubSub_OnEmoteOnly(object sender, OnEmoteOnlyArgs e)
        {
            _logger.Information($"{e.Moderator} enabled emote only mode");
        }

        private void PubSub_OnClear(object sender, OnClearArgs e)
        {
            _logger.Information($"{e.Moderator} cleared the chat");
        }

        private void PubSub_OnSubscribersOnlyOff(object sender, OnSubscribersOnlyOffArgs e)
        {
            _logger.Information($"{e.Moderator} disabled subscriber only mode");
        }

        private void PubSub_OnSubscribersOnly(object sender, OnSubscribersOnlyArgs e)
        {
            _logger.Information($"{e.Moderator} enabled subscriber only mode");
        }

        private void PubSub_OnHost(object sender, OnHostArgs e)
        {
            _logger.Information($"{e.Moderator} started host to {e.HostedChannel}");
        }

        private void PubSub_OnUntimeout(object sender, OnUntimeoutArgs e)
        {
            _logger.Information($"{e.UntimeoutedUser} undid the timeout of {e.UntimeoutedUser}");
        }

        private void PubSub_OnUnban(object sender, OnUnbanArgs e)
        {
            _logger.Information($"{e.UnbannedBy} unbanned {e.UnbannedUser}");
        }

        private void PubSub_OnMessageDeleted(object sender, OnMessageDeletedArgs e)
        {
            _logger.Information($"{e.DeletedBy} deleted the message \"{e.Message}\" from {e.TargetUser}");
        }

        private void PubSub_OnBan(object sender, OnBanArgs e)
        {
            _logger.Information($"{e.BannedBy} banned {e.BannedUser} ({e.BanReason})");
        }

        private void PubSub_OnTimeout(object sender, OnTimeoutArgs e)
        {
            _logger.Information($"{e.TimedoutBy} timed out {e.TimedoutUser} ({e.TimeoutReason}) for {e.TimeoutDuration.Seconds} seconds");
        }

        #endregion

        #region Bits Events

        private void ListenToBits(string channelId)
        {
            PubSub.OnBitsReceived += PubSub_OnBitsReceived;
            PubSub.ListenToBitsEvents(channelId);
        }

        private void PubSub_OnBitsReceived(object sender, OnBitsReceivedArgs e)
        {
            _logger.Information($"{e.Username} trowed {e.TotalBitsUsed} bits");
        }

        #endregion

        #region Pubsub events

        private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            _logger.Error($"{e.Exception.Message}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e)
        {
            _logger.Information($"Connection closed to pubsub server");
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e)
        {
            _logger.Information($"Connected to pubsub server");
            var oauth = Settings.GetSection("twitch.pubsub").GetValue<string>("oauth");
            PubSub.SendTopics(oauth);
        }

        private void OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                _logger.Error($"Failed to listen! Response{e.Response}");
            }
        }

        #endregion
    }
}
