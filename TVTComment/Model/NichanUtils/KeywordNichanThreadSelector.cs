﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TVTComment.Model.NichanUtils
{
    class KeywordNichanThreadSelector : INichanThreadSelector
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly string boardHost;
        private readonly string boardName;

        public string BoardUri { get; }
        public IEnumerable<string> Keywords { get; }

        public KeywordNichanThreadSelector(string boardUri, IEnumerable<string> keywords)
        {
            var uri = new Uri(boardUri);
            boardHost = $"{uri.Scheme}://{uri.Host}";
            boardName = uri.Segments[1];
            if (boardName.EndsWith('/'))
                boardName = boardName[..^1];

            BoardUri = boardUri;
            Keywords = keywords.Select(x => x.ToLower().Normalize(NormalizationForm.FormKD));
        }

        public async Task<IEnumerable<string>> Get(ChannelInfo channel, DateTimeOffset time, CancellationToken cancellationToken)
        {
            byte[] subjectBytes = await httpClient.GetByteArrayAsync($"{boardHost}/{boardName}/subject.txt", cancellationToken);
            string subject = Encoding.GetEncoding(932).GetString(subjectBytes);

            using var textReader = new StringReader(subject);
            IEnumerable<Nichan.Thread> threadsInBoard = await Nichan.SubjecttxtParser.ParseFromStream(textReader);

            return threadsInBoard.Select(
                x => { x.Title = x.Title.ToLower().Normalize(NormalizationForm.FormKD); return x; }
            ).Where(
                x => Keywords.All(keyword => x.Title.Contains(keyword))
            ).Select(
                x => $"{boardHost}/test/read.cgi/{boardName}/{x.Name}"
            );
        }
    }
}
