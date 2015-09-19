using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using HAP = HtmlAgilityPack;

namespace CounterScope
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        // To prevent C# glitch stuff
        List<string> hist = new List<string>();
        // vector of match ID's when list is populated
        List<int> matchIDs = new List<int>();

        // Slang to HLTV name dictionary
        Dictionary<string, string> transHLTV = new Dictionary<string, string>();

        // Boolean to manually input links to parse
        bool manualParse = false;

        private void Form1_Load(object sender, EventArgs e)
        {
            WriteToLog("Setting up Dictionary of team names for HLTV.");
            TranslateToHLTV();
            WriteToLog("Dictionary set");
        }

        /// <summary>
        /// - Set up the pairs for the teams from Reddit to HLTV
        /// </summary>
        private void TranslateToHLTV()
        {
            string[] keys;
            string[] vals;

            if (File.Exists("csglTeams.txt"))
            {
                keys = new string[File.ReadLines("csglTeams.txt").Count()];

                var file = File.OpenText("csglTeams.txt");

                for(int i = 0; i < keys.Count(); i++)
                    keys[i] = file.ReadLine();

                WriteToLog("CSGL Names text file found!");
            }
            else
                keys = new string[100];

            if (File.Exists("hltvTeams.txt"))
            {
                vals = new string[File.ReadLines("hltvTeams.txt").Count()];

                var file = File.OpenText("hltvTeams.txt");

                for (int i = 0; i < vals.Count(); i++)
                    vals[i] = file.ReadLine();

                WriteToLog("HLTV Names text file found!");

            }
            else
                vals = new string[100];

            if (vals.Count() != keys.Count())
                MessageBox.Show("The current key/value pool sizes don't match. Keys: " + keys.Count() + " Vals: " + vals.Count());

            for (int i = 0; i < keys.Count(); i++)
                transHLTV.Add(keys[i], vals[i]);
        }

        string GetTeamName(string key)
        {
            if(transHLTV[key] != null)
            {
                return transHLTV[key];
            }

            return "";
        }

        private void WriteToLog(string text)
        {
            textBoxLog.AppendText(text + "\r\n");
        }
        /////////////////////////////////////////////////////////////////////
        // Parse Match                                                      |
        // [in] matchID - The match's ID.                                   |
        // - Parse and scrape off the match's page given an ID              |
        /////////////////////////////////////////////////////////////////////
        private void ParseMatch(int matchID)
        {
            // Clear the colors
            gbTeamTwo.BackColor = gbTeamOne.BackColor = SystemColors.Control;

            // Set the fonts back
            lbl_returnTeamOne.Font = new Font("Microsoft Sans Serif", 12, FontStyle.Bold);
            lbl_returnTeamTwo.Font = new Font("Microsoft Sans Serif", 12, FontStyle.Bold);

            WriteToLog("Navigating to Match #" + matchID.ToString());

            // Go to the match page
            webBrowser1.Navigate("http://csgolounge.com/match?m=" + matchID.ToString());

            // Wait until we load it
            while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                Application.DoEvents();

            WriteToLog("Match #" + matchID.ToString() + " Loaded.");

            if (hist.Contains(webBrowser1.Url.ToString()))
                return;

            hist.Add(webBrowser1.Url.ToString());

            HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
            HAP.HtmlDocument doc = grabWeb.Load(webBrowser1.Url.ToString());

            // Grab the time left until match
            HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes("//div[@style='display: flex']");
            lbl_TimeLeft.Text = d[0].ChildNodes[1].InnerText;

            // Grab the special note if it exists
            HAP.HtmlNodeCollection special = doc.DocumentNode.SelectNodes("//div[@style='padding: 0.5em;background: #444;line-height: 1.5rem;margin: 1rem;border: 1px solid #333;text-align: center;']");
            if(special != null)
            {
                lbl_specialNote.Visible = true;
                lbl_specialNote.Text = special[0].ChildNodes[0].InnerText.Replace("\r\n            ", "");
                WriteToLog("!!! There's a special note for this match. Please read it !!!");
            }

            // Grab if the match is LIVE right now
            HAP.HtmlNodeCollection live = doc.DocumentNode.SelectNodes("//div[@id='stream']");
            if(live != null)
            {
                lbl_Live.Visible = true;
                WriteToLog("!!! Match is LIVE! !!!");

                // TODO: Decide if it's Hitbox or Twitch or something else

                // Twitch
                HAP.HtmlNodeCollection flashPopup = doc.DocumentNode.SelectNodes("//object[@type='application/x-shockwave-flash']");

                if (flashPopup != null)
                {
                    string streamURL = flashPopup[0].Attributes[4].Value;

                    DialogResult result = MessageBox.Show("Would you like to pop out the English stream in a new window?", "LIVE Match!", MessageBoxButtons.YesNo);

                    if (result == System.Windows.Forms.DialogResult.Yes)
                        System.Diagnostics.Process.Start(streamURL);
                }
                else
                {
                    MessageBox.Show("It seems that the stream isn't Twitch. This will be fixed in later builds.", "LIVE Match Error - Not Twitch.tv", MessageBoxButtons.OK);
                }
            }

            // Grab the 'best of'
            lbl_bestOf.Text = d[0].ChildNodes[3].InnerText;

            // Grab the names and odds of the teams
            HAP.HtmlNodeCollection teamA = doc.DocumentNode.SelectNodes("//span[@style='width: 45%; float: left; text-align: right']");
            HAP.HtmlNodeCollection teamB = doc.DocumentNode.SelectNodes("//span[@style='width: 45%; float: left; text-align: left']");

            lbl_teamOneName.Text = teamA[0].ChildNodes[3].InnerText;
            lbl_teamOneChance.Text = teamA[0].ChildNodes[5].InnerText;

            lbl_teamTwoName.Text = teamB[0].ChildNodes[3].InnerText;
            lbl_teamTwoChance.Text = teamB[0].ChildNodes[5].InnerText;

            // Change the name of the program
            this.Text = "CounterScope v2 BETA - " +  lbl_teamOneName.Text + " vs. " + lbl_teamTwoName.Text + " - " + lbl_TimeLeft.Text;

            // Grab the pictures of the teams
            string teamOnePic = teamA[0].ChildNodes[1].Attributes[1].Value.Replace("float: right; margin-left: 10%; background: url('", "");
            teamOnePic = teamOnePic.Replace("')", "");

            string teamTwoPic = teamB[0].ChildNodes[1].Attributes[1].Value.Replace("float: left; margin-right: 10%; background: url('", "");
            teamTwoPic = teamTwoPic.Replace("')", "");

            pbTeamOne.ImageLocation = teamOnePic;
            pbTeamTwo.ImageLocation = teamTwoPic;

            // Grab value returns
            HAP.HtmlNodeCollection returns = doc.DocumentNode.SelectNodes("//div[@class='full']");
            string returnsA = returns[0].ChildNodes[3].InnerText.Replace("\r\nValue", "");
            returnsA = returnsA.Replace("0 for 0                ", "");
            lbl_returnTeamOne.Text = returnsA;

            string returnsB = returns[0].ChildNodes[5].InnerText.Replace("\r\nValue", "");
            returnsB = returnsB.Replace("0 for 0                ", "");
            lbl_returnTeamTwo.Text = returnsB;


            // Change for ranges
            if (returnsA.Contains("to"))
            {
                lbl_returnTeamOne.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
                lbl_returnTeamOne.Location = new Point(13, 77);
            }

            if (returnsB.Contains("to"))
            {
                lbl_returnTeamTwo.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
                lbl_returnTeamTwo.Location = new Point(125, 82);
            }

            int teamWon = 0;
            // Check what team won
            if (lbl_teamOneName.Text.Contains("(win)"))
                teamWon = 1;
            if (lbl_teamTwoName.Text.Contains("(win"))
                teamWon = 2;

            int chanceOne = Convert.ToInt32(lbl_teamOneChance.Text.Replace("%", ""));
            int chanceTwo = Convert.ToInt32(lbl_teamTwoChance.Text.Replace("%", ""));

            // Visual feedback for favorite/underdog OR winner/loser
            if (teamWon == 0)
            {
                if ((chanceOne > chanceTwo))
                    gbTeamOne.BackColor = Color.PaleGreen;
                else if ((chanceOne < chanceTwo))
                    gbTeamTwo.BackColor = Color.PaleGreen;
            }
            else if (teamWon == 1)
            {
                gbTeamOne.BackColor = Color.ForestGreen;
                gbTeamTwo.BackColor = Color.LightPink;
            }
            else if (teamWon == 2)
            {
                gbTeamTwo.BackColor = Color.ForestGreen;
                gbTeamOne.BackColor = Color.LightPink;
            }

            //// Is the match over
            //bool isMatchOver = false;

            //// What team won (1 or 2)
            //int teamWon = -1;

            //// Clean up some constant requests
            //if (hist.Contains(webBrowser1.Url.ToString()))
            //    return;

            //hist.Add(webBrowser1.Url.ToString());

            //// Grab all the 'span' div id's
            //HtmlElement he = webBrowser1.Document.GetElementById("main");
            //HtmlElementCollection hec = he.GetElementsByTagName("span");

            //// Save the team's name and percent.
            //string teamOneNameAndPercent = hec[0].InnerText.ToString();
            //string teamTwoNameAndPercent = hec[2].InnerText.ToString();

            //// Check what team won
            //if (teamOneNameAndPercent.Contains("(win)"))
            //    teamWon = 1;
            //if (teamTwoNameAndPercent.Contains("(win"))
            //    teamWon = 2;

            //// If a team won, the match is over.
            //if (teamWon != -1)
            //    isMatchOver = true;

            //WriteToLog("Scaping Match #" + matchID.ToString());

            ////  -- Grab the name and odds --
            //string pattern = @"(.+)[\r\n]*(\d{1,2})%";

            //Match m = Regex.Match(teamOneNameAndPercent, pattern);
            //if (m.Success)
            //{
            //    lbl_teamOneName.Text = m.Groups[1].ToString();
            //    lbl_teamOneChance.Text = m.Groups[2].ToString() + "%";
            //}

            //m = Regex.Match(teamTwoNameAndPercent, pattern);
            //if (m.Success)
            //{
            //    lbl_teamTwoName.Text = m.Groups[1].ToString();
            //    lbl_teamTwoChance.Text = m.Groups[2].ToString() + "%";
            //}

            //// -- Grab the pictures of the teams --
            //pattern = ".*?(\\(.*\\))";
            //m = Regex.Match(hec[0].InnerHtml, pattern);
            //if (m.Success)
            //{
            //    String teamPic = m.Groups[1].ToString();
            //    teamPic = teamPic.Replace('(', ' ').Replace(')', ' ');
            //    pbTeamOne.ImageLocation = teamPic;
            //}

            //m = Regex.Match(hec[2].InnerHtml, pattern);
            //if (m.Success)
            //{
            //    String teamPic = m.Groups[1].ToString();
            //    teamPic = teamPic.Replace('(', ' ').Replace(')', ' ');
            //    pbTeamTwo.ImageLocation = teamPic;
            //}


            //// Grab the time remaining to bet
            //he = webBrowser1.Document.GetElementById("main");
            //hec = he.GetElementsByTagName("div");

            //lbl_TimeLeft.Text = hec[1].InnerText;

            //// Grab best of
            //he = webBrowser1.Document.GetElementById("main");
            //hec = he.GetElementsByTagName("div");

            //lbl_bestOf.Text = hec[2].InnerText;

            //// Grab the return values
            //he = webBrowser1.Document.GetElementById("main");
            //hec = he.GetElementsByTagName("div");

            //// Check if we have a range
            //if (hec[7].InnerText.Contains("to"))
            //{
            //    lbl_returnTeamOne.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
            //    lbl_returnTeamOne.Location = new Point(13, 77);
            //}
            //if (hec[9].InnerText.Contains("to"))
            //{
            //    lbl_returnTeamTwo.Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
            //    lbl_returnTeamTwo.Location = new Point(122, 82);
            //}

            //lbl_returnTeamOne.Text = hec[7].InnerText.Replace("Value\r\n", "");
            //lbl_returnTeamTwo.Text = hec[9].InnerText.Replace("Value\r\n", "");

            //// Update the name of the program
            //this.Text = "CounterScope v2 BETA - " + lbl_teamOneName.Text + " v " + lbl_teamTwoName.Text;

            //// Show some visual feedback
            //int chanceOne = Convert.ToInt32(lbl_teamOneChance.Text.Replace("%", ""));
            //int chanceTwo = Convert.ToInt32(lbl_teamTwoChance.Text.Replace("%", ""));

            //if ((chanceOne > chanceTwo) && !isMatchOver)
            //    gbTeamOne.BackColor = Color.PaleGreen;
            //else if ((chanceOne < chanceTwo) && !isMatchOver)
            //    gbTeamTwo.BackColor = Color.PaleGreen;

            //if (teamWon == 1)
            //{
            //    gbTeamOne.BackColor = Color.ForestGreen;
            //    lbl_teamOneName.Location = new Point(14, 18);
            //}
            //else if (teamWon == 2)
            //{
            //    gbTeamTwo.BackColor = Color.ForestGreen;
            //    lbl_teamTwoName.Location = new Point(121, 18);
            //}

            lbl_status.Text = "CounterScope BETA - Match " + matchID.ToString() + " Loaded";

            WriteToLog("Match #" + matchID.ToString() + " Scraped");

        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("Created by Justin 'Chocolate' Mazzola");
        }

        /////////////////////////////////////////////////////////////////////
        // ClearEverything                                                  |
        // - Set everything on the form, to nothing (default)               |
        /////////////////////////////////////////////////////////////////////
        private void ClearEverything()
        {
            // Clear all labels
            lbl_bestOf.Text =
            lbl_teamOneName.Text =
            lbl_teamTwoName.Text =
            lbl_returnTeamOne.Text =
            lbl_returnTeamTwo.Text =
            lbl_teamOneChance.Text =
            lbl_teamTwoChance.Text =
            lbl_TimeLeft.Text = String.Empty;

            // Clear all textboxes
            txt_hltvLink.Text =
            txt_redditLink.Text = String.Empty;

            // Clear all visible texts
            lbl_Live.Visible =
            lbl_specialNote.Visible = false;

            // Disable parsing
            cb_swappedTeams.Enabled =
            btn_manualParse.Enabled = false;

            txt_redditLink.ReadOnly =
            txt_hltvLink.ReadOnly = true;

            manualParse = false;

            // Set all the background colors to default
            gbTeamTwo.BackColor = gbTeamOne.BackColor = SystemColors.Control;

            // Clear all listboxes/views
            lv_matches.Items.Clear();
            lv_teamOneQuickStats.Items.Clear();
            lv_teamTwoQuickStats.Items.Clear();

            //for (int i = 0; i < 5; i++)
            //{
            //    lv_teamOneQuickStats.Items[i].SubItems.Clear();
            //    lv_teamTwoQuickStats.Items[i].SubItems.Clear();
            //}

            // Set the name of the application
            this.Text = "CounterScope v2 BETA";
        }


        /////////////////////////////////////////////////////////////////////
        // Refresh Matches                                                  |
        // - Parse and scrape off all of the current matches off CSGL       |
        /////////////////////////////////////////////////////////////////////
        private void refreshMatches_Click(object sender, EventArgs e)
        {

            WriteToLog("Navigating to CSGOLounge..");
            // Grab the current matches via the home page of the screen
            webBrowser1.Navigate("http://csgolounge.com");

            // Clear all our form elements
            ClearEverything();

            // Wait for the browser to be finished loading the page
            while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                Application.DoEvents();

            WriteToLog("CSGOLounge Loaded.");
            WriteToLog("Scraping match list.");

            HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
            HAP.HtmlDocument doc = grabWeb.Load(webBrowser1.Url.ToString());
            HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes("//article[@class='standard' and @id='bets']");

            HAP.HtmlNode currentMatchNode = d[0].ChildNodes[1];
            HAP.HtmlNodeCollection allMatches = currentMatchNode.SelectNodes("//div[@class='matchmain']");
            HAP.HtmlNodeCollection ladderMatch = currentMatchNode.SelectNodes("//div[@class='matchheader']");

            // Should be 0x15
            for (int i = 0; i < allMatches.Count; i++)
            {
                // Grab the team names facing off
                string teamNameOne = allMatches[i].ChildNodes[3].ChildNodes[1].ChildNodes[1].ChildNodes[1].ChildNodes[3].ChildNodes[0].InnerText;
                string teamNameTwo = allMatches[i].ChildNodes[3].ChildNodes[1].ChildNodes[1].ChildNodes[5].ChildNodes[3].ChildNodes[0].InnerText;

                // Grab the ladder
                string ladder = ladderMatch[i].ChildNodes[3].InnerText;

                // Grab the match ID
                string matchID = allMatches[i].ChildNodes[3].ChildNodes[1].ChildNodes[1].Attributes[0].Value.Replace("match?m=", "");
                matchIDs.Add(Convert.ToInt32(matchID));


                // Add the names to the match list
                lv_matches.Items.Add(teamNameOne + " vs. " + teamNameTwo);
                // Add the ladder
                lv_matches.Items[i].SubItems.Add(ladder);

                // Grab the match status
                // LightPink = Match is over | LightYellow = Tied | PaleGreen = Available | Gray = Else
                string status = ladderMatch[i].ChildNodes[1].InnerText.Replace("  \r\n", "");

                if (status.Contains("from"))
                    lv_matches.Items[i].BackColor = Color.PaleGreen;
                else if (status.Contains("ago"))
                    lv_matches.Items[i].BackColor = Color.LightPink;
            }


            //// Parse the 'bets' section for every match
            //for (int i = 0; i < hec[1].Children.Count; i++)
            //{
            //    // Grab the teams facing off
            //    string faceOff = hec[1].Children[i].Children[1].InnerText;

            //    // Grab only the team names
            //    //string pattern = "((?:[A-z][A-z]+)).*?((?:[A-z][A-z]+))";
            //    // (.+)  - one or more chars != "<br>"   [Group 1]
            //    // [\r\n] - any character inside
            //    // * - 0 or more
            //    // (\d{1,2}) - Single or double digit
            //    // %vs 

            //    string pattern = @"(.+)[\r\n]*(\d{1,2})%vs(.+)[\r\n]*(\d{1,2})%";

            //    Match m = Regex.Match(faceOff, pattern);
            //    if (m.Success) faceOff = m.Groups[1].ToString() + " vs " + m.Groups[3].ToString();

            //    // Grab the match ID
            //    string matchHtml = hec[1].Children[i].Children[1].Children[0].InnerHtml;
            //    pattern = ".*?(match)(\\?)(m)(=)(\\d+)";

            //    m = Regex.Match(matchHtml, pattern);
            //    if (m.Success) matchIDs.Add(Convert.ToInt32(m.Groups[5].ToString()));

            //    // Add it to our matches
            //    lv_matches.Items.Add(faceOff);

            //    string ladderCondition = hec[1].Children[i].Children[0].InnerHtml;
            //    // Give some feedback if the match is over or not
            //    if (matchHtml.Contains("won.png"))
            //        lv_matches.Items[i].BackColor = Color.LightPink;

            //    else if (ladderCondition.ToLower().Contains("postponed") 
            //        || ladderCondition.ToLower().Contains("cancelled") 
            //        || ladderCondition.ToLower().Contains("rescheduled") 
            //        || ladderCondition.ToLower().Contains("forfeit") 
            //        || ladderCondition.ToLower().Contains("canceled") )
            //        lv_matches.Items[i].BackColor = Color.LightGray;

            //    else if (ladderCondition.Contains("15-15"))
            //        lv_matches.Items[i].BackColor = Color.LightYellow;

            //    else
            //        lv_matches.Items[i].BackColor = Color.PaleGreen;

            //    // Grab the ladder
            //    string stuff = hec[1].Children[i].Children[0].InnerText;

            //    if (stuff.Contains("hours ago"))
            //        stuff = stuff.Replace("hours ago", "");

            //    else if (stuff.Contains("days ago"))
            //        stuff = stuff.Replace("days ago", "");

            //    else if (stuff.Contains("hour from now"))
            //        stuff = stuff.Replace("hour from now", "");

            //    else if (stuff.Contains("hours from now"))
            //        stuff = stuff.Replace("hours from now", "");

            //    else if (stuff.Contains("day from now"))
            //        stuff = stuff.Replace("day from now", "");

            //    else if (stuff.Contains("days from now"))
            //        stuff = stuff.Replace("days from now", "");

            //    pattern = "((?:[A-z][A-z]+))";
            //    m = Regex.Match(stuff, pattern);
            //    if (m.Success) lv_matches.Items[i].SubItems.Add(m.Groups[1].ToString());

            //}

            WriteToLog("CSGOLounge Homepage scraped.");

            // Select the first to show the first match listed
            //lv_matches.Items[0].Selected = true;

            // Update status
            lbl_status.Text = "CounterScope BETA - Homepage Loaded";

        }

        /////////////////////////////////////////////////////////////////////
        // GrabQuickPlayerStats                                             |
        // [in] teamOneVsTeamTwo -The string to search in CSGOBetting       |
        // - Parse and scrape off player's quick stats via web crawling     |
        /////////////////////////////////////////////////////////////////////
        private int GrabQuickPlayerStats(string teamOneVsTeamTwo, bool swapped)
        {
            try
            {
                // TODO: Make it so that if the current game is upcoming, look for 'Match' flair
                // If it's ended, look for 'Finished' flair
                // If it's rescheduled, look for 'Postponed' flair
                WriteToLog("Navigating to Reddit..");

                string url = String.Empty;
                Color matchColor = lv_matches.SelectedItems[0].BackColor;

                if (matchColor == Color.PaleGreen)
                    url = "http://www.reddit.com/r/csgobetting/search?q=flair%3A%27%22match%22%27&sort=new&restrict_sr=on";
                else if (matchColor == Color.LightPink || matchColor == Color.LightYellow)
                    url = "http://www.reddit.com/r/csgobetting/search?q=flair%3A%27%22finished%22%27&sort=new&restrict_sr=on";
                else if (matchColor == Color.LightGray)
                    url = "http://www.reddit.com/r/csgobetting/search?q=flair%3A%27%22postponed%22%27&sort=new&restrict_sr=on";
                else
                    return 1000;


                // Go to the CSGO Betting subreddit
                webBrowser1.Navigate(url);

                // Wait until the page loads
                while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                    Application.DoEvents();

                WriteToLog("CSGOBetting Reddit Loaded");

                HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
                HAP.HtmlDocument doc = grabWeb.Load(webBrowser1.Url.ToString());
                HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes("//div[@id='siteTable' and @class='sitetable linklisting']");


                string redditPage = "reddit.com";
                // Load all the threads to look for the match
                for (int i = 0; i < d[0].ChildNodes.Count; i += 2)
                {
                    // If we have a match up with our search string
                    if (d[0].ChildNodes[i].ChildNodes[3].ChildNodes[0].InnerText.ToLower().Contains(teamOneVsTeamTwo.ToLower()))
                    {
                        redditPage += d[0].ChildNodes[i].ChildNodes[3].ChildNodes[0].ChildNodes[1].Attributes[1].Value;
                        break;
                    }
                }

                if (!redditPage.Contains("/r/"))
                {
                    WriteToLog("Reddit page couldn't be located.");
                    return -1;
                }

                txt_redditLink.Text = redditPage;
                webBrowser1.Navigate(redditPage);

                while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                    Application.DoEvents();

                WriteToLog("Reddit page loaded at " + redditPage);

                // Grab HLTV link [ESEA later]

                grabWeb = new HAP.HtmlWeb();
                doc = grabWeb.Load(webBrowser1.Url.ToString());
                d = doc.DocumentNode.SelectNodes("//div[@class='md']");

                string hltvPage = d[1].ChildNodes[0].ChildNodes[2].Attributes[0].Value;

                WriteToLog("HLTV page loaded at " + hltvPage);

                txt_hltvLink.Text = hltvPage;
                webBrowser1.Navigate(hltvPage);

                while (webBrowser1.ReadyState != WebBrowserReadyState.Complete)
                    Application.DoEvents();

                // Make a new web document for HAP to blissfully parse and scrape~
                grabWeb = new HAP.HtmlWeb();
                doc = grabWeb.Load(webBrowser1.Url.ToString());

                // Make a collection looking for everything in html with:
                // <a class="nolinkstyle"> 
                d = doc.DocumentNode.SelectNodes(@"//a[@class=""nolinkstyle""]");

                WriteToLog("Grabbing Team URLs");

                // Grab the links for later usage, that's it.
                string teamOneURL = d[0].GetAttributeValue("href", null);
                string teamTwoURL = d[1].GetAttributeValue("href", null);

                // Grab every player and their page
                d = doc.DocumentNode.SelectNodes(@"//div[@class=""text-center""]");

                string[] playerNames = new string[10];
                string[] playerPages = new string[10];

                for (int i = 0; i < d.Count; i++)
                {
                    playerNames[i] = d[i].ChildNodes[2].InnerText;
                    playerPages[i] = d[i].ChildNodes[2].Attributes[0].Value.ToString();

                    // Hacky way to add their names to the listview if the teams are swapped or not
                    if (i < 5)
                    {
                        ListViewItem duh = swapped ? lv_teamTwoQuickStats.Items.Add(playerNames[i]) : lv_teamOneQuickStats.Items.Add(playerNames[i]);
                    }
                    else
                    {
                        ListViewItem duh = swapped ? lv_teamOneQuickStats.Items.Add(playerNames[i]) : lv_teamTwoQuickStats.Items.Add(playerNames[i]);
                    }

                    continue;
                }

                WriteToLog("Starting Thread to Scrape Player Names");


                // Create a thread to take care of this
                string[] playerKDs = new string[10];
                string[] playerRatios = new string[10];
                Thread thread = new Thread(() => ScrapePlayers(playerNames, playerPages, playerKDs, playerRatios));
                thread.Start();
                thread.Join();

                for (int i = 0; i < 5; i++)
                {
                    if (swapped)
                    {
                        lv_teamTwoQuickStats.Items[i].SubItems.Add(playerKDs[i]);

                        ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                        wever.Text = playerRatios[i];
                        lv_teamTwoQuickStats.Items[i].SubItems.Add(wever);
                    }
                    else
                    {
                        lv_teamOneQuickStats.Items[i].SubItems.Add(playerKDs[i]);

                        ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                        wever.Text = playerRatios[i];
                        lv_teamOneQuickStats.Items[i].SubItems.Add(wever);
                    }
                }
                for (int i = 5; i < 10; i++)
                {
                    if (swapped)
                    {
                        lv_teamOneQuickStats.Items[i - 5].SubItems.Add(playerKDs[i]);

                        ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                        wever.Text = playerRatios[i];
                        lv_teamOneQuickStats.Items[i - 5].SubItems.Add(wever);
                    }
                    else
                    {
                        lv_teamTwoQuickStats.Items[i - 5].SubItems.Add(playerKDs[i]);

                        ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                        wever.Text = playerRatios[i];
                        lv_teamTwoQuickStats.Items[i - 5].SubItems.Add(wever);
                    }
                }

                WriteToLog("Players successfully scraped.. Idle.");
                return 1;
            }
            catch (Exception x)
            {
                // MessageBox.Show(x.Message, "Exception :(", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return -3;
            }

        }

        private void ScrapePlayers(string[] playerNames, string[] playerPages, string[] playerKDs, string[] playerRatios)
        {
            HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
            HAP.HtmlDocument doc;
            // Scrape every player's KD and Rating
            for (int i = 0; i < playerPages.Count(); i++)
            {
                playerPages[i] = "http://www.hltv.org" + playerPages[i];
                doc = grabWeb.Load(playerPages[i].ToString());

                //UpdateLabelCrossThread(String.Format("CounterScope BETA - Scraping {0}'s page", playerNames[i]));

                HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes(@"//div[@class=""covSmallHeadline""]");
                playerKDs[i] = d[19].InnerText;
                playerRatios[i] = d[31].InnerText;
            }

        }

        private void UpdateLabelCrossThread(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)
                    delegate { UpdateLabelCrossThread(text); }
                    );
                return;
            }

            lbl_status.Text = text;
        }

        private void lv_matches_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Just do a quick check if we even grabbed match IDs
            if (matchIDs.Count <= 0)
            {
                MessageBox.Show("There are zero match IDs scraped.", "Zero Match IDs");
                return;
            }

            // Clear out the history to make speeds faster and prevent errors
            hist.Clear();

            // Clear out our quick stats
            lv_teamOneQuickStats.Items.Clear();
            lv_teamTwoQuickStats.Items.Clear();

            // Clear out live and special notes
            lbl_specialNote.Visible =
            lbl_Live.Visible = false;

            // Disable UI and usability for manually parsing matches
            txt_redditLink.Text =
            txt_hltvLink.Text = "No link loaded..";

            cb_swappedTeams.Enabled =
            btn_manualParse.Enabled = false;

            manualParse = false;

            // Clear debug
            textBoxLog.Clear();

            // Parse the selected match
            if (lv_matches.SelectedIndices.Count > 0)
                ParseMatch(matchIDs[lv_matches.SelectedIndices[0]]);

            string teamOneString = lbl_teamOneName.Text;
            string teamTwoString = lbl_teamTwoName.Text;

            if (teamOneString.Contains("(win)"))
                teamOneString = teamOneString.Replace(" (win)", "");
            else if (teamTwoString.Contains("(win)"))
                teamTwoString = teamTwoString.Replace(" (win)", "");

            // Build our parameter for searching Reddit using 'vs' or 'vs.' and swapping the team names

            // TeamOne vs. TeamTwo
            string threadTitle = teamOneString + " vs. " + teamTwoString;
            // Grab the player's quick stats
            if (GrabQuickPlayerStats(threadTitle, false) == 1)
                return;

            // TeamOne vs TeamTwo
            threadTitle = teamOneString + " vs " + teamTwoString;
            if (GrabQuickPlayerStats(threadTitle, false) == 1)
                return;

            // TeamTwo vs. TeamOne
            threadTitle = teamTwoString + " vs. " + teamOneString;
            if (GrabQuickPlayerStats(threadTitle, true) == 1)
                return;

            // TeamTwo vs TeamOne
            threadTitle = teamTwoString + " vs " + teamOneString;
            if (GrabQuickPlayerStats(threadTitle, true) == 1)
                return;

            // TransHLTV[TeamOne] vs TransHLTV[TeamTwo]
            threadTitle = transHLTV[teamOneString.ToLower()] + " vs " + transHLTV[teamTwoString.ToLower()];
            if (GrabQuickPlayerStats(threadTitle, false) == 1)
                return;

            // TransHLTV[TeamOne] vs. TransHLTV[TeamTwo]
            threadTitle = transHLTV[teamOneString.ToLower()] + " vs. " + transHLTV[teamTwoString.ToLower()];
            if (GrabQuickPlayerStats(threadTitle, false) == 1)
                return;

            // TransHLTV[TeamTwo] vs. TransHLTV[TeamOne]
            threadTitle = transHLTV[teamTwoString.ToLower()] + " vs. " + transHLTV[teamOneString.ToLower()];
            if (GrabQuickPlayerStats(threadTitle, true) == 1)
                return;

            // TransHLTV[TeamTwo] vs TransHLTV[TeamOne]
            threadTitle = transHLTV[teamTwoString.ToLower()] + " vs " + transHLTV[teamOneString.ToLower()];
            if (GrabQuickPlayerStats(threadTitle, true) == 1)
                return;

            manualParse = true;
            txt_hltvLink.Text = txt_redditLink.Text = "Unable to grab link. Please manually either link in these textboxes.";
            MessageBox.Show("Error grabbing reddit thread for Quick Stats.\nPossible causes: Thread is mispelled or simply doesn't exist.\n\nPlease manually enter in the links for the reddit page or hltv page to parse your results.");
        }

        private void webBrowser1_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e)
        {
            progressBar.Maximum = (int)e.MaximumProgress;

            // Prevent OOR exception
            if (e.CurrentProgress == -1)
                progressBar.Value = 0;
          //  else
                // Set the progress to the web's progress
               // progressBar.Value = (int)e.CurrentProgress;
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Created by Justin 'Chocolate' Mazzola.\nFollow him at @xichocolate on Twitter.\n\nSpecial Thanks to HAP [HtmlAgilityPack]", "CounterScope v2 BETA");
        }

        private void txt_redditLink_TextChanged(object sender, EventArgs e)
        {
            if (manualParse)
            {
                txt_redditLink.ReadOnly = false;

                btn_manualParse.Enabled =
                cb_swappedTeams.Enabled = true;
            }
        }

        private void txt_hltvLink_TextChanged(object sender, EventArgs e)
        {
            if (manualParse)
            {
                txt_hltvLink.ReadOnly = false;

                btn_manualParse.Enabled =
                cb_swappedTeams.Enabled = true;
            }
        }

        private void btn_TeamTrack_Click(object sender, EventArgs e)
        {
            // Note: Use gosugamers.net to grab information (most cleanest and reliable)
            string url = "google.com";
            OpenTeamInformation(url);
        }

        private void OpenTeamInformation(string gosuTeamURL)
        {

        }

        private void btn_manualParse_Click(object sender, EventArgs e)
        {
            if ((txt_redditLink.Text != String.Empty && txt_hltvLink.Text != String.Empty))
            {
                MessageBox.Show("Either nothing was filled out properly or both were. Please select ONE site to scrape.", "Manually Parse Error", MessageBoxButtons.OK);
            }
            else if(txt_hltvLink.Text != String.Empty && txt_hltvLink.Text.Contains("hltv"))
            {
                ManuallyParseHLTVPage(txt_hltvLink.Text, cb_swappedTeams.Checked);
            }
            else if (txt_redditLink.Text != String.Empty && txt_redditLink.Text.Contains("reddit"))
            {
                ManuallyParseRedditPage(txt_redditLink.Text);
            }
        }

        void ManuallyParseRedditPage(string redditURL)
        {

            HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
            HAP.HtmlDocument doc = grabWeb.Load(redditURL);
            HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes(@"//div[@class='md']");

            string hltvPage = d[1].ChildNodes[0].ChildNodes[2].Attributes[0].Value;

            if(hltvPage.Contains("hltv"))
                ManuallyParseHLTVPage(hltvPage, cb_swappedTeams.Checked);
            else
            {
                MessageBox.Show("HLTV Page either isn't found or valid.", "Manually Parsing Reddit", MessageBoxButtons.OK);
                manualParse = true;
            }
        }

        void ManuallyParseHLTVPage(string hltvURL, bool swapped)
        {
            // Make a new web document for HAP to blissfully parse and scrape~
            HAP.HtmlWeb grabWeb = new HAP.HtmlWeb();
            HAP.HtmlDocument doc = grabWeb.Load(hltvURL);

            // Make a collection looking for everything in html with:
            // <a class="nolinkstyle"> 
            HAP.HtmlNodeCollection d = doc.DocumentNode.SelectNodes(@"//a[@class=""nolinkstyle""]");

            WriteToLog("Grabbing Team URLs");

            // Grab the links for later usage, that's it.
            string teamOneURL = d[0].GetAttributeValue("href", null);
            string teamTwoURL = d[1].GetAttributeValue("href", null);

            // Grab every player and their page
            d = doc.DocumentNode.SelectNodes(@"//div[@class=""text-center""]");

            string[] playerNames = new string[10];
            string[] playerPages = new string[10];

            for (int i = 0; i < d.Count; i++)
            {
                playerNames[i] = d[i].ChildNodes[2].InnerText;
                playerPages[i] = d[i].ChildNodes[2].Attributes[0].Value.ToString();

                // Hacky way to add their names to the listview if the teams are swapped or not
                if (i < 5)
                {
                    ListViewItem duh = swapped ? lv_teamTwoQuickStats.Items.Add(playerNames[i]) : lv_teamOneQuickStats.Items.Add(playerNames[i]);
                }
                else
                {
                    ListViewItem duh = swapped ? lv_teamOneQuickStats.Items.Add(playerNames[i]) : lv_teamTwoQuickStats.Items.Add(playerNames[i]);
                }

                continue;
            }

            WriteToLog("Starting Thread to Scrape Player Names");


            // Create a thread to take care of this
            string[] playerKDs = new string[10];
            string[] playerRatios = new string[10];
            Thread thread = new Thread(() => ScrapePlayers(playerNames, playerPages, playerKDs, playerRatios));
            thread.Start();
            thread.Join();

            for (int i = 0; i < 5; i++)
            {
                if (swapped)
                {
                    lv_teamTwoQuickStats.Items[i].SubItems.Add(playerKDs[i]);

                    ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                    wever.Text = playerRatios[i];
                    lv_teamTwoQuickStats.Items[i].SubItems.Add(wever);
                }
                else
                {
                    lv_teamOneQuickStats.Items[i].SubItems.Add(playerKDs[i]);

                    ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                    wever.Text = playerRatios[i];
                    lv_teamOneQuickStats.Items[i].SubItems.Add(wever);
                }
            }
            for (int i = 5; i < 10; i++)
            {
                if (swapped)
                {
                    lv_teamOneQuickStats.Items[i - 5].SubItems.Add(playerKDs[i]);

                    ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                    wever.Text = playerRatios[i];
                    lv_teamOneQuickStats.Items[i - 5].SubItems.Add(wever);
                }
                else
                {
                    lv_teamTwoQuickStats.Items[i - 5].SubItems.Add(playerKDs[i]);

                    ListViewItem.ListViewSubItem wever = new ListViewItem.ListViewSubItem();
                    wever.Text = playerRatios[i];
                    lv_teamTwoQuickStats.Items[i - 5].SubItems.Add(wever);
                }
            }
        }
    }
}
