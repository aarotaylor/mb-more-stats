using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Windows.Forms;
using Lastfm.Services;
using Lastfm.Scrobbling;
using Lastfm;
using System.IO;

namespace MusicBeePlugin
{
    public partial class ObsessedForm : Form
    {
        public ObsessedForm(Plugin.MusicBeeApiInterface pApi)
        {
            // C:\MusicBee\AppData\LastFmScrobbles.log | Scrobble log location
            InitializeComponent();

            // --------Initializing lastfm api--------
            string apikey = app.Default.API_KEY;//ConfigurationManager.AppSettings["API_KEY"];
            string secretkey = app.Default.SECRET_KEY;//ConfigurationManager.AppSettings["SECRET_KEY"];
            Session session = new Session(apikey, secretkey);
            string usr = pApi.Setting_GetLastFmUserId();

            // -------- URL Construction --------
            string root = "http://ws.audioscrobbler.com/2.0/?";
            
            string m_topTracks = "method=user.gettoptracks&user=" + usr;
            string m_topAlbums = "method=user.gettopalbums&user=" + usr;

            // returns date data in addition to scrobble info. in the @attr header there is a totalPages attribute. 
            // Use this to loop through all pages to collect all data.
            string m_recentTracks = "method=user.getrecenttracks&user=" + usr;
            string limit = "&limit=200";
            string pages = "&page=";
            int pageNum = 1;
            string tail = "&api_key=" + apikey + "&format=json";
            // ---------------------------------------

            string url = root+m_recentTracks+limit+pages+pageNum+tail;

            var request = (HttpWebRequest)WebRequest.Create(url);

            var response = (HttpWebResponse)request.GetResponse();

            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            

            // Use QueryFilesEx to get the fileURLs
            pApi.Library_QueryFilesEx("domain=SelectedFiles", out String[] files); // files contains the fileURL for a selected track(s)

            string[] lines = System.IO.File.ReadAllLines(@"C:\MusicBee\AppData\LastFmScrobble.log");

            string artist = pApi.Library_GetFileTag(files[0], Plugin.MetaDataType.Artist);
            string title = pApi.Library_GetFileTag(files[0], Plugin.MetaDataType.TrackTitle);
            int plays = Int32.Parse(pApi.Library_GetFileTag(files[0], (Plugin.MetaDataType)14));

            string added = pApi.Library_GetFileProperty(files[0], Plugin.FilePropertyType.DateAdded); //Date the selected track was added to the library


            string track = artist + " - " + title; // Consolidation for matching in the scrobble log

            List<DateTime> history = filter(lines, track, plays); // history contains the history of scrobbles for a given track represented in unix time.

            if (history.Count <= 1)
            {
                label1.Text = "Not enough scrobbles for this track";
            }
            else
            {
                label1.Text = responseString+"";// +" | "+obsession(history, added, title, artist, plays);
            }
        }

        // Currently goes off of 70% of total plays
        private string obsession(List<DateTime> history, string added, string track, string artist, int plays)
        {
            double thresh = 0.7;

            DateTime first = history[0];
            DateTime last = history[history.Count - 1];
            int index = (int)Math.Ceiling(thresh * history.Count) - 1; // element of the list to cutoff

            DateTime cutoff = history[index];

            TimeSpan diff = cutoff.Subtract(first); // Gets the time between the first scrobble and cutoff point

            string text = track + " by " + artist + " was first scrobbled on " + first +
                ".\n It reached " + thresh * 100 + "% of its current playcount, " + plays + ", in " + FormatTimeSpan(diff) +
                "\n It has been in your library since " + added;

            return text;
        }

        private DateTime grabDate(string scrobble)
        {

            int index = scrobble.IndexOf("M ");
            string date = scrobble.Substring(0, index + 1);


            DateTime loadedDate = DateTime.Parse(date);

            return loadedDate;
        }

        // returns a List with only scrobbles of the intended track. Scrobbles contain the date
        // full scrobble file, desired track, and desired track playcount are input
        public List<DateTime> filter(string[] scrobbles, string track, int plays)
        {
            var output = new List<DateTime>();
            int j = 0;
            for (int i = 0; i < scrobbles.Length; i++)
            {
                if (scrobbles[i].IndexOf(track) > 0 && scrobbles[i][0] != ' ')
                {
                    j++;
                    output.Add(grabDate(scrobbles[i]));
                }
                if (output.Count == plays)
                {
                    break;
                }

            }
            return output;
        }
        // Converts dates to unix. Sourced from here: https://www.fluxbytes.com/csharp/convert-datetime-to-unix-time-in-c/
        public static long ConvertToUnixTime(DateTime datetime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return (long)(datetime - sTime).TotalSeconds;
        }

        // Makes TimeSpan more readable. Sourced from this comment: https://stackoverflow.com/a/41966914
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            Func<Tuple<int, string>, string> tupleFormatter = t => $"{t.Item1} {t.Item2}{(t.Item1 == 1 ? string.Empty : "s")}";
            var components = new List<Tuple<int, string>>
        {
            Tuple.Create((int) timeSpan.TotalDays, "day"),
            Tuple.Create(timeSpan.Hours, "hour"),
            Tuple.Create(timeSpan.Minutes, "minute"),
            Tuple.Create(timeSpan.Seconds, "second"),
        };

            components.RemoveAll(i => i.Item1 == 0);

            string extra = "";

            if (components.Count > 1)
            {
                var finalComponent = components[components.Count - 1];
                components.RemoveAt(components.Count - 1);
                extra = $" and {tupleFormatter(finalComponent)}";
            }

            return $"{string.Join(", ", components.Select(tupleFormatter))}{extra}";
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
