using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

namespace Subspace
{
    /// <summary>
    /// Wrapper around <see cref="HttpClient"/> to communicate
    /// with the OpenSubtitles server.
    /// </summary>
    public sealed class OpenSubtitlesClient : IDisposable
    {
        #region Ctor, Props, Dtor, Login
        public const string ENDPOINT = "http://api.opensubtitles.org/xml-rpc";
        public const string USER_AGENT = "Subspace V1";

        public HttpClient Client { get; private set; }
        public string Token { get; private set; }
        
        private OpenSubtitlesClient()
        {
            Client = new HttpClient();
            Client.DefaultRequestHeaders.AcceptEncoding.Clear();
            Client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        }

        public void Dispose()
        {
            Client.Dispose();
        }

        public static async Task<OpenSubtitlesClient> LogIn()
        {
            var client = new OpenSubtitlesClient();
            var req = await client.TryLogIn();

            req.EnsureSuccessStatusCode();

            var dic = ParseResponse(await req.Content.UnzipAsStringAsync());

            if (!dic.ContainsKey("token"))
                throw new Exception("Error logging in.");

            client.Token = dic["token"];
            return client;
        }
        #endregion

        #region Private utils
        /// <summary>
        /// Extract XML-RPC data from a response to a dictionary using <see cref="Regex"/>.
        /// </summary>
        private static Dictionary<string, string> ParseResponse(string res)
        {
            return Regex
                .Matches(res, @"<member>\s*<name>(.+?)<\/name>\s*<value>\s*(?:(?:<\w+\/>)|(?:<\w+>(.+?)<\/\w+>))\s*<\/value>\s*<\/member>", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value);
        }

        /// <summary>
        /// Extract XML-RPC data from a response using <see cref="Regex"/>.
        /// </summary>
        private static string ExtractDataFromResponse(string res)
        {
            return Regex.Match(res, @"<data>([\s\S]*)<\/data>", RegexOptions.IgnoreCase).Groups[1].Value;
        }

        /// <summary>
        /// Extract XML-RPC items from an array using <see cref="Regex"/>.
        /// </summary>
        private static string[] ExtractItemsFromArray(string arr)
        {
            return Regex.Split(arr, @"<value>\s*<struct>\s*<member>\s*<name>MatchedBy<\/name>", RegexOptions.IgnoreCase).Skip(1).ToArray();
        }

        /// <summary>
        /// Post data by sending a template, and replacing some of its placeholders
        /// by actual values using LINQ Expressions.
        /// </summary>
        private async Task<HttpResponseMessage> Post(string template, params Expression<Func<string, string>>[] exprs)
        {
            foreach (var expr in exprs)
                template = template.Replace($"%{expr.Parameters[0].Name.ToUpper()}%",
                    expr.Body is ConstantExpression ? (string)(expr.Body as ConstantExpression).Value : (expr.Compile()(String.Empty)));

            return await Client.PostAsync(ENDPOINT, new StringContent(template.Trim()));
        }

        /// <summary>
        /// Try logging in.
        /// </summary>
        private async Task<HttpResponseMessage> TryLogIn()
        {
            return await Post(Templates.LOG_IN, lang => "en", ua => USER_AGENT);
        }

        /// <summary>
        /// Extract <see cref="Subtitle"/> data from a dictionary of
        /// XML-RPC values returned by the server.
        /// </summary>
        private static Subtitle GetSubtitleFromText(Dictionary<string, string> sub)
        {
            return new Subtitle
            {
                Format = sub.Get("SubFormat"),
                Rating = double.Parse(sub.Get("SubRating") ?? "0", NumberFormatInfo.InvariantInfo),
                DownloadsCount = int.Parse(sub.Get("SubDownloadsCnt") ?? "0"),

                ID = int.Parse(sub.Get("IDSubtitle") ?? "0"),
                Hash = sub.Get("SubHash"),
                DownloadLink = new Uri(sub.Get("SubDownloadLink")),
                Filename = sub.Get("SubFileName"),
                HearingImpaired = sub.Get("SubHearingImpaired") == "1",
                Language = sub.Get("ISO639"),

                MovieYear = int.Parse(sub.Get("MovieYear") ?? "0"),
                MovieID = int.Parse(sub.Get("IDMovie") ?? "0"),
                MovieName = sub.Get("MovieName"),
                MovieRating = double.Parse(sub.Get("MovieImdbRating") ?? "0", NumberFormatInfo.InvariantInfo),
                ImdbID = int.Parse(sub.Get("IDMovieImdb") ?? "0")
            };
        }

        /// <summary>
        /// Extract all subtitles from a XML-RPC response,
        /// and rank them by rating, and download count.
        /// </summary>
        private static IEnumerable<Subtitle> ExtractSubtitles(string str, bool hearingImpaired, string language)
        {
            var arr = ExtractDataFromResponse(str);
            var subs = ExtractItemsFromArray(arr);

            return from subtitle in subs
                   let sub = GetSubtitleFromText(ParseResponse(subtitle))
                   where sub.HearingImpaired == hearingImpaired && sub.Language == language && sub.Format == "srt"
                   orderby sub.Rating, sub.DownloadsCount descending
                   select sub;
        }
        #endregion

        #region Actual methods
        /// <summary>
        /// Try to get subtitles from hash, tag, and query
        /// </summary>
        public async Task<IEnumerable<Subtitle>> GetSubtitlesFromAll(string filename, string language = "en", bool hearingImpaired = false)
        {
            string cleanName = Path.GetFileNameWithoutExtension(filename).Replace('.', ' ').Replace('_', ' ');
            Match m;

            // try TV show S01E03
            m = Regex.Match(cleanName, @"(.+) *s(\d{1,2})e(\d{1,3}).*", RegexOptions.IgnoreCase);

            // try TV show 1x03
            if (!m.Success)
                m = Regex.Match(cleanName, @"(.+) *(\d{1,2})x(\d{1,3}).*", RegexOptions.IgnoreCase);

            // try to clean up name even more
            bool isShow = m.Success;
            if (!isShow)
                cleanName = Regex.Replace(cleanName, @"\d{3,4}[ip].*$|\(\d{4}\).*$|\d{4}.*$", "");

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                HttpResponseMessage res = isShow
                    ? await Post(Templates.SEARCH_ALL_TVSHOW,
                        token => Token, lang => language,
                        hash => Helper.ToHexadecimal(Helper.ComputeMovieHash(fs)), size => fs.Length.ToString(),
                        tag => filename, query => m.Groups[1].Value, season => m.Groups[2].Value, episode => m.Groups[3].Value)
                    : await Post(Templates.SEARCH_ALL_MOVIE,
                        token => Token, lang => language,
                        hash => Helper.ToHexadecimal(Helper.ComputeMovieHash(fs)), size => fs.Length.ToString(),
                        tag => filename, query => cleanName);

                res.EnsureSuccessStatusCode();
                return ExtractSubtitles(await res.Content.UnzipAsStringAsync(), hearingImpaired, language);
            }
        }
        
        public async Task<IEnumerable<Subtitle>> GetSubtitlesFromFile(string filename, string language = "en", bool hearingImpaired = false)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                var res = await Post(Templates.SEARCH_HASH,
                    token => Token,
                    lang => language,
                    hash => Helper.ToHexadecimal(Helper.ComputeMovieHash(fs)),
                    size => fs.Length.ToString(),
                    tag => filename);

                res.EnsureSuccessStatusCode();
                return ExtractSubtitles(await res.Content.UnzipAsStringAsync(), hearingImpaired, language);
            }
        }
        
        public async Task<IEnumerable<Subtitle>> GetSubtitlesFromQuery(string movie, string language = "en", bool hearingImpaired = false)
        {
            var res = await Post(Templates.SEARCH_MOVIE,
                token => Token,
                lang => language,
                query => movie);

            res.EnsureSuccessStatusCode();
            return ExtractSubtitles(await res.Content.UnzipAsStringAsync(), hearingImpaired, language);
        }
        
        public async Task<IEnumerable<Subtitle>> GetSubtitlesFromQuery(string tvshow, int se, int ep, string language = "en", bool hearingImpaired = false)
        {
            var res = await Post(Templates.SEARCH_TVSHOW,
                token => Token,
                lang => language,
                query => tvshow,
                season => se.ToString(),
                episode => ep.ToString());

            res.EnsureSuccessStatusCode();
            return ExtractSubtitles(await res.Content.UnzipAsStringAsync(), hearingImpaired, language);
        }

        public async Task<IEnumerable<Subtitle>> GetSubtitlesFromTag(string tag, string language = "en", bool hearingImpaired = false)
        {
            var res = await Post(Templates.SEARCH_TAG,
                token => Token,
                lang => language,
                TAG => tag);

            res.EnsureSuccessStatusCode();
            return ExtractSubtitles(await res.Content.UnzipAsStringAsync(), hearingImpaired, language);
        }

        public async Task<byte[]> RetrieveSubtitle(Subtitle sub)
        {
            using (var res = await Client.GetAsync(sub.DownloadLink))
            {
                res.EnsureSuccessStatusCode();
                return await res.Content.UnzipAsByteArrayAsync();
            }
        }
        #endregion
    }

    #region Helper
    internal static class Helper
    {
        public static string Get(this Dictionary<string, string> dic, string key)
        {
            string res;
            if (dic.TryGetValue(key, out res))
                return res;
            return null;
        }

        public static async Task<string> UnzipAsStringAsync(this HttpContent content)
        {
            return Encoding.UTF8.GetString(await content.UnzipAsByteArrayAsync());
        }

        public static async Task<byte[]> UnzipAsByteArrayAsync(this HttpContent content)
        {
            using (GZipStream gz = new GZipStream(await content.ReadAsStreamAsync(), CompressionMode.Decompress))
            using (MemoryStream unzipped = new MemoryStream())
            {
                await gz.CopyToAsync(unzipped);
                return unzipped.ToArray();
            }
        }

        public static byte[] ComputeMovieHash(Stream input)
        {
            long lhash, streamsize;
            streamsize = input.Length;
            lhash = streamsize;

            long i = 0;
            byte[] buffer = new byte[sizeof(long)];
            while (i < 65536 / sizeof(long) && (input.Read(buffer, 0, sizeof(long)) > 0))
            {
                i++;
                lhash += BitConverter.ToInt64(buffer, 0);
            }

            input.Position = Math.Max(0, streamsize - 65536);
            i = 0;
            while (i < 65536 / sizeof(long) && (input.Read(buffer, 0, sizeof(long)) > 0))
            {
                i++;
                lhash += BitConverter.ToInt64(buffer, 0);
            }
            byte[] result = BitConverter.GetBytes(lhash);
            Array.Reverse(result);
            return result;
        }

        public static string ToHexadecimal(byte[] bytes)
        {
            StringBuilder hexBuilder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                hexBuilder.Append(bytes[i].ToString("x2"));
            }
            return hexBuilder.ToString();
        }
    }
    #endregion

    #region Subtitle class
    /// <summary>
    /// Subtitle object retrieved from OpenSubtitles.
    /// </summary>
    /// <remarks>
    /// Does not contain the actual subtitles, only its metadata.
    /// </remarks>
    public sealed class Subtitle
    {
        public string Format { get; set; }

        public int ID { get; set; }
        public string Hash { get; set; }
        public string Filename { get; set; }
        public string Language { get; set; }

        public int MovieID { get; set; }
        public int ImdbID { get; set; }
        public string MovieName { get; set; }
        public double MovieRating { get; set; }
        public int MovieYear { get; set; }

        public double Rating { get; set; }

        public int DownloadsCount { get; set; }

        public Uri DownloadLink { get; set; }

        public bool HearingImpaired { get; set; }
    }
    #endregion
}
