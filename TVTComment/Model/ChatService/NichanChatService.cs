﻿using ObservableUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TVTComment.Model.ChatService
{
    class NichanChatServiceSettings
    {
        public class GochanApiSettings
        {
            public string HmKey { get; set; } = "";
            public string AppKey { get; set; } = "";
            public string UserId { get; set; } = "";
            public string Password { get; set; } = "";
            public string AuthUserAgent { get; set; } = "";
            public string AuthX2chUA { get; set; } = "JaneStyle/3.80";
            public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; JaneStyle/3.80..)";
        }

        public TimeSpan ThreadUpdateInterval { get; set; } = new TimeSpan(0, 0, 15);
        public TimeSpan ThreadListUpdateInterval { get; set; } = new TimeSpan(0, 0, 1);
        public TimeSpan PastCollectServiceBackTime { get; set; } = new TimeSpan(3, 0, 0);
        public GochanApiSettings GochanApi { get; set; } = new GochanApiSettings();

        // 以下は旧設定移行用
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string HmKey { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AppKey { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UserId { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Password { get; set; } = null;
    }

    class NichanChatService : IChatService
    {
        public class BoardInfo
        {
            public string Title { get; }
            public Uri Uri { get; }
            public BoardInfo(string title,Uri uri)
            {
                Title = title;
                Uri = uri;
            }
        }

        public string Name => "2ch";
        public IReadOnlyList<ChatCollectServiceEntry.IChatCollectServiceEntry> ChatCollectServiceEntries { get; }
        public IReadOnlyList<IChatTrendServiceEntry> ChatTrendServiceEntries { get; }
        public IEnumerable<BoardInfo> BoardList { get; }

        public TimeSpan ResCollectInterval => this.resCollectInterval.Value;
        public TimeSpan ThreadSearchInterval => this.threadSearchInterval.Value;
        public string GochanApiHmKey => this.nichanApiClient.Value.HmKey;
        public string GochanApiAppKey => this.nichanApiClient.Value.AppKey;
        public string GochanApiUserId => this.nichanApiClient.Value.UserId;
        public string GochanApiPassword => this.nichanApiClient.Value.Password;
        public string GochanApiAuthUserAgent => this.nichanApiClient.Value.AuthUserAgent;
        public string GochanApiAuthX2UA => this.nichanApiClient.Value.AuthX2chUA;
        public string GochanApiUserAgent => this.nichanApiClient.Value.UserAgent;
        public TimeSpan PastCollectServiceBackTime => this.pastCollectServiceBackTime.Value;

        //このChatServiceに行われた設定変更が子のChatServiceEntryに伝わるようにするためにObservableValueで包む
        private ObservableValue<TimeSpan> resCollectInterval=new ObservableValue<TimeSpan>();
        private ObservableValue<TimeSpan> threadSearchInterval = new ObservableValue<TimeSpan>();
        private ObservableValue<Nichan.ApiClient> nichanApiClient = new ObservableValue<Nichan.ApiClient>();
        private ObservableValue<TimeSpan> pastCollectServiceBackTime = new ObservableValue<TimeSpan>();

        private NichanUtils.BoardDatabase boardDatabase;
        private NichanUtils.ThreadResolver threadResolver;

        private NichanChatServiceSettings settings;

        public NichanChatService(
            NichanChatServiceSettings settings, ChannelDatabase channelDatabase,
            string threadSettingFilePath
        )
        {
            this.settings = settings;

            // 設定構造変更に伴い設定値を移行
            if (settings.HmKey != null)
            {
                settings.GochanApi.HmKey = settings.HmKey;
                settings.HmKey = null;
            }
            if (settings.AppKey != null)
            {
                settings.GochanApi.AppKey = settings.AppKey;
                settings.AppKey = null;
            }
            if (settings.UserId != null)
            {
                settings.GochanApi.UserId = settings.UserId;
                settings.UserId = null;
            }
            if (settings.Password != null)
            {
                settings.GochanApi.Password = settings.Password;
                settings.Password = null;
            }

            var boardSetting = NichanUtils.ThreadSettingFileParser.Parse(threadSettingFilePath);
            boardDatabase = new NichanUtils.BoardDatabase(boardSetting.BoardEntries, boardSetting.ThreadMappingRuleEntries);
            threadResolver = new NichanUtils.ThreadResolver(channelDatabase, boardDatabase);

            this.resCollectInterval.Value = settings.ThreadUpdateInterval;
            this.threadSearchInterval.Value = settings.ThreadListUpdateInterval;
            this.nichanApiClient.Value = new Nichan.ApiClient(
                settings.GochanApi.HmKey, settings.GochanApi.AppKey,
                settings.GochanApi.UserId, settings.GochanApi.Password,
                settings.GochanApi.AuthUserAgent, settings.GochanApi.AuthX2chUA, settings.GochanApi.UserAgent
            );
            this.pastCollectServiceBackTime.Value = settings.PastCollectServiceBackTime;

            ChatCollectServiceEntries = new ChatCollectServiceEntry.IChatCollectServiceEntry[] {
                new ChatCollectServiceEntry.DATNichanChatCollectServiceEntry(this, resCollectInterval, threadSearchInterval, threadResolver, nichanApiClient),
                new ChatCollectServiceEntry.PastNichanChatCollectServiceEntry(this, threadResolver, pastCollectServiceBackTime),
            };
            ChatTrendServiceEntries = new IChatTrendServiceEntry[0];

            BoardList = boardDatabase.BoardList.Select(x => new BoardInfo(x.Title, x.Uri));
        }

        public void SetIntervalValues(TimeSpan resCollectInterval, TimeSpan threadSearchInterval)
        {
            this.settings.ThreadUpdateInterval = resCollectInterval;
            this.settings.ThreadListUpdateInterval = threadSearchInterval;

            this.resCollectInterval.Value = resCollectInterval;
            this.threadSearchInterval.Value = threadSearchInterval;
        }

        public void SetApiParams(
            string hmKey, string appKey, string userId, string password,
            string authUserAgent, string authX2chUA, string userAgent
        )
        {
            using (this.nichanApiClient.Value)
            {
                this.settings.GochanApi.HmKey = hmKey;
                this.settings.GochanApi.AppKey = appKey;
                this.settings.GochanApi.UserId = userId;
                this.settings.GochanApi.Password = password;
                this.settings.GochanApi.AuthUserAgent = authUserAgent;
                this.settings.GochanApi.AuthX2chUA = authX2chUA;
                this.settings.GochanApi.UserAgent = userAgent;

                this.nichanApiClient.Value = new Nichan.ApiClient(
                    hmKey, appKey, userId, password,
                    authUserAgent, authX2chUA, userAgent
                );
            }
        }

        public void SetPastCollectServiceBackTime(TimeSpan value)
        {
            this.settings.PastCollectServiceBackTime = value;
            this.pastCollectServiceBackTime.Value = value;
        }

        public void Dispose()
        {
        }
    }
}
